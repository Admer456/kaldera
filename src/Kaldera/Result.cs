// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using System.Diagnostics.CodeAnalysis;

namespace Kaldera;

public record Error( string Message, Error? Inner = null )
{
	public static Error NotImplemented => new Error( "Not implemented" );

	public Error Prepend( string message )
		=> new( message, this );
}

public readonly struct Result<T>
{
	private readonly T? mValue;
	private readonly Error? mError;

	private Result( T? value, Error? error )
	{
		mValue = value;
		mError = error;
	}

	public Result( T value )
	{
		mValue = value;
		mError = null;
	}

	public bool Get( [NotNullWhen( false )] out Error? error, [NotNullWhen( true )] out T? value )
	{
		if ( mError is not null )
		{
			error = mError;
			value = default;
			return false;
		}

		error = null;
		value = mValue!;
		return true;
	}

	public static implicit operator Result<T>( T value ) => new( value, null );
	public static implicit operator Result<T>( Error value ) => new( default, value );
	public static implicit operator T( Result<T> value ) => value.mValue!;
}

public readonly struct Result
{
	private readonly Error? mError;

	private Result( Error? error )
	{
		mError = error;
	}

	public bool Get( [NotNullWhen( false )] out Error? error )
	{
		if ( mError is not null )
		{
			error = mError;
			return false;
		}

		error = null;
		return true;
	}

	public static Result Success() => new( null );
	public static implicit operator Result( Error value ) => new( value );
}

