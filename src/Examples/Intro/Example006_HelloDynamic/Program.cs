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
using SDL3;
using Silk.NET.Vulkan;
using Result = Kaldera.Result;

ExampleStartup.Run( "Kaldera Example - Hello dynamic memory!", 1600, 900, new ExampleDynamic(), args );

/// <summary>
/// Renders a grid of quads, dynamically displacing it on the CPU.
/// Typically, you would do this using a compute shader, which is in the next example!
///
/// On my PC (i7-9700 + RTX 3060) this runs at about 2500 fps when deforming the mesh.
/// The next example runs at 6500 fps.
/// </summary>
internal class ExampleDynamic : IExample
{
	private Camera3D mCamera = null!;
	private StagingBuffer mStagingBuffer = null!;
	private IndexBuffer mIndexBuffer = null!;
	private VertexBuffer<VertexData> mVertexBuffer = null!;
	private VertexShaderSet mShaderSet = null!;
	private KaLayout mPipelineLayout = null!;
	private KaGraphicsPipeline mPipeline = null!;
	private KaCommandBuffer mCommands = null!;
	private KaQueue mQueue = null!;

	private VertexData[] mOriginalVertexData = [];
	private float mTime;
	private Vector2 mMouseXy = Vector2.Zero;
	private bool mUserWantsToInteract;

	[StructLayout( LayoutKind.Sequential )]
	private struct VertexData : IVertexData
	{
		public Vector3 Position;

		public static int SizeInBytes => VertexInputLayout.Stride;

		public static VertexAttribute[] VertexAttributes => VertexInputLayout.Elements;

		public static readonly VertexInputLayout VertexInputLayout = new()
		{
			Stride = 12, // sizeof(vec3)
			Elements =
			[
				new() { Offset = 0, Format = Format.R32G32B32Sfloat }
			]
		};
	}

