// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Abstractions.Buffers;
using Kaldera.Abstractions.Interfaces;
using Kaldera.Objects;

namespace Kaldera.Abstractions.Extensions;

public static unsafe class BufferCommandExtensions
{
	internal static bool CheckPipelineBound( this KaCommandBuffer self, string method, string errorMessage )
	{
		if ( self.CurrentPipeline is null )
		{
			KaInstance.DebugLog(
				DebugReportFlagsEXT.ErrorBitExt,
				DebugReportObjectTypeEXT.CommandBufferExt,
				message: $"KaCommandBuffer.{method}: {errorMessage}"
			);
			return false;
		}

		return true;
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public static void CopyBuffer<T>( this KaCommandBuffer self, in StagingBuffer source, in T destination, ulong size, ulong sourceOffset = 0,
		ulong destinationOffset = 0 )
		where T : IGpuBuffer
		=> self.CopyBuffer( source.BufferRange, destination.BufferRange, size, sourceOffset, destinationOffset );

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public static void CopyBuffer<T>( this KaCommandBuffer self, in StagingBuffer source, in T destination )
		where T : IGpuBuffer
		=> self.CopyBuffer( source.BufferRange, destination.BufferRange, source.BufferRange.Length, 0, 0 );

	public static void PushUniformBuffer<T>( this KaCommandBuffer self, in T buffer, int set, int binding = 0 )
		where T : IGpuBuffer
	{
		if ( !self.CheckPipelineBound( "PushUniformBuffer", "Tried pushing a uniform buffer without a bound pipeline" ) )
		{
			return;
		}

		self.PushBuffer( buffer.BufferRange, set, binding, DescriptorType.UniformBuffer );
	}

	public static void PushStorageBuffer<T>( this KaCommandBuffer self, in T buffer, int set, int binding = 0 )
		where T : IGpuBuffer
	{
		if ( !self.CheckPipelineBound( "PushStorageBuffer", "Tried pushing a storage buffer without a bound pipeline" ) )
		{
			return;
		}

		self.PushBuffer( buffer.BufferRange, set, binding, DescriptorType.StorageBuffer );
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public static void UpdateBuffer<TBuffer, TData>( this KaCommandBuffer self, in TBuffer buffer, TData data, int offset = 0 )
		where TBuffer : IGpuBuffer
		where TData : unmanaged
		=> Vulkan.Vk.CmdUpdateBuffer( self.VkCmdBuf, buffer.BufferRange.Buffer, (ulong)offset, new Span<TData>( &data, 1 ) );

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public static void UpdateBuffer<TBuffer, TData>( this KaCommandBuffer self, in TBuffer buffer, Span<TData> data, int offset = 0 )
		where TBuffer : IGpuBuffer
		where TData : unmanaged
		=> Vulkan.Vk.CmdUpdateBuffer( self.VkCmdBuf, buffer.BufferRange.Buffer, (ulong)offset, data );

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public static void BindVertexBuffer<T>( this KaCommandBuffer self, in VertexBuffer<T> vertexBuffer, int binding )
		where T : unmanaged, IVertexData
	{
		VkBuffer buffer = vertexBuffer.BufferRange.Buffer;
		ulong offset = vertexBuffer.BufferRange.Start;
		Vulkan.Vk.CmdBindVertexBuffers( self.VkCmdBuf, (uint)binding, 1, &buffer, &offset );
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public static void BindVertexBuffer<T>( this KaCommandBuffer self, in BufferSlice<VertexBuffer<T>> vertexBuffer, int binding )
		where T : unmanaged, IVertexData
	{
		VkBuffer buffer = vertexBuffer.Buffer.BufferRange.Buffer;
		ulong offset = vertexBuffer.Start;
		Vulkan.Vk.CmdBindVertexBuffers( self.VkCmdBuf, (uint)binding, 1, &buffer, &offset );
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public static void BindIndexBuffer( this KaCommandBuffer self, in IndexBuffer indexBuffer )
	{
		Vulkan.Vk.CmdBindIndexBuffer(
			self.VkCmdBuf,
			indexBuffer.BufferRange.Buffer.VkBuffer,
			indexBuffer.BufferRange.Start,
			IndexType.Uint32
		);
	}
}
