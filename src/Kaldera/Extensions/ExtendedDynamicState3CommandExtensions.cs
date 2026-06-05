// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Objects;
using Silk.NET.Core;

namespace Kaldera.Extensions;

// TODO: Move this to a Kaldera.Addons.ExtendedDynamicState3 module
public static class ExtendedDynamicState3CommandExtensions
{
	public static void SetPolygonMode( this KaCommandBuffer self, PolygonMode mode )
		=> Vulkan.DynamicState3.CmdSetPolygonMode( self.VkCmdBuf, mode );

	public static void SetAlphaToCoverage( this KaCommandBuffer self, bool enabled )
		=> Vulkan.DynamicState3.CmdSetAlphaToCoverageEnable( self.VkCmdBuf, enabled );

	public static void SetColourBlending( this KaCommandBuffer self, int attachment, bool enabled )
	{
		Bool32 value = enabled;
		Vulkan.DynamicState3.CmdSetColorBlendEnable( self.VkCmdBuf, (uint)attachment, 1, ref value );
	}

	public static void SetColourBlendingEquation( this KaCommandBuffer self, int attachment, ColorBlendEquationEXT equation )
		=> Vulkan.DynamicState3.CmdSetColorBlendEquation( self.VkCmdBuf, (uint)attachment, 1, ref equation );

	public static void SetConservativeRasterisationMode( this KaCommandBuffer self, ConservativeRasterizationModeEXT mode )
		=> Vulkan.DynamicState3.CmdSetConservativeRasterizationMode( self.VkCmdBuf, mode );

	public static void SetDepthClamp( this KaCommandBuffer self, bool enabled )
		=> Vulkan.DynamicState3.CmdSetDepthClampEnable( self.VkCmdBuf, enabled );

	public static void SetDepthClip( this KaCommandBuffer self, bool enabled )
		=> Vulkan.DynamicState3.CmdSetDepthClipEnable( self.VkCmdBuf, enabled );

	public static void SetDepthClipNegativeOneToOne( this KaCommandBuffer self, bool enabled )
		=> Vulkan.DynamicState3.CmdSetDepthClipNegativeOneToOne( self.VkCmdBuf, enabled );

	public static void SetLineRasterisationMode( this KaCommandBuffer self, LineRasterizationMode mode )
		=> Vulkan.DynamicState3.CmdSetLineRasterizationMode( self.VkCmdBuf, (LineRasterizationModeEXT)mode );

	public static void SetLineStipple( this KaCommandBuffer self, bool enabled )
		=> Vulkan.DynamicState3.CmdSetLineStippleEnable( self.VkCmdBuf, enabled );

	public static void SetSamples( this KaCommandBuffer self, SampleCountFlags samples )
		=> Vulkan.DynamicState3.CmdSetRasterizationSamples( self.VkCmdBuf, samples );
}
