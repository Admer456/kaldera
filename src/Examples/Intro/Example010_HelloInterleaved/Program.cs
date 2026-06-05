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

ExampleStartup.Run( "Kaldera Example - Hello interleaved vertex buffers!", 1600, 900, new ExampleInterleaved(), args );

/// <summary>
/// How to use multiple vertex streams. It allows for more advanced usecases, as it lets you compose vertex inputs.
/// For example, you could have one vertex buffer for position+UV+normal, and another for colours, bone weights etc.
/// </summary>
internal class ExampleInterleaved : IExample
{
	private VertexShaderSet mScreenShaderSet = null!;
	private KaLayout mScreenPipelineLayout = null!;
	private KaGraphicsPipeline mScreenPipeline = null!;

	private float mResizeTimer;
	private Vector2 mNewSize = Vector2.Zero;
	private TextureRenderTarget mRenderTarget = null!;
	private Camera3D mCamera = null!;
	private KaSampler mSampler;

	private IndexBuffer mIndexBuffer = null!;

	// We have TWO vertex buffers now!
	private VertexBuffer<PosNormal> mPositionBuffer = null!;
	private VertexBuffer<ColourData> mColourBuffer = null!;
	private VertexShaderSet mShaderSet = null!;
	private KaLayout mPipelineLayout = null!;
	private KaGraphicsPipeline mPipeline = null!;
	private KaCommandBuffer mCommands = null!;
	private KaQueue mQueue = null!;

	[StructLayout( LayoutKind.Sequential )]
	private struct ColourData : IVertexData
	{
		public Vector4 Colour;

		public static int SizeInBytes => InputLayout.Stride;
		public static VertexAttribute[] VertexAttributes => InputLayout.Elements;

		public static readonly VertexInputLayout InputLayout = new()
		{
			// Notice how the stride is not 12+16 but rather only 16. Stride is for individual
			// vertex buffers, not whole vertices! The index buffer remains singular tho'
			Stride = 16, // sizeof(vec4)
			Elements =
			[
				new()
				{
					Offset = 0,
					Format = Format.R32G32B32A32Sfloat
				}
			]
		};
	}

	public Result Init( KaInstance instance, KaDevice device, KaQueue queue, IResourceAllocator allocator )
	{
		mCommands = KaCommandBuffer.CreatePrimary( queue ).Checked();
		mShaderSet = Utilities.LoadGraphicsShaderSet( device, "hello_interleaved.spv" ).Checked();
		mScreenShaderSet = Utilities.LoadGraphicsShaderSet( device, "screen.spv" ).Checked();

		mSampler = KaSampler.Create( device, new()
		{
			MagFilter = Filter.Linear,
			MinFilter = Filter.Linear,
			MipmapMode = SamplerMipmapMode.Linear,
			MipLodBias = 0,
			MaxLod = 0,
			MinLod = 0,
			AddressModeU = SamplerAddressMode.ClampToEdge,
			AddressModeV = SamplerAddressMode.ClampToEdge,
			AddressModeW = SamplerAddressMode.ClampToEdge,
			MaxAnisotropy = null,
			BorderColour = BorderColor.FloatTransparentBlack
		} ).Checked();

		LayoutOptions layoutOptions = LayoutBuilder.Begin()
			.StructuredSet( s => s.UniformBuffer() ) // Set #0 binding #0
			.Build();

		mPipelineLayout = KaLayout.Create( device, layoutOptions ).Checked();
		mPipeline = KaGraphicsPipeline.Create( device, new()
		{
			ResourceLayout = mPipelineLayout,
			// There are now two vertex inputs to the pipeline
			VertexInputs = [PosNormal.VertexInputLayout, ColourData.InputLayout],
			ShaderSet = mShaderSet,
			DynamicStates = [DynamicState.Viewport, DynamicState.Scissor],
			Topology = PrimitiveTopology.TriangleList,
			Color = new() { Attachments = [(BlendAttachments.Opaque, Format.B8G8R8A8Unorm)] },
			DepthStencil = new()
			{
				DepthFormat = Format.D32SfloatS8Uint,
				DepthWrite = true,
				DepthComparison = CompareOp.Less,
				StencilState = null,
			},
			Rasterizer = new()
			{
				PolygonMode = PolygonMode.Fill,
				CullMode = CullModeFlags.BackBit,
				FrontFace = FrontFace.Clockwise
			},
			Multisample = new()
			{
				RasterizationSamples = SampleCountFlags.Count4Bit
			}
		} ).Checked();

		layoutOptions = LayoutBuilder.Begin()
			.StructuredSet( s => s
				.SampledTexture()
				.Sampler() )
			.Build();

		mScreenPipelineLayout = KaLayout.Create( device, layoutOptions ).Checked();
		mScreenPipeline = KaGraphicsPipeline.Create( device, new()
		{
			ResourceLayout = mScreenPipelineLayout,
			VertexInputs = null,
			ShaderSet = mScreenShaderSet,
			DynamicStates = [DynamicState.Viewport, DynamicState.Scissor],
			Topology = PrimitiveTopology.TriangleFan,
			Color = new() { Attachments = [(BlendAttachments.Opaque, Format.B8G8R8A8Unorm)] },
			DepthStencil = null,
			Rasterizer = new()
			{
				PolygonMode = PolygonMode.Fill,
				CullMode = CullModeFlags.None,
				FrontFace = FrontFace.Clockwise
			}
		} );

		var renderTargetOptions = TextureRenderTargetOptions.ColourDepthStencil( 1600, 900, Format.B8G8R8A8Unorm ) with
		{
			Samples = SampleCountFlags.Count4Bit
		};

		mRenderTarget = TextureRenderTarget.Create( allocator, renderTargetOptions ).Checked();
		mCamera = Camera3D.Create( allocator );

		// We'll be generating a grid, again
		// This time, we got some real nice utilities!
		float Heightmap( Vector2 xy )
			=> new Vector2[] { new( 0.0f, 0.0f ), new( 2.4f, 6.0f ), new( -2.0f, 4.0f ) }
				.Sum( p => Utilities.Gauss( Vector2.Distance( xy, p ), 8.0f ) * 2.0f );

		PosNormal PosNormalGenerator( Vector2 xy, GridMeshBuilder.HeightmapFunc z )
			=> new()
			{
				Position = GridMeshBuilder.GetPosition( xy, z ),
				Normal = GridMeshBuilder.GetNormal( xy, z )
			};

		Vector3 ColourForHeight( float z )
		{
			// Watr,, :3
			if ( z < 0.05f )
			{
				return new( 0.5f, 0.5f, 1.0f );
			}

			// Samb,,
			if ( z < 0.25f )
			{
				return new( 0.75f, 0.8f, 0.25f );
			}

			// Gras,,
			if ( z < 1.5f )
			{
				return new( 0.25f, 0.7f, 0.15f );
			}

			// Sno,,
			return new( 0.9f );
		}

		ColourData ColourGenerator( Vector2 xy, GridMeshBuilder.HeightmapFunc z )
			=> new()
			{
				Colour = new( ColourForHeight( z( xy ) ), 1.0f )
			};

		UploadHelper builder = UploadHelper.Create( allocator, 16 * 1024 * 1024 );
		mPositionBuffer = builder.CommitVertexBuffer<PosNormal>( GridMeshBuilder.BuildVertexData( 16, 16, PosNormalGenerator, Heightmap ) ).Checked();
		mColourBuffer = builder.CommitVertexBuffer<ColourData>( GridMeshBuilder.BuildVertexData( 16, 16, ColourGenerator, Heightmap ) ).Checked();
		mIndexBuffer = builder.CommitIndexBuffer( GridMeshBuilder.BuildIndices( 16, 16 ) ).Checked();
		builder.Upload();

		device.WaitIdle();
		builder.Dispose();

		mQueue = queue;

		mCamera.UpdateProjection( 16.0f / 9.0f );
		mCamera.Position = new( 0.0f, -4.0f, 1.0f );
		mCamera.PitchYawRoll = new( -30.0f, 0.0f, 0.0f );

		return Result.Success();
	}

