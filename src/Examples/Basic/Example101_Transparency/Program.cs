// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using System.Numerics;
using System.Runtime.InteropServices;
using ExampleBase;
using Kaldera.Abstractions.Buffers;
using Kaldera.Abstractions.Extensions;
using Kaldera.Abstractions.RenderTargets;
using Kaldera.Abstractions.Textures;
using Kaldera.Abstractions.Utilities;
using Kaldera.Extensions;
using Kaldera.Interfaces;
using Kaldera.Objects;
using Silk.NET.Vulkan;
using Buffer = System.Buffer;
using Result = Kaldera.Result;

ExampleStartup.Run( "Kaldera Example - Transparency", 1600, 900, new ExampleTransparency(), args );

/// <summary>
/// Rendering a glTF scene with various transparency options. There are three pipelines:
/// Opaque, alphatest, alphablend. Alphatest uses a special shader. Alphablend surfaces are sorted by distance.
/// </summary>
internal class ExampleTransparency : BasicExampleBase
{
	// All that scene rendering stuff from the previous example is now reusable in RenderWorld
	// (and enhanced with material parsing, feel free to look at that!)
	private RenderWorld mWorld = null!;
	private UploadHelper mUploadHelper = null!;
	private KaSampler mSampler;
	private VertexShaderSet mShaderSet = null!;
	private VertexShaderSet mShaderSetAlphaTest = null!;
	private KaLayout mPipelineLayout = null!;
	private KaGraphicsPipeline mPipelineOpaque = null!;
	private KaGraphicsPipeline mPipelineAlphaTest = null!;
	private KaGraphicsPipeline mPipelineAlphaBlend = null!;

	private struct Drawcall
	{
		public required BufferSlice<StorageBuffer> EntityBuffer;
		public required int RenderEntityId;
		public required int RenderSurfaceId;
	}

	private struct TransparencySortItem
	{
		public required float Distance;
		public required int DrawcallId;
	}

	// To avoid binding pipelines non-stop, we'll instead render stuff in 3 passes: an opaque pass, an alphatest
	// pass and an alphablend pass! The Drawcall struct is just the bare minimum data needed to issue a drawcall
	private readonly List<Drawcall> mOpaqueDrawcalls = new( 128 );
	private readonly List<Drawcall> mAlphaTestDrawcalls = new( 128 );
	private readonly List<Drawcall> mAlphaBlendDrawcalls = new( 128 );

	// The sorting will simply result in a remap: drawcall indices will be shuffled around
	private readonly List<TransparencySortItem> mAlphaBlendSortMap = new( 128 );

