// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Objects;

namespace Kaldera.Interfaces;

public interface IPipeline
{
	KaLayout Layout { get; }
	VkPipeline VkPipeline { get; }
	PipelineBindPoint BindPoint { get; }
}
