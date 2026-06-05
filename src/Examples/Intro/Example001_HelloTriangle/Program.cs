// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using System.Numerics;
using System.Runtime.InteropServices;
using ExampleBase;
using Kaldera;
using Kaldera.Abstractions;
using Kaldera.Abstractions.Extensions;
using Kaldera.Abstractions.Interfaces;
using Kaldera.Abstractions.Memory;
using Kaldera.Abstractions.RenderTargets;
using Kaldera.Extensions;
using Kaldera.Interfaces;
using Kaldera.Objects;
using Silk.NET.Vulkan;
using Result = Kaldera.Result;

ExampleStartup.Run( "Kaldera Example - Hello triangle!", 1600, 900, new ExampleHelloTriangle(), args );

/// <summary>
/// Renders a triangle in the dumbest, simplest way: encoding the triangle into a push constant.
/// It practically emulates OpenGL 1.0's immediate mode rendering.
///
/// You may use this example to see how to load shaders and set up a pipeline more than anything else.
/// </summary>
internal class ExampleHelloTriangle : IExample
{
	private VertexShaderSet mShaderSet = null!;
	private KaLayout mPipelineLayout = null!;
	private KaGraphicsPipeline mPipeline = null!;
	private KaCommandBuffer mCommands = null!;
	private KaQueue mQueue = null!;

	// This will be sent as a push constant. Push constants are a really quick'n'easy way to send data to a shader.
	// Since Vulkan 1.4, they're defined as 256-byte memory banks, so they're ideal for passing a
	// quick little matrix or, in our case... a triangle. =w=
	[StructLayout( LayoutKind.Sequential )]
	private struct TrianglePushConstant
	{
		// We're using vec4 here for padding
		public Vector4 PositionA;
		public Vector4 PositionB;
		public Vector4 PositionC;
	}

	public Result Init( KaInstance instance, KaDevice device, KaQueue queue, IResourceAllocator allocator )
	{
		mCommands = KaCommandBuffer.CreatePrimary( queue ).Checked();

		// Shader loading. Here is a simple preview of what it's like to work with error checking "manually".
		// The rest of the examples will use .Checked() for brevity
		Result<KaShader> shaderFile = Utilities.LoadShader( device, "hello_triangle.spv" );
		if ( !shaderFile.Get( out var error, out var shader ) )
		{
			return error.Prepend( "Could not load shader" );
		}

		// A "shader set" is a Kaldera concept that helps us be more correct when creating pipelines.
		// A graphics pipeline will only accept vertex-pixel shaders and such. A compute pipeline
		// will only accept compute shaders. You get the idea.
		mShaderSet = new( shader, "VertexMain", shader, "PixelMain" );

		// A layout is a description of shader inputs, basically.
		// We're not using descriptor sets here, only push constants.
		mPipelineLayout = KaLayout.Create( device, new()
		{
			Sets = [],
			// The pipeline needs to know ahead of time how large the push constant will be, or rather,
			// what range of the 256-byte bank it may occupy. Very important stuff
			PushConstantRanges = [ new( ShaderStageFlags.VertexBit, offset: 0, size: 48 ) ]
		} ).Checked();

		mPipeline = KaGraphicsPipeline.Create( device, new()
		{
			ResourceLayout = mPipelineLayout,
			// No vertex buffers here, for now. That's in the next example
			VertexInputs = null,
			// Shaders for this pipeline
			ShaderSet = mShaderSet,
			// So we can dynamically set viewport and scissor size
			DynamicStates = [DynamicState.Viewport, DynamicState.Scissor],
			// There's point lists, line lists, triangle fans...
			Topology = PrimitiveTopology.TriangleFan,
			// Colour outputs
			Color = new() { Attachments = [(BlendAttachments.Opaque, Format.B8G8R8A8Unorm)] },
			// No depth buffer
			DepthStencil = null,
			Rasterizer = new()
			{
				PolygonMode = PolygonMode.Fill,
				CullMode = CullModeFlags.BackBit,
				FrontFace = FrontFace.Clockwise
			}
		} ).Checked();

		mQueue = queue;
		return Result.Success();
	}

	public Result OnFrame( float dt, SwapchainRenderTarget windowRt )
	{
		mCommands.Begin();
		mCommands.RenderPass( windowRt, () =>
		{
			mCommands.ClearColour( windowRt, 0, new( 0.0f, 0.13f, 0.13f, 1.0f ) );

			// Before drawing, we must bind the pipeline. In other words, "set the shader" and such
			mCommands.BindPipeline( mPipeline );
			// Before drawing, we must also set the dynamic state.
			// Viewport and scissor must match the size of the render target
			mCommands.SetViewport( 0, windowRt.Extent, 0.0f, 1.0f );
			mCommands.SetScissor( 0, windowRt.Extent );

			// This triangle data is pushed straight to the shader
			TrianglePushConstant triangle = new()
			{
				PositionA = new( 0.0f, 0.5f, 0.0f, 1.0f ),
				PositionB = new( 0.5f, -0.5f, 0.0f, 1.0f ),
				PositionC = new( -0.5f, -0.5f, 0.0f, 1.0f ),
			};
			mCommands.PushConstant( triangle, ShaderStageFlags.VertexBit );

			// And then, we draw. Vertex count is quite obvious. Instance count is how many times we
			// want to draw this triangle. Since we're not doing instanced rendering yet, set that to 1
			mCommands.Draw( vertexCount: 3, instanceCount: 1 );
		} );
		mCommands.End();
		mQueue.Submit( mCommands, windowRt );
		return Result.Success();
	}

	public void Dispose()
	{
		mPipelineLayout.Dispose();
		mPipeline.Dispose();
		mShaderSet.Vertex.Dispose();
		mCommands.Dispose();
	}
}
