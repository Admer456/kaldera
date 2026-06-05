// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Abstractions.Interfaces;
using Kaldera.Interfaces;
using Kaldera.Objects;

namespace Kaldera.Abstractions.Buffers;

public readonly struct BufferSlice<T> : IGpuBuffer
	where T : IGpuBuffer
{
	public required T Buffer { get; init; }
	public required ulong Start { get; init; }
	public required ulong Length { get; init; }

	public static implicit operator BufferSlice<T>( T buffer ) => new()
	{
		Buffer = buffer,
		Start = 0,
		Length = buffer.BufferRange.Length
	};

	public static implicit operator VkBuffer( BufferSlice<T> self ) => self.Buffer.BufferRange.Buffer;

	public void Dispose()
	{
		Buffer.Dispose();
	}

	public KaBufferRange BufferRange => new()
	{
		Buffer = Buffer.BufferRange.Buffer,
		Start = Start,
		Length = Length
	};

	public IResourceAllocator Allocator => Buffer.Allocator;
}

public static class BufferSliceExtensions
{
	public static BufferSlice<T> Slice<T>( this T buffer, int start, int length )
		where T : IGpuBuffer
		=> new()
		{
			Buffer = buffer,
			Start = (ulong)start,
			Length = (ulong)length
		};
}
