// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Abstractions.Interfaces;
using Kaldera.Interfaces;
using Kaldera.Objects;

namespace Kaldera.Abstractions.Buffers;

public interface IVertexBuffer : IGpuBuffer;

public interface IVertexData
{
	static abstract int SizeInBytes { get; }
	static abstract VertexAttribute[] VertexAttributes { get; }
}

public static class VertexDataExtensions
{
	public static VertexInputLayout GetInputLayout<T>( this T self )
		where T : IVertexData
		=> new()
		{
			Stride = T.SizeInBytes,
			Elements = T.VertexAttributes
		};

	public static unsafe Span<byte> AsByteSpan<T>( this Span<T> self )
		where T : IVertexData
		=> new( Unsafe.AsPointer( ref self[0] ), T.SizeInBytes * self.Length );

	public static unsafe Span<byte> AsByteSpan( this Span<uint> self )
		=> new( Unsafe.AsPointer( ref self[0] ), 4 * self.Length );

	public static unsafe Span<byte> AsByteSpan( this Span<int> self )
		=> new( Unsafe.AsPointer( ref self[0] ), 4 * self.Length );

	public static unsafe Span<byte> AsByteSpan( this Span<Vector2> self )
		=> new( Unsafe.AsPointer( ref self[0] ), Unsafe.SizeOf<Vector2>() * self.Length );

	public static unsafe Span<byte> AsByteSpan( this Span<Vector3> self )
		=> new( Unsafe.AsPointer( ref self[0] ), Unsafe.SizeOf<Vector3>() * self.Length );

	public static unsafe Span<byte> AsByteSpan( this Span<Vector4> self )
		=> new( Unsafe.AsPointer( ref self[0] ), Unsafe.SizeOf<Vector4>() * self.Length );

	public static unsafe Span<byte> AsByteSpan( this Span<Matrix4x4> self )
		=> new( Unsafe.AsPointer( ref self[0] ), Unsafe.SizeOf<Matrix4x4>() * self.Length );
}

public class VertexBuffer<TVertex> : IVertexBuffer
	where TVertex : unmanaged, IVertexData
{
	public required KaBufferRange BufferRange { get; init; }
	public required IResourceAllocator Allocator { get; init; }

	public static Result<VertexBuffer<TVertex>> Create<TAllocator>( TAllocator resourceAllocator, int numElements )
		where TAllocator : IResourceAllocator
	{
		Result<KaBuffer> result = resourceAllocator.CreateBuffer(
			(ulong)TVertex.SizeInBytes * (ulong)numElements,
			// Transfer + vertex + storage in case this is consumed by compute shaders
			BufferUsageFlags.TransferSrcBit | BufferUsageFlags.TransferDstBit | BufferUsageFlags.VertexBufferBit | BufferUsageFlags.StorageBufferBit,
			MemoryPropertyFlags.DeviceLocalBit );

		if ( !result.Get( out var error, out var buffer ) )
		{
			return error.Prepend( "VertexBuffer.Create: Failed to create buffer" );
		}

		return new VertexBuffer<TVertex>
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
