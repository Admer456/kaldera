// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Abstractions.Buffers;
using Kaldera.Abstractions.Extensions;
using Kaldera.Abstractions.Interfaces;
using Kaldera.Abstractions.Textures;
using Kaldera.Interfaces;
using Kaldera.Objects;

namespace Kaldera.Abstractions.Utilities;

public class UploadHelper : IDisposable
{
	private const int MaxMips = 16; // Should be enough for textures up to 65536x65536
	private ulong mCurrentOffset;
	private bool mRecording = true;
	private readonly BufferImageCopy[] mImageCopyCache = new BufferImageCopy[MaxMips];

	public required IResourceAllocator Allocator { get; init; }
	public required KaCommandBuffer CommandBuffer { get; init; }
	public required StagingBuffer StagingBuffer { get; init; }
	public ulong CurrentOffset => mCurrentOffset;

	public static Result<UploadHelper> Create( IResourceAllocator allocator, int capacityInBytes )
	{
		var commandBufferResult = KaCommandBuffer.CreatePrimary( allocator.Queue );
		if ( !commandBufferResult.Get( out var error, out var commandBuffer ) )
		{
			return error.Prepend( "UploadHelper.Create: Failed to create internal command buffer" );
		}

		var stagingBufferResult = StagingBuffer.Create( allocator, capacityInBytes );
		if ( !stagingBufferResult.Get( out error, out var stagingBuffer ) )
		{
			commandBuffer.Dispose();
			return error.Prepend( "UploadHelper.Create: Failed to create internal staging buffer" );
		}

		// Let it immediately be ready to commit uploads & updates after creation
		commandBuffer.Begin();

		return new UploadHelper
		{
			Allocator = allocator,
			CommandBuffer = commandBuffer,
			StagingBuffer = stagingBuffer
		};
	}

	#region Buffers

	private void WriteToStaging( Span<byte> bytes, ref ulong offset )
	{
		Span<byte> staged = StagingBuffer.Mapping.AsSpan<byte>( offset );
		bytes.CopyTo( staged );
		offset += (ulong)bytes.Length;
	}

	public void UpdateBuffer<T>( T buffer, Span<byte> data )
		where T : IGpuBuffer
	{
		// Note: CopyBuffer is not immediately executed, so it's fine to do it *before* writing to the staging buffer
		CommandBuffer.CopyBuffer( StagingBuffer, buffer, buffer.BufferRange.Length, CurrentOffset );
		WriteToStaging( data, ref mCurrentOffset );
	}

	public Result<VertexBuffer<T>> CommitVertexBuffer<T>( Span<T> data )
		where T : unmanaged, IVertexData
	{
		Result<VertexBuffer<T>> result = VertexBuffer<T>.Create( Allocator, data.Length );
		if ( !result.Get( out var error, out var vertexBuffer ) )
		{
			return error;
		}

		UpdateBuffer( vertexBuffer, data.AsByteSpan() );
		return vertexBuffer;
	}

	public Result<IndexBuffer> CommitIndexBuffer( Span<uint> data )
	{
		Result<IndexBuffer> result = IndexBuffer.Create( Allocator, data.Length );
		if ( !result.Get( out var error, out var indexBuffer ) )
		{
			return error;
		}

		UpdateBuffer( indexBuffer, data.AsByteSpan() );
		return indexBuffer;
	}

	public unsafe Result<StorageBuffer> CommitUniformBuffer<T>( T data )
		where T : unmanaged
	{
		int size = Unsafe.SizeOf<T>();
		Span<byte> span = new( Unsafe.AsPointer( ref data ), size );

		Result<StorageBuffer> result = StorageBuffer.Create( Allocator, size );
		if ( !result.Get( out var error, out var storageBuffer ) )
		{
			return error;
		}

		UpdateBuffer( storageBuffer, span );
		return storageBuffer;
	}

	public unsafe Result<StorageBuffer> CommitStorageBuffer<T>( Span<T> data )
		where T : unmanaged
	{
		int size = Unsafe.SizeOf<T>();
		Span<byte> dataBytes = new( Unsafe.AsPointer( ref data[0] ), data.Length * size );

		Result<StorageBuffer> result = StorageBuffer.Create( Allocator, dataBytes.Length );
		if ( !result.Get( out var error, out var storageBuffer ) )
		{
			return error;
		}

		UpdateBuffer( storageBuffer, dataBytes );
		return storageBuffer;
	}

	#endregion

	#region Textures

