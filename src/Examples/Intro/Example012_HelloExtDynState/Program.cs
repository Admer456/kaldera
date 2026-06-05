// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using System.Numerics;
using System.Runtime.InteropServices;
using ExampleBase;
using Kaldera.Abstractions.Buffers;
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

ExampleStartup.Run( "Kaldera Example - Hello ext. dynamic state 3!", 1600, 900, new ExampleExtDynState(), args );

/// <summary>
/// Showcases extended dynamic state 3, enabling you to change a lot of render properties dynamically.
/// In this case, polygon mode.
/// </summary>
internal class ExampleExtDynState : IExample
{
	private PolygonMode mPolygonMode = PolygonMode.Fill;
	private IndexBuffer mIndexBuffer = null!;
	private VertexBuffer<VertexData> mVertexBuffer = null!;
	private VertexShaderSet mShaderSet = null!;
	private KaGraphicsPipeline mPipeline = null!;
	private KaCommandBuffer mCommands = null!;
	private KaQueue mQueue = null!;

	[StructLayout( LayoutKind.Sequential )]
	private struct VertexData : IVertexData
	{
		public Vector3 Position;

		public static int SizeInBytes => VertexInputLayout.Stride;

		public static VertexAttribute[] VertexAttributes => VertexInputLayout.Elements;

		public static readonly VertexInputLayout VertexInputLayout = new()
		{
			Stride = 12,
			Elements = [new() { Offset = 0, Format = Format.R32G32B32Sfloat }]
		};
	}

	// Polygon mode requires VK_EXT_extended_dynamic_state3... yeah
	public void Setup( VulkanBootstrapOptions options )
	{
		options.DeviceExtensions.Add( DeviceExtensionNames.ExtExtendedDynamicState3 );
		options.DeviceFeatureModifier = features => features.Add( new PhysicalDeviceExtendedDynamicState3FeaturesEXT
		{
			// Here are also some interesting ones:
			// ExtendedDynamicState3AlphaToCoverageEnable = true,
			// ExtendedDynamicState3ColorBlendEquation = true,
			// ExtendedDynamicState3ConservativeRasterizationMode = true,
			// ExtendedDynamicState3RasterizationSamples = true,
			ExtendedDynamicState3PolygonMode = true
		} );
	}

	public Result Init( KaInstance instance, KaDevice device, KaQueue queue, IResourceAllocator allocator )
	{
		mCommands = KaCommandBuffer.CreatePrimary( queue ).Checked();

		mShaderSet = Utilities.LoadGraphicsShaderSet( device, "hello_eds.spv" ).Checked();
		mPipeline = KaGraphicsPipeline.Create( device, new()
		{
			ResourceLayout = null,
			VertexInputs = [VertexData.VertexInputLayout],
			ShaderSet = mShaderSet,
			// We'll change the polygon mode in this example
			DynamicStates =
			[
				DynamicState.Viewport,
				DynamicState.Scissor,
				DynamicState.PolygonModeExt
			],
			Topology = PrimitiveTopology.TriangleList,
			Color = new() { Attachments = [(BlendAttachments.Opaque, Format.B8G8R8A8Unorm)] },
			DepthStencil = null,
			Rasterizer = new()
			{
				PolygonMode = PolygonMode.Fill, // This is ignored, but we'll set it regardless
				CullMode = CullModeFlags.BackBit,
				FrontFace = FrontFace.Clockwise
			}
		} ).Checked();

		VertexData[] data =
		{
			new() { Position = new( 0.2f, 0.5f, 0.0f ) },
			new() { Position = new( 0.36f, 0.0f, 0.0f ) },
			new() { Position = new( 0.2f, -0.5f, 0.0f ) },
			new() { Position = new( -0.2f, -0.5f, 0.0f ) },
			new() { Position = new( -0.36f, 0.0f, 0.0f ) },
			new() { Position = new( -0.2f, 0.5f, 0.0f ) }
		};

		uint[] indices =
		[
			0, 1, 2,
			0, 2, 3,
			0, 3, 5,
			3, 4, 5
		];

		UploadHelper builder = UploadHelper.Create( allocator, 16 * 1024 * 1024 );
		mVertexBuffer = builder.CommitVertexBuffer<VertexData>( data ).Checked();
		mIndexBuffer = builder.CommitIndexBuffer( indices ).Checked();

		builder.Upload();

		device.WaitIdle();
		builder.Dispose();

		mQueue = queue;

		return Result.Success();
	}

	public bool OnEvent( IntPtr window, SDL.Event @event )
	{
		if ( (SDL.EventType)@event.Type is SDL.EventType.KeyDown )
		{
			mPolygonMode = @event.Key.Scancode switch
			{
				SDL.Scancode.Alpha1 => PolygonMode.Point,
				SDL.Scancode.Alpha2 => PolygonMode.Line,
				SDL.Scancode.Alpha3 => PolygonMode.Fill,
				_ => mPolygonMode
			};
		}

		return true;
	}

	public Result OnFrame( float dt, SwapchainRenderTarget windowRt )
	{
		mCommands.Begin();
		mCommands.RenderPass( windowRt, () =>
		{
			mCommands.ClearColour( windowRt, 0, new( 0.0f, 0.13f, 0.13f, 1.0f ) );
			mCommands.BindPipeline( mPipeline );
			mCommands.SetViewport( 0, windowRt.Extent, 0.0f, 1.0f );
			mCommands.SetScissor( 0, windowRt.Extent );
			// When a pipeline is bound, its dynamic state is basically invalid. You must set up everything you wanna use
			mCommands.SetPolygonMode( mPolygonMode );

			mCommands.BindVertexBuffer( mVertexBuffer, 0 );
			mCommands.BindIndexBuffer( mIndexBuffer );

			mCommands.DrawIndexed( indexCount: 12, instanceCount: 1 );
		} );
		mCommands.End();
		mQueue.Submit( mCommands, windowRt );
		return Result.Success();
	}

	public void Dispose()
	{
		mVertexBuffer.Dispose();
		mIndexBuffer.Dispose();
		mPipeline.Layout.Dispose();
		mPipeline.Dispose();
		mShaderSet.Vertex.Dispose();
		mCommands.Dispose();
	}
}
