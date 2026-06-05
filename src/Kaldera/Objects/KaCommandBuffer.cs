// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Interfaces;

namespace Kaldera.Objects;

public unsafe class KaCommandBuffer : IDisposable
{
	public required KaQueue Queue { get; init; }
	public required VkCommandPool VkCmdPool;
	public required VkCommandBuffer VkCmdBuf;
	public required bool IsSecondary;

	public IPipeline? CurrentPipeline { get; private set; }

	private static Result<KaCommandBuffer> CreateInternal( KaQueue queue, bool longLived, bool secondary )
	{
		CommandPoolCreateInfo poolInfo = new()
		{
			SType = StructureType.CommandPoolCreateInfo,
			QueueFamilyIndex = queue.Family,
			Flags = longLived
				? CommandPoolCreateFlags.ResetCommandBufferBit
				: CommandPoolCreateFlags.ResetCommandBufferBit | CommandPoolCreateFlags.TransientBit
		};

		VkResult result = Vulkan.Vk.CreateCommandPool( queue.Device.VkDevice, &poolInfo, null, out var pool );
		if ( result is not VkResult.Success )
		{
			return new Error( $"KaCommandBuffer.Create: Couldn't create command pool - {result}" );
		}

		CommandBufferAllocateInfo cmdBufInfo = new()
		{
			SType = StructureType.CommandBufferAllocateInfo,
			CommandBufferCount = 1,
			CommandPool = pool,
			Level = secondary ? CommandBufferLevel.Secondary : CommandBufferLevel.Primary
		};

		result = Vulkan.Vk.AllocateCommandBuffers( queue.Device.VkDevice, &cmdBufInfo, out var buffer );
		if ( result is not VkResult.Success )
		{
			return new Error( $"KaCommandBuffer.Create: Couldn't allocate command buffer - {result}" );
		}

		return new KaCommandBuffer
		{
			Queue = queue,
			VkCmdPool = pool,
			VkCmdBuf = buffer,
			IsSecondary = secondary
		};
	}

	public static Result<KaCommandBuffer> CreatePrimary( KaQueue queue, bool longLived = false )
		=> CreateInternal( queue, longLived, false );

	public static Result<KaCommandBuffer> CreateSecondary( KaQueue queue, bool longLived = false )
		=> CreateInternal( queue, longLived, true );

	public void Begin( bool dontReset = false )
	{
		CommandBufferUsageFlags usageFlags = dontReset
			? CommandBufferUsageFlags.None
			: CommandBufferUsageFlags.OneTimeSubmitBit;

		CommandBufferBeginInfo beginInfo = new()
		{
			SType = StructureType.CommandBufferBeginInfo,
			Flags = usageFlags,
			PInheritanceInfo = null
		};

		Vulkan.Vk.BeginCommandBuffer( VkCmdBuf, ref beginInfo );
	}

	public void End()
		=> Vulkan.Vk.EndCommandBuffer( VkCmdBuf );

	public void Reset( bool dontReleaseResources = false )
		=> Vulkan.Vk.ResetCommandBuffer( VkCmdBuf, dontReleaseResources ? CommandBufferResetFlags.None : CommandBufferResetFlags.ReleaseResourcesBit );

	public void Execute( KaCommandBuffer otherCommands )
		=> Vulkan.Vk.CmdExecuteCommands( VkCmdBuf, new ReadOnlySpan<VkCommandBuffer>( ref otherCommands.VkCmdBuf ) );

	public void BeginRendering( ref RenderingInfo info )
		=> Vulkan.Vk.CmdBeginRendering( VkCmdBuf, ref info );

	public void EndRendering()
		=> Vulkan.Vk.CmdEndRendering( VkCmdBuf );

