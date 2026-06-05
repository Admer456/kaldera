// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Extensions;

namespace Kaldera.Objects;

public class LayoutSetBindingInfo
{
	public required DescriptorType Type { get; set; }
	public required int Count { get; set; }
	public required ShaderStageFlags ShaderStages { get; set; }
}

public class LayoutSetInfo
{
	public required LayoutSetBindingInfo[] Bindings { get; set; }
	public DescriptorSetLayoutCreateFlags Flags { get; set; } = DescriptorSetLayoutCreateFlags.None;
	public DescriptorBindingFlags? IndexingFlags { get; set; }
}

public class LayoutOptions
{
	public required LayoutSetInfo[] Sets { get; set; }
	public PushConstantRange[] PushConstantRanges { get; set; } = [];
}

public unsafe class KaLayout : IDisposable
{
	public required KaDevice Device { get; init; }
	public required List<DescriptorSetLayout> VkDescriptorSetLayouts { get; init; }
	public required VkPipelineLayout VkLayout { get; init; }

	public static Result<KaLayout> CreateEmpty( KaDevice device )
		=> Create( device, new() { Sets = [] } );

	public static Result<KaLayout> Create( KaDevice device, LayoutOptions options )
	{
		List<DescriptorSetLayout> setList = new( options.Sets.Length );
		for ( int i = 0; i < options.Sets.Length; i++ )
		{
			uint tempBindingId = 0;
			DescriptorSetLayoutBinding[] setBindings = options.Sets[i].Bindings.Select( b => new DescriptorSetLayoutBinding
			{
				Binding = tempBindingId++,
				DescriptorCount = (uint)b.Count,
				DescriptorType = b.Type,
				StageFlags = b.ShaderStages,
				PImmutableSamplers = null
			} ).ToArray();

			// Descriptor indexing
			DescriptorBindingFlags bindingFlags = options.Sets[i].IndexingFlags ?? DescriptorBindingFlags.None;
			DescriptorSetLayoutBindingFlagsCreateInfo bindingFlagsInfo = new()
			{
				SType = StructureType.DescriptorSetLayoutBindingFlagsCreateInfo,
				BindingCount = 1,
				PBindingFlags = &bindingFlags
			};

			DescriptorSetLayoutCreateInfo setInfo = new()
			{
				SType = StructureType.DescriptorSetLayoutCreateInfo,
				Flags = options.Sets[i].Flags,
				BindingCount = (uint)setBindings.Length,
				PBindings = setBindings.Length is 0 ? null : setBindings.AsPointer(),
				PNext = options.Sets[i].IndexingFlags is null ? null : &bindingFlagsInfo
			};

			VkResult errorCode = Vulkan.Vk.CreateDescriptorSetLayout( device.VkDevice, &setInfo, null, out DescriptorSetLayout set );
			if ( errorCode is not VkResult.Success )
			{
				return new Error( $"KaLayout.Create: Could not create set {i}: {errorCode}" );
			}

			setList.Add( set );
		}

		DescriptorSetLayout[] sets = setList.ToArray();

		var pushConstantRanges = options.PushConstantRanges;
		PipelineLayoutCreateInfo layoutInfo = new()
		{
			SType = StructureType.PipelineLayoutCreateInfo,
			SetLayoutCount = (uint)sets.Length,
			PSetLayouts = sets.Length is 0 ? null : sets.AsPointer(),
			PushConstantRangeCount = (uint)pushConstantRanges.Length,
			PPushConstantRanges = pushConstantRanges.Length is 0 ? null : pushConstantRanges.AsPointer()
		};

		VkResult result = Vulkan.Vk.CreatePipelineLayout( device.VkDevice, &layoutInfo, null, out VkPipelineLayout layout );
		if ( result is not VkResult.Success )
		{
			return new Error( $"KaLayout.Create: Could not create layout: {result}" );
		}

		return new KaLayout
		{
			Device = device,
			VkLayout = layout,
			VkDescriptorSetLayouts = setList
		};
	}

	public void Dispose()
	{
		foreach ( var setLayout in VkDescriptorSetLayouts )
		{
			Vulkan.Vk.DestroyDescriptorSetLayout( Device.VkDevice, setLayout, null );
		}

		Vulkan.Vk.DestroyPipelineLayout( Device.VkDevice, VkLayout, null );
	}
}
