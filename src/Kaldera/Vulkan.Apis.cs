// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

namespace Kaldera;

public static partial class Vulkan
{
	public static Vk Vk { get; private set; } = null!;
	public static Silk.NET.Vulkan.Extensions.EXT.ExtDebugReport DebugReport { get; internal set; } = null!;
	public static Silk.NET.Vulkan.Extensions.EXT.ExtExtendedDynamicState3 DynamicState3 { get; internal set; } = null!;
	public static Silk.NET.Vulkan.Extensions.KHR.KhrSwapchain Swapchain { get; internal set; } = null!;
	public static Silk.NET.Vulkan.Extensions.KHR.KhrSurface Surface { get; internal set; } = null!;
}