	public void TransitionImageLayout(
		VkImage image,
		ImageLayout oldLayout,
		ImageLayout newLayout,
		AccessFlags2 srcAccessMask,
		AccessFlags2 dstAccessMask,
		PipelineStageFlags2 srcStageMask,
		PipelineStageFlags2 dstStageMask,
		ImageAspectFlags imageAspectFlags = ImageAspectFlags.ColorBit,
		int levelCount = 1,
		int layerCount = 1,
		int baseMipLevel = 0,
		int baseArrayLayer = 0 )
	{
		ImageMemoryBarrier2 barrier = new()
		{
			SType = StructureType.ImageMemoryBarrier2,
			SrcStageMask = srcStageMask,
			SrcAccessMask = srcAccessMask,
			DstStageMask = dstStageMask,
			DstAccessMask = dstAccessMask,
			OldLayout = oldLayout,
			NewLayout = newLayout,
			SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
			DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
			Image = image,
			SubresourceRange =
			{
				AspectMask = imageAspectFlags,
				BaseMipLevel = (uint)baseMipLevel,
				LevelCount = (uint)levelCount,
				BaseArrayLayer = (uint)baseArrayLayer,
				LayerCount = (uint)layerCount
			}
		};

		DependencyInfo dependencyInfo = new()
		{
			SType = StructureType.DependencyInfo,
			DependencyFlags = DependencyFlags.None,
			ImageMemoryBarrierCount = 1,
			PImageMemoryBarriers = &barrier
		};

		Vulkan.Vk.CmdPipelineBarrier2( VkCmdBuf, ref dependencyInfo );
	}

	public void Barrier( AccessFlags2 accessBefore, PipelineStageFlags2 stageBefore, AccessFlags2 accessAfter, PipelineStageFlags2 stageAfter )
	{
		MemoryBarrier2 barrier = new()
		{
			SType = StructureType.MemoryBarrier2,
			SrcAccessMask = accessBefore,
			SrcStageMask = stageBefore,
			DstAccessMask = accessAfter,
			DstStageMask = stageAfter
		};

		DependencyInfo dependencyInfo = new()
		{
			SType = StructureType.DependencyInfo,
			MemoryBarrierCount = 1,
			PMemoryBarriers = &barrier
		};

		Vulkan.Vk.CmdPipelineBarrier2( VkCmdBuf, ref dependencyInfo );
	}

	public void ClearImageColour(
		VkImage image,
		Vector4 clearColour,
		int levelCount = 1,
		int layerCount = 1,
		int baseMipLevel = 0,
		int baseArrayLayer = 0 )
	{
		ClearColorValue clearValue = new( clearColour.X, clearColour.Y, clearColour.Z, clearColour.W );

		ImageSubresourceRange range = new()
		{
			AspectMask = ImageAspectFlags.ColorBit,
			BaseMipLevel = (uint)baseMipLevel,
			LevelCount = (uint)levelCount,
			BaseArrayLayer = (uint)baseArrayLayer,
			LayerCount = (uint)layerCount
		};

		Vulkan.Vk.CmdClearColorImage( VkCmdBuf, image, ImageLayout.General, ref clearValue, 1, ref range );
	}

	public void CopyBuffer( KaBuffer source, KaBuffer destination, ulong size, ulong sourceOffset = 0, ulong destinationOffset = 0 )
	{
		BufferCopy bufferCopy = new()
		{
			SrcOffset = sourceOffset,
			DstOffset = destinationOffset,
			Size = size
		};

		Vulkan.Vk.CmdCopyBuffer( VkCmdBuf, source.VkBuffer, destination.VkBuffer, 1, ref bufferCopy );
	}

	public void CopyBufferToImage( KaBuffer source, KaImage destination, ImageLayout layout, Span<BufferImageCopy> regions )
	{
		Vulkan.Vk.CmdCopyBufferToImage(
			commandBuffer: VkCmdBuf,
			srcBuffer: source.VkBuffer,
			dstImage: destination.VkImage,
			dstImageLayout: layout,
			pRegions: regions
		);
	}

	public void BindPipeline<T>( T pipeline )
		where T : IPipeline
	{
		CurrentPipeline = pipeline;
		Vulkan.Vk.CmdBindPipeline( VkCmdBuf, pipeline.BindPoint, pipeline.VkPipeline );
	}

	private bool CheckIfPipelineBound( string method, string errorMessage )
	{
		if ( CurrentPipeline is null )
		{
			KaInstance.DebugLog(
				DebugReportFlagsEXT.ErrorBitExt,
				DebugReportObjectTypeEXT.CommandBufferExt,
				message: $"KaCommandBuffer.{method}: {errorMessage}"
			);
			return false;
		}

		return true;
	}

