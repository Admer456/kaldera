// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using System.Diagnostics;
using Kaldera;

namespace ExampleBase;

public static class ResultExtensions
{
	public static bool Check( this Result self )
	{
		if ( !self.Get( out var error ) )
		{
			error.PrintError();
			Debugger.Break();

			return false;
		}

		return true;
	}

	public static bool Check<T>( this Result<T> self )
	{
		if ( !self.Get( out var error, out _ ) )
		{
			error.PrintError();
			Debugger.Break();

			return false;
		}

		return true;
	}

	public static void PrintError( this Error self )
	{
		Error? currentError = self;
		int level = 0;
		while ( currentError is not null )
		{
			for ( int i = 0; i < level; i++ )
			{
				Console.Write( "  " );
			}
			Console.WriteLine( currentError.Message );

			currentError = currentError.Inner;
			level++;
		}
	}

	public static T Checked<T>( this Result<T> self )
	{
		if ( !self.Get( out var error, out var value ) )
		{
			error.PrintError();
			Debugger.Break();

#pragma warning disable CS8603
			return default;
#pragma warning restore CS8603
		}

		return value;
	}
}
