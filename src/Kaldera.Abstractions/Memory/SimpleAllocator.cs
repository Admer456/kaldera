// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Interfaces;
using Kaldera.Objects;

namespace Kaldera.Abstractions.Memory;

/// <summary>
/// Allocates naively. For each new buffer/image, a new block of device memory is allocated.
/// Not recommended for production. Consider VMA instead.
/// If you rely on this, you will likely run out of allocations.
/// </summary>
public unsafe class SimpleAllocator : IResourceAllocator
{
	private readonly Dictionary<KaBuffer, DeviceMemory> mBufferMemoryMap = [];
	private readonly Dictionary<KaImage, DeviceMemory> mImageMemoryMap = [];

	public required KaDevice Device { get; init; }
	public required KaQueue Queue { get; init; }
	public required uint MaxAllocations { get; init; }

	public uint NumAllocations { get; private set; }

	public static SimpleAllocator Create( KaDevice device, KaQueue queue )
		=> new()
		{
			Queue = queue,
			Device = device,
			MaxAllocations = device.Physical.GetProperties().Limits.MaxMemoryAllocationCount
		};

	private Result<T> AllocateAndBind<T>( string method, Dictionary<T, DeviceMemory> map, T resource, MemoryPropertyFlags memoryFlags )
		where T : IMemoryBindable, IDisposable
	{
		MemoryAllocateInfo allocateInfo = Device.GetAllocationInfo( resource.GetMemoryRequirements(), memoryFlags );
		if ( allocateInfo.MemoryTypeIndex is uint.MaxValue )
		{
			resource.Dispose();
			return new Error( $"SimpleAllocator.{method}: Couldn't find a good memory type" );
		}

		VkResult error = Vulkan.Vk.AllocateMemory( Device.VkDevice, ref allocateInfo, null, out DeviceMemory memory );
		if ( error is not VkResult.Success )
		{
			resource.Dispose();
			return new Error( $"SimpleAllocator.{method}: Couldn't allocate memory: {error}" );
		}

		error = resource.Bind( memory, offset: 0U );
		if ( error is not VkResult.Success )
		{
			resource.Dispose();
			Vulkan.Vk.FreeMemory( Device.VkDevice, memory, null );
			return new Error( $"SimpleAllocator.{method}: Couldn't bind buffer memory: {error}" );
		}

		NumAllocations++;
		map[resource] = memory;
		return resource;
	}

	public Result<KaBuffer> CreateBuffer( ulong size, BufferUsageFlags usageFlags, MemoryPropertyFlags memoryFlags )
	{
		if ( NumAllocations >= MaxAllocations )
		{
			return new Error( $"SimpleAllocator.CreateBuffer: Reached too many allocations: {MaxAllocations}" );
		}

		Result<KaBuffer> result = KaBuffer.Create( Device, size, usageFlags );
		if ( !result.Get( out var error, out var buffer ) )
		{
			return error;
		}

		return AllocateAndBind( "CreateBuffer", mBufferMemoryMap, buffer, memoryFlags );
	}

	public Result<KaImage> CreateImage( ImageOptions options )
	{
		if ( NumAllocations >= MaxAllocations )
		{
			return new Error( $"SimpleAllocator.CreateImage: Reached too many allocations: {MaxAllocations}" );
		}

		Result<KaImage> result = KaImage.Create( Device, options );
		if ( !result.Get( out var error, out var image ) )
		{
			return error;
		}

		return AllocateAndBind( "CreateImage", mImageMemoryMap, image, MemoryPropertyFlags.DeviceLocalBit );
	}

	public Result<DeviceMemory> GetBufferMemory( KaBuffer buffer )
	{
		if ( mBufferMemoryMap.TryGetValue( buffer, out DeviceMemory memory ) )
		{
			return memory;
		}

		return new Error( "SimpleAllocator.GetBufferMemory: Not found" );
	}

	public Result<DeviceMemory> GetImageMemory( KaImage image )
	{
		if ( mImageMemoryMap.TryGetValue( image, out DeviceMemory memory ) )
		{
			return memory;
		}

		return new Error( "SimpleAllocator.GetImageMemory: Not found" );
	}

	public ulong GetBufferMemoryOffset( KaBuffer buffer )
		=> 0UL;

	public ulong GetImageMemoryOffset( KaImage image )
		=> 0UL;

	public bool DestroyBuffer( KaBuffer buffer )
	{
		if ( !mBufferMemoryMap.TryGetValue( buffer, out DeviceMemory value ) )
		{
			return false;
		}

		buffer.Dispose();
		Vulkan.Vk.FreeMemory( Device.VkDevice, value, null );
		return true;
	}

	public bool DestroyImage( KaImage image )
	{
		if ( !mImageMemoryMap.TryGetValue( image, out DeviceMemory value ) )
		{
			return false;
		}

		image.Dispose();
		Vulkan.Vk.FreeMemory( Device.VkDevice, value, null );
		return true;
	}
}