	public override Result Init( KaInstance instance, KaDevice device, KaQueue queue, IResourceAllocator allocator )
	{
		base.Init( instance, device, queue, allocator ).Check();

		mShaderSet = Utilities.LoadGraphicsShaderSet( device, "transparency.spv" ).Checked();
		mShaderSetAlphaTest = Utilities.LoadGraphicsShaderSet( device, "transparency_alphatest.spv" ).Checked();
		mSampler = KaSampler.Create( device, Utilities.CommonSampler( Filter.Nearest ) );

		LayoutOptions layoutOptions = LayoutBuilder.Begin()
			.StructuredSet( s => s
				.UniformBuffer()
				.Sampler()
				.UniformBuffer()
				.SampledTexture() )
			.Build();

		mPipelineLayout = KaLayout.Create( device, layoutOptions ).Checked();
		VertexInputLayout[] vertexInputs = [PosNormTanColUv.VertexInputLayout];
		GraphicsPipelineOptions opaquePipeline = Utilities.CommonPipeline( mPipelineLayout, vertexInputs, mShaderSet );
		var alphaTestPipeline = Utilities.CommonPipeline( mPipelineLayout, vertexInputs, mShaderSetAlphaTest );
		// Oh yeah we'll also enable alpha2coverage here
		alphaTestPipeline.Multisample!.AlphaToCoverage = true;
		var alphaBlendPipeline = Utilities.CommonPipeline( mPipelineLayout, vertexInputs, mShaderSet, blending: BlendAttachments.AlphaBlend );
		// Alpha blending shall NOT write to the depth buffer, lest trouble awaits...
		alphaBlendPipeline.DepthStencil!.DepthWrite = false;

		mPipelineOpaque = KaGraphicsPipeline.Create( device, opaquePipeline ).Checked();
		mPipelineAlphaTest = KaGraphicsPipeline.Create( device, alphaTestPipeline ).Checked();
		mPipelineAlphaBlend = KaGraphicsPipeline.Create( device, alphaBlendPipeline ).Checked();

		mUploadHelper = UploadHelper.Create( allocator, 16 * 1024 * 1024 ).Checked();
		{
			mWorld = new RenderWorld( mUploadHelper );
			mWorld.CreateDefaultResources();
			int transparencyModelId = mWorld.GetOrLoadModel( "models/transparency_bunker.glb" ).First();
			mWorld.LoadGltfEntities( "models/transparency_bunker.glb", transparencyModelId );
		}
		mUploadHelper.Upload();

		// Drawcalls are cached ahead of time here. Alphablend drawcalls will be sorted dynamically
		for ( int renderEntityId = 0; renderEntityId < mWorld.Entities.Length; renderEntityId++ )
		{
			ref RenderEntity entity = ref mWorld.Entities[renderEntityId];
			ref RenderModel model = ref mWorld.Models[entity.RenderModelId];
			for ( int renderSurfaceId = model.FirstRenderSurface; renderSurfaceId < model.FirstRenderSurface + model.NumRenderSurfaces; renderSurfaceId++ )
			{
				ref RenderSurface surface = ref mWorld.Surfaces[renderSurfaceId];
				ref RenderMaterial material = ref mWorld.Materials[surface.MaterialId];

				if ( material.Flags.HasFlag( RenderMaterialFlags.AlphaBlend ) )
				{
					mAlphaBlendDrawcalls.Add( new()
					{
						EntityBuffer = entity.EntityBuffer,
						RenderEntityId = renderEntityId,
						RenderSurfaceId = renderSurfaceId
					} );
				}
				else if ( material.Flags.HasFlag( RenderMaterialFlags.AlphaTest ) )
				{
					mAlphaTestDrawcalls.Add( new()
					{
						EntityBuffer = entity.EntityBuffer,
						RenderEntityId = renderEntityId,
						RenderSurfaceId = renderSurfaceId
					} );
				}
				else
				{
					mOpaqueDrawcalls.Add( new()
					{
						EntityBuffer = entity.EntityBuffer,
						RenderEntityId = renderEntityId,
						RenderSurfaceId = renderSurfaceId
					} );
				}
			}
		}

		return Result.Success();
	}

	private void RecordDrawcalls( KaCommandBuffer commands, TextureRenderTarget renderTarget, KaGraphicsPipeline pipeline, Span<Drawcall> drawcalls )
	{
		commands.BindPipeline( pipeline );
		commands.SetViewport( 0, renderTarget.Extent, 0.0f, 1.0f );
		commands.SetScissor( 0, renderTarget.Extent );

		commands.PushUniformBuffer( Camera.UniformBuffer, 0, 0 );
		commands.PushSampler( mSampler, 0, 1 );

		int currentEntity = -1;
		for ( int i = 0; i < drawcalls.Length; i++ )
		{
			// In case we're rendering the same entity, this won't change. So we don't need to rebind it
			if ( currentEntity != drawcalls[i].RenderEntityId )
			{
				currentEntity = drawcalls[i].RenderEntityId;
				commands.PushUniformBuffer( drawcalls[i].EntityBuffer, 0, 2 );
			}

			// We do not have to worry about material.Flags here, it is taken care of in advance!
			ref RenderSurface surface = ref mWorld.Surfaces[drawcalls[i].RenderSurfaceId];
			ref RenderMaterial material = ref mWorld.Materials[surface.MaterialId];
			ref Texture texture = ref mWorld.Textures[material.DiffuseTextureId];

			commands.PushSampledTexture( texture, 0, 3 );
			commands.BindVertexBuffer( surface.VertexBuffer, 0 );
			commands.BindIndexBuffer( surface.IndexBuffer );
			commands.DrawIndexed( surface.IndexBuffer.Count, instanceCount: 1 );
		}
	}

