// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Abstractions.Interfaces;
using Kaldera.Objects;

namespace Kaldera.Abstractions.Utilities;

public class LayoutSetBuilder
{
	public required DescriptorSetLayoutCreateFlags Flags { get; init; }
	public DescriptorBindingFlags? IndexingFlags { get; set; }
	public List<LayoutSetBindingInfo> Bindings { get; set; } = new();

	public LayoutSetBuilder Resource( DescriptorType type, ShaderStageFlags flags )
	{
		Bindings.Add( new()
		{
			Count = 1,
			ShaderStages = flags,
			Type = type
		} );

		return this;
	}

	public LayoutSetBuilder StorageBuffer( ShaderStageFlags flags = ShaderStageFlags.AllGraphics )
		=> Resource( DescriptorType.StorageBuffer, flags );

	public LayoutSetBuilder UniformBuffer( ShaderStageFlags flags = ShaderStageFlags.AllGraphics )
		=> Resource( DescriptorType.UniformBuffer, flags );

	public LayoutSetBuilder SampledTexture( ShaderStageFlags flags = ShaderStageFlags.AllGraphics )
		=> Resource( DescriptorType.SampledImage, flags );

	public LayoutSetBuilder Sampler( ShaderStageFlags flags = ShaderStageFlags.FragmentBit )
		=> Resource( DescriptorType.Sampler, flags );
}

public class LayoutBuilder
{
	public List<LayoutSetBuilder> Sets { get; } = new();
	public List<PushConstantRange> Ranges { get; } = new();

	public static LayoutBuilder Begin()
		=> new();

	public LayoutBuilder StructuredSet( Action<LayoutSetBuilder> modifier )
	{
		Sets.Add( new()
		{
			Flags = DescriptorSetLayoutCreateFlags.PushDescriptorBit
		} );

		modifier( Sets.Last() );
		return this;
	}

	public LayoutBuilder SampledTextureHeap( int maxCapacity, ShaderStageFlags flags = ShaderStageFlags.FragmentBit )
	{
		Sets.Add( new()
		{
			Flags = DescriptorSetLayoutCreateFlags.PushDescriptorBit |
			        DescriptorSetLayoutCreateFlags.UpdateAfterBindPoolBit,

			IndexingFlags = DescriptorBindingFlags.PartiallyBoundBit |
			                DescriptorBindingFlags.UpdateAfterBindBit |
			                DescriptorBindingFlags.UpdateUnusedWhilePendingBit |
			                DescriptorBindingFlags.VariableDescriptorCountBit
		} );

		Sets.Last().Bindings.Add( new()
		{
			Count = maxCapacity,
			ShaderStages = flags,
			Type = DescriptorType.SampledImage
		} );

		return this;
	}

	public LayoutBuilder StorageBufferHeap( int maxCapacity, ShaderStageFlags flags = ShaderStageFlags.ComputeBit )
	{
		Sets.Add( new()
		{
			Flags = DescriptorSetLayoutCreateFlags.PushDescriptorBit |
			        DescriptorSetLayoutCreateFlags.UpdateAfterBindPoolBit,

			IndexingFlags = DescriptorBindingFlags.PartiallyBoundBit |
			                DescriptorBindingFlags.UpdateAfterBindBit |
			                DescriptorBindingFlags.UpdateUnusedWhilePendingBit |
			                DescriptorBindingFlags.VariableDescriptorCountBit
		} );

		Sets.Last().Bindings.Add( new()
		{
			Count = maxCapacity,
			ShaderStages = flags,
			Type = DescriptorType.StorageBuffer
		} );

		return this;
	}

	public LayoutBuilder PushConstant<T>( ShaderStageFlags stageFlags = ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit | ShaderStageFlags.ComputeBit, int offset = 0 )
		where T : unmanaged
	{
		Ranges.Add( new( stageFlags, (uint)offset, (uint)Unsafe.SizeOf<T>() ) );
		return this;
	}

	public LayoutOptions Build()
	{
		return new()
		{
			Sets = Sets.Select( s => new LayoutSetInfo
			{
				Bindings = s.Bindings.ToArray(),
				Flags = s.Flags,
				IndexingFlags = s.IndexingFlags
			} ).ToArray(),

			PushConstantRanges = Ranges.ToArray()
		};
	}
}
