// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using System.Numerics;
using ExampleBase;
using Kaldera;
using Kaldera.Abstractions.Buffers;
using Kaldera.Abstractions.Extensions;
using Kaldera.Abstractions.RenderTargets;
using Kaldera.Abstractions.Utilities;
using Kaldera.Extensions;
using Kaldera.Interfaces;
using Kaldera.Objects;
using SDL3;
using Silk.NET.Vulkan;
using Result = Kaldera.Result;

ExampleStartup.Run( "Kaldera Example - Hello instanced rendering!", 1600, 900, new ExampleInstanced(), args );

/// <summary>
/// How to render thousands of instanced objects.
/// </summary>
internal class ExampleInstanced : IExample
{
	private VertexShaderSet mScreenShaderSet = null!;
	private KaLayout mScreenPipelineLayout = null!;
	private KaGraphicsPipeline mScreenPipeline = null!;

	private float mResizeTimer;
	private Vector2 mNewSize = Vector2.Zero;
	private TextureRenderTarget mRenderTarget = null!;
	private Camera3D mCamera = null!;
	private KaSampler mSampler;
	private StorageBuffer mInstanceBuffer = null!;
	private IndexBuffer mIndexBuffer = null!;
	private VertexBuffer<PosNormal> mVertexBuffer = null!;
	private VertexShaderSet mShaderSet = null!;
	private KaLayout mPipelineLayout = null!;
	private KaGraphicsPipeline mPipeline = null!;
	private KaCommandBuffer mCommands = null!;
	private KaQueue mQueue = null!;

	public Result Init( KaInstance instance, KaDevice device, KaQueue queue, IResourceAllocator allocator )
	{
		mCommands = KaCommandBuffer.CreatePrimary( queue ).Checked();

		mShaderSet = Utilities.LoadGraphicsShaderSet( device, "hello_instanced.spv" ).Checked();
		mScreenShaderSet = Utilities.LoadGraphicsShaderSet( device, "screen.spv" ).Checked();

		mSampler = KaSampler.Create( device, new()
		{
			MagFilter = Filter.Linear,
			MinFilter = Filter.Linear,
			MipmapMode = SamplerMipmapMode.Linear,
			MipLodBias = 0,
			MaxLod = 0,
			MinLod = 0,
			AddressModeU = SamplerAddressMode.ClampToEdge,
			AddressModeV = SamplerAddressMode.ClampToEdge,
			AddressModeW = SamplerAddressMode.ClampToEdge,
			MaxAnisotropy = null,
			BorderColour = BorderColor.FloatTransparentBlack
		} ).Checked();

		LayoutOptions layoutOptions = LayoutBuilder.Begin()
			.StructuredSet( s => s
				.UniformBuffer() // Set #0 binding #0
				.StorageBuffer() ) // Set #0 binding #1
			.Build();

		mPipelineLayout = KaLayout.Create( device, layoutOptions ).Checked();
		mPipeline = KaGraphicsPipeline.Create( device, new()
		{
			ResourceLayout = mPipelineLayout,
			VertexInputs = [PosNormal.VertexInputLayout],
			ShaderSet = mShaderSet,
			DynamicStates = [DynamicState.Viewport, DynamicState.Scissor],
			Topology = PrimitiveTopology.TriangleList,
			Color = new() { Attachments = [(BlendAttachments.Opaque, Format.B8G8R8A8Unorm)] },
			DepthStencil = new()
			{
				DepthFormat = Format.D32SfloatS8Uint,
				DepthWrite = true,
				DepthComparison = CompareOp.Less,
				StencilState = null,
			},
			Rasterizer = new()
			{
				PolygonMode = PolygonMode.Fill,
				CullMode = CullModeFlags.BackBit,
				FrontFace = FrontFace.Clockwise
			},
			Multisample = new()
			{
				RasterizationSamples = SampleCountFlags.Count4Bit
			}
		} ).Checked();

		layoutOptions = LayoutBuilder.Begin()
			.StructuredSet( s => s
				.SampledTexture()
				.Sampler() )
			.Build();

		mScreenPipelineLayout = KaLayout.Create( device, layoutOptions ).Checked();
		mScreenPipeline = KaGraphicsPipeline.Create( device, new()
		{
			ResourceLayout = mScreenPipelineLayout,
			VertexInputs = null,
			ShaderSet = mScreenShaderSet,
			DynamicStates = [DynamicState.Viewport, DynamicState.Scissor],
			Topology = PrimitiveTopology.TriangleFan,
			Color = new() { Attachments = [(BlendAttachments.Opaque, Format.B8G8R8A8Unorm)] },
			DepthStencil = null,
			Rasterizer = new()
			{
				PolygonMode = PolygonMode.Fill,
				CullMode = CullModeFlags.None,
				FrontFace = FrontFace.Clockwise
			}
		} ).Checked();

		var renderTargetOptions = TextureRenderTargetOptions.ColourDepthStencil( 1600, 900, Format.B8G8R8A8Unorm ) with
		{
			Samples = SampleCountFlags.Count4Bit
		};

		mRenderTarget = TextureRenderTarget.Create( allocator, renderTargetOptions ).Checked();

		// We'll generate a 3D grid. This is roughly 32k instances
		Vector3[] instancePositions = Enumerable.Range( 0, 32 * 32 * 32 ).Select( i =>
		{
			float x = i % 32;
			float y = (i % (32 * 32)) / 32;
			float z = i / (32 * 32);

			// Centre -> (n-1)/2
			x -= 31.0f / 2.0f;
			y -= 31.0f / 2.0f;
			z -= 31.0f / 2.0f;

			// Space em 1.5m apart
			return new Vector3( x, y, z ) * 1.5f;
		} ).ToArray();

		Matrix4x4[] instanceMatrices = instancePositions
			.Select( Matrix4x4.CreateTranslation )
			.ToArray();

		mCamera = Camera3D.Create( allocator ).Checked();

		UploadHelper builder = UploadHelper.Create( allocator, 16 * 1024 * 1024 );
		var mesh = MeshBuilder
			.Begin()
			.Cube( Vector3.Zero )
			.Export( builder )
			.Checked();

		mInstanceBuffer = builder.CommitStorageBuffer( instanceMatrices.AsSpan() ).Checked();

		builder.Upload();

		device.WaitIdle();
		builder.Dispose();

		mVertexBuffer = mesh.VertexBuffer;
		mIndexBuffer = mesh.IndexBuffer;
		mQueue = queue;

		mCamera.UpdateProjection( 16.0f / 9.0f );
		mCamera.Position = new( 0.0f, -4.0f, 1.0f );
		mCamera.PitchYawRoll = new( -30.0f, 0.0f, 0.0f );

		return Result.Success();
	}

