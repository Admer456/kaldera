// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Abstractions.Interfaces;
using Kaldera.Abstractions.Memory;
using Kaldera.Interfaces;
using Kaldera.Objects;

namespace Kaldera.Abstractions.Textures;

public class AttachmentColour : IGpuTexture, ITextureArrayCapable, ITextureBlitDst, IDisposable
{
	private static ImageUsageFlags UsageFlags => ImageUsageFlags.TransferSrcBit
	                                             | ImageUsageFlags.TransferDstBit
	                                             | ImageUsageFlags.SampledBit
	                                             | ImageUsageFlags.ColorAttachmentBit;

	public required RawTextureState State { get; init; }
	public required IResourceAllocator Allocator { get; init; }
	public ImageLayout CurrentLayout { get; set; } = ImageLayout.Undefined;
	public ImageAspectFlags AspectFlags => ImageAspectFlags.ColorBit;

	public static Result<AttachmentColour> Create<T>( T allocator, Format format, int width, int height, SampleCountFlags samples )
		where T : IResourceAllocator
	{
		ImageOptions options = ImageOptions.Common( format, width, height, 1, 1, 1, UsageFlags ) with { Samples = samples };
		ImageViewOptions viewOptions = ImageViewOptions.Common( options );

		var result = Texture.Create( allocator, options, viewOptions );
		if ( !result.Get( out var error, out var textureState ) )
		{
			return error;
		}

		return new AttachmentColour { Allocator = allocator, State = textureState };
	}

	public static Result<TextureArray<AttachmentColour>> CreateArray<T>( T allocator, Format format, int width, int height, SampleCountFlags samples,
		int layers )
		where T : IResourceAllocator
	{
		ImageOptions options = ImageOptions.Common( format, width, height, 1, 1, layers, UsageFlags ) with { Samples = samples };
		ImageViewOptions viewOptions = ImageViewOptions.Common( options );

		var result = Texture.Create( allocator, options, viewOptions );
		if ( !result.Get( out var error, out var textureState ) )
		{
			return error;
		}

		var arrayViews = TextureArray<Texture>.GetArrayViews( textureState.Image, viewOptions );
		return new TextureArray<AttachmentColour> { Allocator = allocator, State = textureState, AspectFlags = ImageAspectFlags.ColorBit, ArrayViews = arrayViews };
	}

	public void Dispose()
		=> Texture.Destroy( Allocator, State );
}
