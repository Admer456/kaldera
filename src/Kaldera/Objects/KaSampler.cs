// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

namespace Kaldera.Objects;

public struct SamplerOptions
{
	public required Filter MagFilter { get; set; }
	public required Filter MinFilter { get; set; }
	public required SamplerMipmapMode MipmapMode { get; set; }
	public required float MipLodBias { get; set; }
	public required float MaxLod { get; set; }
	public required float MinLod { get; set; }
	public required SamplerAddressMode AddressModeU { get; set; }
	public required SamplerAddressMode AddressModeV { get; set; }
	public required SamplerAddressMode AddressModeW { get; set; }
	public required float? MaxAnisotropy { get; set; }
	// TODO: Custom border colours with VK_EXT_custom_border_color
	public required BorderColor BorderColour { get; set; }
};

public readonly struct KaSampler : IDisposable
{
	public required KaDevice Device { get; init; }
	public required VkSampler VkSampler { get; init; }

	public static unsafe Result<KaSampler> Create( KaDevice device, SamplerOptions options )
	{
		SamplerCreateInfo createInfo = new()
		{
			SType = StructureType.SamplerCreateInfo,
			MagFilter = options.MagFilter,
			MinFilter = options.MinFilter,
			MipmapMode = options.MipmapMode,
			MipLodBias = options.MipLodBias,
			MaxLod = options.MaxLod,
			MinLod = options.MinLod,
			AddressModeU = options.AddressModeU,
			AddressModeV = options.AddressModeV,
			AddressModeW = options.AddressModeW,
			AnisotropyEnable = options.MaxAnisotropy is not null,
			MaxAnisotropy = options.MaxAnisotropy ?? 0.0f,
			CompareEnable = false,
			BorderColor = options.BorderColour,
			UnnormalizedCoordinates = false
		};

		VkResult result = Vulkan.Vk.CreateSampler( device.VkDevice, ref createInfo, null, out VkSampler sampler );
		if ( result is not VkResult.Success )
		{
			return new Error( $"KaSampler.Create: {result}" );
		}

		return new KaSampler
		{
			Device = device,
			VkSampler = sampler
		};
	}

	public unsafe void Dispose()
		=> Vulkan.Vk.DestroySampler( Device.VkDevice, VkSampler, null );
}
