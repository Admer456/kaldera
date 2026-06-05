// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using System.Text;

namespace Kaldera.Extensions;

/// <summary>
/// Short-lived C (null-terminated) string.
/// </summary>
public unsafe struct CString
{
	public required byte[] Bytes;

	public static implicit operator CString( byte* ptr )
		=> new()
		{
			Bytes = UnsafeStringExtensions
				.CStringToString( ptr )
				.ToCString()
		};

	public static implicit operator CString( string str )
		=> new()
		{
			Bytes = str.ToCString()
		};

	public override string ToString()
		=> ((ReadOnlySpan<byte>)Bytes)
			.TrimNull()
			.ToActualString();

	public byte* Pointer => Bytes.AsPointer();
}

/// <summary>
/// Short-lived array of C (null-terminated) strings.
/// </summary>
public unsafe struct CStringArray
{
	public byte[][] Bytes; // Contains the actual string data
	public IntPtr[] StringPointers; // Contains a C-style 2D array structure

	private CStringArray( string[] strings )
	{
		Bytes = strings
			.Select( s => s.ToCString() )
			.ToArray();

		StringPointers = Bytes.Select( str => (IntPtr)str.AsPointer() ).ToArray();
	}

	public static implicit operator CStringArray( string[] strings )
		=> new( strings );

	public void Decompose( out byte** ptr, out uint count )
	{
		ptr = Pointer;
		count = Count;
	}

	public uint Count => (uint)Bytes.Length;
	public byte** Pointer => Count > 0 ? (byte**)Unsafe.AsPointer( ref StringPointers[0] ) : null;
}

public static unsafe class UnsafeStringExtensions
{
	public static string ToActualString( this ReadOnlySpan<byte> span )
		=> Encoding.UTF8.GetString( span );

	public static T* AsPointer<T>( this T[] self )
		where T : unmanaged
		=> (T*)Unsafe.AsPointer( ref self[0] );

	public static T* PointerAt<T>( this T[] self, int index )
		where T : unmanaged
		=> (T*)Unsafe.AsPointer( ref self[index] );

	public static byte[] ToCString( this string self )
		=> Encoding.UTF8.GetBytes( self + "\0" );

	public static int CStringLength( byte* bytes )
	{
		for ( int i = 0; i < 1024; i++ )
		{
			if ( bytes[i] == '\0' )
			{
				return i;
			}
		}

		return -1;
	}

	public static ReadOnlySpan<byte> TrimNull( this ReadOnlySpan<byte> bytes )
	{
		for ( int i = 0; i < bytes.Length; i++ )
		{
			if ( bytes[i] == '\0' )
			{
				return bytes.Slice( 0, i );
			}
		}

		return bytes;
	}

	public static string CStringToString( byte* bytes, int maxLength = -1 )
	{
		if ( maxLength == -1 )
		{
			maxLength = CStringLength( bytes );
		}

		ReadOnlySpan<byte> span = new( bytes, maxLength );
		return Encoding.ASCII.GetString( span.TrimNull() );
	}
}
