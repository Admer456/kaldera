// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using System.Runtime.InteropServices;
using Kaldera.Interfaces;

namespace Kaldera.Objects;

public struct ImageOptions
{
	public required ImageCreateFlags Flags;
	public required ImageType ImageType;
	public required Format Format;
	public required int Width;
	public required int Height;
	public required int Depth;
	public required uint MipLevels;
	public required uint ArrayLayers;
	public required SampleCountFlags Samples;
	public required ImageUsageFlags Usage;
	public bool Exportable;

	public static ImageOptions Common( Format format, int width, int height, int depth, int mips, int layers, ImageUsageFlags usage )
		=> new()
		{
			Flags = ImageCreateFlags.None,
			ImageType = (width, height, depth) switch
			{
				(_, 1, 1) => ImageType.Type1D,
				(_, _, 1) => ImageType.Type2D,
				_ => ImageType.Type3D,
			},
			Format = format,
			Width = width,
			Height = height,
			Depth = depth,
			MipLevels = (uint)mips,
			ArrayLayers = (uint)layers,
			Samples = SampleCountFlags.Count1Bit,
			Usage = usage
		};
}

public struct ImageViewOptions
{
	public required ImageViewCreateFlags Flags;
	public required ImageViewType ViewType;
	public required Format Format;
	public required ComponentMapping Components;
	public required ImageSubresourceRange SubresourceRange;

	public static ImageViewOptions Common( ImageOptions options, ImageAspectFlags aspect = ImageAspectFlags.ColorBit )
		=> new()
		{
			Flags = ImageViewCreateFlags.None,
			ViewType = (options.ArrayLayers, options.ImageType) switch
			{
				(1, ImageType.Type1D) => ImageViewType.Type1D,
				(1, ImageType.Type2D) => ImageViewType.Type2D,
				(1, ImageType.Type3D) => ImageViewType.Type3D,
				(_, ImageType.Type1D) => ImageViewType.Type1DArray,
				_ => ImageViewType.Type2DArray
			},
			Format = options.Format,
			Components = new(),
			SubresourceRange = new()
			{
				AspectMask = aspect,
				BaseArrayLayer = 0,
				LayerCount = options.ArrayLayers,
				BaseMipLevel = 0,
				LevelCount = options.MipLevels
			}
		};

	public static ImageViewOptions Cube( ImageOptions options, ImageAspectFlags aspect = ImageAspectFlags.ColorBit )
		=> Common( options, aspect ) with
		{
			ViewType = ImageViewType.TypeCube
		};
}

public unsafe struct KaImage : IMemoryBindable, IDisposable
{
	public required KaDevice Device { get; init; }
	public required VkImage VkImage { get; init; }

