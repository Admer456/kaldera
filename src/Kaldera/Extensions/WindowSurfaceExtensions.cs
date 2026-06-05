// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Objects;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Kaldera.Extensions;

public abstract record SurfaceData;

public sealed record Win32SurfaceData( IntPtr Hwnd, IntPtr HInstance ) : SurfaceData;

public sealed record X11SurfaceData( IntPtr Window, IntPtr Display ) : SurfaceData;

public sealed record WaylandSurfaceData( IntPtr Surface, IntPtr Display ) : SurfaceData;

public sealed record MacSurfaceData( IntPtr Layer ) : SurfaceData;

public static unsafe class WindowSurfaceExtensions
{
	private static KhrWin32Surface? mWin32Surface;
	private static KhrXlibSurface? mXlibSurface;
	private static KhrWaylandSurface? mWaylandSurface;
	private static ExtMetalSurface? mMetalSurface;

	public static Result<KhrWin32Surface> TryGetWin32SurfaceApi( this KaInstance self )
	{
		if ( mWin32Surface is null )
		{
			if ( !Vulkan.Vk.TryGetInstanceExtension( self.VkInstance, out mWin32Surface ) )
			{
				return new Error( "Cannot obtain instance extension KhrWin32Surface" );
			}
		}

		return mWin32Surface is null ? new Error( "KhrWin32Surface not available" ) : mWin32Surface;
	}

	public static Result<KhrXlibSurface> TryGetXlibSurfaceApi( this KaInstance self )
	{
		if ( mXlibSurface is null )
		{
			if ( !Vulkan.Vk.TryGetInstanceExtension( self.VkInstance, out mXlibSurface ) )
			{
				return new Error( "Cannot obtain instance extension KhrXlibSurface" );
			}
		}

		return mXlibSurface is null ? new Error( "KhrXlibSurface not available" ) : mXlibSurface;
	}

	public static Result<KhrWaylandSurface> TryGetWaylandSurfaceApi( this KaInstance self )
	{
		if ( mWaylandSurface is null )
		{
			if ( !Vulkan.Vk.TryGetInstanceExtension( self.VkInstance, out mWaylandSurface ) )
			{
				return new Error( "Cannot obtain instance extension KhrWaylandSurface" );
			}
		}

		return mWaylandSurface is null ? new Error( "KhrWaylandSurface not available" ) : mWaylandSurface;
	}

	public static Result<ExtMetalSurface> TryGetMetalSurfaceApi( this KaInstance self )
	{
		if ( mMetalSurface is null )
		{
			if ( !Vulkan.Vk.TryGetInstanceExtension( self.VkInstance, out mMetalSurface ) )
			{
				return new Error( "Cannot obtain instance extension ExtMetalSurface" );
			}
		}

		return mMetalSurface is null ? new Error( "ExtMetalSurface not available" ) : mMetalSurface;
	}

	public static Result<SurfaceKHR> CreateWin32Surface( this KaInstance self, Win32SurfaceData data )
	{
		var surfaceApi = self.TryGetWin32SurfaceApi();
		if ( !surfaceApi.Get( out var error, out var surfaceApiValue ) )
		{
			return error;
		}

		Win32SurfaceCreateInfoKHR ci = new()
		{
			Hwnd = data.Hwnd,
			Hinstance = data.HInstance
		};

		VkResult result = surfaceApiValue.CreateWin32Surface( self.VkInstance, &ci, null, out var surface );
		if ( result is not VkResult.Success )
		{
			return new Error( $"Couldn't create Win32 surface: {result}" );
		}

		return surface;
	}

	public static Result<SurfaceKHR> CreateX11Surface( this KaInstance self, X11SurfaceData data )
	{
		var surfaceApi = self.TryGetXlibSurfaceApi();
		if ( !surfaceApi.Get( out var error, out var surfaceApiValue ) )
		{
			return error;
		}

		XlibSurfaceCreateInfoKHR ci = new()
		{
			Window = data.Window,
			Dpy = (IntPtr*)data.Display
		};

		VkResult result = surfaceApiValue.CreateXlibSurface( self.VkInstance, &ci, null, out var surface );
		if ( result is not VkResult.Success )
		{
			return new Error( $"Couldn't create X11 surface: {result}" );
		}

		return surface;
	}

	public static Result<SurfaceKHR> CreateWaylandSurface( this KaInstance self, WaylandSurfaceData data )
	{
		var surfaceApi = self.TryGetWaylandSurfaceApi();
		if ( !surfaceApi.Get( out var error, out var surfaceApiValue ) )
		{
			return error;
		}

		WaylandSurfaceCreateInfoKHR ci = new()
		{
			// For some reason it sets it to ApplicationInfo??? Got to set them manually here
			SType = StructureType.WaylandSurfaceCreateInfoKhr,
			Surface = (IntPtr*)data.Surface,
			Display = (IntPtr*)data.Display
		};

		VkResult result = surfaceApiValue.CreateWaylandSurface( self.VkInstance, &ci, null, out var surface );
		if ( result is not VkResult.Success )
		{
			return new Error( $"Couldn't create Wayland surface: {result}" );
		}

		return surface;
	}

	public static Result<SurfaceKHR> CreateMacSurface( this KaInstance self, MacSurfaceData data )
	{
		var surfaceApi = self.TryGetMetalSurfaceApi();
		if ( !surfaceApi.Get( out var error, out var surfaceApiValue ) )
		{
			return error;
		}

		MetalSurfaceCreateInfoEXT ci = new()
		{
			PLayer = (IntPtr*)data.Layer
		};

		VkResult result = surfaceApiValue.CreateMetalSurface( self.VkInstance, &ci, null, out var surface );
		if ( result is not VkResult.Success )
		{
			return new Error( $"Couldn't create Wayland surface: {result}" );
		}

		return surface;
	}

	public static Result<SurfaceKHR> CreateSurface( this KaInstance self, SurfaceData surfaceData )
		=> surfaceData switch
		{
			Win32SurfaceData win32 => self.CreateWin32Surface( win32 ),
			X11SurfaceData x11 => self.CreateX11Surface( x11 ),
			WaylandSurfaceData wayland => self.CreateWaylandSurface( wayland ),
			MacSurfaceData mac => self.CreateMacSurface( mac ),
			_ => new Error( "CreateSurface: Platform not supported" )
		};

	public static void DestroySurface( this KaInstance instance, SurfaceKHR surface )
		=> Vulkan.Surface.DestroySurface( instance.VkInstance, surface, null );
}
