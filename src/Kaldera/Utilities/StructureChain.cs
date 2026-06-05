// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using System.Runtime.InteropServices;

namespace Kaldera.Utilities;

// TODO: Check out Silk.NET's chain :)
//  This was written when I initially used some other bindings

/// <summary>
/// Implements a linked list of Vulkan structures.
/// </summary>
public unsafe class StructureChain : IDisposable
{
	public List<IBlob> Blobs { get; } = new();

	public static StructureChain Begin<T>( T structure )
		where T : unmanaged, IStructuredType, IChainable, IChainStart
		=> new StructureChain().Add( structure );

	public StructureChain Add<T>( T structure )
		where T : unmanaged, IStructuredType, IChainable
	{
		structure.StructureType();
		Blob<T> blob = new( ref structure );

		if ( Blobs.Count != 0 )
		{
			Blobs.Last().ConnectTo( ref blob );
		}

		Blobs.Add( blob );
		return this;
	}

	public void* Head => Blobs.Count == 0 ? null : Blobs.First().This;

	public interface IBlob
	{
		BaseInStructure* This { get; }
		BaseInStructure* Next { get; set; }
		void ConnectTo<T>( ref T other )
			where T : IBlob;
	}

	// Keeps a living memory blob until it's no longer needed
	public class Blob<T> : IBlob
		where T : unmanaged, IStructuredType, IChainable
	{
		public readonly IntPtr StructurePtr;
		public ref T Structure => ref Unsafe.AsRef<T>( (void*)StructurePtr );

		public Blob( ref T structure )
		{
			StructurePtr = Marshal.AllocHGlobal( Unsafe.SizeOf<T>() );

			Unsafe.Copy( (T*)StructurePtr, ref structure );
		}

		~Blob()
		{
			Marshal.FreeHGlobal( StructurePtr );
		}

		public BaseInStructure* This => (BaseInStructure*)StructurePtr;

		public BaseInStructure* Next
		{
			get => Structure.PNext;
			set => Structure.PNext = value;
		}

		public void ConnectTo<TBlob>( ref TBlob other )
			where TBlob : IBlob
		{
			Next = other.This;
		}
	}

	public void Dispose()
	{
		Blobs.Clear();
	}
}