	private void WriteTexture<T>( T texture, int layer, Span<byte> data )
		where T : IGpuTexture
	{
		uint textureOffset = 0u;
		var state = texture.State;

		for ( int i = 0; i < state.MipLevels; i++ )
		{
			// TODO:
			// Look into compressed textures and how to load mipmaps from those
			// https://docs.vulkan.org/samples/latest/samples/performance/texture_compression_basisu/README.html
			mImageCopyCache[i] = new()
			{
				BufferOffset = mCurrentOffset,
				BufferImageHeight = 0,
				BufferRowLength = 0,
				ImageExtent = new()
				{
					Width = texture.MipDimension( state.Width, i ),
					Height = texture.MipDimension( state.Height, i ),
					Depth = texture.MipDimension( state.Depth, i )
				},
				ImageOffset = new( 0, 0, 0 ),
				ImageSubresource = new()
				{
					AspectMask = texture.AspectFlags,
					BaseArrayLayer = (uint)layer,
					LayerCount = 1,
					MipLevel = (uint)i
				}
			};

			// Example values for a 1024x1024 RGBA8 texture:
			// Mip 0 | 1024x1024 | textureOffset 0       | size 4194304
			// Mip 1 |   512x512 | textureOffset 4194304 | size 1048576
			// Mip 2 |   256x256 | textureOffset 5242880 | size 262144
			// Mip 3 |   128x128 | textureOffset 5505024 | size 65536
			// Mip 4 |     64x64 | textureOffset 5570560 | size 16384 ...
			uint size = texture.CalculateSize( i );
			WriteToStaging( data.Slice( (int)textureOffset, (int)size ), ref mCurrentOffset );
			textureOffset += size;
		}

		CommandBuffer.CopyBufferToTexture(
			source: StagingBuffer,
			destination: texture,
			regions: mImageCopyCache.AsSpan( 0, state.MipLevels )
		);
	}

	public void UpdateTexture<T>( ref T texture, Span<byte> data, ImageLayout? layoutAfterUpdate = null )
		where T : IGpuTexture
	{
		layoutAfterUpdate ??= texture.CurrentLayout switch
		{
			ImageLayout.Undefined => ImageLayout.ReadOnlyOptimal,
			_ => texture.CurrentLayout
		};

		CommandBuffer.TransitionTextureLayout( ref texture, ImageLayout.TransferDstOptimal );
		WriteTexture( texture, 0, data );
		CommandBuffer.TransitionTextureLayout( ref texture, layoutAfterUpdate.Value );
	}

	public void UpdateTextureArray<T>( ref TextureArray<T> textureArray, Span<byte> data, int layer, ImageLayout? layoutAfterUpdate = null )
		where T : IGpuTexture, ITextureArrayCapable
		=> UpdateTextureArray( ref textureArray, data, layer, 1, layoutAfterUpdate );

	public void UpdateTextureArray<T>( ref TextureArray<T> textureArray, Span<byte> data, int baseLayer, int layers, ImageLayout? layoutAfterUpdate = null )
		where T : IGpuTexture, ITextureArrayCapable
	{
		layoutAfterUpdate ??= textureArray.CurrentLayout switch
		{
			ImageLayout.Undefined => ImageLayout.ReadOnlyOptimal,
			_ => textureArray.CurrentLayout
		};

		CommandBuffer.TransitionTextureLayout( ref textureArray, ImageLayout.TransferDstOptimal );
		for ( int i = baseLayer; i < baseLayer + layers; i++ )
		{
			WriteTexture( textureArray, i, data );
		}
		CommandBuffer.TransitionTextureLayout( ref textureArray, layoutAfterUpdate.Value );
	}

	public Result<Texture> CommitTexture( Span<byte> pixelData, Format textureFormat, int width, int height, int mips )
	{
		Result<Texture> result = Texture.Create( Allocator, textureFormat, width, height, mips );
		if ( !result.Get( out var error, out var texture ) )
		{
			return error;
		}

		UpdateTexture( ref texture, pixelData );
		return texture;
	}

	//public Result<TextureCompressed> CommitTextureCompressed( Memory<byte> pixelData, Format textureFormat, int width, int height, int mips )
	//{
	//	throw new NotImplementedException();
	//}

	//public Result<Texture3D> CommitTexture3D( Memory<byte> pixelData, Format textureFormat )
	//{
	//	throw new NotImplementedException();
	//}

	#endregion

	public void Upload()
	{
		// We've just done a ton of memory writes, so prevent this from being read til it's done
		CommandBuffer.Barrier( BarrierStages.Transfer, BarrierStages.Graphics | BarrierStages.ComputeShader );
		CommandBuffer.End();

		Allocator.Queue.Submit( CommandBuffer );
		mCurrentOffset = 0;
		mRecording = false;
	}

	private void ResetCommandBuffer()
	{
		if ( mRecording )
		{
			CommandBuffer.End();
			if ( mCurrentOffset is not 0 )
			{
				CommandBuffer.Reset();
			}
		}
	}

	public void Reset()
	{
		ResetCommandBuffer();

		mCurrentOffset = 0;
		mRecording = true;

		CommandBuffer.Begin();
	}

	public void Dispose()
	{
		ResetCommandBuffer();

		StagingBuffer.Dispose();
		CommandBuffer.Dispose();
	}
}
