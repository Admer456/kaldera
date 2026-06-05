// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using System.Numerics;
using System.Runtime.InteropServices;
using ExampleBase;
using Kaldera.Abstractions;
using Kaldera.Abstractions.Buffers;
using Kaldera.Abstractions.Extensions;
using Kaldera.Abstractions.Interfaces;
using Kaldera.Abstractions.RenderTargets;
using Kaldera.Abstractions.Utilities;
using Kaldera.Extensions;
using Kaldera.Interfaces;
using Kaldera.Objects;
using SDL3;
using Silk.NET.Vulkan;
using Result = Kaldera.Result;

ExampleStartup.Run( "Kaldera Example - Hello compute shader!", 1600, 900, new ExampleCompute(), args );

/// <summary>
/// Renders a grid of quads, dynamically displacing it on the GPU.
///
/// On my PC (i7-9700 + RTX 3060) this runs at 6500 fps, no matter if I'm deforming the mesh
/// or not. The previous example runs at 3500 fps without deforming, 2500 fps when deforming.
///
/// It should be quite obvious why you'd want to do this on the GPU. :)
/// </summary>
internal class ExampleCompute : IExample
{
	private Camera3D mCamera = null!;

	private KaLayout mComputePipelineLayout = null!;
	private ComputeShaderSet mComputeShaderSet = null!;
	private KaComputePipeline mComputePipeline = null!;

	// In the shader, we'll do a "base, previous, next" kind of arrangement. Then we just swap
	private StorageBuffer mOriginalVertexDataBuffer = null!;
	private DoubleBuffer<VertexBuffer<VertexData>> mVertexBufferPair = null!;

	private IndexBuffer mIndexBuffer = null!;
	private VertexShaderSet mVertexPixelShaderSet = null!;
	private KaLayout mGraphicsPipelineLayout = null!;
	private KaGraphicsPipeline mGraphicsPipeline = null!;
	private KaCommandBuffer mCommands = null!;
	private KaQueue mQueue = null!;

	private float mTime;
	private Vector2 mMouseXy = Vector2.Zero;
	private bool mUserWantsToInteract;

	private class DoubleBuffer<T>
		where T : IGpuBuffer
	{
		public required T Buffer0 { get; init; }
		public required T Buffer1 { get; init; }
		public bool Toggle { get; private set; }

		public void Swap()
			=> Toggle = !Toggle;

		public T Current()
			=> Toggle ? Buffer1 : Buffer0;

		public T Next()
		{
			Swap();
			return Current();
		}
	}

	private struct Int4
	{
		public int X;
		public int Y;
		public int Z;
		public int W;
	}

	// We're gonna tightly pack all the inputs for the compute shader
	[StructLayout( LayoutKind.Sequential )]
	private struct ComputeInput
	{
		public Vector4 FloatData;
		public Int4 IntData;

		public required Vector2 Mouse
		{
			set
			{
				FloatData.X = value.X;
				FloatData.Y = value.Y;
			}
		}

		public required float DeltaTime
		{
			set => FloatData.Z = value;
		}

		public required float Time
		{
			set => FloatData.W = value;
		}

		public required int Width
		{
			set => IntData.X = value;
		}

		public required int Height
		{
			set => IntData.Y = value;
		}

		public required int Held
		{
			set => IntData.Z = value;
		}
	}

	[StructLayout( LayoutKind.Sequential )]
	private struct VertexData : IVertexData
	{
		// For this example, you have to align everything to 16 bytes, so we need some padding here
		public Vector4 Position;

		public static int SizeInBytes => VertexInputLayout.Stride;

		public static VertexAttribute[] VertexAttributes => VertexInputLayout.Elements;

		public static readonly VertexInputLayout VertexInputLayout = new()
		{
			Stride = 16, // sizeof(vec3) + padding
			Elements =
			[
				new() { Offset = 0, Format = Format.R32G32B32Sfloat }
			]
		};
	}

