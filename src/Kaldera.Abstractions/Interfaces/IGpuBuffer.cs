// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Interfaces;
using Kaldera.Objects;

namespace Kaldera.Abstractions.Interfaces;

public interface IGpuBuffer : IDisposable
{
	KaBufferRange BufferRange { get; }
	IResourceAllocator Allocator { get; }
}
