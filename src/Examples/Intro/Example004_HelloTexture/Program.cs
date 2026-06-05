// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using System.Numerics;
using System.Runtime.InteropServices;
using ExampleBase;
using Kaldera;
using Kaldera.Abstractions.Buffers;
using Kaldera.Abstractions.Extensions;
using Kaldera.Abstractions.RenderTargets;
using Kaldera.Abstractions.Textures;
using Kaldera.Abstractions.Utilities;
using Kaldera.Extensions;
using Kaldera.Interfaces;
using Kaldera.Objects;
using Silk.NET.Vulkan;
using Result = Kaldera.Result;

ExampleStartup.Run( "Kaldera Example - Hello texture mapping!", 1280, 900, new ExampleTexture(), args );

/// <summary>
/// Renders a quad with a simple 2D texture on it.
/// </summary>
internal class ExampleTexture : IExample
{
	private KaSampler mSampler;
	private Texture mTexture;
	private IndexBuffer mIndexBuffer = null!;
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

		// Yup. Texture coordinate time
		public Vector2 Uv;

		public static int SizeInBytes => VertexInputLayout.Stride;

		public static VertexAttribute[] VertexAttributes => VertexInputLayout.Elements;

		public static readonly VertexInputLayout VertexInputLayout = new()
		{
			Stride = 20, // sizeof(vec3) + sizeof(vec2)
			Elements =
			[
				new() { Offset = 0, Format = Format.R32G32B32Sfloat },
				// Note that the UV coordinates are 12 bytes after from the position
				new() { Offset = 12, Format = Format.R32G32Sfloat }
			]
		};
	}

	public Result Init( KaInstance instance, KaDevice device, KaQueue queue, IResourceAllocator allocator )
	{
		Result<KaCommandBuffer> commandBuffer = KaCommandBuffer.CreatePrimary( queue ).Checked();

		mShaderSet = Utilities.LoadGraphicsShaderSet( device, "hello_texture.spv" ).Checked();

		// This here describes a descriptor layout, composed of one or more descriptor set layouts, composed
		// of one or more descriptor set bindings. Got that? No? Well...
		// Descriptors are really just references to shader resources. That's all.
		//
		// Textures are one of 3 types of shader resources (besides buffers and samplers). The GPU needs to know
		// how we're going to send it that texture, and that's what layouts are for.
		// So remember, this is the INPUT LAYOUT for a shader.
		//
		// You'll typically have a maximum of 32 sets, so whenever it makes sense, do group different resources
		// into the same set. More on that below...
		LayoutOptions layoutOptions = LayoutBuilder.Begin()
			.StructuredSet( s => s // Set #0
				.SampledTexture() // Set #0 binding #0
				.Sampler() ) // Set #0 binding #1
			.Build();

		// You can also think of it this way. If you had a couple structs like so:
		// struct TextureData {      // Set #0
		//     Texture2D DiffuseMap; // Binding #0
		//     Texture2D NormalMap;  // Binding #1
		// }
		// struct LightingData {     // Set #1
		//     Texture2D LightMap;   // Binding #0
		//     Texture2D AmbientMap; // Binding #1
		//     TextureCube SkyMap;   // Binding #2
		// }
		// It'd be represented as two sets. One with two bindings, and one with three bindings.
		// The separation of descriptors into sets also lets you dictate *how often* things can change.
		// E.g. you can have a descriptor set for per-view things, per-entity things and per-surface things.

		// Finally, we create the pipeline layout.
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
				// Note: this example is Y-down, so face windings are counterclockwise
				FrontFace = FrontFace.CounterClockwise
			}
		} );

		// A sampler is basically a configuration for texture sampling. It tells the GPU
		// if it should sample it in a pixelated fashion, blurry fashion, tile, clamp etc.
		mSampler = KaSampler.Create( device, new()
		{
			MagFilter = Filter.Linear,
			MinFilter = Filter.Linear,
			MipmapMode = SamplerMipmapMode.Linear,
			MipLodBias = 0.5f,
			MaxLod = 1.0f,
			MinLod = 0.0f,
			AddressModeU = SamplerAddressMode.Repeat,
			AddressModeV = SamplerAddressMode.Repeat,
			AddressModeW = SamplerAddressMode.Repeat,
			MaxAnisotropy = null, // Anisotropic filtering yay
			BorderColour = BorderColor.FloatOpaqueBlack
		} );

		// Textures are pretty simple. These can be 1D, 2D or 3D arrays of pixels,
		// uncompressed or compressed (e.g. BC7), of various, various formats that
		// may or may not have operational limitations on some graphics cards...
		// Hmm. Maybe they're not THAT simple.
		//
		// In this library we have texture classes for different use cases:
		// * Texture - 1D and 2D all-rounder. You can use it for uncompressed diffuse maps, normal maps...
		// * TextureCompressed - Same as above, but compressed. Usually diffuse maps.
		// * Texture3D - 3D texture. Particle volumes, fog and the like
		// * TextureArray - Array of Texture, TextureCompressed etc.
		// * Attachment[...] - Framebuffer textures. They have special logic and special formats!
		var texture = Utilities.LoadTexture( "hello_texture.png" ).Checked();

		VertexData[] data =
		{
			new() { Position = new( 0.5f, 0.5f, 0.0f ), Uv = new( 1.0f, 1.0f ) },
			new() { Position = new( 0.5f, -0.5f, 0.0f ), Uv = new( 1.0f, -1.0f ) },
			new() { Position = new( -0.5f, -0.5f, 0.0f ), Uv = new( -1.0f, -1.0f ) },
			new() { Position = new( -0.5f, 0.5f, 0.0f ), Uv = new( -1.0f, 1.0f ) },
		};

		UploadHelper builder = UploadHelper.Create( allocator, 16 * 1024 * 1024 ).Checked();
		mVertexBuffer = builder.CommitVertexBuffer<VertexData>( data ).Checked();
		mIndexBuffer = builder.CommitIndexBuffer( [0, 1, 2, 0, 2, 3] ).Checked();
		// The builder can also upload textures :)
		mTexture = builder.CommitTexture( texture.Data, Format.R8G8B8A8Unorm, texture.Width, texture.Height, mips: 1 ).Checked();

		builder.Upload();

		device.WaitIdle();
		builder.Dispose();

		mCommands = commandBuffer;
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
			// Since there's no projection going on or anything, i.e. we're rendering in 2D,
			// we use SetViewportFlipped which treats everything as Y-down
			mCommands.SetViewportFlipped( 0, windowRt.Extent, 0.0f, 1.0f );
			mCommands.SetScissor( 0, windowRt.Extent );

			// Following the layout, we will push:
			// * the texture to set 0 binding 0
			// * the sampler to set 0 binding 1
			// This is the part where descriptors are actually sent. Internally, PushSampledTexture et al. end up
			// calling vkCmdPushDescriptor. No need to create a descriptor set or anything, just chuck it in there :)
			mCommands.PushSampledTexture( mTexture, set: 0, binding: 0 );
			mCommands.PushSampler( mSampler, set: 0, binding: 1 );

			mCommands.BindVertexBuffer( mVertexBuffer, 0 );
			mCommands.BindIndexBuffer( mIndexBuffer );

			mCommands.DrawIndexed( indexCount: 6, instanceCount: 1 );
		} );
		mCommands.End();
		mQueue.Submit( mCommands, windowRt );
		return Result.Success();
	}

	public void Dispose()
	{
		mTexture.Dispose();
		mSampler.Dispose();
		mVertexBuffer.Dispose();
		mIndexBuffer.Dispose();
		mPipelineLayout.Dispose();
		mPipeline.Dispose();
		mShaderSet.Vertex.Dispose();
		mCommands.Dispose();
	}
}
