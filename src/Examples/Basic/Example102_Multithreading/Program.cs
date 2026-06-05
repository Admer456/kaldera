// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using System.Numerics;
using System.Runtime.InteropServices;
using BepuUtilities;
using ExampleBase;
using Kaldera.Abstractions.Buffers;
using Kaldera.Abstractions.Extensions;
using Kaldera.Abstractions.RenderTargets;
using Kaldera.Abstractions.Textures;
using Kaldera.Abstractions.Utilities;
using Kaldera.Extensions;
using Kaldera.Interfaces;
using Kaldera.Objects;
using SDL3;
using Silk.NET.Vulkan;
using Result = Kaldera.Result;

ExampleStartup.Run( "Kaldera Example - Multithreading", 1600, 900, new ExampleMultithreading(), args );

/// <summary>
/// Multithreaded rendering example. A glTF scene with some terrain and a huge load of rocks, trees,
/// bushes and the like. BepuPhysics' thread dispatcher is used since it's a little lighter than Parallel.For.
///
/// You won't get too much of a performance gain due to the overhead of dispatching threads. This particular
/// scenario would benefit more from instancing, batching, indirect rendering... yeah.
/// </summary>
internal class ExampleMultithreading : BasicExampleBase
{
	private enum RenderingMode
	{
		Singlethreaded,
		Multithreaded,
		Instanced
	}

	private List<InstanceBucket> mInstanceBuckets = null!;
	private RenderingMode mRenderingMode = RenderingMode.Singlethreaded;
	private ThreadDispatcher mThreadDispatcher = null!;
	private WorkItem[] mWorkerCache = null!;
	private RenderWorld mWorld = null!;
	private UploadHelper mUploadHelper = null!;
	private KaSampler mSampler;
	private VertexShaderSet mShaderSet = null!;
	private VertexShaderSet mShaderSetAlphaTest = null!;
	private VertexShaderSet mShaderSetInstanced = null!;
	private VertexShaderSet mShaderSetInstancedAlphaTest = null!;
	private KaLayout mPipelineLayout = null!;
	private KaLayout mPipelineLayoutInstanced = null!;
	private KaGraphicsPipeline mPipelineOpaque = null!;
	private KaGraphicsPipeline mPipelineAlphaTest = null!;
	private KaGraphicsPipeline mPipelineInstancedOpaque = null!;
	private KaGraphicsPipeline mPipelineInstancedAlphaTest = null!;

	private struct Drawcall
	{
		public required int RenderEntityId;
		public required int RenderSurfaceId;
	}

	private struct InstanceBucket
	{
		public required StorageBuffer InstanceBuffer;
		public required int RenderSurfaceId;
		public required RenderMaterialFlags MaterialFlags;
		public required int InstanceCount;
	}

	private class WorkItem( KaCommandBuffer commandBuffer )
	{
		public TextureRenderTarget? RenderTarget;

		public KaCommandBuffer Commands = commandBuffer;

		// For simplicity, we'll not have alpha blending in this example. Sorting & multithreading
		// together would complicate the example a bit too much.
		public List<Drawcall> OpaqueCalls = new( 8192 );
		public List<Drawcall> AlphaTestCalls = new( 8192 );
	}

