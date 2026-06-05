// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using System.Diagnostics;
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
using Result = Kaldera.Result;

ExampleStartup.Run( "Kaldera Example - Model loading", 1600, 900, new ExampleModelLoading(), args );

/// <summary>
/// How to load and render an OBJ model and a glTF2 model next to it. Also showcases a mini renderer structure.
///
/// In essence, you want to convert any external model format into a representation
/// that's optimal for your app/engine. In this case, the Utilities.PosNormalUv vertex format
/// </summary>
internal class ExampleModelLoading : BasicExampleBase
{
	// In preparation for scene rendering, one must define the smallest renderable unit. In our case,
	// this is a "render surface", simply a combination of some renderable geometry + material data.
	// Or in this case, vertex+index buffer and a diffuse texture.
	private struct RenderSurface
	{
		public required VertexBuffer<PosNormalUv> VertexBuffer;
		public required IndexBuffer IndexBuffer;
		public required int DiffuseTextureId;
	}

	private struct RenderModel
	{
		// These are indices into mRenderSurfaces
		public required int FirstRenderSurface;
		public required int NumRenderSurfaces;
	}

	// It is perfectly possible to have a really nice and "clean" looking OOP-ahh representation
	// of renderable entities. However, those are prone to cache misses. Lots and lots of cache misses...
	// So remember: keep it stupid simple.
	private struct RenderEntity
	{
		public required StorageBuffer EntityMatrixBuffer;
		public required int RenderModelId;
	}

	private List<string> mTextureNames = [];
	private List<Texture> mTextures = [];
	private List<RenderSurface> mRenderSurfaces = [];
	private List<RenderModel> mRenderModels = [];
	private List<RenderEntity> mRenderEntities = [];

	private Span<Texture> TextureSpan => CollectionsMarshal.AsSpan( mTextures );
	private Span<RenderSurface> RenderSurfaceSpan => CollectionsMarshal.AsSpan( mRenderSurfaces );
	private Span<RenderModel> RenderModelSpan => CollectionsMarshal.AsSpan( mRenderModels );
	private Span<RenderEntity> RenderEntitySpan => CollectionsMarshal.AsSpan( mRenderEntities );

	private UploadHelper mUploadHelper = null!;
	private KaSampler mSampler;
	private VertexShaderSet mShaderSet = null!;
	private KaLayout mPipelineLayout = null!;
	private KaGraphicsPipeline mPipeline = null!;

	private int AddRenderSurface( PosNormalUv[] vertices, uint[] indices, int diffuseTexture )
	{
		VertexBuffer<PosNormalUv> vertexBuffer = mUploadHelper.CommitVertexBuffer( vertices.AsSpan() ).Checked();
		IndexBuffer indexBuffer = mUploadHelper.CommitIndexBuffer( indices.AsSpan() ).Checked();

		mRenderSurfaces.Add( new()
		{
			VertexBuffer = vertexBuffer,
			IndexBuffer = indexBuffer,
			DiffuseTextureId = diffuseTexture
		} );

		return mRenderSurfaces.Count - 1;
	}

	private int AddRenderModel( int firstSurface, int numSurfaces )
	{
		mRenderModels.Add( new()
		{
			FirstRenderSurface = firstSurface,
			NumRenderSurfaces = numSurfaces
		} );

		return mRenderModels.Count - 1;
	}

	private int GetOrLoadTexture( string? name )
	{
		if ( name is null )
		{
			return 0;
		}

		// Texture paths may be relative to the model, fix them up relative
		// to the working directory
		string path = name.Replace( "../", null );

		if ( mTextureNames.Contains( path ) )
		{
			return mTextureNames.IndexOf( path );
		}

		if ( !File.Exists( path ) )
		{
			Console.WriteLine( $"WARNING: Texture '{name}' is missing" );
			return 0;
		}

		(int width, int height, byte[] data) = Utilities.LoadTexture( path ).Checked();
		Texture texture = mUploadHelper.CommitTexture( data.AsSpan(), Format.R8G8B8A8Unorm, width, height, 1 ).Checked();
		mTextures.Add( texture );
		mTextureNames.Add( path );
		return mTextures.Count - 1;
	}

