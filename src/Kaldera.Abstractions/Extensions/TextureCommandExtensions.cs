// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Abstractions.Buffers;
using Kaldera.Abstractions.Interfaces;
using Kaldera.Objects;

namespace Kaldera.Abstractions.Extensions;

public static class TextureCommandExtensions
{
	public static Result<nint> Export<T>( this T self )
		where T : IGpuTexture
	{
		Result<DeviceMemory> result = self.Allocator.GetImageMemory( self.State.Image );
		if ( !result.Get( out Error? error, out DeviceMemory memoryBlock ) )
		{
			return error.Prepend( "Failed to Export() texture" );
		}

		return self.State.Image.Export( memoryBlock );
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
