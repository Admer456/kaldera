// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Extensions;
using Silk.NET.Core;
using static Silk.NET.Vulkan.Vk;

namespace Kaldera;

public static partial class Vulkan
{
	public static Result Init()
	{
		Vk = GetApi();
		return Result.Success();
	}

	public static unsafe Version32 QueryInstanceVersion()
	{
		uint versionValue = 0;
		Vk.EnumerateInstanceVersion( &versionValue );
		return (Version32)versionValue;
	}

	public static unsafe string[] GetInstanceExtensions()
	{
		uint propertyCount = 0;
		Vk.EnumerateInstanceExtensionProperties( (byte*)null, ref propertyCount, null );
		ExtensionProperties[] result = new ExtensionProperties[propertyCount];
		Vk.EnumerateInstanceExtensionProperties( (byte*)null, ref propertyCount, result.AsPointer() );

		return result
			.Select( p => ((CString)p.ExtensionName).ToString() )
			.ToArray();
	}

	public static unsafe string[] GetInstanceLayers()
	{
		uint propertyCount = 0;
		Vk.EnumerateInstanceLayerProperties( ref propertyCount, null );
		LayerProperties[] result = new LayerProperties[propertyCount];
		Vk.EnumerateInstanceLayerProperties( ref propertyCount, result.AsPointer() );

		return result
			.Select( p => ((CString)p.LayerName).ToString() )
			.ToArray();
	}

	public static void Shutdown()
	{
		//vkShutdown();
	}
}