	// We don't care much about the actual details of parsing an OBJ.
	// What we're *really* interested in is how to fit the OBJ data
	// into our little entity-and-surface structure
	private int LoadObj( string path )
	{
		// The parsing is some 300 lines of code, so feel free to look at it. It
		// sorta  treats the OBJ file as one mesh when in reality it's a "scene"
		var objResultResult = ObjLoader.Parse( path );
		if ( !objResultResult.Get( out var error, out var objResult ) )
		{
			// Homework: "Missing model" model, kinda like ERROR in a certain engine...
			error.PrintError();
			return -1;
		}

		int? firstRenderSurface = null;
		foreach ( ObjSurface surface in objResult.Surfaces )
		{
			int diffuseTexture = GetOrLoadTexture( surface.DiffuseTexture );
			int renderSurface = AddRenderSurface( surface.Vertices, surface.Indices, diffuseTexture );
			firstRenderSurface ??= renderSurface;
		}
		Debug.Assert( firstRenderSurface is not null );

		return AddRenderModel( firstRenderSurface.Value, objResult.Surfaces.Length );
	}

	private void LoadGltf( string path )
	{
		// GLTF is a scene format and unlike my incomplete OBJ parser above, this actually parses
		// it like a scene. You have objects, objects have a transform and some surfaces, and surfaces
		// ultimately carry vertex and index information
		//
		// So, for this example, we will respect that. Load the meshes, then instantiate them. In a game engine,
		// you would instantiate entities separately from the model (you'd have a model referencing a .glb or so)
		var gltfResultResult = GltfLoader.Parse( path );
		if ( !gltfResultResult.Get( out var error, out var gltfResult ) )
		{
			error.PrintError();
			return;
		}
		Debug.Assert( gltfResult.Meshes.Length is not 0 );

		List<int> renderModelIds = new( gltfResult.Meshes.Length );
		foreach ( GltfMesh mesh in gltfResult.Meshes )
		{
			int? firstSurface = null;
			foreach ( GltfSurface surface in mesh.Surfaces )
			{
				int diffuseTexture = GetOrLoadTexture( surface.DiffuseTexture );
				int surfaceId = AddRenderSurface( PosNormalUv.From( surface.Vertices ), surface.Indices, diffuseTexture );
				firstSurface ??= surfaceId;
			}
			Debug.Assert( firstSurface is not null );

			renderModelIds.Add( AddRenderModel( firstSurface.Value, mesh.Surfaces.Length ) );
		}

		foreach ( GltfEntity entity in gltfResult.Entities )
		{
			AddRenderEntity( renderModelIds[entity.MeshId], entity.Transform );
		}
	}

	private void AddRenderEntity( int renderModelId, Matrix4x4 entityMatrix )
	{
		StorageBuffer uniformBuffer = mUploadHelper.CommitUniformBuffer( entityMatrix ).Checked();
		mRenderEntities.Add( new()
		{
			EntityMatrixBuffer = uniformBuffer,
			RenderModelId = renderModelId
		} );
	}

	private void CreateBuiltinResources()
	{
		byte[] palette =
		[
			0, 0, 0, 255, // Black
			0, 255, 255, 255, // Cyan
			255, 0, 255, 255, // Magenta
			255, 255, 0, 255 // Yellow
		];

		// "Missing texture" texture
		byte[] pixelData = new byte[16 * 16 * 4];
		for ( int y = 0; y < 16; y++ )
		{
			for ( int x = 0; x < 16; x++ )
			{
				int i = y * 16 + x;
				// Quick little "forward diagonal step" formula
				int colour = Math.Abs( (x % 4) + (y % -4) ) % 4;

				i *= 4;
				colour *= 4;

				pixelData[i] = palette[colour];
				pixelData[i + 1] = palette[colour + 1];
				pixelData[i + 2] = palette[colour + 2];
				pixelData[i + 3] = palette[colour + 3];
			}
		}

		Texture tex = mUploadHelper.CommitTexture( pixelData, Format.R8G8B8A8Unorm, 16, 16, 1 ).Checked();
		mTextureNames.Add( "_missing" );
		mTextures.Add( tex );
	}