	public Result OnFrame( float dt, SwapchainRenderTarget windowRt )
	{
		if ( mResizeTimer > 0.0f )
		{
			mResizeTimer -= dt;

			if ( mResizeTimer <= 0.0f )
			{
				mResizeTimer = -1.0f;
				mQueue.Device.WaitIdle();
				mRenderTarget.RequestResize( mNewSize );
			}
		}

		mCamera.OnFrame( dt );

		mCommands.Begin();
		mCamera.UploadData( mCommands );
		mCommands.RenderPass( mRenderTarget, () =>
		{
			mCommands.ClearColour( mRenderTarget, 0, new( 0.0f, 0.13f, 0.13f, 1.0f ) );
			mCommands.ClearDepth( mRenderTarget, 0, 1.0f );
			mCommands.BindPipeline( mPipeline );
			mCommands.SetViewport( 0, mRenderTarget.Extent, 0.0f, 1.0f );
			mCommands.SetScissor( 0, mRenderTarget.Extent );

			mCommands.PushUniformBuffer( mCamera.UniformBuffer, 0 );
			// The order of these depends on the order in the shader
			mCommands.BindVertexBuffer( mPositionBuffer, 0 );
			mCommands.BindVertexBuffer( mColourBuffer, 1 );
			mCommands.BindIndexBuffer( mIndexBuffer );

			mCommands.DrawIndexed( indexCount: mIndexBuffer.Count, instanceCount: 1 );
		} );
		mCommands.RenderPass( windowRt, () =>
		{
			mCommands.BindPipeline( mScreenPipeline );
			mCommands.SetViewportFlipped( 0, windowRt.Extent, 0.0f, 1.0f );
			mCommands.SetScissor( 0, windowRt.Extent );

			mCommands.PushSampledTexture( mRenderTarget.ColourAttachments.Value, 0 );
			mCommands.PushSampler( mSampler, 0, 1 );
			mCommands.Draw( vertexCount: 3, instanceCount: 1 );
		} );
		mCommands.End();
		mQueue.Submit( mCommands, windowRt );
		return Result.Success();
	}

	public bool OnEvent( IntPtr window, SDL.Event @event )
	{
		mCamera.OnEvent( window, @event );

		if ( (SDL.EventType)@event.Type is SDL.EventType.WindowResized )
		{
			SDL.GetWindowSizeInPixels( window, out int width, out int height );

			mResizeTimer = 0.5f;
			mNewSize = new( width, height );
		}

		return true;
	}

	public void Dispose()
	{
		mSampler.Dispose();
		mScreenPipelineLayout.Dispose();
		mScreenPipeline.Dispose();
		mScreenShaderSet.Vertex.Dispose();
		mRenderTarget.Dispose();
		mCamera.Dispose();
		mPositionBuffer.Dispose();
		mColourBuffer.Dispose();
		mIndexBuffer.Dispose();
		mPipelineLayout.Dispose();
		mPipeline.Dispose();
		mShaderSet.Vertex.Dispose();
		mCommands.Dispose();
	}
}