	public override Result Init( KaInstance instance, KaDevice device, KaQueue queue, IResourceAllocator allocator )
	{
		base.Init( instance, device, queue, allocator ).Check();

		mShaderSet = Utilities.LoadGraphicsShaderSet( device, "transparency.spv" ).Checked();
		mShaderSetAlphaTest = Utilities.LoadGraphicsShaderSet( device, "transparency_alphatest.spv" ).Checked();
		mShaderSetInstanced = Utilities.LoadGraphicsShaderSet( device, "transparency_instanced.spv" ).Checked();
		mShaderSetInstancedAlphaTest = Utilities.LoadGraphicsShaderSet( device, "transparency_instanced_alphatest.spv" ).Checked();
		mSampler = KaSampler.Create( device, Utilities.CommonSampler( Filter.Nearest ) );

		LayoutOptions layoutOptions = LayoutBuilder.Begin()
			.StructuredSet( s => s
				.UniformBuffer()
				.Sampler()
				.UniformBuffer()
				.SampledTexture() )
			.Build();

		mPipelineLayout = KaLayout.Create( device, layoutOptions ).Checked();

		layoutOptions = LayoutBuilder.Begin()
			.StructuredSet( s => s
				.UniformBuffer()
				.Sampler()
				.StorageBuffer()
				.SampledTexture() )
			.Build();

		mPipelineLayoutInstanced = KaLayout.Create( device, layoutOptions ).Checked();

		VertexInputLayout[] vertexInputs = [PosNormTanColUv.VertexInputLayout];
		GraphicsPipelineOptions opaquePipeline = Utilities.CommonPipeline( mPipelineLayout, vertexInputs, mShaderSet );
		var alphaTestPipeline = Utilities.CommonPipeline( mPipelineLayout, vertexInputs, mShaderSetAlphaTest );
		alphaTestPipeline.Multisample!.AlphaToCoverage = true;

		mPipelineOpaque = KaGraphicsPipeline.Create( device, opaquePipeline ).Checked();
		mPipelineAlphaTest = KaGraphicsPipeline.Create( device, alphaTestPipeline ).Checked();

		opaquePipeline.ResourceLayout = mPipelineLayoutInstanced;
		alphaTestPipeline.ResourceLayout = mPipelineLayoutInstanced;
		opaquePipeline.ShaderSet = mShaderSetInstanced;
		alphaTestPipeline.ShaderSet = mShaderSetInstancedAlphaTest;

		mPipelineInstancedOpaque = KaGraphicsPipeline.Create( device, opaquePipeline ).Checked();
		mPipelineInstancedAlphaTest = KaGraphicsPipeline.Create( device, alphaTestPipeline ).Checked();

		float RandomFloat() => Random.Shared.NextSingle() * 2.0f - 1.0f;

		// To get varied terrain, we scatter a bunch of random points that act as brush strokes
		Vector3[] terrainHeightmapBrushes = Enumerable.Range( 0, 512 )
			.Select( _ => new Vector3( RandomFloat() * 150.0f, RandomFloat() * 150.0f, RandomFloat() * 50.0f ) )
			.ToArray();

		float HeightmapFunction( Vector2 xy )
			=> terrainHeightmapBrushes.Sum( p =>
			{
				float distance = (xy - new Vector2( p.X, p.Y )).Length();
				return Utilities.Gauss( distance, 2.0f + MathF.Abs( p.Z * p.Z ) * 0.33f ) * p.Z * 0.05f;
			} );

		PosNormTanColUv VertexFunction( Vector2 xy, GridMeshBuilder.HeightmapFunc z )
		{
			PosNormTanColUv result = new();

			result.Position = GridMeshBuilder.GetPosition( xy, z );
			Vector3 normal = GridMeshBuilder.GetNormal( xy, z );
			result.Normal.X = (sbyte)(normal.X * 127.0f);
			result.Normal.Y = (sbyte)(normal.Y * 127.0f);
			result.Normal.Z = (sbyte)(normal.Z * 127.0f);
			result.Uv = xy * new Vector2( 1.0f, -1.0f ) * (1.0f / 16.0f);

			return result;
		}

#if DEBUG
		int areaSize = 128;
		int totalInstances = 8192;
#else
		int areaSize = 512;
		int totalInstances = 61440;
#endif

		mUploadHelper = UploadHelper.Create( allocator, 48 * 1024 * 1024 ).Checked();
		{
			mWorld = new RenderWorld( mUploadHelper, numEntities: totalInstances + 256 );
			mWorld.CreateDefaultResources();

			Console.WriteLine( "Generating terrain vertices..." );
			var terrainMeshData = GridMeshBuilder.BuildVertexData( areaSize, areaSize, VertexFunction, HeightmapFunction );
			Console.WriteLine( "Generating terrain indices..." );
			var terrainIndicesData = GridMeshBuilder.BuildIndices( areaSize, areaSize );

			// And this is how we'd "manually" set up a model
			int gravelMaterial = mWorld.GetOrLoadMaterial( "textures/retro/gravel1" );
			int terrainSurface = mWorld.AddRenderSurface( terrainMeshData, terrainIndicesData, gravelMaterial );
			int terrainModel = mWorld.AddRenderModel( "Terrain", terrainSurface, 1 );

			mWorld.AddRenderEntity( terrainModel, new()
			{
				Transform = Matrix4x4.Identity
			} );
			Console.WriteLine( "Terrain loaded!" );

			// In this example, we'll load the meshes themselves and instantiate them procedurally, as
			// opposed to instantiating them from the glTF scene. The model file contains one terrain mesh,
			// and a few vegetation meshes roughly in the centre
			List<int> models = mWorld.GetOrLoadModel( "models/forest_zoo.glb" );
			Console.WriteLine( "Vegetation models loaded!" );

			for ( int i = 0; i < totalInstances; i++ )
			{
				Vector2 xy = new()
				{
					X = (Random.Shared.NextSingle() - 0.5f) * 2.0f * (areaSize * 0.5f),
					Y = (Random.Shared.NextSingle() - 0.5f) * 2.0f * (areaSize * 0.5f)
				};

				int modelId = models[Random.Shared.Next( 0, models.Count )];
				mWorld.AddRenderEntity( modelId, new()
				{
					Transform = Matrix4x4.CreateTranslation( new( xy, HeightmapFunction( xy ) ) )
				} );

				if ( i % 4096 == 0 )
				{
					Console.WriteLine( $"Spawned {i} entities..." );
				}
			}

			// We're also gonna bake instances from these many entities, skipping the terrain of course
			Dictionary<int, List<RenderEntityState>> instanceMap = new();
			for ( int i = 1; i < mWorld.Entities.Length; i++ )
			{
				int renderModelId = mWorld.Entities[i].RenderModelId;
				if ( !instanceMap.TryGetValue( renderModelId, out var states ) )
				{
					states = new();
					instanceMap[renderModelId] = states;
				}

				states.Add( mWorld.EntityStates[i] );
			}

			mInstanceBuckets = instanceMap.Select( pair => new InstanceBucket
			{
				InstanceBuffer = mUploadHelper.CommitStorageBuffer( CollectionsMarshal.AsSpan( pair.Value ) ).Checked(),
				RenderSurfaceId = pair.Key,
				MaterialFlags = mWorld.Materials[mWorld.Surfaces[pair.Key].MaterialId].Flags,
				InstanceCount = pair.Value.Count
			} ).ToList();
		}
		mUploadHelper.Upload();

		// Standard parallel programming pattern right here. For each thread in your CPU, you'll have
		// one "worker cache" entry which is basically thread-local data. It can be named whatever, but
		// I use "worker cache"
		mThreadDispatcher = new( Environment.ProcessorCount );
		mWorkerCache = new WorkItem[Environment.ProcessorCount];
		for ( int i = 0; i < mWorkerCache.Length; i++ )
		{
			// Each worker thread gets its own command buffer!
			mWorkerCache[i] = new WorkItem( KaCommandBuffer.CreateSecondary( queue ).Checked() );
		}

		return Result.Success();
	}