	public Result Init( KaInstance instance, KaDevice device, KaQueue queue, IResourceAllocator allocator )
	{
		mCommands = KaCommandBuffer.CreatePrimary( queue ).Checked();

		mVertexPixelShaderSet = Utilities.LoadGraphicsShaderSet( device, "hello_dynamic.spv" ).Checked();
		mComputeShaderSet = Utilities.LoadComputeShaderSet( device, "hello_dynamic_comp.spv" ).Checked();

		LayoutOptions layoutOptions = LayoutBuilder.Begin()
			.StructuredSet( s => s.UniformBuffer() )
			.Build();

		mGraphicsPipelineLayout = KaLayout.Create( device, layoutOptions ).Checked();
		mGraphicsPipeline = KaGraphicsPipeline.Create( device, new()
		{
			ResourceLayout = mGraphicsPipelineLayout,
			VertexInputs = [VertexData.VertexInputLayout],
			ShaderSet = mVertexPixelShaderSet,
			DynamicStates = [DynamicState.Viewport, DynamicState.Scissor],
			Topology = PrimitiveTopology.TriangleList,
			Color = new() { Attachments = [(BlendAttachments.Opaque, Format.B8G8R8A8Unorm)] },
			DepthStencil = null,
			Rasterizer = new()
			{
				PolygonMode = PolygonMode.Line,
				CullMode = CullModeFlags.None,
				FrontFace = FrontFace.Clockwise
			}
		} ).Checked();

		// We must specify that these inputs are intended for the compute shader
		layoutOptions = LayoutBuilder.Begin()
			.StructuredSet( s => s
				.StorageBuffer( ShaderStageFlags.ComputeBit )
				.StorageBuffer( ShaderStageFlags.ComputeBit )
				.StorageBuffer( ShaderStageFlags.ComputeBit ) )
			.PushConstant<ComputeInput>( ShaderStageFlags.ComputeBit )
			.Build();

		// Compute pipelines are WAY simpler!
		mComputePipelineLayout = KaLayout.Create( device, layoutOptions ).Checked();
		mComputePipeline = KaComputePipeline.Create( device, new()
		{
			ResourceLayout = mComputePipelineLayout,
			ShaderSet = mComputeShaderSet
		} ).Checked();

		float Gauss( float x, float width )
			=> MathF.Exp( -(x * x) / width );

		float HeightmapFunction( float x, float y )
			=> Gauss( new Vector2( x, y ).Length(), 15.0f ) * 1.5f;

		VertexData[] vertexData = Enumerable.Range( 0, 16 * 16 ).Select( i =>
		{
			float x = i % 16;
			float y = (int)(i / 16);

			// Centre -> (n-1)/2
			x -= 15.0f / 2.0f;
			y -= 15.0f / 2.0f;

			return new VertexData
			{
				Position = new()
				{
					X = x,
					Y = y,
					Z = HeightmapFunction( x, y )
				}
			};
		} ).ToArray();

		uint[] indices = Enumerable.Range( 0, 15 * 15 ).SelectMany<int, uint>( i =>
		{
			int row = i / 15;
			int column = i % 15;

			int top = row * 16;
			int bottom = (row + 1) * 16;

			uint topLeft = (uint)(top + column);
			uint topRight = topLeft + 1;
			uint bottomLeft = (uint)(bottom + column);
			uint bottomRight = bottomLeft + 1;

			return [topLeft, topRight, bottomRight, topLeft, bottomRight, bottomLeft];
		} ).ToArray();

		mCamera = Camera3D.Create( allocator ).Checked();

		// This is a little wasteful since we're uploading the same data twice, instead of copying it or w/e
		// but it's alright in this case
		UploadHelper builder = UploadHelper.Create( allocator, 16 * 1024 * 1024 );
		mOriginalVertexDataBuffer = builder.CommitStorageBuffer<VertexData>( vertexData );
		var vertexBuffer0 = builder.CommitVertexBuffer<VertexData>( vertexData );
		var vertexBuffer1 = builder.CommitVertexBuffer<VertexData>( vertexData );
		mIndexBuffer = builder.CommitIndexBuffer( indices );

		builder.Upload();

		device.WaitIdle();
		builder.Dispose();

		mQueue = queue;
		mVertexBufferPair = new()
		{
			Buffer0 = vertexBuffer0,
			Buffer1 = vertexBuffer1
		};

		mCamera.UpdateProjection( 16.0f / 9.0f );
		mCamera.Position = new( -4.0f, -4.0f, 3.0f );
		mCamera.PitchYawRoll = new( -30.0f, 45.0f, 0.0f );

		return Result.Success();
	}

