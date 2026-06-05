// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Objects;

namespace Kaldera.Abstractions.Interfaces;

public interface IRenderTarget
{
	Vector2 CurrentSize { get; }
	Format GetColourImageFormat();
	ClearRect GetClearInfo();
	Result RequestResize( Vector2 newSize );

	void GetRenderPassContinueInfo( ref CommandBufferInheritanceRenderingInfo info, Span<Format> colourFormats );
	void BeginRenderPass( KaCommandBuffer commands, bool secondaryCommandsHint );
	void EndRenderPass( KaCommandBuffer commands );
}
