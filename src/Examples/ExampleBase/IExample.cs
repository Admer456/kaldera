// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera;
using Kaldera.Abstractions.RenderTargets;
using Kaldera.Interfaces;
using Kaldera.Objects;
using SDL3;

namespace ExampleBase;

public interface IExample : IDisposable
{
	void Setup( VulkanBootstrapOptions options )
	{
	}

	Result Init( KaInstance instance, KaDevice device, KaQueue queue, IResourceAllocator allocator );
	bool OnEvent( IntPtr window, SDL.Event @event ) => true;
	Result OnFrame( float dt, SwapchainRenderTarget windowRt );
}
