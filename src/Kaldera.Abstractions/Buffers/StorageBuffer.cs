// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Abstractions.Interfaces;
using Kaldera.Abstractions.Memory;
using Kaldera.Interfaces;
using Kaldera.Objects;

namespace Kaldera.Abstractions.Buffers;

public class StorageBuffer : IGpuBuffer
{
	public required KaBufferRange BufferRange { get; init; }
	public required IResourceAllocator Allocator { get; init; }

	public static Result<StorageBuffer> Create<TAllocator>( TAllocator resourceAllocator, int sizeInBytes )
		where TAllocator : IResourceAllocator
	{
		Result<KaBuffer> result = resourceAllocator.CreateBuffer(
			(ulong)sizeInBytes,
			BufferUsageFlags.TransferSrcBit | BufferUsageFlags.TransferDstBit | BufferUsageFlags.StorageBufferBit | BufferUsageFlags.UniformBufferBit,
			MemoryPropertyFlags.DeviceLocalBit );

		if ( !result.Get( out var error, out var buffer ) )
		{
			return error.Prepend( "StorageBuffer.Create: Failed to create buffer" );
		}

		return new StorageBuffer
		{
			Allocator = resourceAllocator,
			BufferRange = new()
			{
				Buffer = buffer,
				Length = buffer.Size,
				Start = 0
			}
		};
	}

	public void Dispose()
		=> Allocator.DestroyBuffer( BufferRange.Buffer );
}
