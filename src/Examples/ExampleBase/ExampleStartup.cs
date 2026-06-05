// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using System.Diagnostics;
using Kaldera;
using Kaldera.Abstractions.Extensions;
using Kaldera.Abstractions.RenderTargets;
using Kaldera.Extensions;
using Kaldera.Interfaces;
using Kaldera.Objects;
using SDL3;
using Silk.NET.Vulkan;

namespace ExampleBase;

public static class ExampleStartup
{
	public static void Run( string title, int width, int height, IExample exampleApp, string[] args )
	{
		if ( !SDL.Init( SDL.InitFlags.Video | SDL.InitFlags.Events ) )
		{
			Console.WriteLine( "SDL failed to initialise" );
			return;
		}

		bool limitFramerate = !args.Contains( "-nocap" );

		string[] sdlInstanceExtensions = SDL.VulkanGetInstanceExtensions( out _ ) ?? [];

		VulkanBootstrapOptions bootstrapOptions = new()
		{
			InstanceExtensions = sdlInstanceExtensions.ToList()
		};
		exampleApp.Setup( bootstrapOptions );
		VulkanBootstrap bootstrap = VulkanBootstrap.Create( bootstrapOptions ).Checked();

		KaInstance instance = bootstrap.Instance;
		KaDevice device = bootstrap.Device;
		KaQueue queue = bootstrap.Queue;
		IResourceAllocator allocator = bootstrap.Allocator;

		IntPtr sdlWindow = SDL.CreateWindow( title, width, height, SDL.WindowFlags.Vulkan | SDL.WindowFlags.Resizable );
		SwapchainRenderTarget renderTarget = CreateRenderTarget( instance, queue, sdlWindow, width, height ).Checked();

		uint displayId = SDL.GetDisplayForWindow( sdlWindow );
		SDL.DisplayMode? displayMode = SDL.GetCurrentDisplayMode( displayId );
		if ( displayMode is null )
		{
			Console.WriteLine( "Failed to obtain display mode" );
			return;
		}

		if ( !exampleApp.Init( instance, device, queue, allocator ).Check() )
		{
			Console.WriteLine( "Failed to start example app" );
			return;
		}

		device.WaitIdle();

		float refreshRate = displayMode.Value.RefreshRate;
		float lastFrametime = 1.0f / 100.0f;
		while ( HandleEvents( sdlWindow, renderTarget, exampleApp, out double frameStart ) )
		{
			device.WaitIdle().Check();

			var prepareResult = renderTarget.Prepare();
			if ( !prepareResult.Check() )
			{
				continue;
			}

			if ( (SwapchainResult)prepareResult is SwapchainResult.Skip )
			{
				continue;
			}

			if ( !exampleApp.OnFrame( lastFrametime, renderTarget ).Check() )
			{
				continue;
			}

			if ( !queue.Present( renderTarget ).Check() )
			{
				continue;
			}

			if ( limitFramerate )
			{
				LimitFramerate( refreshRate, frameStart );
			}
			lastFrametime = (float)(GetSeconds() - frameStart);
		}

		device.WaitIdle();
		exampleApp.Dispose();
		renderTarget.Dispose();
		bootstrap.Dispose();
		SDL.Quit();
	}

	private static Stopwatch mStopwatch = Stopwatch.StartNew();

	private static double GetSeconds()
		=> (double)mStopwatch.ElapsedTicks / Stopwatch.Frequency;

	private static void LimitFramerate( double framerate, double frameStart )
	{
		// Somewhat okay-ish frame limiter. Stable and doesn't eat a whole thread
		double frameDuration = GetSeconds() - frameStart;
		double toSleep = (1.0f / framerate) - frameDuration;
		if ( toSleep <= 0.0f )
		{
			return;
		}

		// If we have to wait for 2 or more milliseconds, use Thread.Sleep. 2 because the OS
		// may or may not quite respect 1ms of sleep, so it gives us headroom for the next step...
		TimeSpan sleepSpan = TimeSpan.FromSeconds( toSleep );
		if ( sleepSpan.Milliseconds > 2 )
		{
			Thread.Sleep( sleepSpan.Milliseconds );
		}

		// For more fine-grained "sleep" we use spin-waiting. Normally this would
		// "eat" a thread, but we're doing it for a relatively small amount of time
		frameDuration = GetSeconds() - frameStart;
		while ( frameDuration <= 1.0 / framerate )
		{
			frameDuration = GetSeconds() - frameStart;
			Thread.SpinWait( 10 );
		}
	}

	private static bool HandleEvents( IntPtr sdlWindow, SwapchainRenderTarget renderTarget, IExample example, out double frameStart )
	{
		frameStart = GetSeconds();

		while ( SDL.PollEvent( out SDL.Event ev ) )
		{
			if ( !example.OnEvent( sdlWindow, ev ) )
			{
				return false;
			}

			switch ( (SDL.EventType)ev.Type )
			{
				case SDL.EventType.Quit:
					return false;

				case SDL.EventType.WindowResized:
					SDL.GetWindowSize( sdlWindow, out int width, out int height );
					renderTarget.RequestResize( new( width, height ) );
					break;
			}
		}

		return true;
	}

	private static Result<SwapchainRenderTarget> CreateRenderTarget( KaInstance instance, KaQueue queue, IntPtr sdlWindow, int width, int height )
	{
		SurfaceData surfaceData = ExampleWindowHelper.GetWindowSurfaceData( sdlWindow ).Checked();
		SurfaceKHR surface = instance.CreateSurface( surfaceData ).Checked();

		// This is an abstraction over Swapchain. It makes things a lot easier as it handles resizing, image views, sync etc.
		return SwapchainRenderTarget.Create( queue, new()
		{
			Surface = surface,
			Buffering = BufferingType.Triple,
			InitialSize = new( width, height )
		} );
	}
}
