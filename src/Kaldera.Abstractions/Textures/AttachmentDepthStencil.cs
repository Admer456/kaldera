// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Abstractions.Interfaces;
using Kaldera.Abstractions.Memory;
using Kaldera.Interfaces;
using Kaldera.Objects;

namespace Kaldera.Abstractions.Textures;

public class AttachmentDepthStencil : IGpuTexture, ITextureArrayCapable, ITextureBlitDst, IDisposable
{
	private static (Format Format, ImageAspectFlags AspectFlags) GetFormatAndAspect( bool withStencil )
		=> withStencil
			? (Format.D32SfloatS8Uint, ImageAspectFlags.DepthBit | ImageAspectFlags.StencilBit)
			: (Format.D32Sfloat, ImageAspectFlags.DepthBit);

	private static ImageUsageFlags UsageFlags => ImageUsageFlags.TransferSrcBit
	                                             | ImageUsageFlags.TransferDstBit
	                                             | ImageUsageFlags.SampledBit
	                                             | ImageUsageFlags.DepthStencilAttachmentBit;

	public required RawTextureState State { get; init; }
	public required IResourceAllocator Allocator { get; init; }
	public required ImageAspectFlags AspectFlags { get; init; }
	public ImageLayout CurrentLayout { get; set; } = ImageLayout.Undefined;

	public static Result<AttachmentDepthStencil> Create<T>( T allocator, int width, int height, bool withStencil, SampleCountFlags samples )
		where T : IResourceAllocator
	{
		(Format format, ImageAspectFlags aspectFlags) = GetFormatAndAspect( withStencil );
		ImageOptions options = ImageOptions.Common( format, width, height, 1, 1, 1, UsageFlags ) with { Samples = samples };
		ImageViewOptions viewOptions = ImageViewOptions.Common( options, aspectFlags );

		var result = Texture.Create( allocator, options, viewOptions );
		if ( !result.Get( out var error, out var textureState ) )
		{
			return error;
		}

		return new AttachmentDepthStencil { Allocator = allocator, State = textureState, AspectFlags = aspectFlags };
	}

	public static Result<TextureArray<AttachmentDepthStencil>> CreateArray<T>( T allocator, int width, int height, bool withStencil, SampleCountFlags samples,
		int layers )
		where T : IResourceAllocator
	{
		(Format format, ImageAspectFlags aspectFlags) = GetFormatAndAspect( withStencil );
		ImageOptions options = ImageOptions.Common( format, width, height, 1, 1, layers, UsageFlags ) with { Samples = samples };
		ImageViewOptions viewOptions = ImageViewOptions.Common( options, aspectFlags );

		var result = Texture.Create( allocator, options, viewOptions );
		if ( !result.Get( out var error, out var textureState ) )
		{
			return error;
		}

		var arrayViews = TextureArray<Texture>.GetArrayViews( textureState.Image, viewOptions );
		return new TextureArray<AttachmentDepthStencil> { Allocator = allocator, State = textureState, AspectFlags = aspectFlags, ArrayViews = arrayViews };
	}

	public void Dispose()
		=> Texture.Destroy( Allocator, State );
}
