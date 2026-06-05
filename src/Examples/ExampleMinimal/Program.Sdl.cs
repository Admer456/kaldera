// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera;
using SDL3;

namespace ExampleMinimal;

internal static partial class Program
{
	private static IntPtr mWindow;

	private static Result InitSdl()
	{
		for ( int i = 0; i < SDL.GetNumVideoDrivers(); i++ )
		{
			Console.WriteLine( $"Driver available: {SDL.GetVideoDriver( i )}" );
		}

		if ( !SDL.Init( SDL.InitFlags.Video | SDL.InitFlags.Events ) )
		{
			return new Error( "SDL failed to initialise" );
		}

		mWindow = SDL.CreateWindow( "Kaldera - Basic example", 1600, 900, SDL.WindowFlags.Vulkan | SDL.WindowFlags.Resizable );
		return Result.Success();
	}

	private static void ShutdownSdl()
	{
		SDL.DestroyWindow( mWindow );
		SDL.Quit();
	}
}