	public override Result Init( KaInstance instance, KaDevice device, KaQueue queue, IResourceAllocator allocator )
	{
		base.Init( instance, device, queue, allocator ).Check();

		mShaderSet = Utilities.LoadGraphicsShaderSet( device, "model_loading.spv" ).Checked();
		mSampler = KaSampler.Create( device, Utilities.CommonSampler( Filter.Nearest ) );

		LayoutOptions layoutOptions = LayoutBuilder.Begin()
			.StructuredSet( s => s
				.UniformBuffer()
				.Sampler()
				.UniformBuffer()
				.SampledTexture() )
			.Build();

		mPipelineLayout = KaLayout.Create( device, layoutOptions ).Checked();
		mPipeline = KaGraphicsPipeline.Create( device, Utilities.CommonPipeline(
			pipelineLayout: mPipelineLayout,
			vertexInputs: [PosNormalUv.VertexInputLayout],
			shaderSet: mShaderSet
		) ).Checked();

		// In this scope, we can load all render assets
		mUploadHelper = UploadHelper.Create( allocator, 16 * 1024 * 1024 ).Checked();
		{
			CreateBuiltinResources();

			int mainAreaModel = LoadObj( "models/area.obj" );
			LoadGltf( "models/landscape.glb" );
			//int shaderBallModel = LoadGltf( "models/shader_ball.obj" );

			AddRenderEntity( mainAreaModel, Matrix4x4.Identity );
			//AddRenderEntity( shaderBallModel, ... );
		}
		mUploadHelper.Upload();

		return Result.Success();
	}

	protected override void OnDraw( KaCommandBuffer commands, TextureRenderTarget renderTarget )
	{
		commands.RenderPass( renderTarget, () =>
		{
			commands.ClearColour( renderTarget, 0, new( 0.0f, 0.13f, 0.13f, 1.0f ) );
			commands.ClearDepth( renderTarget, 0, 1.0f );

			if ( mRenderEntities.Count is 0 )
			{
				return;
			}

			commands.BindPipeline( mPipeline );
			commands.SetViewport( 0, renderTarget.Extent, 0.0f, 1.0f );
			commands.SetScissor( 0, renderTarget.Extent );

			// Per-frame scope
			commands.PushUniformBuffer( Camera.UniformBuffer, 0, 0 );
			commands.PushSampler( mSampler, 0, 1 );

			for ( int renderEntityId = 0; renderEntityId < mRenderEntities.Count; renderEntityId++ )
			{
				// Per-entity scope
				ref RenderEntity entity = ref RenderEntitySpan[renderEntityId];
				// Notice how lower sets change only once per frame, whereas higher sets change more frequently: per entity, per surface...
				commands.PushUniformBuffer( entity.EntityMatrixBuffer, 0, 2 );

				ref RenderModel model = ref RenderModelSpan[entity.RenderModelId];
				for ( int renderSurfaceId = model.FirstRenderSurface; renderSurfaceId < model.FirstRenderSurface + model.NumRenderSurfaces; renderSurfaceId++ )
				{
					// Per-surface scope. This is where drawcalls are made
					ref RenderSurface surface = ref RenderSurfaceSpan[renderSurfaceId];
					ref Texture texture = ref TextureSpan[surface.DiffuseTextureId];
					commands.PushSampledTexture( texture, 0, 3 );

					commands.BindVertexBuffer( surface.VertexBuffer, 0 );
					commands.BindIndexBuffer( surface.IndexBuffer );
					commands.DrawIndexed( surface.IndexBuffer.Count, instanceCount: 1 );
				}
			}
		} );
	}

	public override void Dispose()
	{
		foreach ( var texture in mTextures )
		{
			texture.Dispose();
		}

		foreach ( var surface in mRenderSurfaces )
		{
			surface.IndexBuffer.Dispose();
			surface.VertexBuffer.Dispose();
		}

		foreach ( var entity in mRenderEntities )
		{
			entity.EntityMatrixBuffer.Dispose();
		}

		mSampler.Dispose();
		mUploadHelper.Dispose();
		mPipelineLayout.Dispose();
		mPipeline.Dispose();
		mShaderSet.Vertex.Dispose();
		base.Dispose();
	}
}
