// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using System.Numerics;
using ExampleBase;
using Kaldera;
using Kaldera.Abstractions.Extensions;
using Kaldera.Abstractions.RenderTargets;
using Kaldera.Interfaces;
using Kaldera.Objects;

ExampleStartup.Run( "Kaldera Example - Hello screen!", 1600, 900, new ExampleHelloScreen(), args );

/// <summary>
/// Fills the screen with a solid colour.
/// </summary>
internal class ExampleHelloScreen : IExample
{
	private static Vector4 ScreenColour => new( 0.0f, 0.13f, 0.13f, 1.0f );

	private KaCommandBuffer mCommands = null!;
	private KaQueue mQueue = null!;

	public Result Init( KaInstance instance, KaDevice device, KaQueue queue, IResourceAllocator allocator )
	{
		// Throughout the examples, you will see this pattern:
		//
		// var something = Something.Create( options ).Checked();
		//
		// There is a whole Result<T>/Error type construct going on here.
		// Throughout the codebase tho', you'll see this pattern:
		//
		// if ( Something.Create( options ).Get( out var error, out var value ) )
		// {
		//   return error.Prepend( "Failed to do XYZ" );
		// }
		// return value;
		//
		// And quite indeed, in your own engine/application, you may implement
		// your own Checked() for convenience.
		mCommands = KaCommandBuffer.CreatePrimary( queue ).Checked(); 
		mQueue = queue;

		return Result.Success();
	}

	public Result OnFrame( float dt, SwapchainRenderTarget windowRt )
	{
		// Quite simply, start recording drawing commands
		mCommands.Begin();
		// Instruct the GPU to begin rendering into an image. In this case,
		// the framebuffer associated with this app's SDL window
		mCommands.RenderPass( windowRt, () =>
		{
			// Attachment 0 is the colour attachment
			mCommands.ClearColour( windowRt, 0, ScreenColour );
		} );
		mCommands.End();

		// The command buffer is submitted to the queue. This is done in conjunction
		// with the render target, so that synchronisation can take place.
		mQueue.Submit( mCommands, windowRt );
		return Result.Success();
	}

	public void Dispose()
	{
		mCommands.Dispose();
	}
}