	#region Drawcall and work utilities

	// This is a utility used by both singlethreaded and multithreaded approaches. It's the same all around
	private void RecordDrawcalls( KaCommandBuffer commands, TextureRenderTarget renderTarget, KaGraphicsPipeline pipeline, Span<Drawcall> drawcalls )
	{
		if ( drawcalls.IsEmpty )
		{
			return;
		}

		commands.BindPipeline( pipeline );
		commands.SetViewport( 0, renderTarget.Extent, 0.0f, 1.0f );
		commands.SetScissor( 0, renderTarget.Extent );

		commands.PushUniformBuffer( Camera.UniformBuffer, 0, 0 );
		commands.PushSampler( mSampler, 0, 1 );

		int currentEntity = -1;
		int currentMaterial = -1;
		int currentSurface = -1;
		for ( int i = 0; i < drawcalls.Length; i++ )
		{
			// In case we're rendering the same entity, this won't change. So we don't need to rebind it
			if ( currentEntity != drawcalls[i].RenderEntityId )
			{
				currentEntity = drawcalls[i].RenderEntityId;
				commands.PushUniformBuffer( mWorld.Entities[currentEntity].EntityBuffer, 0, 2 );
			}

			// We do not have to worry about material.Flags here, it is taken care of in advance!
			ref RenderSurface surface = ref mWorld.Surfaces[drawcalls[i].RenderSurfaceId];
			if ( drawcalls[i].RenderSurfaceId != currentSurface )
			{
				currentSurface = drawcalls[i].RenderSurfaceId;
				commands.BindVertexBuffer( surface.VertexBuffer, 0 );
				commands.BindIndexBuffer( surface.IndexBuffer );
			}

			// This constant checking is not too beneficial a.t.m. because the incoming drawcalls are unsorted.
			// Ideally you'd want to sort them by something like (materialId * 65536 + surfaceId)
			// or some other way, depending on the frequency of each thing changing. Or, alternatively,
			// use bindless and instancing and speed everything up :3c
			if ( surface.MaterialId != currentMaterial )
			{
				currentMaterial = surface.MaterialId;
				ref RenderMaterial material = ref mWorld.Materials[surface.MaterialId];
				ref Texture texture = ref mWorld.Textures[material.DiffuseTextureId];
				commands.PushSampledTexture( texture, 0, 3 );
			}

			commands.DrawIndexed( surface.IndexBuffer.Count, instanceCount: 1 );
		}
	}

