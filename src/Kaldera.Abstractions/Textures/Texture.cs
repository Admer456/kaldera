// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Abstractions.Interfaces;
using Kaldera.Interfaces;
using Kaldera.Objects;

namespace Kaldera.Abstractions.Textures;

/// <summary>
/// Uncompressed 2D (and 1D) texture.
/// </summary>
public struct Texture : IGpuTexture, ITextureArrayCapable, ITextureComputable, ITextureBlitDst, IDisposable
{
	public Texture()
	{
	}

	public required RawTextureState State { get; init; }
	public required IResourceAllocator Allocator { get; init; }
	public ImageLayout CurrentLayout { get; set; } = ImageLayout.Undefined;
	public ImageAspectFlags AspectFlags => ImageAspectFlags.ColorBit;

	internal static Result<RawTextureState> Create<TAllocator>( TAllocator allocator, ImageOptions imageOptions,
		ImageViewOptions viewOptions )
		where TAllocator : IResourceAllocator
	{
		Result<KaImage> result = allocator.CreateImage( imageOptions );
		if ( !result.Get( out var error, out var image ) )
		{
			return error.Prepend( "Texture.Create: Failed to create image" );
		}

		VkImageView imageView = image.CreateView( viewOptions );
		return new RawTextureState( imageOptions, image, imageView );
	}

	internal static unsafe void Destroy<T>( T allocator, in RawTextureState state )
		where T : IResourceAllocator
	{
		// TODO: Wrap image view destruction
		Vulkan.Vk.DestroyImageView( allocator.Device.VkDevice, state.VkImageView, null );
		allocator.DestroyImage( state.Image );
	}

	internal static ImageUsageFlags UsageFlags
		=> ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit | ImageUsageFlags.StorageBit;

	public static Result<Texture> Create<TAllocator>( TAllocator allocator, Format format, int width, int height, int mips )
		where TAllocator : IResourceAllocator
	{
		ImageOptions options = ImageOptions.Common( format, width, height, 1, mips, 1, UsageFlags );
		ImageViewOptions viewOptions = ImageViewOptions.Common( options );

		var result = Create( allocator, options, viewOptions );
		if ( !result.Get( out var error, out var textureState ) )
		{
			return error;
		}

		return new Texture { Allocator = allocator, State = textureState };
	}

	public static Result<TextureArray<Texture>> CreateArray<TAllocator>( TAllocator allocator, Format format, int width, int height, int mips, int layers )
		where TAllocator : IResourceAllocator
	{
		ImageOptions options = ImageOptions.Common( format, width, height, 1, mips, layers, UsageFlags );
		ImageViewOptions viewOptions = ImageViewOptions.Common( options );

		var result = Create( allocator, options, viewOptions );
		if ( !result.Get( out var error, out var textureState ) )
		{
			return error;
		}

		var arrayViews = TextureArray<Texture>.GetArrayViews( textureState.Image, viewOptions );
		return new TextureArray<Texture> { Allocator = allocator, State = textureState, AspectFlags = ImageAspectFlags.ColorBit, ArrayViews = arrayViews };
	}

	public static Result<TextureArray<Texture>> CreateCube<TAllocator>( TAllocator allocator, Format format, int width, int height, int mips )
		where TAllocator : IResourceAllocator
	{
		ImageOptions options = ImageOptions.Common( format, width, height, 1, mips, 6, UsageFlags ) with
		{
			Flags = ImageCreateFlags.CreateCubeCompatibleBit
		};
		ImageViewOptions viewOptions = ImageViewOptions.Cube( options );

		var result = Create( allocator, options, viewOptions );
		if ( !result.Get( out var error, out var textureState ) )
		{
			return error;
		}

		var arrayViews = TextureArray<Texture>.GetArrayViews( textureState.Image, viewOptions );
		return new TextureArray<Texture> { Allocator = allocator, State = result, AspectFlags = ImageAspectFlags.ColorBit, ArrayViews = arrayViews };
	}

	public void Dispose()
		=> Texture.Destroy( Allocator, State );
}
