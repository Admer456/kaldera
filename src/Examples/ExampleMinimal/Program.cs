// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

// If you count SDL, comments, error checking, window events and the like,
// this is indeed some 400 lines of code. However, without all that,
// the crux of this example is some 200 LoC. Quite compact for Vulkan :3

using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using ExampleBase;
using Kaldera;
using Kaldera.Abstractions.Buffers;
using Kaldera.Abstractions.Extensions;
using Kaldera.Abstractions.Memory;
using Kaldera.Abstractions.RenderTargets;
using Kaldera.Abstractions.Utilities;
using Kaldera.Extensions;
using Kaldera.Names;
using Kaldera.Objects;
using SDL3;
using Silk.NET.Core;
using Silk.NET.Vulkan;
using Result = Kaldera.Result;

namespace ExampleMinimal;

internal static partial class Program
{
	#region Application

	// High-level overview. Start up the platform (SDL, GLFW, Win32, X11/Wayland/whatever),
	// then initialise all graphics-related stuff and loop the loop
	private static void Main( string[] _ )
	{
		// For my examples I use SDL. It's nice, I like it :)
		// You may use GLFW or whatever else, it should work just fine
		if ( !InitSdl().Check() )
		{
			return;
		}

		// At this stage, we have an SDL window and all needed SDL subsystems initialised
		// So go ahead and create a Vulkan device, swapchain, buffers, load shaders...
		if ( !InitGraphics().Check() )
		{
			return;
		}

		// It's not a bad idea to wait for the GPU to finish doing some work (copying buffers etc.)
		// before we start rendering everything. Yeah.
		mDevice.WaitIdle();

		// HandleEvents also handles resizing!
		double frameStart = 0.0;
		while ( HandleEvents( ref frameStart ) )
		{
			if ( RenderFrame().Check() )
			{
				LimitFramerate( 100.0f, frameStart );
			}
		}

		ShutdownGraphics();
		ShutdownSdl();
	}

	private static Result RenderFrame()
	{
		mDevice.WaitIdle().Check();

		// Skip this frame if the render target isn't ready
		// Cases where this happens: window minimised, just resized etc.
		if ( !mRenderTarget.Prepare().Get( out var error, out var swapchainResult ) || swapchainResult is SwapchainResult.Skip )
		{
			return Result.Success();
		}

		// Begin recording rendering commands
		mCommands.Begin();
		// Capture some rendering commands into this render pass
		// The render target could be anything, like a window, a texture, a VR multiview etc.
		mCommands.RenderPass( mRenderTarget, () =>
		{
			mCommands.ClearColour( mRenderTarget, 0, new( 0.0f, 0.13f, 0.13f, 1.0f ) );

			// All dynamic state params need to be set before any rendering
			// Viewport and scissor are part of said state
			mCommands.SetViewport( 0, mRenderTarget.Extent, 0.0f, 1.0f );
			mCommands.SetScissor( 0, mRenderTarget.Extent );

			// Glorptastic
			mCommands.BindPipeline( mPipeline );
			mCommands.BindVertexBuffer( mVertexBuffer, 0 );
			mCommands.BindIndexBuffer( mIndexBuffer );
			mCommands.DrawIndexed( 3, 1 );
		} );
		// It is now ready to be submitted to the GPU
		mCommands.End();

		// The command buffer is submitted together with the render target, so that
		// we can know when the RT is ready to present again etc.
		mQueue.Submit( mCommands, mRenderTarget );

		// "My dear OS/UI framework/compositor, please put the image on the screen now"
		return mQueue.Present( mRenderTarget );
	}

	private static bool HandleEvents( ref double frameStart )
	{
		frameStart = GetSeconds();

		while ( SDL.PollEvent( out SDL.Event ev ) )
		{
			switch ( (SDL.EventType)ev.Type )
			{
				case SDL.EventType.Quit:
					return false;

				case SDL.EventType.WindowResized:
					SDL.GetWindowSize( mWindow, out int width, out int height );
					mRenderTarget.RequestResize( new( width, height ) );
					break;
			}
		}

		return true;
	}

	private static Stopwatch mStopwatch = Stopwatch.StartNew();

	private static double GetSeconds()
		=> (double)mStopwatch.ElapsedTicks / Stopwatch.Frequency;

	private static void LimitFramerate( double framerate, double frameStart )
	{
		// Really, really wonky frame limiter
		double frameDuration = GetSeconds() - frameStart;
		double toSleep = (1.0f / framerate) - frameDuration;
		if ( toSleep <= 0.0 )
		{
			return;
		}

		Thread.Sleep( TimeSpan.FromSeconds( toSleep ) );
	}

	#endregion

	#region Graphics