	private static int CalculateWorkRange( int workerIndex, int numWorkers, int count, out int start, out int end )
	{
		int stride = count / numWorkers;
		start = stride * workerIndex;
		end = workerIndex == numWorkers - 1 ? count : stride * (workerIndex + 1);
		return stride;
	}

	private void AccumulateDrawcalls( int start, int end, List<Drawcall> opaqueDrawcalls, List<Drawcall> alphaTestDrawcalls )
	{
		Span<RenderEntity> entities = mWorld.Entities;
		Span<RenderEntityState> states = mWorld.EntityStates;
		Span<RenderModel> models = mWorld.Models;
		Span<RenderSurface> surfaces = mWorld.Surfaces;
		Span<RenderMaterial> materials = mWorld.Materials;

		for ( int i = start; i < end; i++ )
		{
			RenderEntity entity = entities[i];
			RenderModel model = models[entity.RenderModelId];
			for ( int s = model.FirstRenderSurface; s < model.FirstRenderSurface + model.NumRenderSurfaces; s++ )
			{
				RenderSurface surface = surfaces[s];
				RenderMaterial material = materials[surface.MaterialId];
				List<Drawcall> drawcalls = opaqueDrawcalls;
				if ( material.Flags.HasFlag( RenderMaterialFlags.AlphaTest ) )
				{
					drawcalls = alphaTestDrawcalls;
				}

				drawcalls.Add( new()
				{
					RenderEntityId = i,
					RenderSurfaceId = s
				} );
			}
		}
	}

	#endregion

	private void RenderingWorker( int workerIndex )
	{
		CalculateWorkRange( workerIndex, mWorkerCache.Length, mWorld.Entities.Length, out int startId, out int endId );
		WorkItem work = mWorkerCache[workerIndex];
		AccumulateDrawcalls( startId, endId, work.OpaqueCalls, work.AlphaTestCalls );

		// As this is a secondary command buffer, it will "inherit" the render target from the primary one
		work.Commands.BeginSecondary( work.RenderTarget! );
		RecordDrawcalls( work.Commands, work.RenderTarget!, mPipelineOpaque, CollectionsMarshal.AsSpan( work.OpaqueCalls ) );
		RecordDrawcalls( work.Commands, work.RenderTarget!, mPipelineAlphaTest, CollectionsMarshal.AsSpan( work.AlphaTestCalls ) );
		work.Commands.End();
	}

