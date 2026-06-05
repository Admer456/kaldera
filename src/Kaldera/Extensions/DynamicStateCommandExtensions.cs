// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Objects;
using Silk.NET.Core;

namespace Kaldera.Extensions;

public static class DynamicStateCommandExtensions
{
	public static void SetViewportFlipped( this KaCommandBuffer self, int viewportId, Extent2D extent, float minDepth, float maxDepth )
	{
		Viewport viewport = new()
		{
			X = 0.0f,
			Y = 0.0f,
			Width = extent.Width,
			Height = extent.Height,
			MinDepth = minDepth,
			MaxDepth = maxDepth
		};

		Vulkan.Vk.CmdSetViewport( self.VkCmdBuf, (uint)viewportId, 1, ref viewport );
	}

	public static void SetViewport( this KaCommandBuffer self, int viewportId, Extent2D extent, float minDepth, float maxDepth )
	{
		Viewport viewport = new()
		{
			X = 0.0f,
			Width = extent.Width,

			// Vulkan is Y-down by default, this here makes it render
			// as Y-up to the framebuffer. This has an effect
			// on clockwise vs. counterclockwise orientation too
			Y = extent.Height,
			Height = -extent.Height,

			MinDepth = minDepth,
			MaxDepth = maxDepth
		};

		Vulkan.Vk.CmdSetViewport( self.VkCmdBuf, (uint)viewportId, 1, ref viewport );
	}

	public static void SetScissor( this KaCommandBuffer self, int scissorId, Extent2D extent )
	{
		Rect2D scissor = new()
		{
			Offset = new(),
			Extent = extent
		};
		Vulkan.Vk.CmdSetScissor( self.VkCmdBuf, (uint)scissorId, 1, ref scissor );
	}
}
