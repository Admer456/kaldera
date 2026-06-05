// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using System.Diagnostics;
using Kaldera;
using Kaldera.Objects;
using Silk.NET.Vulkan;
using StbImageSharp;

namespace ExampleBase;

public static class Utilities
{
	private static readonly Stopwatch mStopwatch = Stopwatch.StartNew();

	public static double GetSeconds()
		=> mStopwatch.Elapsed.TotalSeconds;

	public static int CalculateWorkRange( int workerIndex, int numWorkers, int count, out int start, out int end )
	{
		int stride = count / numWorkers;
		start = stride * workerIndex;
		end = workerIndex == numWorkers - 1 ? count : stride * (workerIndex + 1);
		return stride;
	}

	public static float Gauss( float x, float width )
		=> MathF.Exp( -(x * x) / width );

	public static Result<KaShader> LoadShader( KaDevice device, string shaderPath )
	{
		if ( !File.Exists( shaderPath ) )
		{
			return new Error( $"Cannot find shader: '{shaderPath}'" );
		}

		return KaShader.Create( device, File.ReadAllBytes( shaderPath ) );
	}

	public static Result<VertexShaderSet> LoadGraphicsShaderSet( KaDevice device, string shaderPath )
	{
		Result<KaShader> shaderFile = LoadShader( device, shaderPath );
		if ( !shaderFile.Get( out var error, out var shader ) )
		{
			return error.Prepend( "Could not load shader" );
		}

		return new VertexShaderSet( shader, "VertexMain", shader, "PixelMain" );
	}

	public static Result<ComputeShaderSet> LoadComputeShaderSet( KaDevice device, string shaderPath )
	{
		Result<KaShader> shaderFile = LoadShader( device, shaderPath );
		if ( !shaderFile.Get( out var error, out var shader ) )
		{
			return error.Prepend( "Could not load shader" );
		}

		return new ComputeShaderSet( shaderFile, "main" );
	}

	public static Result<(int Width, int Height, byte[] Data)> LoadTexture( string texturePath )
	{
		if ( !File.Exists( texturePath ) )
		{
			return new Error( $"Cannot find texture: '{texturePath}'" );
		}

		ImageResult? res = ImageResult.FromStream( File.OpenRead( texturePath ), ColorComponents.RedGreenBlueAlpha );
		if ( res is null )
		{
			return new Error( $"Texture not supported or corrupted: '{texturePath}'" );
		}

		return (res.Width, res.Height, res.Data);
	}

	public static SamplerOptions CommonSampler( Filter filter, int maxLod = 0 )
		=> new()
		{
			MagFilter = filter,
			MinFilter = Filter.Linear,
			MipmapMode = SamplerMipmapMode.Linear,
			MipLodBias = 0,
			MaxLod = maxLod,
			MinLod = 0,
			AddressModeU = SamplerAddressMode.Repeat,
			AddressModeV = SamplerAddressMode.Repeat,
			AddressModeW = SamplerAddressMode.Repeat,
			MaxAnisotropy = 16.0f,
			BorderColour = BorderColor.FloatTransparentBlack
		};

	public static GraphicsPipelineOptions CommonPipeline( KaLayout pipelineLayout, VertexInputLayout[]? vertexInputs, VertexShaderSet shaderSet,
		PipelineColorBlendAttachmentState? blending = null, bool depthTesting = true, SampleCountFlags multisampling = SampleCountFlags.Count4Bit,
		DynamicState[]? dynamicStates = null )
		=> new()
		{
			ResourceLayout = pipelineLayout,
			VertexInputs = vertexInputs,
			ShaderSet = shaderSet,
			DynamicStates = dynamicStates ?? [DynamicState.Viewport, DynamicState.Scissor],
			Topology = PrimitiveTopology.TriangleList,
			Color = new() { Attachments = [(blending ?? BlendAttachments.Opaque, Format.B8G8R8A8Unorm)] },
			DepthStencil = depthTesting
				? new()
				{
					DepthFormat = Format.D32SfloatS8Uint,
					DepthWrite = true,
					DepthComparison = CompareOp.Less,
					StencilState = null,
				}
				: null,
			Rasterizer = new()
			{
				PolygonMode = PolygonMode.Fill,
				CullMode = CullModeFlags.BackBit,
				FrontFace = FrontFace.Clockwise
			},
			Multisample = new()
			{
				RasterizationSamples = multisampling
			}
		};
}