	private void RenderMultiThreaded( KaCommandBuffer commands )
	{
		// Ideally, you'd have persistent threads or something of that sort. But this still runs faster than singlethreaded, so w/e
		// Alternatively, I could've used: Parallel.For( 0, mWorkerCache.Length, RenderingWorker );
		unsafe
		{
			// On my 8-core (no HT) CPU, this would call RenderingWorker(0) on the
			// main thread and dispatch the remaining 7 to the worker threads
			mThreadDispatcher.DispatchWorkers( RenderingWorker );
		}

		// This example also demonstrates how you may use secondary command buffers
		for ( int i = 0; i < mWorkerCache.Length; i++ )
		{
			commands.Execute( mWorkerCache[i].Commands );
		}
	}

	private void RenderSingleThreaded( KaCommandBuffer commands, TextureRenderTarget renderTarget )
	{
		WorkItem work = mWorkerCache[0];
		// Sidenote: you may also do occlusion culling here!
		double accumulateTime = Utilities.GetSeconds();
		AccumulateDrawcalls( 0, mWorld.Entities.Length, work.OpaqueCalls, work.AlphaTestCalls );

		double opaqueTime = Utilities.GetSeconds();
		RecordDrawcalls( commands, renderTarget, mPipelineOpaque, CollectionsMarshal.AsSpan( work.OpaqueCalls ) );
		double alphaTestTime = Utilities.GetSeconds();
		RecordDrawcalls( commands, renderTarget, mPipelineAlphaTest, CollectionsMarshal.AsSpan( work.AlphaTestCalls ) );
		double drawEnd = Utilities.GetSeconds();

		accumulateTime = (opaqueTime - accumulateTime) * 1000.0 * 1000.0;
		opaqueTime = (alphaTestTime - opaqueTime) * 1000.0 * 1000.0;
		alphaTestTime = (drawEnd - alphaTestTime) * 1000.0 * 1000.0;

		Console.WriteLine( "Profiling:" );
		Console.WriteLine( $"Accumulate: {accumulateTime:F1} us" );
		Console.WriteLine( $"Opaque:     {opaqueTime:F1} us" );
		Console.WriteLine( $"Alphatest:  {alphaTestTime:F1} us" );
	}

	private void RenderInstanced( KaCommandBuffer commands, TextureRenderTarget renderTarget )
	{
		void RecordInstancedDrawcalls( InstanceBucket bucket, KaGraphicsPipeline pipeline )
		{
			RenderSurface surface = mWorld.Surfaces[bucket.RenderSurfaceId];
			RenderMaterial material = mWorld.Materials[surface.MaterialId];
			Texture texture = mWorld.Textures[material.DiffuseTextureId];

			commands.BindPipeline( pipeline );
			commands.SetViewport( 0, renderTarget.Extent, 0.0f, 1.0f );
			commands.SetScissor( 0, renderTarget.Extent );

			commands.PushUniformBuffer( Camera.UniformBuffer, 0, 0 );
			commands.PushSampler( mSampler, 0, 1 );
			commands.PushStorageBuffer( bucket.InstanceBuffer, 0, 2 );
			commands.PushSampledTexture( texture, 0, 3 );

			commands.BindVertexBuffer( surface.VertexBuffer, 0 );
			commands.BindIndexBuffer( surface.IndexBuffer );
			commands.DrawIndexed( surface.IndexBuffer.Count, instanceCount: bucket.InstanceCount );
		}

		// We're gonna render the terrain exactly the same as in RenderSingleThreaded
		WorkItem work = mWorkerCache[0];
		AccumulateDrawcalls( 0, 1, work.OpaqueCalls, work.AlphaTestCalls );
		RecordDrawcalls( commands, renderTarget, mPipelineOpaque, CollectionsMarshal.AsSpan( work.OpaqueCalls ) );
		RecordDrawcalls( commands, renderTarget, mPipelineAlphaTest, CollectionsMarshal.AsSpan( work.AlphaTestCalls ) );

		// The rest is all instanced
		foreach ( InstanceBucket bucket in mInstanceBuckets )
		{
			RecordInstancedDrawcalls( bucket,
				bucket.MaterialFlags.HasFlag( RenderMaterialFlags.AlphaTest )
					? mPipelineInstancedAlphaTest
					: mPipelineInstancedOpaque
			);
		}
	}

