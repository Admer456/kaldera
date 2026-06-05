// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera;
using Kaldera.Extensions;
using SDL3;

namespace ExampleBase;

public static class ExampleWindowHelper
{
	public static Result<SurfaceData> GetWindowSurfaceData( IntPtr window )
	{
		uint windowPropertyHandle = SDL.GetWindowProperties( window );

		if ( Environment.OSVersion.Platform is PlatformID.Win32NT )
		{
			return new Win32SurfaceData(
				HInstance: SDL.GetPointerProperty( windowPropertyHandle, "SDL.window.win32.instance", 0 ),
				Hwnd: SDL.GetPointerProperty( windowPropertyHandle, "SDL.window.win32.hwnd", 0 )
			);
		}

		if ( Environment.OSVersion.Platform is PlatformID.Unix )
		{
			string? videoDriver = SDL.GetCurrentVideoDriver();
			if ( videoDriver is "wayland" )
			{
				return new WaylandSurfaceData(
					Surface: SDL.GetPointerProperty( windowPropertyHandle, "SDL.window.wayland.surface", 0 ),
					Display: SDL.GetPointerProperty( windowPropertyHandle, "SDL.window.wayland.display", 0 )
				);
			}

			if ( videoDriver is "xlib" )
			{
				return new X11SurfaceData(
					Window: SDL.GetPointerProperty( windowPropertyHandle, "SDL.window.x11.window", 0 ),
					Display: SDL.GetPointerProperty( windowPropertyHandle, "SDLw.window.x11.display", 0 )
				);
			}

			return new Error( $"Unsupported windowing protocol '{videoDriver}'" );
		}

		if ( Environment.OSVersion.Platform is PlatformID.MacOSX )
		{
			return new Error( "MacOS is not yet supported, sorry :(" );
		}

		return new Error( "Unsupported platform" );
	}
}
