// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

namespace Kaldera.Abstractions.Utilities;

/// <summary>
/// Elevates a value type to a reference type.
/// </summary>
public class Box<T>( in T value )
	where T : struct
{
	private T mValue = value;

	public ref T Value => ref mValue;
	public static implicit operator Box<T>( T value ) => new( value );
}

public static class BoxExtensions
{
	public static void Dispose<T>( this Box<T> self )
		where T : struct, IDisposable
		=> self.Value.Dispose();
}
