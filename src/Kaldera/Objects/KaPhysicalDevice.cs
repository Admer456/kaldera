// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Extensions;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Kaldera.Objects;

public sealed unsafe class KaPhysicalDevice
{
	public VkInstance VkInstance => Instance.VkInstance;

	public required KaInstance Instance { get; init; }
	public required VkPhysicalDevice VkPhysicalDevice { get; init; }

	public string[] GetSupportedExtensions()
	{
		uint count = 0;
		Vulkan.Vk.EnumerateDeviceExtensionProperties( VkPhysicalDevice, (byte*)null, &count, null );
		ExtensionProperties[] props = new ExtensionProperties[count];
		Vulkan.Vk.EnumerateDeviceExtensionProperties( VkPhysicalDevice, (byte*)null, &count, props.AsSpan() );

		return props
			.Select( p => ((CString)p.ExtensionName).ToString() )
			.ToArray();
	}

	public PhysicalDeviceFeatures GetFeatures()
	{
		Vulkan.Vk.GetPhysicalDeviceFeatures( VkPhysicalDevice, out var features );
		return features;
	}

	public PhysicalDeviceProperties GetProperties()
	{
		Vulkan.Vk.GetPhysicalDeviceProperties( VkPhysicalDevice, out var properties );
		return properties;
	}

	public QueueFamilyProperties[] GetQueueFamilyProperties()
	{
		uint count = 0;
		Vulkan.Vk.GetPhysicalDeviceQueueFamilyProperties( VkPhysicalDevice, &count );
		QueueFamilyProperties[] items = new QueueFamilyProperties[count];
		Vulkan.Vk.GetPhysicalDeviceQueueFamilyProperties( VkPhysicalDevice, &count, items.AsSpan() );
		return items;
	}

	// TODO: Move to an extension
	public uint GetFirstQueueFamilyIndex( QueueFlags flags )
	{
		uint queueIndex = 0;
		foreach ( var queueFamily in GetQueueFamilyProperties() )
		{
			if ( queueFamily.QueueFlags.HasFlag( flags ) )
			{
				break;
			}
			queueIndex++;
		}

		return queueIndex;
	}

	// TODO: Move to an extension
	public SampleCountFlags GetMaxSampleCount()
	{
		var props = GetProperties();
		SampleCountFlags counts = props.Limits.FramebufferColorSampleCounts & props.Limits.FramebufferDepthSampleCounts;

		if ( (counts & SampleCountFlags.Count64Bit) != 0 ) return SampleCountFlags.Count64Bit;
		if ( (counts & SampleCountFlags.Count32Bit) != 0 ) return SampleCountFlags.Count32Bit;
		if ( (counts & SampleCountFlags.Count16Bit) != 0 ) return SampleCountFlags.Count16Bit;
		if ( (counts & SampleCountFlags.Count8Bit) != 0 ) return SampleCountFlags.Count8Bit;
		if ( (counts & SampleCountFlags.Count4Bit) != 0 ) return SampleCountFlags.Count4Bit;
		if ( (counts & SampleCountFlags.Count2Bit) != 0 ) return SampleCountFlags.Count2Bit;

		return SampleCountFlags.Count1Bit;
	}

	public SurfaceCapabilitiesKHR GetSurfaceCapabilities( SurfaceKHR surface )
	{
		Vulkan.Surface.GetPhysicalDeviceSurfaceCapabilities( VkPhysicalDevice, surface, out SurfaceCapabilitiesKHR surfaceCapabilities );
		return surfaceCapabilities;
	}

	public SurfaceFormatKHR[] GetSurfaceFormats( SurfaceKHR surface )
	{
		uint count = 0;
		Vulkan.Surface.GetPhysicalDeviceSurfaceFormats( VkPhysicalDevice, surface, &count, null );
		SurfaceFormatKHR[] items = new SurfaceFormatKHR[count];
		Vulkan.Surface.GetPhysicalDeviceSurfaceFormats( VkPhysicalDevice, surface, &count, items.AsSpan() );
		return items;
	}

	public PresentModeKHR[] GetSurfacePresentModes( SurfaceKHR surface )
	{
		uint count = 0;
		Vulkan.Surface.GetPhysicalDeviceSurfacePresentModes( VkPhysicalDevice, surface, &count, null );
		PresentModeKHR[] items = new PresentModeKHR[count];
		Vulkan.Surface.GetPhysicalDeviceSurfacePresentModes( VkPhysicalDevice, surface, &count, items.AsSpan() );
		return items;
	}

	public PhysicalDeviceDriverProperties GetDriverProperties()
		=> GetExtendedProperties<PhysicalDeviceDriverProperties>();

	public PhysicalDeviceMultiviewFeatures GetMultiviewFeatures()
		=> GetExtendedFeatures<PhysicalDeviceMultiviewFeatures>();

	public PhysicalDeviceMultiviewProperties GetMultiviewProperties()
		=> GetExtendedProperties<PhysicalDeviceMultiviewProperties>();

	public PhysicalDeviceMemoryProperties GetMemoryProperties()
		=> Vulkan.Vk.GetPhysicalDeviceMemoryProperties( VkPhysicalDevice );

	public T GetExtendedFeatures<T>()
		where T : unmanaged, IStructuredType, IChainable
	{
		T extendedProperties = new();
		extendedProperties.StructureType();
		extendedProperties.PNext = null;

		PhysicalDeviceFeatures2 properties = new();
		properties.SType = StructureType.PhysicalDeviceFeatures2;
		properties.PNext = &extendedProperties;
		Vulkan.Vk.GetPhysicalDeviceFeatures2( VkPhysicalDevice, &properties );

		return extendedProperties;
	}

	public T GetExtendedProperties<T>()
		where T : unmanaged, IStructuredType, IChainable
	{
		T extendedProperties = new();
		extendedProperties.StructureType();
		extendedProperties.PNext = null;

		PhysicalDeviceProperties2 properties = new();
		properties.SType = StructureType.PhysicalDeviceProperties2;
		properties.PNext = &extendedProperties;
		Vulkan.Vk.GetPhysicalDeviceProperties2( VkPhysicalDevice, &properties );

		return extendedProperties;
	}
}
