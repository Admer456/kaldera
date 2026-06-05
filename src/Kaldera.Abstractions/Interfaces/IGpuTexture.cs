// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Abstractions.Textures;
using Kaldera.Interfaces;

namespace Kaldera.Abstractions.Interfaces;

public interface IGpuTexture
{
	RawTextureState State { get; }
	IResourceAllocator Allocator { get; }
	ImageAspectFlags AspectFlags { get; }
	ImageLayout CurrentLayout { get; set; }
}

public static class GpuTextureExtensions
{
	public static uint MipDimension( this IGpuTexture self, int x, int mip )
		=> (uint)Math.Max( 1, x / (1 << mip) );

	public static uint CalculateSize( this IGpuTexture self, int mipLevel )
	{
		int formatSize = self.SizeOfFormat( self.State.Format );
		if ( formatSize < 0 )
		{
			return self.NumPixelsForMip( mipLevel ) / (uint)Math.Abs( formatSize );
		}

		return self.NumPixelsForMip( mipLevel ) * (uint)formatSize;
	}

	public static uint NumPixelsForMip( this IGpuTexture self, int mipLevel )
	{
		uint width = self.MipDimension( self.State.Width, mipLevel );
		uint height = self.MipDimension( self.State.Height, mipLevel );
		uint depth = self.MipDimension( self.State.Depth, mipLevel );

		return width * height * depth;
	}

