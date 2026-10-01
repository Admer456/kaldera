// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Names;
using Kaldera.Objects;
using Kaldera.Utilities;

namespace Kaldera.Abstractions.Utilities;

public class StartupOptions
{
	public delegate Result<KaPhysicalDevice> DeviceSelector( KaInstance instance, string[]? deviceExtensions = null, StructureChain? features = null );
	public delegate void FeatureModifier( StructureChain features );

	public required string ApplicationName { get; set; }
	public required string EngineName { get; set; }

	/// <summary> Extra instance extensions. </summary>
	public string[] InstanceExtensions { get; set; } = [];

	/// <summary> Extra instance extensions that aren't strictly required. </summary>
	public string[] OptionalInstanceExtensions { get; set; } = [];

	/// <summary> Extra device extensions. </summary>
	public string[] DeviceExtensions { get; set; } = [];

	/// <summary> Extra device extensions that aren't strictly required. </summary>
	public string[] OptionalDeviceExtensions { get; set; } = [];

	/// <summary> Layers. </summary>
	public string[] Layers { get; set; } = [];

	/// <summary> Extra optional layers. </summary>
	public string[] OptionalLayers { get; set; } = [];

	/// <summary> An override to select a physical device. Required by OpenXR. </summary>
	public DeviceSelector? PhysicalDeviceSelector { get; set; }

	/// <summary> Modifies desired device features. </summary>
	public FeatureModifier? DeviceFeatureModifier { get; set; }

	public KaInstance.DebugReport? LogMethod { get; set; }
	public VulkanDebugLogLevel LogLevel { get; set; } = VulkanDebugLogLevel.None;
}

public class GraphicsContext
{
	public required KaDevice Device { get; init; }
	public required KaQueue Queue { get; init; }
	public required int QueueFamily { get; init; }
}

public static class Startup
{
	public static string[] GetDesiredDeviceExtensions()
	{
		// In Vulkan 1.0, you would have to include EVERY extension you'd use, OOF
		// Here we're just enabling some extra extensions that aren't part of the Vulkan
		// core 1.4 specification
		List<string> result =
		[
			// Lets us render to a window
			DeviceExtensionNames.KhrSwapchain,
			// Lets us use cool new SPIR-V features
			DeviceExtensionNames.KhrSpirv14
		];

		// Compatibility with MoltenVK, or:
		// "[...] allows a non-conformant Vulkan implementation to be built on top of
		// another non-Vulkan graphics API, and identifies differences between that
		// implementation and a fully-conformant native Vulkan implementation."
		if ( Environment.OSVersion.Platform is PlatformID.MacOSX )
		{
			result.Add( DeviceExtensionNames.KhrPortabilitySubset );
		}

		return result.ToArray();
	}

	public static StructureChain GetDesiredDeviceFeatures()
		=> StructureChain.Begin( new PhysicalDeviceFeatures2
			{
				Features = new()
				{
					FillModeNonSolid = true,
					IndependentBlend = true,
					SamplerAnisotropy = true,
					SampleRateShading = true,
					TextureCompressionBC = true,
					RobustBufferAccess = true
				}
			} )
			.Add( new PhysicalDeviceVulkan11Features
			{
				ShaderDrawParameters = true,
				Multiview = true,
				VariablePointers = true,
				VariablePointersStorageBuffer = true
			} )
			.Add( new PhysicalDeviceVulkan12Features
			{
				ShaderFloat16 = true,
				ShaderInt8 = true,
				DescriptorIndexing = true
			} )
			.Add( new PhysicalDeviceVulkan13Features
			{
				Synchronization2 = true,
				DynamicRendering = true,
				Maintenance4 = true
			} )
			.Add( new PhysicalDeviceVulkan14Features
			{
				PushDescriptor = true,
				BresenhamLines = true,
				StippledBresenhamLines = true,
				DynamicRenderingLocalRead = true,
				Maintenance5 = true,
				Maintenance6 = true
			} );