	public void PushSampler( KaSampler sampler, int set, int binding = 0 )
	{
		if ( !CheckIfPipelineBound( "PushSampler", "Tried pushing a sampler without a bound pipeline" ) )
		{
			return;
		}

		DescriptorImageInfo imageInfo = new()
		{
			Sampler = sampler.VkSampler
		};

		WriteDescriptorSet write = new()
		{
			SType = StructureType.WriteDescriptorSet,
			DstBinding = (uint)binding,
			DescriptorCount = 1,
			DescriptorType = DescriptorType.Sampler,
			PImageInfo = (DescriptorImageInfo*)Unsafe.AsPointer( ref imageInfo )
		};

		Vulkan.Vk.CmdPushDescriptorSet( VkCmdBuf, CurrentPipeline!.BindPoint, CurrentPipeline.Layout.VkLayout, (uint)set, 1, &write );
	}

	public void PushImage( in VkImageView imageView, ImageLayout layout, DescriptorType type, int set, int binding )
	{
		DescriptorImageInfo imageInfo = new()
		{
			ImageLayout = layout,
			ImageView = imageView
		};

		WriteDescriptorSet write = new()
		{
			SType = StructureType.WriteDescriptorSet,
			DstBinding = (uint)binding,
			DescriptorCount = 1,
			DescriptorType = type,
			PImageInfo = (DescriptorImageInfo*)Unsafe.AsPointer( ref imageInfo )
		};

		Vulkan.Vk.CmdPushDescriptorSet( VkCmdBuf, CurrentPipeline!.BindPoint, CurrentPipeline.Layout.VkLayout, (uint)set, 1, &write );
	}

	public void PushConstant<T>( T data, ShaderStageFlags stageFlags )
		where T : unmanaged
		=> PushConstant( data, 0, stageFlags );

	public void PushConstant<T>( T data, int offset, ShaderStageFlags stageFlags )
		where T : unmanaged
	{
		if ( !CheckIfPipelineBound( "PushConstant", "Tried pushing a constant without a bound pipeline" ) )
		{
			return;
		}

		Vulkan.Vk.CmdPushConstants( VkCmdBuf, CurrentPipeline!.Layout.VkLayout, stageFlags, (uint)offset, (uint)Unsafe.SizeOf<T>(), ref data );
	}

	public void PushBuffer( in KaBufferRange range, int set, int binding, DescriptorType type )
	{
		DescriptorBufferInfo bufferInfo = new()
		{
			Buffer = range.Buffer.VkBuffer,
			Offset = range.Start,
			Range = range.Length
		};

		WriteDescriptorSet write = new()
		{
			SType = StructureType.WriteDescriptorSet,
			DstBinding = (uint)binding,
			DescriptorCount = 1,
			DescriptorType = type,
			PBufferInfo = &bufferInfo
		};

		Vulkan.Vk.CmdPushDescriptorSet( VkCmdBuf, CurrentPipeline!.BindPoint, CurrentPipeline.Layout.VkLayout, (uint)set, 1, &write );
	}

	public void Draw( int vertexCount, int instanceCount )
		=> Vulkan.Vk.CmdDraw( VkCmdBuf, (uint)vertexCount, (uint)instanceCount, 0, 0 );

	public void DrawIndexed( int indexCount, int instanceCount )
		=> Vulkan.Vk.CmdDrawIndexed( VkCmdBuf, (uint)indexCount, (uint)instanceCount, 0, 0, 0 );

	public void Dispatch( int workGroupsX = 1, int workGroupsY = 1, int workGroupsZ = 1 )
		=> Vulkan.Vk.CmdDispatch( VkCmdBuf, (uint)workGroupsX, (uint)workGroupsY, (uint)workGroupsZ );

	public void Dispose()
	{
		Vulkan.Vk.FreeCommandBuffers( Queue.Device.VkDevice, VkCmdPool, 1, ref VkCmdBuf );
		Vulkan.Vk.DestroyCommandPool( Queue.Device.VkDevice, VkCmdPool, null );
	}
}
