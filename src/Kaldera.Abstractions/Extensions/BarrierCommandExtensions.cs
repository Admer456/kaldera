// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Objects;

namespace Kaldera.Abstractions.Extensions;

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
}