	public static Result<GraphicsContext> CreateVulkan14Context( StartupOptions options )
	{
		List<string> instanceExtensions = options.InstanceExtensions
			.Concat( options.OptionalInstanceExtensions )
			.ToList();

		if ( !instanceExtensions.Contains( InstanceExtensionNames.KhrSurface ) )
		{
			instanceExtensions.Add( InstanceExtensionNames.KhrSurface );
		}

		// Mandatory & optional instance extension handling
		{
			List<string> unsupportedInstanceExtensions = new();
			string[] supportedInstanceExtensions = Vulkan.GetInstanceExtensions();
			foreach ( var mandatoryExtension in options.InstanceExtensions )
			{
				if ( !supportedInstanceExtensions.Contains( mandatoryExtension ) )
				{
					unsupportedInstanceExtensions.Add( mandatoryExtension );
				}
			}

			if ( unsupportedInstanceExtensions.Count > 0 )
			{
				return new Error( $"Startup: Required instance extensions not present: {unsupportedInstanceExtensions}" );
			}

			foreach ( var desiredExtension in options.OptionalInstanceExtensions )
			{
				// If an optional extension is not available, remove it from the enabled extension list.
				// The Vulkan API itself does not have a concept of optional extensions
				if ( !supportedInstanceExtensions.Contains( desiredExtension ) )
				{
					instanceExtensions.RemoveAll( s => s.Equals( desiredExtension ) );
				}
			}
		}

		List<string> layers = options.Layers
			.Concat( options.OptionalLayers )
			.ToList();

		{
			List<string> unsupportedLayers = new();
			string[] supportedLayers = Vulkan.GetInstanceLayers();
			foreach ( var mandatoryLayer in options.Layers )
			{
				if ( !supportedLayers.Contains( mandatoryLayer ) )
				{
					unsupportedLayers.Add( mandatoryLayer );
				}
			}

			if ( unsupportedLayers.Count > 0 )
			{
				return new Error( $"Startup: Required layers not present: {unsupportedLayers}" );
			}

			foreach ( var desiredLayer in options.OptionalLayers )
			{
				if ( !supportedLayers.Contains( desiredLayer ) )
				{
					layers.RemoveAll( s => s.Equals( desiredLayer ) );
				}
			}
		}

		Result<KaInstance> vulkanInstanceResult = KaInstance.Create( new()
		{
			ApplicationName = options.ApplicationName,
			EngineName = options.EngineName,
			ApiVersion = new( 1, 4, 0 ),

			// If this is set, VK_EXT_debug_report will be loaded
			// so you can actually receive debug logs
			LogLevel = options.LogLevel,

			// In this case, SDL will give us some needed extensions for windowing,
			// and we just add VK_KHR_portability_subset there for compatibility with MoltenVK
			EnabledInstanceExtensions = instanceExtensions.ToArray(),
			EnabledLayers = layers.ToArray()
		} );

		if ( !vulkanInstanceResult.Get( out var error, out var vulkanInstance ) )
		{
			return new Error( "Startup: Could not create Vulkan instance", error );
		}

		if ( options.LogMethod is not null )
		{
			KaInstance.OnDebugLog += options.LogMethod;
		}

		StructureChain deviceFeatures = GetDesiredDeviceFeatures();
		List<string> deviceExtensions = GetDesiredDeviceExtensions()
			.Concat( options.DeviceExtensions )
			.Concat( options.OptionalDeviceExtensions )
			.ToList();

		// Allow the user to request extra features
		options.DeviceFeatureModifier?.Invoke( deviceFeatures );

		// We have an instance now, so can start looking for physical devices/GPUs. Typically, there's a combination
		// of a discrete and integrated GPU, and you'd pick the one with the best Vulkan support or w/e.
		// There's a score-based algorithm built into this library for your convenience
		options.PhysicalDeviceSelector ??= DeviceSelection.ScoreBased;
		Result<KaPhysicalDevice> physicalDeviceResult = options.PhysicalDeviceSelector( vulkanInstance, deviceExtensions.ToArray(), deviceFeatures );
		if ( !physicalDeviceResult.Get( out error, out var physicalDevice ) )
		{
			return error.Prepend( "Startup: Could not find an appropriate GPU" );
		}

		// Mandatory & optional device extension handling
		{
			List<string> unsupportedDeviceExtensions = new();
			string[] supportedDeviceExtensions = physicalDevice.GetSupportedExtensions();
			foreach ( var mandatoryExtension in options.DeviceExtensions )
			{
				if ( !supportedDeviceExtensions.Contains( mandatoryExtension ) )
				{
					unsupportedDeviceExtensions.Add( mandatoryExtension );
				}
			}

			if ( unsupportedDeviceExtensions.Count > 0 )
			{
				return new Error( $"Startup: Required instance extensions not present: {unsupportedDeviceExtensions}" );
			}

			foreach ( var desiredExtension in options.OptionalDeviceExtensions )
			{
				// If an optional extension is not available, remove it from the enabled extension list.
				// The Vulkan API itself does not have a concept of optional extensions
				if ( !supportedDeviceExtensions.Contains( desiredExtension ) )
				{
					deviceExtensions.RemoveAll( s => s.Equals( desiredExtension ) );
				}
			}
		}

		// Typically, this would be done manually, but there's a few builtin utilities here.
		// GPUs have many, many queues that you can stuff commands into.
		// It's 2026, your typical desktop GPU will support all three.
		uint queueFamily = physicalDevice.GetFirstQueueFamilyIndex( QueueFlags.GraphicsBit | QueueFlags.ComputeBit | QueueFlags.TransferBit );

		// Now we create the logical device. There's a separation between physical and logical devices
		// because "one" logical device might actually be two physical GPUs working together etc. you get the idea
		Result<KaDevice> gpuResult = KaDevice.Create( vulkanInstance, physicalDeviceResult, new()
		{
			// Select some features and extensions
			EnabledFeatures = deviceFeatures,
			EnabledDeviceExtensions = deviceExtensions.ToArray(),
			// Just one queue for now :)
			QueueFamilies = [new( queueFamily )]
		} );

		if ( !gpuResult.Get( out error, out var gpu ) )
		{
			return error.Prepend( "Startup: Could not create Vulkan device" );
		}

		// Then it's the dull and boring process of extracting any needed queues from the device...
		// From here onwards it's really easy to just have an all-rounder queue and a presentation queue
		// and whatnot. That's up to you, I'm just using one here
		Result<KaQueue> queueResult = gpu.GetQueue( queueFamily );
		if ( !queueResult.Get( out error, out var queue ) )
		{
			return error.Prepend( "Startup: Could not obtain queue" );
		}

		return new GraphicsContext
		{
			Device = gpu,
			Queue = queue,
			QueueFamily = (int)queueFamily
		};
	}
}