	public Result OnFrame( float dt, SwapchainRenderTarget windowRt )
	{
		if ( mResizeTimer > 0.0f )
		{
			mResizeTimer -= dt;

			if ( mResizeTimer <= 0.0f )
			{
				mResizeTimer = -1.0f;
				mQueue.Device.WaitIdle();
				mRenderTarget.RequestResize( mNewSize );
			}
		}

		mCamera.OnFrame( dt );

		mCommands.Begin();
		mCamera.UploadData( mCommands );
		mCommands.RenderPass( mRenderTarget, () =>
		{
			mCommands.ClearColour( mRenderTarget, 0, new( 0.0f, 0.13f, 0.13f, 1.0f ) );
			mCommands.ClearDepth( mRenderTarget, 0, 1.0f );
			mCommands.BindPipeline( mPipeline );
			mCommands.SetViewport( 0, mRenderTarget.Extent, 0.0f, 1.0f );
			mCommands.SetScissor( 0, mRenderTarget.Extent );

			mCommands.PushUniformBuffer( mCamera.UniformBuffer, 0 );
			mCommands.PushStorageBuffer( mInstanceBuffer, 0, 1 );

			mCommands.BindVertexBuffer( mVertexBuffer, 0 );
			mCommands.BindIndexBuffer( mIndexBuffer );

			// Besides sending the instance buffer to the shader, we'll also use a higher-than-1 instance count :3
			mCommands.DrawIndexed( indexCount: mIndexBuffer.Count, instanceCount: 32 * 32 * 32 );
		} );
		mCommands.RenderPass( windowRt, () =>
		{
			mCommands.BindPipeline( mScreenPipeline );
			mCommands.SetViewportFlipped( 0, windowRt.Extent, 0.0f, 1.0f );
			mCommands.SetScissor( 0, windowRt.Extent );

			mCommands.PushSampledTexture( mRenderTarget.ColourAttachments.Value, 0 );
			mCommands.PushSampler( mSampler, 0, 1 );
			mCommands.Draw( vertexCount: 3, instanceCount: 1 );
		} );
		mCommands.End();
		mQueue.Submit( mCommands, windowRt );
		return Result.Success();
	}

	public bool OnEvent( IntPtr window, SDL.Event @event )
	{
		mCamera.OnEvent( window, @event );

		if ( (SDL.EventType)@event.Type is SDL.EventType.WindowResized )
		{
			SDL.GetWindowSizeInPixels( window, out int width, out int height );

			mResizeTimer = 0.5f;
			mNewSize = new( width, height );
		}

		return true;
	}

	public void Dispose()
	{
		mSampler.Dispose();
		mScreenPipelineLayout.Dispose();
		mScreenPipeline.Dispose();
		mScreenShaderSet.Vertex.Dispose();
		mRenderTarget.Dispose();
		mCamera.Dispose();
		mInstanceBuffer.Dispose();
		mVertexBuffer.Dispose();
		mIndexBuffer.Dispose();
		mPipelineLayout.Dispose();
		mPipeline.Dispose();
		mShaderSet.Vertex.Dispose();
		mCommands.Dispose();
	}
}
