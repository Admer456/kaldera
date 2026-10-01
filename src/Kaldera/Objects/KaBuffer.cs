// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using System.Runtime.InteropServices;
using Kaldera.Interfaces;

namespace Kaldera.Objects;

public struct KaBufferRange
{
	public required KaBuffer Buffer { get; init; }
	public required ulong Start { get; init; }
	public required ulong Length { get; init; }

	public static implicit operator KaBuffer( KaBufferRange range ) => range.Buffer;
}

public readonly unsafe struct KaBuffer : IMemoryBindable, IDisposable
{
	public required KaDevice Device { get; init; }
	public required VkBuffer VkBuffer { get; init; }
	public required ulong Size { get; init; }
	public required BufferUsageFlags Usage { get; init; }

	public static Result<KaBuffer> Create( KaDevice device, ulong size, BufferUsageFlags usageFlags )
	{
		BufferCreateInfo createInfo = new()
		{
			SType = StructureType.BufferCreateInfo,
			Size = size,
			Usage = usageFlags,
			SharingMode = SharingMode.Exclusive
		};

		VkResult result = Vulkan.Vk.CreateBuffer( device.VkDevice, ref createInfo, null, out VkBuffer buffer );
		if ( result is not VkResult.Success )
		{
			return new Error( $"KaBuffer.Create: {result}" );
		}

		return new KaBuffer
		{
			Device = device,
			VkBuffer = buffer,
			Size = size,
			Usage = usageFlags
		};
	}

	public MemoryRequirements GetMemoryRequirements()
	{
		Vulkan.Vk.GetBufferMemoryRequirements( Device.VkDevice, this, out MemoryRequirements result );
		return result;
	}

	public VkResult Bind( DeviceMemory memory, ulong offset )
		=> Vulkan.Vk.BindBufferMemory( Device.VkDevice, this, memory, offset );

	public KaBufferRange Slice( int start, int length = -1 )
	{
		ulong longLength = length >= 0 ? (ulong)length : (ulong)start - Size;
		return Slice( (ulong)start, longLength );
	}

	public KaBufferRange Slice( ulong start, ulong length )
		=> new()
		{
			Buffer = this,
			Start = start,
			Length = length
		};

	public void GetExportableInfo(
		out MemoryDedicatedAllocateInfo dedicatedAllocation,
		out ExportMemoryAllocateInfo exportAllocateInfo )
	{
		dedicatedAllocation = new()
		{
			SType = StructureType.MemoryDedicatedAllocateInfo,
			Buffer = VkBuffer
		};

		exportAllocateInfo = new()
		{
			SType = StructureType.ExportMemoryAllocateInfo,
			HandleTypes = RuntimeInformation.IsOSPlatform( OSPlatform.Windows )
				? ExternalMemoryHandleTypeFlags.OpaqueWin32Bit
				: ExternalMemoryHandleTypeFlags.OpaqueFDBit
		};
	}

	// TODO: Export someday

	public void Dispose()
		=> Vulkan.Vk.DestroyBuffer( Device.VkDevice, VkBuffer, null );

	public static implicit operator KaBufferRange( KaBuffer self ) => new()
	{
		Buffer = self,
		Start = 0UL,
		Length = self.Size
	};

	public static implicit operator VkBuffer( KaBuffer self ) => self.VkBuffer;
}