	private static KaInstance mInstance = null!;
	private static KaPhysicalDevice mPhysicalDevice = null!;
	private static KaDevice mDevice = null!;
	private static KaQueue mQueue = null!;
	private static KaCommandBuffer mCommands = null!;
	private static KaGraphicsPipeline mPipeline = null!;
	private static SwapchainRenderTarget mRenderTarget = null!;

	private static VertexShaderSet mShaderSet = null!;
	private static SimpleAllocator mAllocator = null!;
	private static VertexBuffer<VertexData> mVertexBuffer = null!;
	private static IndexBuffer mIndexBuffer = null!;

	[StructLayout( LayoutKind.Sequential )]
	private struct VertexData : IVertexData
	{
		public Vector3 Position;
		public Vector2 Uv;

		public static int SizeInBytes => 3 * 4 + 2 * 4;

		/// <summary>
		/// Look at <see cref="GraphicsPipelineOptions.VertexInputs"/>.
		/// </summary>
		public static VertexAttribute[] VertexAttributes =>
		[
			new() { Offset = 0, Format = Format.R32G32B32Sfloat },
			new() { Offset = 3 * 4, Format = Format.R32G32Sfloat }
		];

		// TODO: Upgrade to .NET 10, use static property extension
		public static VertexInputLayout InputLayout => new VertexData().GetInputLayout();
	}

	private static Result InitGraphics()
	{
		// Vulkan initialisation
		if ( !Vulkan.Init().Check() )
		{
			return new Error( "InitGraphics: Could not init context" );
		}

		// You can check for the supported Vulkan version beforehand
		Version32 supportedVersion = Vulkan.QueryInstanceVersion();
		Console.WriteLine( $"Vulkan version: {supportedVersion.Major}.{supportedVersion.Minor}.{supportedVersion.Patch}" );

		// Obviously you don't have to do this, but it is nice to see what your GPU supports!
		Console.WriteLine( "Supported Vulkan layers:" );
		foreach ( string layer in Vulkan.GetInstanceLayers() )
		{
			Console.WriteLine( $" * {layer}" );
		}

		Console.WriteLine( "Supported Vulkan instance extensions:" );
		foreach ( string ext in Vulkan.GetInstanceExtensions() )
		{
			Console.WriteLine( $" * {ext}" );
		}

		// Starts up a Vulkan instance, selects a GPU, and creates a queue. The queue is central to so many
		// GPU operations, because you can submit commands to it and present images to the screen with it
		CreateDeviceAndQueue();

		// A command buffer is where you'll write down series of commands for the GPU. Copy this
		// buffer into that buffer, clear this image, draw these triangles, compute XYZ etc.
		CreateCommandBuffer();

		// A render target can be a window, an arbitrary texture image, a VR headset's display or whatever
		CreateRenderTarget();

		// A pipeline can be thought of as a "rendering preset". Use this shader, use that shader,
		// take geometry of a certain format, use opaque/additive/transparent blending, cull back faces etc.
		CreatePipeline().Check();

		// Traditional vertex buffer + index buffer setup
		CreateGeometry().Check();

		return Result.Success();
	}

	// Vulkan can give us feedback on how we're using it. Hopefully you won't trigger this in your projects, but
	// this is still a good way to catch improper usage of Vulkan, as well as a way to find bugs in this library...
	private static void VulkanDebugLog( DebugReportFlagsEXT flags, DebugReportObjectTypeEXT objectType, string message )
	{
		Debug.WriteLine( $"[{flags}] ({objectType}) {message}" );
	}

	private static string[] GetSdlInstanceExtensions()
		=> SDL.VulkanGetInstanceExtensions( out _ ) ?? [];

	private static void CreateDeviceAndQueue()
	{
		// Before creating an instance, you can check for any needed extensions
		//Vulkan.GetInstanceExtensions().Any( ext => ext is "VK_KHR_surface" );

		var graphicsContext = Startup.CreateVulkan14Context( new()
		{
			ApplicationName = "VulkanTest",
			EngineName = "ExampleEngine5",
#if DEBUG
			LogMethod = VulkanDebugLog,
			// If this is set, VK_EXT_debug_report will be loaded, and you will receive debug logs
			LogLevel = VulkanDebugLogLevel.Warning,
			// Validation layers are awesomesauce, they let you know if you're doing something wrong
			OptionalLayers = [LayerNames.KhronosValidation],
#endif
			// In this case, SDL will give us some needed extensions for windowing,
			// and Startup will add VK_KHR_portability_subset there for compatibility with MoltenVK,
			// as well as some other extensions like VK_KHR_surface
			InstanceExtensions = GetSdlInstanceExtensions()
		} ).Checked();

		mDevice = graphicsContext.Device;
		mQueue = graphicsContext.Queue;
		mPhysicalDevice = mDevice.Physical;
		mInstance = mDevice.Instance;
	}

