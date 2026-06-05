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

ExampleStartup.Run( "Kaldera Example - Hello 3D!", 1600, 900, new Example3D(), args );

/// <summary>
/// Renders a couple of quads with 3D projection.
/// </summary>
internal class Example3D : IExample
{
	private IndexBuffer mIndexBuffer = null!;
	private StorageBuffer mUniformBuffer = null!;
	private VertexBuffer<VertexData> mVertexBuffer = null!;
	private VertexShaderSet mShaderSet = null!;
	private KaLayout mPipelineLayout = null!;
	private KaGraphicsPipeline mPipeline = null!;
	private KaCommandBuffer mCommands = null!;
	private KaQueue mQueue = null!;

	[StructLayout( LayoutKind.Sequential )]
	private struct VertexData : IVertexData
	{
		public Vector3 Position;
		public Vector3 Normal;

		public static int SizeInBytes => VertexInputLayout.Stride;

		public static VertexAttribute[] VertexAttributes => VertexInputLayout.Elements;

		public static readonly VertexInputLayout VertexInputLayout = new()
		{
			Stride = 24, // sizeof(vec3) + sizeof(vec3)
			Elements =
			[
				new() { Offset = 0, Format = Format.R32G32B32Sfloat },
				// Normal is 12 bytes away from Position
				new() { Offset = 12, Format = Format.R32G32B32Sfloat }
			]
		};
	}

	// Camera data is typically a struct. It's a good idea to combine the view and projection matrix into one, as is
	// done by this CameraData.Create utility. Note that this needs to be 4-byte-aligned just like everything else...
	private struct CameraData
	{
		public Matrix4x4 ViewProjection;

		public static CameraData Create( Matrix4x4 projection, Matrix4x4 view )
			=> new() { ViewProjection = Matrix4x4.Multiply( view, projection ) };
	}

	public Result Init( KaInstance instance, KaDevice device, KaQueue queue, IResourceAllocator allocator )
	{
		mCommands = KaCommandBuffer.CreatePrimary( queue ).Checked();

		mShaderSet = Utilities.LoadGraphicsShaderSet( device, "hello_3d.spv" ).Checked();

		LayoutOptions layoutOptions = LayoutBuilder.Begin()
			.StructuredSet( s => s.UniformBuffer() ) // Set #0 binding #0
			.Build();

		mPipelineLayout = KaLayout.Create( device, layoutOptions );
		mPipeline = KaGraphicsPipeline.Create( device, new()
		{
			ResourceLayout = mPipelineLayout,
			VertexInputs = [VertexData.VertexInputLayout],
			ShaderSet = mShaderSet,
			DynamicStates = [DynamicState.Viewport, DynamicState.Scissor],
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

		// Just two quads for now
		VertexData[] data =
		[
			new() { Position = new( 1.0f, 1.0f, 1.0f ), Normal = Vector3.UnitZ },
			new() { Position = new( 1.0f, -1.0f, 1.0f ), Normal = Vector3.UnitZ },
			new() { Position = new( -1.0f, -1.0f, 1.0f ), Normal = Vector3.UnitZ },
			new() { Position = new( -1.0f, 1.0f, 1.0f ), Normal = Vector3.UnitZ },
			// Even though 4 vertices share positions here, they have different normals! So they are separate vertices
			new() { Position = new( -1.0f, -1.0f, 1.0f ), Normal = -Vector3.UnitX },
			new() { Position = new( -1.0f, -1.0f, -1.0f ), Normal = -Vector3.UnitX },
			new() { Position = new( -1.0f, 1.0f, -1.0f ), Normal = -Vector3.UnitX },
			new() { Position = new( -1.0f, 1.0f, 1.0f ), Normal = -Vector3.UnitX }
		];

		// Camera initialisation
		CameraData camera = CameraData.Create(
			// 90 degrees FOV, 16:9 aspect ratio, 0.01 to 100 depth range
			projection: Matrix4x4.CreatePerspectiveFieldOfView( MathF.PI * 0.25f, 1600.0f / 900.0f, 0.01f, 100.0f ),
			view: Matrix4x4.CreateLookTo(
				cameraPosition: new( -4.0f, -4.0f, 2.0f ),
				cameraDirection: Vector3.Normalize( new( 1.0f, 1.0f, -0.4f ) ),
				// NORMALLY you would calculate this from Euler angles (pitch yaw roll) or a quat, but oh well.
				// The camera direction is pointing slightly down, and the up vector being straight up will make
				// for a slight skewing effect... this is handled properly in future examples with a camera utility
				cameraUpVector: Vector3.UnitZ
			)
		);

		UploadHelper builder = UploadHelper.Create( allocator, 16 * 1024 * 1024 );
		mVertexBuffer = builder.CommitVertexBuffer<VertexData>( data ).Checked();
		mIndexBuffer = builder.CommitIndexBuffer( [0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7] ).Checked();
		// Here we'll create a uniform buffer. This could've been done just as well using push constants though!
		mUniformBuffer = builder.CommitUniformBuffer( camera ).Checked();

		builder.Upload();

		device.WaitIdle();
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

			// As an alternative, again, you can use push constants here :3
			mCommands.PushUniformBuffer( mUniformBuffer, 0 );

			mCommands.BindVertexBuffer( mVertexBuffer, 0 );
			mCommands.BindIndexBuffer( mIndexBuffer );

			mCommands.DrawIndexed( indexCount: 12, instanceCount: 1 );
		} );
		mCommands.End();
		mQueue.Submit( mCommands, windowRt );
		return Result.Success();
	}

	public void Dispose()
	{
		mUniformBuffer.Dispose();
		mVertexBuffer.Dispose();
		mIndexBuffer.Dispose();
		mPipelineLayout.Dispose();
		mPipeline.Dispose();
		mShaderSet.Vertex.Dispose();
		mCommands.Dispose();
	}
}
