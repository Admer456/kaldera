// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using System.Diagnostics;
using Kaldera;
using Kaldera.Abstractions.Memory;
using Kaldera.Abstractions.Utilities;
using Kaldera.Names;
using Kaldera.Objects;
using Silk.NET.Vulkan;

namespace ExampleBase;

public class VulkanBootstrapOptions
{
	public List<string> InstanceExtensions { get; set; } = [];
	public List<string> DeviceExtensions { get; set; } = [];
	public StartupOptions.DeviceSelector? PhysicalDeviceSelector { get; set; }
	public StartupOptions.FeatureModifier? DeviceFeatureModifier { get; set; }
}

public class VulkanBootstrap : IDisposable
{
	public required KaInstance Instance { get; init; }
	public required KaPhysicalDevice PhysicalDevice { get; init; }
	public required KaDevice Device { get; init; }
	public required KaQueue Queue { get; init; }
	public required uint QueueFamily { get; init; }
	public required SimpleAllocator Allocator { get; init; }

	public static Result<VulkanBootstrap> Create( VulkanBootstrapOptions options )
	{
		if ( !Vulkan.Init().Get( out var error ) )
		{
			return error.Prepend( "VulkanBootstrap.Create: Could not init Vulkan context" );
		}

		// Vulkan instance, device and queue creation etc.
		GraphicsContext graphicsContext = Startup.CreateVulkan14Context( new()
		{
			ApplicationName = "VulkanTest",
			EngineName = "ExampleEngine5",
#if DEBUG
			LogMethod = VulkanDebugLog,
			LogLevel = VulkanDebugLogLevel.Warning,
			OptionalLayers = [ LayerNames.KhronosValidation ],
#endif
			InstanceExtensions = options.InstanceExtensions.ToArray(),
			DeviceExtensions = options.DeviceExtensions.ToArray(),
			PhysicalDeviceSelector = options.PhysicalDeviceSelector,
			DeviceFeatureModifier = options.DeviceFeatureModifier
		} ).Checked();

		KaDevice device = graphicsContext.Device;
		KaQueue queue = graphicsContext.Queue;
		SimpleAllocator allocator = SimpleAllocator.Create( device, queue );

		return new VulkanBootstrap
		{
			Instance = device.Instance,
			PhysicalDevice = device.Physical,
			Device = device,
			Queue = queue,
			QueueFamily = queue.Family,
			Allocator = allocator
		};
	}

	private static void VulkanDebugLog( DebugReportFlagsEXT flags, DebugReportObjectTypeEXT objectType, string message )
	{
		if ( flags.HasFlag( DebugReportFlagsEXT.ErrorBitExt ) && Debugger.IsAttached )
		{
			Debugger.Break();
		}
		Debug.WriteLine( $"[{flags}] ({objectType}) {message}" );
	}

	public void Dispose()
	{
		Device.Dispose();
		Instance.Dispose();
		Vulkan.Shutdown();
	}
}