	public Result Init( KaInstance instance, KaDevice device, KaQueue queue, IResourceAllocator allocator )
	{
		mCommands = KaCommandBuffer.CreatePrimary( queue ).Checked();

		mShaderSet = Utilities.LoadGraphicsShaderSet( device, "hello_dynamic.spv" ).Checked();

		LayoutOptions layoutOptions = LayoutBuilder.Begin()
			.StructuredSet( s => s.UniformBuffer() ) // Set #0 binding #0
			.Build();

		mPipelineLayout = KaLayout.Create( device, layoutOptions ).Checked();
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
				PolygonMode = PolygonMode.Line,
				CullMode = CullModeFlags.None,
				FrontFace = FrontFace.Clockwise
			}
		} ).Checked();

		// This is a bit more advanced. We will procedurally generate a 16x16 mesh against a simple heightmap function
		float Gauss( float x, float width )
			=> MathF.Exp( -(x * x) / width );

		float HeightmapFunction( float x, float y )
			=> Gauss( new Vector2( x, y ).Length(), 15.0f ) * 1.5f;

		mOriginalVertexData = Enumerable.Range( 0, 16 * 16 ).Select( i =>
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

		// Came up with this real quick, drew it in Inkscape. Might be useful to you too!
		uint[] indices = Enumerable.Range( 0, 15 * 15 ).SelectMany<int, uint>( i =>
		{
			// We ignore the last row & column here, since they are implicitly added below
			int row = i / 15;
			int column = i % 15;

			int top = row * 16;
			int bottom = (row + 1) * 16;

			// tl*   tr
			//  ------
			//  |\   |
			//  | \  |
			//  |  \ |
			//  |   \|
			//  ------
			// bl    br
			//
			// * = reference point. It's what 'i' refers to here
			uint topLeft = (uint)(top + column);
			uint topRight = topLeft + 1;
			uint bottomLeft = (uint)(bottom + column);
			uint bottomRight = bottomLeft + 1;

			return [topLeft, topRight, bottomRight, topLeft, bottomRight, bottomLeft];
		} ).ToArray();

		// Camera initialisation. Yeah. We got a camera now :)
		var camera = Camera3D.Create( allocator ).Checked();

		// BufferBuilder internally uses two huge staging buffers, so we're gonna do something similar here
		var stagingBuffer = StagingBuffer.Create( allocator, mOriginalVertexData.Length * VertexData.SizeInBytes ).Checked();

		UploadHelper builder = UploadHelper.Create( allocator, 16 * 1024 * 1024 );
		mVertexBuffer = builder.CommitVertexBuffer<VertexData>( mOriginalVertexData ).Checked();
		mIndexBuffer = builder.CommitIndexBuffer( indices ).Checked();

		builder.Upload();

		device.WaitIdle();
		builder.Dispose();

		mStagingBuffer = stagingBuffer;
		mCamera = camera;
		mQueue = queue;

		// We've copied this to the vertex buffer, but we'll do the same for the staging buffer
		Span<VertexData> modifiedVertexData = mStagingBuffer.Mapping.AsSpan<VertexData>();
		mOriginalVertexData.CopyTo( modifiedVertexData );

		mCamera.UpdateProjection( 16.0f / 9.0f );
		mCamera.Position = new( -4.0f, -4.0f, 3.0f );
		mCamera.PitchYawRoll = new( -30.0f, 45.0f, 0.0f );

		return Result.Success();
	}

	// Finally, about time we're reacting to events here
	public bool OnEvent( IntPtr window, SDL.Event @event )
	{
		// We're gonna displace the grid if the user presses LMB
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

	private void TransformGeometry( float dt )
	{
		mTime += dt;
		// Screen -> grid coords
		// This will break if you resize the window, but oh well :3c
		Vector2 click = new Vector2
		{
			X = 16.0f * mMouseXy.X / 1600.0f,
			Y = 16.0f - (16.0f * mMouseXy.Y / 900.0f)
		} - Vector2.One * 8.0f;

		Span<VertexData> modifiedVertexData = mStagingBuffer.Mapping.AsSpan<VertexData>();

		if ( mUserWantsToInteract )
		{
			for ( int i = 0; i < mOriginalVertexData.Length; i++ )
			{
				ref Vector3 pos = ref modifiedVertexData[i].Position;

				float distance = MathF.Max( 1.0f, Vector2.DistanceSquared( click, new( pos.X, pos.Y ) ) );
				float falloff = 1.0f / distance;

				pos += new Vector3
				{
					X = MathF.Sin( mTime * 3.0f + pos.Y ) * 0.1f * falloff,
					Y = MathF.Cos( mTime * 3.0f + pos.X ) * 0.1f * falloff,
					Z = (MathF.Sin( mTime * 6.0f + pos.Y ) + MathF.Cos( mTime * 6.0f + pos.X )) * 0.033f * falloff
				};
			}
		}

		for ( int i = 0; i < mOriginalVertexData.Length; i++ )
		{
			ref Vector3 pos = ref modifiedVertexData[i].Position;

			// Really stupid "bounce back" thing
			Vector3 originalPos = mOriginalVertexData[i].Position;
			float distance = Vector3.Distance( pos, originalPos );
			pos += (originalPos - pos) * dt * distance;
		}
	}

	public Result OnFrame( float dt, SwapchainRenderTarget windowRt )
	{
		// Update camera angles etc.
		mCamera.OnFrame( dt );
		TransformGeometry( dt );

		mCommands.Begin();
		{
			// In this little scope, we'll be uploading the data. We cannot really do that while in a render pass...
			// Note that this is also completely doable with an UploadHelper!
			// helper.UpdateBuffer( mVertexBuffer, vertexData.AsSpan() );
			// helper.Upload();
			mCommands.CopyBuffer( mStagingBuffer, mVertexBuffer );

			// Welcome to: memory barriers!
			// In a nutshell, this ensures CopyBuffer above gets done first, and ONLY then can BindVertexBuffer,
			// BindIndexBuffer and others run.
			//
			// The "before" stage here captures transfer-related commands, so you could have any number of buffer
			// copies above. The "after" stage specified here also captures any vertex shader input commands.
			//
			// Thus, a write-after-read hazard is prevented!
			mCommands.Barrier( before: BarrierStages.Transfer, after: BarrierStages.VertexShader );

			// Look what happens inside! The camera is also doing the same streaming we're doing here
			mCamera.UploadData( mCommands );

			// Sidenote:
			// mCamera.UploadData places its own memory barrier, which makes our barrier above it unnecessary.
			// Typically, you'd place just one "wide" memory barrier, at the end of your "data uploading" scope :3c
		}
		mCommands.RenderPass( windowRt, () =>
		{
			mCommands.ClearColour( windowRt, 0, new( 0.0f, 0.13f, 0.13f, 1.0f ) );
			mCommands.BindPipeline( mPipeline );
			mCommands.SetViewport( 0, windowRt.Extent, 0.0f, 1.0f );
			mCommands.SetScissor( 0, windowRt.Extent );

			// The camera utility provides a nice little uniform buffer we can use
			mCommands.PushUniformBuffer( mCamera.UniformBuffer, 0 );

			mCommands.BindVertexBuffer( mVertexBuffer, 0 );
			mCommands.BindIndexBuffer( mIndexBuffer );

			mCommands.DrawIndexed( indexCount: mIndexBuffer.Count, instanceCount: 1 );
		} );
		mCommands.End();
		mQueue.Submit( mCommands, windowRt );
		return Result.Success();
	}

	public void Dispose()
	{
		mStagingBuffer.Dispose();
		mCamera.Dispose();
		mVertexBuffer.Dispose();
		mIndexBuffer.Dispose();
		mPipelineLayout.Dispose();
		mPipeline.Dispose();
		mShaderSet.Vertex.Dispose();
		mCommands.Dispose();
	}
}
