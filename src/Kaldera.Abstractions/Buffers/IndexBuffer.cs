// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Abstractions.Interfaces;
using Kaldera.Abstractions.Memory;
using Kaldera.Interfaces;
using Kaldera.Objects;

namespace Kaldera.Abstractions.Buffers;

public class IndexBuffer : IGpuBuffer
{
	public required KaBufferRange BufferRange { get; init; }
	public required IResourceAllocator Allocator { get; init; }
	public int Count => (int)(BufferRange.Length / 4);

	public static Result<IndexBuffer> Create<TAllocator>( TAllocator resourceAllocator, int numElements )
		where TAllocator : IResourceAllocator
	{
		Result<KaBuffer> result = resourceAllocator.CreateBuffer(
			4UL * (ulong)numElements,
			// Transfer + index + storage in case this is consumed by a compute shader
			BufferUsageFlags.TransferSrcBit | BufferUsageFlags.TransferDstBit | BufferUsageFlags.IndexBufferBit | BufferUsageFlags.StorageBufferBit,
			MemoryPropertyFlags.DeviceLocalBit );

		if ( !result.Get( out var error, out var buffer ) )
		{
			return error.Prepend( "IndexBuffer.Create: Failed to create buffer" );
		}

		return new IndexBuffer
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