	protected override void OnUpload( KaCommandBuffer commands )
	{
		mWorld.UpdateBuffers( commands );
		base.OnUpload( commands );
	}

	protected override void OnDraw( KaCommandBuffer commands, TextureRenderTarget renderTarget )
	{
		double clearUs = Utilities.GetSeconds();
		// If you don't clear, these will pile up to infinity. Should be pretty obvious
		for ( int i = 0; i < mWorkerCache.Length; i++ )
		{
			mWorkerCache[i].OpaqueCalls.Clear();
			mWorkerCache[i].AlphaTestCalls.Clear();
			mWorkerCache[i].RenderTarget = renderTarget;
		}

		double renderPassUs = Utilities.GetSeconds();
		// Pay attention to the structure here. We start a render pass with a "secondary command hint"
		// if it's gonna have secondary command buffers inside
		commands.RenderPass( renderTarget, secondaryCommandsHint: mRenderingMode is RenderingMode.Multithreaded, what: () =>
		{
			commands.ClearColour( renderTarget, 0, new( 0.0f, 0.13f, 0.13f, 1.0f ) );
			commands.ClearDepth( renderTarget, 0, 1.0f );

			if ( mWorld.Entities.Length > 0 )
			{
				if ( mRenderingMode is RenderingMode.Multithreaded )
				{
					RenderMultiThreaded( commands );
				}
				else if ( mRenderingMode is RenderingMode.Singlethreaded )
				{
					RenderSingleThreaded( commands, renderTarget );
				}
				else
				{
					RenderInstanced( commands, renderTarget );
				}
			}
		} );
		double drawEnd = Utilities.GetSeconds();

		clearUs = (renderPassUs - clearUs) * 1000.0 * 1000.0;
		renderPassUs = (drawEnd - renderPassUs) * 1000.0 * 1000.0;

		Console.WriteLine( "OnDraw:" );
		Console.WriteLine( $"Cache clearing: {clearUs:F1} us" );
		Console.WriteLine( $"Render pass:    {renderPassUs:F1} us" );
	}

	public override bool OnEvent( IntPtr window, SDL.Event @event )
	{
		if ( @event.Type == (int)SDL.EventType.KeyDown )
		{
			if ( @event.Key.Key == SDL.Keycode.Alpha1 )
			{
				mRenderingMode = RenderingMode.Singlethreaded;
			}
			else if ( @event.Key.Key == SDL.Keycode.Alpha2 )
			{
				mRenderingMode = RenderingMode.Multithreaded;
			}
			else if ( @event.Key.Key == SDL.Keycode.Alpha3 )
			{
				mRenderingMode = RenderingMode.Instanced;
			}
		}

		return base.OnEvent( window, @event );
	}

	public override void Dispose()
	{
		foreach ( var bucket in mInstanceBuckets )
		{
			bucket.InstanceBuffer.Dispose();
		}

		foreach ( var work in mWorkerCache )
		{
			work.Commands.Dispose();
		}

		mThreadDispatcher.Dispose();

		mWorld.Dispose();
		mSampler.Dispose();
		mUploadHelper.Dispose();
		mPipelineLayout.Dispose();
		mPipelineLayoutInstanced.Dispose();
		mPipelineAlphaTest.Dispose();
		mPipelineOpaque.Dispose();
		mPipelineInstancedAlphaTest.Dispose();
		mPipelineInstancedOpaque.Dispose();
		mShaderSet.Vertex.Dispose();
		mShaderSetAlphaTest.Vertex.Dispose();
		mShaderSetInstanced.Vertex.Dispose();
		mShaderSetInstancedAlphaTest.Vertex.Dispose();
		base.Dispose();
	}
}
