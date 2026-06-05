// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Abstractions.Interfaces;
using Kaldera.Objects;

namespace Kaldera.Abstractions.Extensions;

public static unsafe class RenderTargetCommandExtensions
{
	public static void ClearColour<TRenderTarget>( this KaCommandBuffer self, TRenderTarget renderTarget, int attachment, Vector4 clearColour )
		where TRenderTarget : IRenderTarget
	{
		ClearColorValue clearColorValue = renderTarget.GetColourImageFormat() switch
		{
			// Cauchemar :(
			Format.B8G8R8A8Sint or Format.B8G8R8Sint or
				Format.R8G8B8A8Sint or Format.R8G8B8Sint or Format.R8G8Sint or Format.R8Sint or
				Format.R16G16B16A16Sint or Format.R16G16B16Sint or Format.R16G16Sint or Format.R16Sint or
				Format.R32G32B32A32Sint or Format.R32G32B32Sint or Format.R32G32Sint or Format.R32Sint or
				Format.R64G64B64A64Sint or Format.R64G64B64Sint or Format.R64G64Sint or Format.R64Sint or
				Format.A2B10G10R10SintPack32 or Format.A8B8G8R8SintPack32 => new()
				{
					Int32_0 = (int)(clearColour.X * 255.0f),
					Int32_1 = (int)(clearColour.Y * 255.0f),
					Int32_2 = (int)(clearColour.Z * 255.0f),
					Int32_3 = (int)(clearColour.W * 255.0f)
				},
			Format.B8G8R8A8Uint or Format.B8G8R8Uint or
				Format.R8G8B8A8Uint or Format.R8G8B8Uint or Format.R8G8Uint or Format.R8Uint or
				Format.R16G16B16A16Uint or Format.R16G16B16Uint or Format.R16G16Uint or Format.R16Uint or
				Format.R32G32B32A32Uint or Format.R32G32B32Uint or Format.R32G32Uint or Format.R32Uint or
				Format.R64G64B64A64Uint or Format.R64G64B64Uint or Format.R64G64Uint or Format.R64Uint or
				Format.A2B10G10R10UintPack32 or Format.A8B8G8R8UintPack32 => new()
				{
					Uint32_0 = (uint)(clearColour.X * 255.0f),
					Uint32_1 = (uint)(clearColour.Y * 255.0f),
					Uint32_2 = (uint)(clearColour.Z * 255.0f),
					Uint32_3 = (uint)(clearColour.W * 255.0f)
				},
			_ => new()
			{
				Float32_0 = clearColour.X,
				Float32_1 = clearColour.Y,
				Float32_2 = clearColour.Z,
				Float32_3 = clearColour.W
			}
		};

		ClearAttachment clearInfo = new()
		{
			ClearValue = new()
			{
				Color = clearColorValue
			},
			ColorAttachment = (uint)attachment,
			AspectMask = ImageAspectFlags.ColorBit
		};

		ClearRect rectInfo = renderTarget.GetClearInfo();

		Vulkan.Vk.CmdClearAttachments( self.VkCmdBuf, 1U, &clearInfo, 1U, &rectInfo );
	}

	public static void ClearDepth<TRenderTarget>( this KaCommandBuffer self, TRenderTarget renderTarget, int attachment, float depth, uint? stencil = null )
		where TRenderTarget : IRenderTarget
	{
		ClearDepthStencilValue clearDepthValue = new()
		{
			Depth = depth,
			Stencil = stencil ?? 0U
		};

		ClearAttachment clearInfo = new()
		{
			ClearValue = new()
			{
				DepthStencil = clearDepthValue
			},
			ColorAttachment = (uint)attachment,
			AspectMask = ImageAspectFlags.DepthBit | (stencil is not null ? ImageAspectFlags.StencilBit : ImageAspectFlags.None)
		};

		ClearRect rectInfo = renderTarget.GetClearInfo();

		Vulkan.Vk.CmdClearAttachments( self.VkCmdBuf, 1U, &clearInfo, 1U, &rectInfo );
	}

	public static void BeginSecondary<T>( this KaCommandBuffer self, T renderTarget, bool simultaneous = false, bool dontReset = false )
		where T : IRenderTarget
	{
		CommandBufferUsageFlags usageFlags = dontReset
			? CommandBufferUsageFlags.None
			: CommandBufferUsageFlags.OneTimeSubmitBit;

		usageFlags |= CommandBufferUsageFlags.RenderPassContinueBit;

		if ( simultaneous )
		{
			usageFlags |= CommandBufferUsageFlags.SimultaneousUseBit;
		}

		CommandBufferInheritanceRenderingInfo inheritanceRenderingInfo = new()
		{
			SType = StructureType.CommandBufferInheritanceRenderingInfo
		};

		// colourFormats are mapped to PColorAttachmentFormats. It is not set inside GetRenderPassContinueInfo
		// because, well, it'd be set to a temporary variable which would disappear immediately and cause trouble =w=
		Span<Format> colourFormats = stackalloc Format[8];
		renderTarget.GetRenderPassContinueInfo( ref inheritanceRenderingInfo, colourFormats );
		inheritanceRenderingInfo.PColorAttachmentFormats = (Format*)Unsafe.AsPointer( ref colourFormats[0] );

		CommandBufferInheritanceInfo inheritanceInfo = new()
		{
			SType = StructureType.CommandBufferInheritanceInfo,
			PNext = &inheritanceRenderingInfo
		};

		CommandBufferBeginInfo beginInfo = new()
		{
			SType = StructureType.CommandBufferBeginInfo,
			Flags = usageFlags,
			PInheritanceInfo = &inheritanceInfo
		};

		Vulkan.Vk.BeginCommandBuffer( self.VkCmdBuf, ref beginInfo );
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public static void RenderPass<T>( this KaCommandBuffer self, T renderTarget, Action<KaCommandBuffer, T> what, bool secondaryCommandsHint = false )
		where T : IRenderTarget
	{
		renderTarget.BeginRenderPass( self, secondaryCommandsHint );
		what( self, renderTarget );
		renderTarget.EndRenderPass( self );
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public static void RenderPass<T>( this KaCommandBuffer self, T renderTarget, Action what, bool secondaryCommandsHint = false )
		where T : IRenderTarget
	{
		renderTarget.BeginRenderPass( self, secondaryCommandsHint );
		what();
		renderTarget.EndRenderPass( self );
	}
}