	public static Result<KaImage> Create( KaDevice device, ImageOptions options )
	{
		ImageCreateInfo imageInfo = new()
		{
			SType = StructureType.ImageCreateInfo,
			Flags = options.Flags,
			ImageType = options.ImageType,
			Format = options.Format,
			Extent = new( (uint)options.Width, (uint)options.Height, (uint)options.Depth ),
			MipLevels = options.MipLevels,
			ArrayLayers = options.ArrayLayers,
			Samples = options.Samples,
			Tiling = ImageTiling.Optimal,
			Usage = options.Usage,
			InitialLayout = ImageLayout.Undefined,
			SharingMode = SharingMode.Exclusive
		};

		ExternalMemoryImageCreateInfo externalMemory = new()
		{
			SType = StructureType.ExternalMemoryImageCreateInfo,
			HandleTypes = ExternalMemoryHandleTypeFlags.OpaqueFDBit
		};

		ExportMetalObjectCreateInfoEXT externalMemoryMac = new()
		{
			SType = StructureType.ExportMetalObjectCreateInfoExt,
			ExportObjectType = ExportMetalObjectTypeFlagsEXT.IosurfaceBitExt
		};

		if ( options.Exportable )
		{
			imageInfo.PNext = &externalMemory; // Linux

			if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) )
			{
				externalMemory.HandleTypes = ExternalMemoryHandleTypeFlags.OpaqueWin32Bit;
			}
			else if ( RuntimeInformation.IsOSPlatform( OSPlatform.OSX ) )
			{
				imageInfo.PNext = &externalMemoryMac;
			}
		}

		VkResult result = Vulkan.Vk.CreateImage( device.VkDevice, &imageInfo, null, out VkImage image );
		if ( result is not VkResult.Success )
		{
			return new Error( $"KaImage.Create: {result}" );
		}

		return new KaImage
		{
			Device = device,
			VkImage = image
		};
	}

	// TODO: KaImage.Import
	//  It should import existing images (e.g. D3D images), useful for interop with Avalonia etc. 

	public static KaImage FromExisting( KaDevice device, VkImage image )
		=> new()
		{
			Device = device,
			VkImage = image
		};

	public static VkImageView ViewFromExisting( KaDevice device, VkImage image, ImageViewOptions options )
	{
		ImageViewCreateInfo imageViewCreateInfo = new()
		{
			SType = StructureType.ImageViewCreateInfo,
			Flags = options.Flags,
			ViewType = options.ViewType,
			Image = image,
			Format = options.Format,
			Components = options.Components,
			SubresourceRange = options.SubresourceRange
		};

		Vulkan.Vk.CreateImageView( device.VkDevice, &imageViewCreateInfo, null, out VkImageView view );
		return view;
	}

	public VkImageView CreateView( ImageViewOptions options )
		=> ViewFromExisting( Device, VkImage, options );

	public MemoryRequirements GetMemoryRequirements()
	{
		Vulkan.Vk.GetImageMemoryRequirements( Device.VkDevice, VkImage, out MemoryRequirements result );
		return result;
	}

	public VkResult Bind( DeviceMemory memory, ulong offset )
		=> Vulkan.Vk.BindImageMemory( Device.VkDevice, VkImage, memory, offset );

	public void GetExportableInfo(
		out MemoryDedicatedAllocateInfo dedicatedAllocation,
		out ExportMemoryAllocateInfo exportAllocateInfo )
	{
		dedicatedAllocation = new()
		{
			SType = StructureType.MemoryDedicatedAllocateInfo,
			Image = VkImage
		};

		exportAllocateInfo = new()
		{
			SType = StructureType.ExportMemoryAllocateInfo,
			HandleTypes = RuntimeInformation.IsOSPlatform( OSPlatform.Windows )
				? ExternalMemoryHandleTypeFlags.OpaqueWin32Bit
				: ExternalMemoryHandleTypeFlags.OpaqueFDBit
		};
	}

	public Result<IntPtr> Export( DeviceMemory memoryBlock )
		=> Environment.OSVersion.Platform switch
		{
			PlatformID.Unix => ExportLinuxFdHandle( memoryBlock ),
			PlatformID.Win32NT => ExportWin32Handle( memoryBlock ),
			PlatformID.MacOSX => ExportMacHandle(),
			_ => new Error( "Platform not supported" )
		};

	public Result<IntPtr> ExportWin32Handle( DeviceMemory memoryBlock )
	{
		MemoryGetWin32HandleInfoKHR info = new()
		{
			SType = StructureType.MemoryGetWin32HandleInfoKhr,
			HandleType = ExternalMemoryHandleTypeFlags.OpaqueWin32Bit,
			Memory = memoryBlock
		};

		VkResult errorCode = Vulkan.ExternalMemoryWin32.GetMemoryWin32Handle( Device.VkDevice, ref info, out IntPtr handle );
		if ( errorCode is not VkResult.Success )
		{
			return new Error( errorCode.ToString() );
		}

		return handle;
	}

	public Result<IntPtr> ExportLinuxFdHandle( DeviceMemory memoryBlock )
	{
		MemoryGetFdInfoKHR info = new()
		{
			SType = StructureType.MemoryGetFDInfoKhr,
			HandleType = ExternalMemoryHandleTypeFlags.OpaqueFDBit,
			Memory = memoryBlock
		};

		VkResult errorCode = Vulkan.ExternalMemoryFd.GetMemoryF( Device.VkDevice, ref info, out int handle );
		if ( errorCode is not VkResult.Success )
		{
			return new Error( errorCode.ToString() );
		}

		return handle;
	}

	public Result<IntPtr> ExportMacHandle()
	{
		ExportMetalIOSurfaceInfoEXT surfaceExport = new()
		{
			SType = StructureType.ExportMetalIOSurfaceInfoExt,
			Image = VkImage
		};

		ExportMetalObjectsInfoEXT export = new()
		{
			SType = StructureType.ExportMetalObjectsInfoExt,
			PNext = &surfaceExport
		};

		Vulkan.MetalObjects.ExportMetalObjects( Device.VkDevice, ref export );

		if ( surfaceExport.IoSurface == IntPtr.Zero )
		{
			return new Error( "Couldn't export IOSurfaceRef" );
		}

		return surfaceExport.IoSurface;
	}

	public void Dispose()
		=> Vulkan.Vk.DestroyImage( Device.VkDevice, VkImage, null );
}
