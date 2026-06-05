// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Objects;

namespace Kaldera.Interfaces;

/// <summary>
/// Simplifies buffer and image allocation.
/// </summary>
public interface IResourceAllocator
{
	KaDevice Device { get; }
	KaQueue Queue { get; }

	/// <summary>
	/// Creates a buffer, bound to device memory.
	/// </summary>
	Result<KaBuffer> CreateBuffer( ulong size, BufferUsageFlags usageFlags, MemoryPropertyFlags memoryFlags );
	Result<KaImage> CreateImage( ImageOptions options );

	Result<DeviceMemory> GetBufferMemory( KaBuffer buffer );
	Result<DeviceMemory> GetImageMemory( KaImage image );

	ulong GetBufferMemoryOffset( KaBuffer buffer );
	ulong GetImageMemoryOffset( KaImage image );

	/// <summary>
	/// Destroys a buffer, clearing up allocated resources as necessary.
	/// </summary>
	bool DestroyBuffer( KaBuffer buffer );
	bool DestroyImage( KaImage image );
}
