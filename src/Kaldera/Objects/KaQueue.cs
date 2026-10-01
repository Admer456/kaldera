// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

namespace Kaldera.Objects;

public sealed unsafe class KaQueue
{
	public required KaDevice Device;
	public required uint Family;
	public required VkQueue VkQueue;

	internal static Result<KaQueue> Create( KaDevice device, uint queueFamily )
	{
		Vulkan.Vk.GetDeviceQueue( device.VkDevice, queueFamily, 0, out VkQueue queue );

		if ( queue.Handle is 0 )
		{
			return new Error( "KaQueue.Create: queue family index is invalid" );
		}

		return new KaQueue
		{
			Family = queueFamily,
			Device = device,
			VkQueue = queue
		};
	}

	public void Submit( KaCommandBuffer commandBuffer, VkFence? fence = null, PipelineStageFlags? stageFlags = PipelineStageFlags.ColorAttachmentOutputBit )
		// TODO: Check if this [commandBuffer] has any influence on performance
		=> Submit( [commandBuffer], null, null, fence, stageFlags );

	public void Submit( Span<KaCommandBuffer> commands, VkFence? fence = null, PipelineStageFlags? stageFlags = PipelineStageFlags.ColorAttachmentOutputBit )
		=> Submit( commands, null, null, fence, stageFlags );

	public void Submit( KaCommandBuffer commandBuffer, VkSemaphore? waitSemaphore, VkSemaphore? signalSemaphore, VkFence? fence,
		PipelineStageFlags? waitStage )
		=> Submit( [commandBuffer], waitSemaphore, signalSemaphore, fence, waitStage );

	public void Submit( ReadOnlySpan<KaCommandBuffer> commands, VkSemaphore? waitSemaphore, VkSemaphore? signalSemaphore, VkFence? fence,
		PipelineStageFlags? waitStage )
	{
		Span<VkCommandBuffer> commandBuffers = stackalloc VkCommandBuffer[commands.Length];
		for ( int i = 0; i < commandBuffers.Length; i++ )
		{
			commandBuffers[i] = commands[i].VkCmdBuf;
		}

		PipelineStageFlags stageFlags = waitStage ?? PipelineStageFlags.None;
		VkSemaphore waitSemaphoreValue = new( 0 );
		VkSemaphore signalSemaphoreValue = new( 0 );
		SubmitInfo submitInfo = new()
		{
			SType = StructureType.SubmitInfo,
			PWaitDstStageMask = waitStage is not null ? &stageFlags : null,
			WaitSemaphoreCount = waitSemaphore is not null ? 1U : 0U,
			SignalSemaphoreCount = signalSemaphore is not null ? 1U : 0U,
			CommandBufferCount = (uint)commandBuffers.Length,
			PCommandBuffers = (VkCommandBuffer*)Unsafe.AsPointer( ref commandBuffers[0] )
		};

		if ( waitSemaphore is not null )
		{
			waitSemaphoreValue = waitSemaphore.Value;
			submitInfo.PWaitSemaphores = &waitSemaphoreValue;
		}

		if ( signalSemaphore is not null )
		{
			signalSemaphoreValue = signalSemaphore.Value;
			submitInfo.PSignalSemaphores = &signalSemaphoreValue;
		}

		Vulkan.Vk.QueueSubmit( VkQueue, 1U, &submitInfo, fence ?? new( 0 ) );
	}

	public VkResult Present( VkSemaphore waitSemaphore, KaSwapchain swapchain, int imageIndex )
	{
		SwapchainKHR vulkanSwapchain = swapchain.VkSwapchain;
		uint presentImageIndex = (uint)imageIndex;
		PresentInfoKHR presentInfo = new()
		{
			SType = StructureType.PresentInfoKhr,
			WaitSemaphoreCount = 1,
			PWaitSemaphores = &waitSemaphore,
			SwapchainCount = 1,
			PSwapchains = &vulkanSwapchain,
			PImageIndices = &presentImageIndex
		};
		return Vulkan.Swapchain.QueuePresent( VkQueue, &presentInfo );
	}
}
