// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Extensions;
using Kaldera.Names;
using Kaldera.Utilities;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Kaldera.Objects;

public sealed record QueueOptions( uint FamilyIndex, float Priority = 1.0f );

public sealed class DeviceOptions
{
	public required QueueOptions[] QueueFamilies { get; set; }
	public required StructureChain EnabledFeatures { get; set; }
	public string[] EnabledDeviceExtensions { get; set; } = [];
}

public sealed unsafe class KaDevice : IDisposable
{
	public required KaInstance Instance { get; init; }
	public required KaPhysicalDevice Physical { get; init; }
	public required VkDevice VkDevice { get; init; }

	public static Result<KaDevice> Create( KaInstance instance, KaPhysicalDevice physicalDevice, DeviceOptions options )
	{
		float[] queuePriorities = options.QueueFamilies.Select( qf => qf.Priority ).ToArray();
		DeviceQueueCreateInfo[] queueInfos = new DeviceQueueCreateInfo[options.QueueFamilies.Length];
		for ( int i = 0; i < options.QueueFamilies.Length; i++ )
		{
			queueInfos[i].SType = StructureType.DeviceQueueCreateInfo;
			queueInfos[i].Flags = DeviceQueueCreateFlags.None;
			queueInfos[i].QueueCount = 1;
			queueInfos[i].QueueFamilyIndex = options.QueueFamilies[i].FamilyIndex;
			queueInfos[i].PQueuePriorities = (float*)Unsafe.AsPointer( ref queuePriorities[i] );
		}

		CStringArray extensionNames = options.EnabledDeviceExtensions;

		DeviceCreateInfo deviceInfo = new()
		{
			SType = StructureType.DeviceCreateInfo,
			Flags = 0,
			QueueCreateInfoCount = (uint)queueInfos.Length,
			PQueueCreateInfos = queueInfos.AsPointer(),
			PNext = options.EnabledFeatures.Head
		};
		extensionNames.Decompose( out deviceInfo.PpEnabledExtensionNames, out deviceInfo.EnabledExtensionCount );

		VkResult code = Vulkan.Vk.CreateDevice( physicalDevice.VkPhysicalDevice, &deviceInfo, null, out VkDevice device );
		if ( code is not VkResult.Success )
		{
			return new Error( $"Cannot create device: {code}" );
		}

		// Automatically load device extensions as applicable
		foreach ( var extension in options.EnabledDeviceExtensions )
		{
			switch ( extension )
			{
				case DeviceExtensionNames.KhrExternalMemoryWin32:
					Vulkan.Vk.TryGetDeviceExtension( instance.VkInstance, device, out KhrExternalMemoryWin32 extMemWin32 );
					Vulkan.ExternalMemoryWin32 = extMemWin32;
					break;

				case DeviceExtensionNames.KhrExternalSemaphoreWin32:
					Vulkan.Vk.TryGetDeviceExtension( instance.VkInstance, device, out KhrExternalSemaphoreWin32 extSemWin32 );
					Vulkan.ExternalSemaphoreWin32 = extSemWin32;
					break;

				case DeviceExtensionNames.KhrExternalMemoryFd:
					Vulkan.Vk.TryGetDeviceExtension( instance.VkInstance, device, out KhrExternalMemoryFd extMemFd );
					Vulkan.ExternalMemoryFd = extMemFd;
					break;

				case DeviceExtensionNames.KhrExternalSemaphoreFd:
					Vulkan.Vk.TryGetDeviceExtension( instance.VkInstance, device, out KhrExternalSemaphoreFd extSemFd );
					Vulkan.ExternalSemaphoreFd = extSemFd;
					break;

				case DeviceExtensionNames.ExtMetalObjects:
					Vulkan.Vk.TryGetDeviceExtension( instance.VkInstance, device, out ExtMetalObjects metalObjects );
					Vulkan.MetalObjects = metalObjects;
					break;

				case DeviceExtensionNames.KhrSwapchain:
					Vulkan.Vk.TryGetDeviceExtension( instance.VkInstance, device, out KhrSwapchain swampchain );
					Vulkan.Swapchain = swampchain;
					break;

				// TODO: EDS3 calls a.t.m. are extension methods, and they could be isolated to their own "addon" module. Let's wait
				//  and see how many extensions we'll rack up (e.g. descriptor heaps), and then figure out what they have in common
				case DeviceExtensionNames.ExtExtendedDynamicState3:
					Vulkan.Vk.TryGetDeviceExtension( instance.VkInstance, device, out ExtExtendedDynamicState3 eds3 );
					Vulkan.DynamicState3 = eds3;
					break;
			}
		}

		options.EnabledFeatures.Dispose();

		return new KaDevice
		{
			VkDevice = device,
			Instance = instance,
			Physical = physicalDevice
		};
	}

	public Result<KaQueue> GetQueue( uint familyIndex )
		=> KaQueue.Create( this, familyIndex );

	public Result WaitIdle()
	{
		VkResult result = Vulkan.Vk.DeviceWaitIdle( VkDevice );
		if ( result is not VkResult.Success )
		{
			return new Error( result.ToString() );
		}

		return Result.Success();
	}

	public VkSemaphore CreateSemaphore()
	{
		SemaphoreCreateInfo semaphoreInfo = new()
		{
			SType = StructureType.SemaphoreCreateInfo,
			Flags = SemaphoreCreateFlags.None
		};

		Vulkan.Vk.CreateSemaphore( VkDevice, &semaphoreInfo, null, out var semaphore );
		return semaphore;
	}

	public VkFence CreateFence( in FenceCreateFlags flags )
	{
		FenceCreateInfo fenceInfo = new()
		{
			SType = StructureType.FenceCreateInfo,
			Flags = flags
		};

		Vulkan.Vk.CreateFence( VkDevice, &fenceInfo, null, out var fence );
		return fence;
	}

	public VkResult WaitForFences( VkFence fence, bool waitAll, ulong timeoutMs )
		=> WaitForFences( [ fence ], waitAll, timeoutMs );

	public VkResult WaitForFences( Span<VkFence> fences, bool waitAll, ulong timeoutMs )
		=> Vulkan.Vk.WaitForFences( VkDevice, fences, waitAll, timeoutMs );

	public VkResult ResetFences( VkFence fence )
		=> ResetFences( [ fence ] );

	public VkResult ResetFences( Span<VkFence> fences )
		=> Vulkan.Vk.ResetFences( VkDevice, fences );

	public int FindMemoryType( uint typeFilter, MemoryPropertyFlags flags )
	{
		PhysicalDeviceMemoryProperties memoryProperties = Physical.GetMemoryProperties();

		for ( int i = 0; i < memoryProperties.MemoryTypeCount; i++ )
		{
			bool filterTest = (typeFilter & (1 << i)) != 0;
			bool propertyTest = memoryProperties.MemoryTypes[i].PropertyFlags.HasFlag( flags );

			if ( filterTest && propertyTest )
			{
				return i;
			}
		}

		return -1;
	}

	public MemoryAllocateInfo GetAllocationInfo( MemoryRequirements requirements, MemoryPropertyFlags flags )
	{
		int memoryTypeIndex = FindMemoryType( requirements.MemoryTypeBits, flags );

		return new()
		{
			SType = StructureType.MemoryAllocateInfo,
			AllocationSize = requirements.Size,
			MemoryTypeIndex = memoryTypeIndex < 0 ? uint.MaxValue : (uint)memoryTypeIndex
		};
	}

	public void Dispose()
	{
		Vulkan.Vk.DestroyDevice( VkDevice, null );
	}
}
