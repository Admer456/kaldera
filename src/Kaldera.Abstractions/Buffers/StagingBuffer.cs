// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Abstractions.Interfaces;
using Kaldera.Interfaces;
using Kaldera.Objects;

namespace Kaldera.Abstractions.Buffers;

public readonly unsafe struct BufferMemoryMapping
{
	public required void* Data { get; init; }
	public required int Length { get; init; }

	public Span<T> AsSpan<T>( int offset = 0 ) where T : unmanaged
		=> new( (void*)((IntPtr)Data + offset), Length - offset );

	// TODO: Span<T> that supports more than 2 GB. Someone might try to map a 4 or 8 GB buffer and will be disappointed
	//  to find out this can only go up to 2 GB. As-is, this here should support a 2GB chunk out of a larger buffer, but w/e
	public Span<T> AsSpan<T>( ulong offset ) where T : unmanaged
		=> new( (void*)((IntPtr)Data + (IntPtr)offset), (int)((ulong)Length - offset) );

	public Span<T> AsSpan<T>( int offset, int length ) where T : unmanaged
	{
		if ( length <= 0 || length >= Length )
		{
			throw new IndexOutOfRangeException();
		}

		return new( (void*)((IntPtr)Data + offset), length );
	}
}

public unsafe class StagingBuffer : IGpuBuffer
{
	public required BufferMemoryMapping Mapping { get; init; }
	public required KaBufferRange BufferRange { get; init; }
	public required IResourceAllocator Allocator { get; init; }

	public static Result<StagingBuffer> Create<TAllocator>( TAllocator resourceAllocator, int sizeInBytes )
		where TAllocator : IResourceAllocator
		=> Create( resourceAllocator, (ulong)sizeInBytes );

	public static Result<StagingBuffer> Create<TAllocator>( TAllocator resourceAllocator, ulong sizeInBytes )
		where TAllocator : IResourceAllocator
	{
		Result<KaBuffer> result = resourceAllocator.CreateBuffer(
			sizeInBytes,
			BufferUsageFlags.TransferSrcBit,
			MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit );

		if ( !result.Get( out var error, out var buffer ) )
		{
			return error.Prepend( "StagingBuffer.Create: Failed to create buffer" );
		}

		// TODO: Move memory mapping to KaDevice or whatever
		void* data = null;
		VkResult vkRes = Vulkan.Vk.MapMemory(
			resourceAllocator.Device.VkDevice,
			memory: resourceAllocator.GetBufferMemory( buffer ),
			offset: resourceAllocator.GetBufferMemoryOffset( buffer ),
			size: sizeInBytes,
			flags: MemoryMapFlags.None,
			ppData: ref data
		);

		if ( vkRes is not VkResult.Success )
		{
			return new Error( $"StagingBuffer.Create: Failed to map buffer - {vkRes}" );
		}

		return new StagingBuffer
		{
			Allocator = resourceAllocator,
			Mapping = new()
			{
				Data = data,
				Length = (int)sizeInBytes
			},
			BufferRange = new()
			{
				Buffer = buffer,
				Length = buffer.Size,
				Start = 0
			}
		};
	}

	public void Dispose()
	{
		// TODO: Wrap memory unmapping
		Vulkan.Vk.UnmapMemory(
			Allocator.Device.VkDevice,
			Allocator.GetBufferMemory( BufferRange.Buffer )
		);

		Allocator.DestroyBuffer( BufferRange.Buffer );
	}
}
