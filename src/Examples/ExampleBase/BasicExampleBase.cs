// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using System.Numerics;
using Kaldera.Abstractions.Extensions;
using Kaldera.Abstractions.RenderTargets;
using Kaldera.Abstractions.Utilities;
using Kaldera.Extensions;
using Kaldera.Interfaces;
using Kaldera.Names;
using Kaldera.Objects;
using SDL3;
using Silk.NET.Vulkan;
using Result = Kaldera.Result;

namespace ExampleBase;

public abstract class BasicExampleBase : IExample
{
	private KaCommandBuffer mCommands = null!;
	private VertexShaderSet mScreenShaderSet = null!;
	private KaLayout mScreenPipelineLayout = null!;
	private KaGraphicsPipeline mScreenPipeline = null!;

	private float mResizeTimer;
	private Vector2 mNewSize = Vector2.Zero;
	private TextureRenderTarget mMainRenderTarget = null!;
	private Camera3D mCamera = null!;
	private KaSampler mScreenSampler;
	private KaQueue mQueue = null!;

	public TextureRenderTarget MainRenderTarget => mMainRenderTarget;
	public KaQueue Queue => mQueue;
	public Camera3D Camera => mCamera;

	public virtual void Setup( VulkanBootstrapOptions options )
	{
		options.DeviceExtensions.Add( DeviceExtensionNames.ExtExtendedDynamicState3 );
		options.DeviceFeatureModifier = features => features.Add( new PhysicalDeviceExtendedDynamicState3FeaturesEXT
		{
			ExtendedDynamicState3PolygonMode = true
		} );
	}

	public virtual Result Init( KaInstance instance, KaDevice device, KaQueue queue, IResourceAllocator allocator )
	{
		mCommands = KaCommandBuffer.CreatePrimary( queue ).Checked();

		mScreenShaderSet = Utilities.LoadGraphicsShaderSet( device, "screen.spv" ).Checked();
		mScreenSampler = KaSampler.Create( device, new()
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

		mQueue = queue;
		mMainRenderTarget = TextureRenderTarget.Create( allocator, renderTargetOptions ).Checked();

		mCamera = Camera3D.Create( allocator ).Checked();
		mCamera.UpdateProjection( 16.0f / 9.0f );
		mCamera.Position = new( 0.0f, -4.0f, 2.0f );
		mCamera.PitchYawRoll = Vector3.Zero;

		return Result.Success();
	}

	public virtual bool OnEvent( IntPtr window, SDL.Event @event )
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

	protected virtual void OnUpdate( float dt )
	{
	}

	protected virtual void OnUpload( KaCommandBuffer commands )
	{
	}

	protected virtual void OnDraw( KaCommandBuffer commands, TextureRenderTarget renderTarget )
	{
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
				mMainRenderTarget.RequestResize( mNewSize );
			}
		}

		mCamera.OnFrame( dt );

		mCommands.Begin();
		mCamera.UploadData( mCommands );
		OnUpload( mCommands );
		OnDraw( mCommands, MainRenderTarget );
		mCommands.RenderPass( windowRt, () =>
		{
			mCommands.BindPipeline( mScreenPipeline );
			mCommands.SetViewportFlipped( 0, windowRt.Extent, 0.0f, 1.0f );
			mCommands.SetScissor( 0, windowRt.Extent );

			mCommands.PushSampledTexture( mMainRenderTarget.ColourAttachments.Value, 0 );
			mCommands.PushSampler( mScreenSampler, 0, 1 );
			mCommands.Draw( vertexCount: 3, instanceCount: 1 );
		} );
		mCommands.End();
		mQueue.Submit( mCommands, windowRt );
		return Result.Success();
	}

	public virtual void Dispose()
	{
		mScreenSampler.Dispose();
		mScreenPipelineLayout.Dispose();
		mScreenShaderSet.Vertex.Dispose();
		mScreenPipeline.Dispose();
		mMainRenderTarget.Dispose();
		mCamera.Dispose();
		mCommands.Dispose();
	}
}
