// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Abstractions.Interfaces;
using Kaldera.Objects;

namespace Kaldera.Abstractions.Extensions;

public ref struct BarrierBuilder
{
	public BarrierBuilder( KaCommandBuffer commandBuffer )
	{
		CommandBuffer = commandBuffer;
	}

	public KaCommandBuffer CommandBuffer;

	public PipelineStageFlags2 BeforeStage = PipelineStageFlags2.None;
	public AccessFlags2 BeforeAccess = AccessFlags2.None;
	public PipelineStageFlags2 AfterStage = PipelineStageFlags2.None;
	public AccessFlags2 AfterAccess = AccessFlags2.None;

	public BarrierBuilder Capture( BarrierStages stage )
	{
		BeforeStage = stage.ToPipelineStageFlags();
		return this;
	}

	public BarrierBuilder Capture( PipelineStageFlags2 stage, AccessFlags2 access )
	{
		BeforeStage = stage;
		BeforeAccess = access;
		return this;
	}

	public BarrierBuilder Block( BarrierStages stage )
	{
		AfterStage = stage.ToPipelineStageFlags();
		return this;
	}

	public BarrierBuilder Block( PipelineStageFlags2 stage, AccessFlags2 access )
	{
		AfterStage = stage;
		AfterAccess = access;
		return this;
	}

	public BarrierBuilder Hazard( BarrierHazards hazards = BarrierHazards.ReadAfterWrite )
	{
		(BeforeAccess, AfterAccess) = hazards.ToAccessFlags();
		return this;
	}

	public BarrierBuilder Transition<T>( ref T texture, ImageLayout layout )
		where T : IGpuTexture
	{
		if ( texture.CurrentLayout == layout )
		{
			return this;
		}

		CommandBuffer.TransitionImageLayout(
			texture.State.Image.VkImage,
			oldLayout: texture.CurrentLayout,
			layout,
			srcAccessMask: BeforeAccess,
			dstAccessMask: AfterAccess,
			srcStageMask: BeforeStage,
			dstStageMask: AfterStage,
			texture.AspectFlags,
			texture.State.MipLevels,
			texture.State.ArrayLayers
		);

		texture.CurrentLayout = layout;
		return this;
	}
}

public static class BarrierCommandExtensions
{
	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public static void Barrier( this KaCommandBuffer self, BarrierStages before, BarrierStages after,
		BarrierHazards hazards = BarrierHazards.ReadAfterWrite )
	{
		PipelineStageFlags2 beforeStage = before.ToPipelineStageFlags();
		PipelineStageFlags2 afterStage = after.ToPipelineStageFlags();
		(AccessFlags2 beforeAccess, AccessFlags2 afterAccess) = hazards.ToAccessFlags();

		self.Barrier( beforeAccess, beforeStage, afterAccess, afterStage );
	}

	public static BarrierBuilder Barrier( this KaCommandBuffer self )
		=> new( self );
}
