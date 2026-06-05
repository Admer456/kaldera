// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using System.Diagnostics;
using System.Runtime.InteropServices;
using Kaldera.Extensions;
using Kaldera.Names;
using Silk.NET.Core;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Kaldera.Objects;

public enum VulkanDebugLogLevel
{
	None,
	Error,
	PerformanceWarning,
	Warning,
	Information,
	Debug
}

public sealed class InstanceOptions
{
	public VulkanDebugLogLevel LogLevel { get; set; } = VulkanDebugLogLevel.None;
	public required string ApplicationName { get; set; }
	public required string EngineName { get; set; }

	public required Version32 ApiVersion { get; set; }
	public Version32 ApplicationVersion { get; set; } = new( 1, 0, 0 );
	public Version32 EngineVersion { get; set; } = new( 1, 0, 0 );

	public string[] EnabledInstanceExtensions { get; set; } = [];
	public string[] EnabledLayers { get; set; } = [];
}

public sealed unsafe class KaInstance : IDisposable
{
	public delegate void DebugReport( DebugReportFlagsEXT flags, DebugReportObjectTypeEXT objectType, string message );

	public required VkInstance VkInstance { get; init; }
	public DebugReportCallbackEXT? DebugReportHandle { get; init; }

	public static Result<KaInstance> Create( InstanceOptions options )
	{
		byte[] applicationNameUtf8 = options.ApplicationName.ToCString();
		byte[] engineNameUtf8 = options.EngineName.ToCString();

		ApplicationInfo appInfo = new()
		{
			SType = StructureType.ApplicationInfo,
			PApplicationName = applicationNameUtf8.AsPointer(),
			PEngineName = engineNameUtf8.AsPointer(),
			ApiVersion = options.ApiVersion,
			ApplicationVersion = options.ApplicationVersion,
			EngineVersion = options.EngineVersion
		};

		CStringArray enabledLayers = options.EnabledLayers;
		CStringArray enabledExtensions = options.EnabledInstanceExtensions
			.Concat( options.LogLevel != VulkanDebugLogLevel.None ? [InstanceExtensionNames.ExtDebugReport] : [] )
			.ToArray();

		InstanceCreateInfo instanceInfo = new( pApplicationInfo: &appInfo );

		enabledExtensions.Decompose(
			out instanceInfo.PpEnabledExtensionNames,
			out instanceInfo.EnabledExtensionCount );
		enabledLayers.Decompose(
			out instanceInfo.PpEnabledLayerNames,
			out instanceInfo.EnabledLayerCount );

		VkResult errorCode = Vulkan.Vk.CreateInstance( ref instanceInfo, null, out VkInstance instance );
		if ( errorCode is not VkResult.Success )
		{
			return new Error( $"KaInstance.Create: Couldn't create Vulkan instance: {errorCode}" );
		}

		DebugReportCallbackEXT debugReportCallbackHandle = new( 0 );
		if ( options.LogLevel != VulkanDebugLogLevel.None )
		{
			Vulkan.Vk.TryGetInstanceExtension( instance, out ExtDebugReport extDebugReport );
			Vulkan.DebugReport = extDebugReport;

			DebugReportCallbackCreateInfoEXT debugReportInfo = new()
			{
				SType = StructureType.DebugReportCallbackCreateInfoExt,
				PfnCallback = new( &InternalDebugCallback ),
				Flags = DebugReportFlagsEXT.ErrorBitExt
			};

			if ( options.LogLevel >= VulkanDebugLogLevel.PerformanceWarning )
			{
				debugReportInfo.Flags |= DebugReportFlagsEXT.PerformanceWarningBitExt;
			}

			if ( options.LogLevel >= VulkanDebugLogLevel.Warning )
			{
				debugReportInfo.Flags |= DebugReportFlagsEXT.WarningBitExt;
			}

			if ( options.LogLevel >= VulkanDebugLogLevel.Information )
			{
				debugReportInfo.Flags |= DebugReportFlagsEXT.InformationBitExt;
			}

			if ( options.LogLevel >= VulkanDebugLogLevel.Debug )
			{
				debugReportInfo.Flags |= DebugReportFlagsEXT.DebugBitExt;
			}

			errorCode = Vulkan.DebugReport.CreateDebugReportCallback( instance, &debugReportInfo, null, &debugReportCallbackHandle );
			if ( errorCode is not VkResult.Success )
			{
				return new Error( $"KaInstance.Create: Could not create debug report callback: {errorCode}" );
			}
		}

		// Automatically load device & instance extensions
		foreach ( var extension in options.EnabledInstanceExtensions )
		{
			switch ( extension )
			{
				case KhrSurface.ExtensionName:
					Vulkan.Vk.TryGetInstanceExtension( instance, out KhrSurface surface );
					Vulkan.Surface = surface;
					break;
			}
		}

		return new KaInstance
		{
			VkInstance = instance,
			DebugReportHandle = debugReportCallbackHandle.Handle is 0 ? null : debugReportCallbackHandle
		};
	}

	public KaPhysicalDevice[] EnumeratePhysicalDevices()
	{
		uint count = 0;
		Vulkan.Vk.EnumeratePhysicalDevices( VkInstance, ref count, null );
		VkPhysicalDevice[] items = new VkPhysicalDevice[count];
		Vulkan.Vk.EnumeratePhysicalDevices( VkInstance, ref count, items.AsPointer() );

		return items
			.Select( pd => new KaPhysicalDevice { Instance = this, VkPhysicalDevice = pd } )
			.ToArray();
	}

	public static event DebugReport? OnDebugLog;

	[Conditional( "DEBUG" )]
	public static void DebugLog(
		DebugReportFlagsEXT flags,
		DebugReportObjectTypeEXT objectType,
		string message )
	{
		OnDebugLog?.Invoke( flags, objectType, message );
	}

	[UnmanagedCallersOnly( CallConvs = [typeof( CallConvCdecl )] )]
	private static Bool32 InternalDebugCallback(
		DebugReportFlagsEXT flags,
		DebugReportObjectTypeEXT objectType,
		ulong @object,
		nuint location,
		int messageCode,
		byte* pLayerPrefix,
		byte* pMessage,
		void* pUserData )
	{
		CString message = pMessage;
		OnDebugLog?.Invoke( flags, objectType, message.ToString() );
		return 0;
	}

	public void Dispose()
	{
		if ( DebugReportHandle is not null )
		{
			Vulkan.DebugReport.DestroyDebugReportCallback( VkInstance, DebugReportHandle.Value, null );
		}

		Vulkan.Vk.DestroyInstance( VkInstance, null );
	}
}