	private void OpaquePass( KaCommandBuffer commands, TextureRenderTarget renderTarget )
	{
		RecordDrawcalls( commands, renderTarget, mPipelineOpaque, CollectionsMarshal.AsSpan( mOpaqueDrawcalls ) );

		// The alphatest pass is 100% identical. It's just sourced from a different list
		// of drawcalls, using a different pipeline, and that's quite beautiful!
		RecordDrawcalls( commands, renderTarget, mPipelineAlphaTest, CollectionsMarshal.AsSpan( mAlphaTestDrawcalls ) );
	}

	private void SortTransparentCalls()
	{
		Vector3 cameraPosition = Camera.Position;

		mAlphaBlendSortMap.Clear();
		Span<Drawcall> drawcalls = CollectionsMarshal.AsSpan( mAlphaBlendDrawcalls );
		for ( int i = 0; i < drawcalls.Length; i++ )
		{
			Vector3 entityPosition = mWorld.EntityStates[drawcalls[i].RenderEntityId].GetPosition();

			mAlphaBlendSortMap.Add( new()
			{
				// One micro-optimisation here is that we use distance² instead of distance 
				Distance = (entityPosition - cameraPosition).LengthSquared(),
				DrawcallId = i
			} );
		}

		// B is compared to A so we get a furthest-to-closest order
		mAlphaBlendSortMap.Sort( static ( a, b ) => b.Distance.CompareTo( a.Distance ) );
	}

	private void TransparentPass( KaCommandBuffer commands, TextureRenderTarget renderTarget )
	{
		commands.BindPipeline( mPipelineAlphaBlend );
		commands.SetViewport( 0, renderTarget.Extent, 0.0f, 1.0f );
		commands.SetScissor( 0, renderTarget.Extent );

		commands.PushUniformBuffer( Camera.UniformBuffer, 0, 0 );
		commands.PushSampler( mSampler, 0, 1 );

		int currentEntity = -1;
		// The drawcalls themselves are unsorted. What is sorted is the order in which we'll access them
		Span<Drawcall> drawcalls = CollectionsMarshal.AsSpan( mAlphaBlendDrawcalls );
		foreach ( var sortMap in mAlphaBlendSortMap )
		{
			int i = sortMap.DrawcallId;

			if ( currentEntity != drawcalls[i].RenderEntityId )
			{
				currentEntity = drawcalls[i].RenderEntityId;
				commands.PushUniformBuffer( drawcalls[i].EntityBuffer, 0, 2 );
			}

			ref RenderSurface surface = ref mWorld.Surfaces[drawcalls[i].RenderSurfaceId];
			ref RenderMaterial material = ref mWorld.Materials[surface.MaterialId];
			ref Texture texture = ref mWorld.Textures[material.DiffuseTextureId];

			commands.PushSampledTexture( texture, 0, 3 );
			commands.BindVertexBuffer( surface.VertexBuffer, 0 );
			commands.BindIndexBuffer( surface.IndexBuffer );
			commands.DrawIndexed( surface.IndexBuffer.Count, instanceCount: 1 );
		}
	}

	protected override void OnUpload( KaCommandBuffer commands )
	{
		mWorld.UpdateBuffers( commands );
		base.OnUpload( commands );
	}

	protected override void OnDraw( KaCommandBuffer commands, TextureRenderTarget renderTarget )
	{
		commands.RenderPass( renderTarget, () =>
		{
			commands.ClearColour( renderTarget, 0, new( 0.0f, 0.13f, 0.13f, 1.0f ) );
			commands.ClearDepth( renderTarget, 0, 1.0f );

			if ( mWorld.Entities.Length is 0 )
			{
				return;
			}

			// From a high level, this is what it looks like
			OpaquePass( commands, renderTarget );
			SortTransparentCalls();
			TransparentPass( commands, renderTarget );
		} );
	}

	public override void Dispose()
	{
		mWorld.Dispose();
		mSampler.Dispose();
		mUploadHelper.Dispose();
		mPipelineLayout.Dispose();
		mPipelineOpaque.Dispose();
		mPipelineAlphaTest.Dispose();
		mPipelineAlphaBlend.Dispose();
		mShaderSet.Vertex.Dispose();
		mShaderSetAlphaTest.Vertex.Dispose();
		base.Dispose();
	}
}
