// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using System.Runtime.InteropServices;

namespace Kaldera.Objects;

public readonly unsafe struct KaSemaphore : IDisposable
{
	public required KaDevice Device { get; init; }
	public required VkSemaphore VkSemaphore { get; init; }

	public static Result<KaSemaphore> Create( KaDevice device, bool exportable = false )
	{
		ExportSemaphoreCreateInfo exportInfo = new()
		{
			SType = StructureType.ExportSemaphoreCreateInfo,
			HandleTypes = RuntimeInformation.IsOSPlatform( OSPlatform.Windows )
				? ExternalSemaphoreHandleTypeFlags.OpaqueWin32Bit
				: ExternalSemaphoreHandleTypeFlags.OpaqueFDBit
		};

		SemaphoreCreateInfo semaphoreInfo = new()
		{
			SType = StructureType.SemaphoreCreateInfo,
			Flags = SemaphoreCreateFlags.None,
			PNext = exportable ? &exportInfo : null
		};

		VkResult code = Vulkan.Vk.CreateSemaphore( device.VkDevice, &semaphoreInfo, null, out var semaphore );
		if ( code is not VkResult.Success )
		{
			return new Error( $"KaSemaphore.Create: Failed to create semaphore: {code}" );
		}

		return new KaSemaphore
		{
			Device = device,
			VkSemaphore = semaphore
		};
	}

	public Result<IntPtr> Export()
		=> Environment.OSVersion.Platform switch
		{
			PlatformID.Unix => ExportLinuxFdHandle(),
			PlatformID.Win32NT => ExportWin32Handle(),
			_ => new Error( "Platform not supported" )
		};

	public Result<IntPtr> ExportWin32Handle()
	{
		SemaphoreGetWin32HandleInfoKHR getHandleInfo = new()
		{
			SType = StructureType.SemaphoreGetWin32HandleInfoKhr,
			Semaphore = VkSemaphore,
			HandleType = ExternalSemaphoreHandleTypeFlags.OpaqueWin32Bit
		};

		VkResult code = Vulkan.ExternalSemaphoreWin32.GetSemaphoreWin32Handle( Device.VkDevice, &getHandleInfo, out IntPtr handle );
		if ( code is not VkResult.Success )
		{
			return new Error( $"KaSemaphore.ExportWin32Handle: Vulkan error: {code}" );
		}

		return handle;
	}

	public Result<IntPtr> ExportLinuxFdHandle()
	{
		SemaphoreGetFdInfoKHR getHandleInfo = new()
		{
			SType = StructureType.SemaphoreGetFDInfoKhr,
			Semaphore = VkSemaphore,
			HandleType = ExternalSemaphoreHandleTypeFlags.OpaqueFDBit
		};

		VkResult code = Vulkan.ExternalSemaphoreFd.GetSemaphoreF( Device.VkDevice, &getHandleInfo, out int handle );
		if ( code is not VkResult.Success )
		{
			return new Error( $"KaSemaphore.ExportLinuxFdHandle: Vulkan error: {code}" );
		}

		return handle;
	}

	public void Dispose()
	{
		Vulkan.Vk.DestroySemaphore( Device.VkDevice, VkSemaphore, null );
	}
}
