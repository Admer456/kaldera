// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using System.Numerics;
using System.Runtime.InteropServices;
using ExampleBase;
using Kaldera;
using Kaldera.Abstractions;
using Kaldera.Abstractions.Buffers;
using Kaldera.Abstractions.Extensions;
using Kaldera.Abstractions.Memory;
using Kaldera.Abstractions.RenderTargets;
using Kaldera.Abstractions.Utilities;
using Kaldera.Extensions;
using Kaldera.Interfaces;
using Kaldera.Objects;
using Silk.NET.Vulkan;
using Result = Kaldera.Result;

ExampleStartup.Run( "Kaldera Example - Hello buffer!", 1600, 900, new ExampleHelloBuffer(), args );

/// <summary>
/// Renders a hexagon using a vertex buffer and an index buffer.
/// This showcases how to upload meshes.
/// </summary>
internal class ExampleHelloBuffer : IExample
{
	private IndexBuffer mIndexBuffer = null!;
	private VertexBuffer<VertexData> mVertexBuffer = null!;
	private VertexShaderSet mShaderSet = null!;
	private KaGraphicsPipeline mPipeline = null!;
	private KaCommandBuffer mCommands = null!;
	private KaQueue mQueue = null!;

	// Describes vertex data. Currently this is very simple, but you can extend it with quite a bit of data,
	// like UVs, normals, tangents, vertex colours...
	[StructLayout( LayoutKind.Sequential )]
	private struct VertexData : IVertexData
	{
		public Vector3 Position;
		//public Vector2 UV;
		//public Vector4Byte Normal;
		//public Vector4Byte Tangent;
		//...

		public static int SizeInBytes => VertexInputLayout.Stride;

		public static VertexAttribute[] VertexAttributes => VertexInputLayout.Elements;

		public static readonly VertexInputLayout VertexInputLayout = new()
		{
			Stride = 12, // sizeof(Vector3)
			Elements = [ new() { Offset = 0, Format = Format.R32G32B32Sfloat } ] // one vertex = float[3]
		};
	}

	public Result Init( KaInstance instance, KaDevice device, KaQueue queue, IResourceAllocator allocator )
	{
		mCommands = KaCommandBuffer.CreatePrimary( queue ).Checked();

		mShaderSet = Utilities.LoadGraphicsShaderSet( device, "hello_buffer.spv" ).Checked();
		mPipeline = KaGraphicsPipeline.Create( device, new()
		{
			// There's no shader inputs, so no need for a layout
			ResourceLayout = null,
			// This describes vertex buffer inputs to the shader.
			// This is an array, in case you are binding 2 or more vertex buffers when drawing
			VertexInputs = [VertexData.VertexInputLayout],
			ShaderSet = mShaderSet,
			DynamicStates = [DynamicState.Viewport, DynamicState.Scissor],
			// In this instance, since we're using an index buffer, we'll use a triangle list
			Topology = PrimitiveTopology.TriangleList,
			Color = new() { Attachments = [(BlendAttachments.Opaque, Format.B8G8R8A8Unorm)] },
			DepthStencil = null,
			Rasterizer = new()
			{
				PolygonMode = PolygonMode.Fill,
				CullMode = CullModeFlags.BackBit,
				FrontFace = FrontFace.Clockwise
			}
		} ).Checked();

		// Vertex & index buffer uploading...
		VertexData[] data =
		{
			new() { Position = new( 0.2f, 0.5f, 0.0f ) },
			new() { Position = new( 0.36f, 0.0f, 0.0f ) },
			new() { Position = new( 0.2f, -0.5f, 0.0f ) },
			new() { Position = new( -0.2f, -0.5f, 0.0f ) },
			new() { Position = new( -0.36f, 0.0f, 0.0f ) },
			new() { Position = new( -0.2f, 0.5f, 0.0f ) }
		};

		// An index buffer basically says "vertices at elements X, Y and Z make one triangle".
		// This lets us reuse the same vertices from a vertex buffer. Otherwise, you'd be duplicating the vertices...
		uint[] indices =
		[
			0, 1, 2,
			0, 2, 3,
			0, 3, 5,
			3, 4, 5
		];

		// Traditionally in Vulkan, you'd do a whole ritual to upload data to a buffer.
		// Here, you can just use a nice buffer builder :3c
		UploadHelper builder = UploadHelper.Create( allocator, 16 * 1024 * 1024 );

		// This part prepares the data to be uploaded and creates buffer objects.
		// However, don't be fooled, this still needs to be *actually* uploaded to the GPU!
		mVertexBuffer = builder.CommitVertexBuffer( data.AsSpan() ).Checked();
		mIndexBuffer = builder.CommitIndexBuffer( indices.AsSpan() ).Checked();

		// This creates a staging buffer (CPU-visible video memory), writes everything to it,
		// and then copies everything to their respective GPU-local buffers.
		// In other words, actually uploads the stuff to the GPU
		builder.Upload();

		// Wait for the uploading to finish before we dispose of the builder...
		device.WaitIdle();
		// We can dispose of the builder right now. The vertex & index buffer
		// will be disposed later!
		builder.Dispose();

		mQueue = queue;

		return Result.Success();
	}

	public Result OnFrame( float dt, SwapchainRenderTarget windowRt )
	{
		mCommands.Begin();
		mCommands.RenderPass( windowRt, () =>
		{
			mCommands.ClearColour( windowRt, 0, new( 0.0f, 0.13f, 0.13f, 1.0f ) );
			mCommands.BindPipeline( mPipeline );
			mCommands.SetViewport( 0, windowRt.Extent, 0.0f, 1.0f );
			mCommands.SetScissor( 0, windowRt.Extent );

			// The geometry lies somewhere in video memory. We must bind it before drawing!
			mCommands.BindVertexBuffer( mVertexBuffer, 0 );
			mCommands.BindIndexBuffer( mIndexBuffer );

			// This time, instead of Draw, we use DrawIndexed. Still one instance, but now 12 indices!
			// 12 indices translates to 4 triangles, which does fit a hexagon.
			mCommands.DrawIndexed( indexCount: 12, instanceCount: 1 );
		} );
		mCommands.End();
		mQueue.Submit( mCommands, windowRt );
		return Result.Success();
	}

	public void Dispose()
	{
		mVertexBuffer.Dispose();
		mIndexBuffer.Dispose();
		mPipeline.Layout.Dispose();
		mPipeline.Dispose();
		mShaderSet.Vertex.Dispose();
		mCommands.Dispose();
	}
}
