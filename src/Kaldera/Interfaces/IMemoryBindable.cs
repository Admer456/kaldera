// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

namespace Kaldera.Interfaces;

public interface IMemoryBindable
{
	VkResult Bind( DeviceMemory memory, ulong offset );
	MemoryRequirements GetMemoryRequirements();
}