	public bool OnEvent( IntPtr window, SDL.Event @event )
	{
		if ( (SDL.EventType)@event.Type is SDL.EventType.MouseButtonDown or SDL.EventType.MouseButtonUp )
		{
			SDL.MouseButtonEvent mouseEvent = @event.Button;
			if ( mouseEvent.Button is SDL.ButtonLeft )
			{
				mUserWantsToInteract = mouseEvent.Down;
				mMouseXy = new( mouseEvent.X, mouseEvent.Y );
			}
		}
		else if ( (SDL.EventType)@event.Type is SDL.EventType.MouseMotion )
		{
			SDL.MouseMotionEvent motion = @event.Motion;
			mMouseXy = new( motion.X, motion.Y );
		}

		mCamera.OnEvent( window, @event );
		return true;
	}

	private ComputeInput PrepareTransform( float dt )
	{
		mTime += dt;
		// Screen -> grid coords
		// This will break if you resize the window, but oh well :3c
		Vector2 click = new Vector2
		{
			X = 16.0f * mMouseXy.X / 1600.0f,
			Y = 16.0f - (16.0f * mMouseXy.Y / 900.0f)
		} - Vector2.One * 8.0f;

		return new()
		{
			Mouse = click,
			DeltaTime = dt,
			Time = mTime,
			// The shader may need to know how many vertices there are or something
			Width = 16,
			Height = 16,
			Held = mUserWantsToInteract ? 1 : 0
		};
	}

	public Result OnFrame( float dt, SwapchainRenderTarget windowRt )
	{
		mCamera.OnFrame( dt );

		// Make sure to swap it at some point, either at the start or end of the frame
		mVertexBufferPair.Swap();

		mCommands.Begin();
		{
			// Here we utilise a simple rotation scheme. This frame, it's buffers 0 and 1,
			// next frame it's 1 and 0 and so it swaps. Previous becomes next, next becomes previous...
			mCommands.BindPipeline( mComputePipeline );
			mCommands.PushConstant( PrepareTransform( dt ), ShaderStageFlags.ComputeBit );
			mCommands.PushStorageBuffer( mOriginalVertexDataBuffer, 0, 0 );
			mCommands.PushStorageBuffer( mVertexBufferPair.Next(), 0, 1 );
			mCommands.PushStorageBuffer( mVertexBufferPair.Next(), 0, 2 );
			// The compute shader is hardcoded to spawn 16 threads. For us, this means
			// one dispatch processes 16 vertices. So... dispatch 16 groups
			mCommands.Dispatch( workGroupsX: 16 );

			// Compute -> vertex input barrier
			// Or, when the compute shader writes, block any drawing operations
			mCommands.Barrier( before: BarrierStages.ComputeShader, after: BarrierStages.VertexShader );

			mCamera.UploadData( mCommands );
		}
		mCommands.RenderPass( windowRt, () =>
		{
			mCommands.ClearColour( windowRt, 0, new( 0.0f, 0.13f, 0.13f, 1.0f ) );
			mCommands.BindPipeline( mGraphicsPipeline );
			mCommands.SetViewport( 0, windowRt.Extent, 0.0f, 1.0f );
			mCommands.SetScissor( 0, windowRt.Extent );

			// This is then rendered as usual. No changes here
			mCommands.PushUniformBuffer( mCamera.UniformBuffer, 0 );
			mCommands.BindVertexBuffer( mVertexBufferPair.Current(), 0 );
			mCommands.BindIndexBuffer( mIndexBuffer );

			mCommands.DrawIndexed( indexCount: mIndexBuffer.Count, instanceCount: 1 );
		} );
		mCommands.End();
		mQueue.Submit( mCommands, windowRt );
		return Result.Success();
	}

	public void Dispose()
	{
		mComputePipelineLayout.Dispose();
		mComputeShaderSet.Shader.Dispose();
		mComputePipeline.Dispose();

		mCamera.Dispose();
		mOriginalVertexDataBuffer.Dispose();
		mVertexBufferPair.Buffer0.Dispose();
		mVertexBufferPair.Buffer1.Dispose();
		mIndexBuffer.Dispose();
		mGraphicsPipelineLayout.Dispose();
		mGraphicsPipeline.Dispose();
		mVertexPixelShaderSet.Vertex.Dispose();
		mCommands.Dispose();
	}
}