	private static void CreateCommandBuffer()
	{
		mCommands = KaCommandBuffer.CreatePrimary( mQueue ).Checked();
	}

	private static void CreateRenderTarget()
	{
		// Standard stuff, extract native window pointers/handles
		SurfaceData surfaceData = ExampleWindowHelper.GetWindowSurfaceData( mWindow ).Checked();
		SurfaceKHR surface = mInstance.CreateSurface( surfaceData ).Checked();

		// This is an abstraction over Swapchain. It makes things a lot easier as it handles resizing, image views, sync etc.
		SDL.GetWindowSizeInPixels( mWindow, out int windowWidth, out int windowHeight );
		mRenderTarget = SwapchainRenderTarget.Create( mQueue, new()
		{
			Surface = surface,
			Buffering = BufferingType.Triple,
			InitialSize = new( windowWidth, windowHeight )
		} ).Checked();
	}

	private static Result CreatePipeline()
	{
		// Quick and dirty little offline shader loader
		Result<KaShader> LoadShader( string shaderPath )
			=> !File.Exists( shaderPath )
				? new Error( $"CreatePipeline: Cannot find shader: '{shaderPath}'" )
				: KaShader.Create( mDevice, File.ReadAllBytes( shaderPath ) );

		Result<KaShader> shaderFile = LoadShader( "default.spv" );
		if ( !shaderFile.Check() )
		{
			return new Error( "Could not load shader" );
		}

		// You can compile multiple entry points into the same shader module
		// In this case, the shader has both a VertexMain and a PixelMain
		mShaderSet = new VertexShaderSet( shaderFile, "VertexMain", shaderFile, "PixelMain" );

		mPipeline = KaGraphicsPipeline.Create( mDevice, new()
		{
			// No shader resources for now
			ResourceLayout = null,
			// Vertex input format (Position + Normal + UV, or some other combination...)
			// This can be assembled manually, in case of a data-driven shader system and such
			VertexInputs = [VertexData.InputLayout],
			ShaderSet = mShaderSet,
			// The viewport and scissor are set up each time when rendering to mRenderTarget
			DynamicStates = [DynamicState.Viewport, DynamicState.Scissor],
			// You'll be using this one 99% of the time
			Topology = PrimitiveTopology.TriangleList,
			// Render to one color image with opaque blending
			Color = new() { Attachments = [(BlendAttachments.Opaque, Format.B8G8R8A8Unorm)] },
			// No depth buffer
			DepthStencil = null,
			// Typical render mode
			Rasterizer = new()
			{
				PolygonMode = PolygonMode.Fill,
				CullMode = CullModeFlags.BackBit,
				FrontFace = FrontFace.Clockwise
			}
		} ).Checked();

		return Result.Success();
	}

	private static Result CreateGeometry()
	{
		VertexData[] data =
		[
			new() { Position = new( 0.0f, 0.5f, 0.0f ), Uv = new( 0.5f, 1.0f ) },
			new() { Position = new( 0.5f, -0.5f, 0.0f ), Uv = new( 1.0f, 0.0f ) },
			new() { Position = new( -0.5f, -0.5f, 0.0f ), Uv = new( 0.0f, 0.0f ) },
		];

		// An allocator determines how buffers are laid out in video memory. Right now we're using
		// a "dumb" one - for each buffer it allocates a unique block of GPU memory. This is not good,
		// but it works for a tutorial.
		mAllocator = SimpleAllocator.Create( mDevice, mQueue );

		// Traditionally in Vulkan, you'd do a multistep process in order to upload data.
		// Here, though, this is all done for you in a few simple utilities :)
		// This creates one big happy staging buffer and copies to the vertex & index buffers respectively
		UploadHelper builder = UploadHelper.Create( mAllocator, capacityInBytes: 1024 * 1024 );

		// This part copies the data into the buffer builder and prepares buffer objects.
		// It also issues copy commands to an internal command buffer
		mVertexBuffer = builder.CommitVertexBuffer( data.AsSpan() );
		mIndexBuffer = builder.CommitIndexBuffer( [0, 1, 2] );

		// Effectively just mQueue.Submit( builder.CommandBuffer )
		builder.Upload();

		// The builder does *not* own the created vertex buffers etc.
		// It is your responsibility to dispose of them later
		mDevice.WaitIdle();
		builder.Dispose();

		return Result.Success();
	}

	private static void ShutdownGraphics()
	{
		mDevice.WaitIdle();

		mIndexBuffer.Dispose();
		mVertexBuffer.Dispose();
		mPipeline.Dispose();
		mShaderSet.Vertex.Dispose();
		mRenderTarget.Dispose();
		mCommands.Dispose();
		mDevice.Dispose();
		mInstance.Dispose();
		Vulkan.Shutdown();
	}

	#endregion
}
