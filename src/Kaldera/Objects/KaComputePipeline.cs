// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Interfaces;

namespace Kaldera.Objects;

public sealed record ComputeShaderSet(
	KaShader Shader,
	string Func ) : ShaderSet;

public class ComputePipelineOptions
{
	public required ComputeShaderSet ShaderSet { get; set; }
	public required KaLayout ResourceLayout { get; set; }
}

public unsafe class KaComputePipeline : IDisposable, IPipeline
{
	public required KaDevice Device { get; init; }
	public required KaLayout Layout { get; init; }
	public required VkPipeline VkPipeline { get; init; }

	public PipelineBindPoint BindPoint => PipelineBindPoint.Compute;

	public static Result<KaComputePipeline> Create( KaDevice device, ComputePipelineOptions options )
	{
		ShaderStage shaderStage = new()
		{
			Name = options.ShaderSet.Func,
			KaShader = options.ShaderSet.Shader,
			Flags = ShaderStageFlags.ComputeBit
		};

		PipelineShaderStageCreateInfo stage = new()
		{
			SType = StructureType.PipelineShaderStageCreateInfo,
			Module = shaderStage.KaShader.VkShader,
			Stage = shaderStage.Flags,
			PName = shaderStage.NamePtr,
			Flags = PipelineShaderStageCreateFlags.None
		};

		ComputePipelineCreateInfo createInfo = new()
		{
			SType = StructureType.ComputePipelineCreateInfo,
			Stage = stage,
			Layout = options.ResourceLayout.VkLayout
		};

		VkPipeline pipeline = default;
		VkResult result = Vulkan.Vk.CreateComputePipelines( device.VkDevice, default, 1, &createInfo, null, &pipeline );
		if ( result is not VkResult.Success )
		{
			return new Error( $"KaComputePipeline.Create: Could not create pipeline - {result}" );
		}

		return new KaComputePipeline
		{
			Device = device,
			Layout = options.ResourceLayout,
			VkPipeline = pipeline,
		};
	}

	public void Dispose()
	{
		Vulkan.Vk.DestroyPipeline( Device.VkDevice, VkPipeline, null );
	}
}
