// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Extensions;

namespace Kaldera.Objects;

public readonly unsafe struct KaSwapchain : IDisposable
{
	public required KaDevice Device { get; init; }
	public required SwapchainKHR VkSwapchain { get; init; }

	public static Result<KaSwapchain> Create( KaDevice device, in SwapchainCreateInfoKHR options )
	{
		SwapchainCreateInfoKHR createInfo = options;
		VkResult result = Vulkan.Swapchain.CreateSwapchain( device.VkDevice, ref createInfo, null, out SwapchainKHR swapchain );
		if ( result is not VkResult.Success )
		{
			return new Error( $"SwapChain.Create: Got Vulkan error '{result}'" );
		}

		return new KaSwapchain
		{
			Device = device,
			VkSwapchain = swapchain
		};
	}

	public VkImage[] GetImages()
	{
		uint count = 0;
		Vulkan.Swapchain.GetSwapchainImages( Device.VkDevice, VkSwapchain, &count, null );
		VkImage[] items = new VkImage[count];
		Vulkan.Swapchain.GetSwapchainImages( Device.VkDevice, VkSwapchain, &count, items.AsPointer() );
		return items;
	}

	public VkResult AcquireNextImage( ulong timeout, VkSemaphore semaphore, out int imageIndex )
	{
		uint index = 0;
		VkResult result = Vulkan.Swapchain.AcquireNextImage( Device.VkDevice, VkSwapchain, timeout, semaphore, new Fence(), &index );
		imageIndex = (int)index;
		return result;
	}

	public VkResult AcquireNextImage( ulong timeout, VkSemaphore semaphore, VkFence fence, out int imageIndex )
	{
		uint index = 0;
		VkResult result = Vulkan.Swapchain.AcquireNextImage( Device.VkDevice, VkSwapchain, timeout, semaphore, fence, &index );
		imageIndex = (int)index;
		return result;
	}

	public void Dispose()
	{
		Vulkan.Swapchain.DestroySwapchain( Device.VkDevice, VkSwapchain, null );
	}
}