	public static int SizeOfFormat( this IGpuTexture self, Format format )
		=> format switch
		{
			// Yes, I did this all manually.
			// No, it only took 6 minutes.
			Format.R4G4UnormPack8 => 1,
			Format.R4G4B4A4UnormPack16 => 2,
			Format.B4G4R4A4UnormPack16 => 2,
			Format.R5G6B5UnormPack16 => 2,
			Format.B5G6R5UnormPack16 => 2,
			Format.R5G5B5A1UnormPack16 => 2,
			Format.B5G5R5A1UnormPack16 => 2,
			Format.A1R5G5B5UnormPack16 => 2,
			Format.R8Unorm => 1,
			Format.R8SNorm => 1,
			Format.R8Uscaled => 1,
			Format.R8Sscaled => 1,
			Format.R8Uint => 1,
			Format.R8Sint => 1,
			Format.R8Srgb => 1,
			Format.R8G8Unorm => 2,
			Format.R8G8SNorm => 2,
			Format.R8G8Uscaled => 2,
			Format.R8G8Sscaled => 2,
			Format.R8G8Uint => 2,
			Format.R8G8Sint => 2,
			Format.R8G8Srgb => 2,
			Format.R8G8B8Unorm => 3,
			Format.R8G8B8SNorm => 3,
			Format.R8G8B8Uscaled => 3,
			Format.R8G8B8Sscaled => 3,
			Format.R8G8B8Uint => 3,
			Format.R8G8B8Sint => 3,
			Format.R8G8B8Srgb => 3,
			Format.B8G8R8Unorm => 3,
			Format.B8G8R8SNorm => 3,
			Format.B8G8R8Uscaled => 3,
			Format.B8G8R8Sscaled => 3,
			Format.B8G8R8Uint => 3,
			Format.B8G8R8Sint => 3,
			Format.B8G8R8Srgb => 3,
			Format.R8G8B8A8Unorm => 4,
			Format.R8G8B8A8SNorm => 4,
			Format.R8G8B8A8Uscaled => 4,
			Format.R8G8B8A8Sscaled => 4,
			Format.R8G8B8A8Uint => 4,
			Format.R8G8B8A8Sint => 4,
			Format.R8G8B8A8Srgb => 4,
			Format.B8G8R8A8Unorm => 4,
			Format.B8G8R8A8SNorm => 4,
			Format.B8G8R8A8Uscaled => 4,
			Format.B8G8R8A8Sscaled => 4,
			Format.B8G8R8A8Uint => 4,
			Format.B8G8R8A8Sint => 4,
			Format.B8G8R8A8Srgb => 4,
			Format.A8B8G8R8UnormPack32 => 4,
			Format.A8B8G8R8SNormPack32 => 4,
			Format.A8B8G8R8UscaledPack32 => 4,
			Format.A8B8G8R8SscaledPack32 => 4,
			Format.A8B8G8R8UintPack32 => 4,
			Format.A8B8G8R8SintPack32 => 4,
			Format.A8B8G8R8SrgbPack32 => 4,
			Format.A2R10G10B10UnormPack32 => 4,
			Format.A2R10G10B10SNormPack32 => 4,
			Format.A2R10G10B10UscaledPack32 => 4,
			Format.A2R10G10B10SscaledPack32 => 4,
			Format.A2R10G10B10UintPack32 => 4,
			Format.A2R10G10B10SintPack32 => 4,
			Format.A2B10G10R10UnormPack32 => 4,
			Format.A2B10G10R10SNormPack32 => 4,
			Format.A2B10G10R10UscaledPack32 => 4,
			Format.A2B10G10R10SscaledPack32 => 4,
			Format.A2B10G10R10UintPack32 => 4,
			Format.A2B10G10R10SintPack32 => 4,
			Format.R16Unorm => 2,
			Format.R16SNorm => 2,
			Format.R16Uscaled => 2,
			Format.R16Sscaled => 2,
			Format.R16Uint => 2,
			Format.R16Sint => 2,
			Format.R16Sfloat => 2,
			Format.R16G16Unorm => 4,
			Format.R16G16SNorm => 4,
			Format.R16G16Uscaled => 4,
			Format.R16G16Sscaled => 4,
			Format.R16G16Uint => 4,
			Format.R16G16Sint => 4,
			Format.R16G16Sfloat => 4,
			Format.R16G16B16Unorm => 6,
			Format.R16G16B16SNorm => 6,
			Format.R16G16B16Uscaled => 6,
			Format.R16G16B16Sscaled => 6,
			Format.R16G16B16Uint => 6,
			Format.R16G16B16Sint => 6,
			Format.R16G16B16Sfloat => 6,
			Format.R16G16B16A16Unorm => 8,
			Format.R16G16B16A16SNorm => 8,
			Format.R16G16B16A16Uscaled => 8,
			Format.R16G16B16A16Sscaled => 8,
			Format.R16G16B16A16Uint => 8,
			Format.R16G16B16A16Sint => 8,
			Format.R16G16B16A16Sfloat => 8,
			Format.R32Uint => 4,
			Format.R32Sint => 4,
			Format.R32Sfloat => 4,
			Format.R32G32Uint => 8,
			Format.R32G32Sint => 8,
			Format.R32G32Sfloat => 8,
			Format.R32G32B32Uint => 12,
			Format.R32G32B32Sint => 12,
			Format.R32G32B32Sfloat => 12,
			Format.R32G32B32A32Uint => 16,
			Format.R32G32B32A32Sint => 16,
			Format.R32G32B32A32Sfloat => 16,
			Format.R64Uint => 8,
			Format.R64Sint => 8,
			Format.R64Sfloat => 8,
			Format.R64G64Uint => 16,
			Format.R64G64Sint => 16,
			Format.R64G64Sfloat => 16,
			Format.R64G64B64Uint => 24,
			Format.R64G64B64Sint => 24,
			Format.R64G64B64Sfloat => 24,
			Format.R64G64B64A64Uint => 32,
			Format.R64G64B64A64Sint => 32,
			Format.R64G64B64A64Sfloat => 32,
			Format.B10G11R11UfloatPack32 => 4,
			Format.E5B9G9R9UfloatPack32 => 4,
			Format.D16Unorm => 2,
			Format.X8D24UnormPack32 => 4,
			Format.D32Sfloat => 4,
			Format.D24UnormS8Uint => 4,
			Format.S8Uint => 1,
			Format.A8Unorm => 1,

			// Now these ones are a bit special.
			// BC1 and BC4 encode 16 pixels to 8 bytes (2px per byte)
			Format.BC1RgbaUnormBlock => -2,
			Format.BC1RgbaSrgbBlock => -2,
			Format.BC1RgbUnormBlock => -2,
			Format.BC1RgbSrgbBlock => -2,
			Format.BC4UnormBlock => -2,
			Format.BC4SNormBlock => -2,
			// BC2, BC3, BC5, BC6H BC7 encode 16 pixels to 16 bytes (1px per byte)
			Format.BC2UnormBlock => 1,
			Format.BC2SrgbBlock => 1,
			Format.BC3UnormBlock => 1,
			Format.BC3SrgbBlock => 1,
			Format.BC5UnormBlock => 1,
			Format.BC5SNormBlock => 1,
			Format.BC6HUfloatBlock => 1,
			Format.BC6HSfloatBlock => 1,
			Format.BC7UnormBlock => 1,
			Format.BC7SrgbBlock => 1,

			_ => throw new NotSupportedException()
		};
}
