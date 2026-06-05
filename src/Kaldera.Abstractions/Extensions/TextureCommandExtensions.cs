// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Abstractions.Buffers;
using Kaldera.Abstractions.Interfaces;
using Kaldera.Objects;

namespace Kaldera.Abstractions.Extensions;

public static class TextureCommandExtensions
{
	public static void TransitionTextureLayout<T>( this KaCommandBuffer self, ref T texture, ImageLayout newLayout )
		where T : IGpuTexture
	{
		if ( texture.CurrentLayout == newLayout )
		{
			return;
		}

		self.TransitionImageLayout(
			texture.State.Image.VkImage,
			oldLayout: texture.CurrentLayout,
			newLayout,
			srcAccessMask: AccessFlags2.None,
			dstAccessMask: AccessFlags2.None,
			srcStageMask: PipelineStageFlags2.None,
			dstStageMask: PipelineStageFlags2.None,
			texture.AspectFlags,
			texture.State.MipLevels,
			texture.State.ArrayLayers
		);

		texture.CurrentLayout = newLayout;
	}

	public static void CopyBufferToTexture<T>( this KaCommandBuffer self, StagingBuffer source, in T destination, Span<BufferImageCopy> regions )
		where T : IGpuTexture
		=> self.CopyBufferToImage( source.BufferRange, destination.State.Image, destination.CurrentLayout, regions );

	public static void PushStorageTexture<T>( this KaCommandBuffer self, in T texture, int set, int binding = 0 )
		where T : IGpuTexture, ITextureComputable
	{
		if ( !self.CheckPipelineBound( "PushStorageTexture", "Tried pushing a storage texture without a bound pipeline" ) )
		{
			return;
		}

		self.PushImage( texture.State.VkImageView, texture.CurrentLayout, DescriptorType.StorageImage, set, binding );
	}

	public static void PushSampledTexture<T>( this KaCommandBuffer self, in T texture, int set, int binding = 0 )
		where T : IGpuTexture
	{
		if ( !self.CheckPipelineBound( "PushSampledTexture", "Tried pushing a sampled texture without a bound pipeline" ) )
		{
			return;
		}

		self.PushImage( texture.State.VkImageView, texture.CurrentLayout, DescriptorType.SampledImage, set, binding );
	}
}
