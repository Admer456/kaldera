// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

namespace Kaldera.Objects;

public unsafe class KaShader : IDisposable
{
	public required KaDevice Device { get; init; }
	public required ShaderModule VkShader { get; init; }

	public static Result<KaShader> Create( KaDevice device, Span<byte> shaderInstructions )
	{
		ShaderModuleCreateInfo shaderInfo = new()
		{
			SType = StructureType.ShaderModuleCreateInfo,
			Flags = ShaderModuleCreateFlags.None,
			CodeSize = (uint)shaderInstructions.Length,
			PCode = (uint*)Unsafe.AsPointer( ref shaderInstructions[0] )
		};

		VkResult result = Vulkan.Vk.CreateShaderModule( device.VkDevice, &shaderInfo, null, out var shader );
		if ( result is not VkResult.Success )
		{
			return new Error( $"KaShader.Create: {result}" );
		}

		return new KaShader
		{
			Device = device,
			VkShader = shader
		};
	}

	public void Dispose()
	{
		Vulkan.Vk.DestroyShaderModule( Device.VkDevice, VkShader, null );
	}
}
