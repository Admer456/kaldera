// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Extensions;
using Kaldera.Interfaces;

namespace Kaldera.Objects;

public abstract record ShaderSet;

public sealed record NullShaderSet : ShaderSet;

public sealed record DepthShaderSet(
	KaShader Vertex,
	string VertexFunc ) : ShaderSet;

public sealed record VertexShaderSet(
	KaShader Vertex,
	string VertexFunc,
	KaShader Pixel,
	string PixelFunc ) : ShaderSet;

public record MeshShaderSet(
	KaShader Mesh,
	string MeshFunc,
	KaShader Pixel,
	string PixelFunc ) : ShaderSet;

public sealed record AmplifiedMeshShaderSet(
	KaShader Amplification,
	string AmplificationFunc,
	KaShader Mesh,
	string MeshFunc,
	KaShader Pixel,
	string PixelFunc ) : MeshShaderSet( Mesh, MeshFunc, Pixel, PixelFunc );

public unsafe struct ShaderStage
{
	private byte[] mNameBytes;

	public required KaShader KaShader;
	public required ShaderStageFlags Flags;

	public required string Name
	{
		set => mNameBytes = value.ToCString();
	}

	public byte* NamePtr => mNameBytes.AsPointer();
}

public class RasterizerOptions
{
	public required PolygonMode PolygonMode { get; set; }
	public required CullModeFlags CullMode { get; set; }
	public required FrontFace FrontFace { get; set; }

	/// <summary>
	/// Clamps the pixel between zmin and zmax.
	/// </summary>
	public bool DepthClampEnable { get; set; } = false;

	public bool RasterizerDiscardEnable { get; set; } = false;
	public bool DepthBiasEnable { get; set; } = false;
	public float DepthBiasConstantFactor { get; set; } = 0.0f;
	public float DepthBiasClamp { get; set; } = 0.0f;
	public float DepthBiasSlopeFactor { get; set; } = 1.0f;
	public float LineWidth { get; set; } = 1.0f;
}

public class MultisampleOptions
{
	/// <summary>
	/// Anti-aliasing value.
	/// </summary>
	public required SampleCountFlags RasterizationSamples { get; set; }

	/// <summary>
	/// Minimum fraction for sample shading. If this is set, sample shading is ENABLED.
	/// Sample shading acts like antialiasing on the texel level.
	/// </summary>
	public float? MinSampleShading { get; set; }

	/// <summary>
	/// Mask for sample shading. If not set, the mask is treated as all bits set to 1.
	/// </summary>
	public uint[]? SampleMask { get; set; }

	/// <summary>
	/// Alpha-to-coverage and other MSAA options.
	/// </summary>
	public bool AlphaToCoverage { get; set; }
}

public static class BlendAttachments
{
	public const ColorComponentFlags ComponentsRgb = ColorComponentFlags.RBit |
	                                                 ColorComponentFlags.GBit |
	                                                 ColorComponentFlags.BBit;

	public const ColorComponentFlags ComponentsRgba = ColorComponentFlags.RBit |
	                                                  ColorComponentFlags.GBit |
	                                                  ColorComponentFlags.BBit |
	                                                  ColorComponentFlags.ABit;

	public static PipelineColorBlendAttachmentState Opaque => new()
	{
		BlendEnable = false,
		ColorWriteMask = ComponentsRgba
	};

	/// <summary> src*src_alpha + dst*1 = src*src_alpha + dst </summary>
	public static PipelineColorBlendAttachmentState Additive => new()
	{
		BlendEnable = true,

		SrcColorBlendFactor = BlendFactor.SrcAlpha,
		DstColorBlendFactor = BlendFactor.One,
		SrcAlphaBlendFactor = BlendFactor.SrcAlpha,
		DstAlphaBlendFactor = BlendFactor.One,

		ColorBlendOp = BlendOp.Add,
		ColorWriteMask = ComponentsRgba
	};

	/// <summary> src*(src_alpha) + dst*(1-src_alpha) = lerp(src, dst, src_alpha) </summary>
	public static PipelineColorBlendAttachmentState AlphaBlend => new()
	{
		BlendEnable = true,

		SrcColorBlendFactor = BlendFactor.SrcAlpha,
		DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
		SrcAlphaBlendFactor = BlendFactor.SrcAlpha,
		DstAlphaBlendFactor = BlendFactor.OneMinusSrcAlpha,

		ColorBlendOp = BlendOp.Add,
		ColorWriteMask = ComponentsRgba
	};

	/// <summary> src*dst + dst*0 = src*dst </summary>
	public static PipelineColorBlendAttachmentState Multiply => new()
	{
		BlendEnable = true,

		SrcColorBlendFactor = BlendFactor.DstColor,
		DstColorBlendFactor = BlendFactor.Zero,
		SrcAlphaBlendFactor = BlendFactor.Zero,
		DstAlphaBlendFactor = BlendFactor.One,

		ColorBlendOp = BlendOp.Add,
		ColorWriteMask = ComponentsRgba
	};

	/// <summary> src*dst + dst*src = 2(src*dst) </summary>
	public static PipelineColorBlendAttachmentState MiddleGray => new()
	{
		BlendEnable = true,

		SrcColorBlendFactor = BlendFactor.DstColor,
		DstColorBlendFactor = BlendFactor.One,
		SrcAlphaBlendFactor = BlendFactor.SrcColor,
		DstAlphaBlendFactor = BlendFactor.Zero,

		ColorBlendOp = BlendOp.Add,
		ColorWriteMask = ComponentsRgb
	};
}

public class ColorOptions
{
	/// <summary> Rendering outputs and their formats. </summary>
	public required (PipelineColorBlendAttachmentState BlendState, Format Format)[] Attachments { get; set; }

	/// <summary> Selects which logical operation to apply, if any. </summary>
	public LogicOp? BlendOperator { get; set; } = null;

	/// <summary> RGBA constants used in blending. See <see cref="BlendFactor"/>. </summary>
	public Vector4 BlendConstantsRgba { get; set; } = Vector4.One;
}

public class DepthStencilOptions
{
	/// <summary> Selects the comparison operator used when performing the depth test.
	/// Setting this to non-null implicitly enables depth testing, which culls pixels
	/// that fail the specified depth comparison. </summary>
	public required CompareOp? DepthComparison { get; set; }

	/// <summary> Format of the depth attachment. </summary>
	public required Format DepthFormat { get; set; }

	/// <summary> Format of the stencil attachment. </summary>
	public Format StencilFormat { get; set; } = Format.Undefined;

	/// <summary> Controls whether depth writing is enabled. Depends on <see cref="DepthComparison"/>. </summary>
	public bool DepthWrite { get; set; } = true;

	/// <summary> Specifies the bounds for depth bounds testing.
	/// Setting this to non-null implicitly enables depth bounds testing, which
	/// culls pixels outside the specified range. </summary>
	public Vector2? DepthBounds { get; set; } = null;

	/// <summary> Stencil testing. The first key is the front stencil state, second is back. </summary>
	public required (StencilOpState Front, StencilOpState Back)? StencilState { get; set; }
}

public struct VertexAttribute
{
	public VertexAttribute()
	{
	}

	/// <summary> Byte offset relative to the start of the vertex buffer. </summary>
	public required int Offset { get; set; }

	/// <summary> Vertex data format for this channel. </summary>
	public required Format Format { get; set; }
}

public class VertexInputLayout
{
	/// <summary> Byte stride between consecutive elements within the buffer. </summary>
	public required int Stride { get; set; }

	/// <summary> Vertex input information. </summary>
	public required VertexAttribute[] Elements { get; set; }

	/// <summary> How many vertex elements to wait before incrementing the instance number.
	/// If set to 3, the instance ID will increment every 3 vertices, or every triangle. </summary>
	public VertexInputRate Rate { get; set; } = VertexInputRate.Vertex;
}

public class GraphicsPipelineOptions
{
	public required ShaderSet ShaderSet { get; set; }
	public required KaLayout? ResourceLayout { get; set; }
	public required DynamicState[] DynamicStates { get; set; }

	public required ColorOptions Color { get; set; }
	public DepthStencilOptions? DepthStencil { get; set; }
	public required RasterizerOptions Rasterizer { get; set; }

	public required VertexInputLayout[]? VertexInputs { get; set; }
	public required PrimitiveTopology Topology { get; set; }
	public bool PrimitiveRestart { get; set; }

	/// <summary> Anti-aliasing options. </summary>
	public MultisampleOptions? Multisample { get; set; }

	/// <summary> Bitfield of view indices describing which views are active during rendering. </summary>
	public uint ViewMask { get; set; }
}

public unsafe class KaGraphicsPipeline : IDisposable, IPipeline
{
	public required KaDevice Device { get; init; }
	public required KaLayout Layout { get; init; }
	public required VkPipeline VkPipeline { get; init; }

	public PipelineBindPoint BindPoint => PipelineBindPoint.Graphics;

	public static Result<KaGraphicsPipeline> Create( KaDevice device, GraphicsPipelineOptions options )
	{
		// TODO: Get this hack out of here :)
		// Vulkan does not like null layouts in the pipeline, so we create an empty one here
		options.ResourceLayout ??= KaLayout.Create( device, new()
		{
			Sets = []
		} );

		ShaderStage[] shaderStages = options.ShaderSet switch
		{
			VertexShaderSet vss =>
			[
				new() { Name = vss.VertexFunc, KaShader = vss.Vertex, Flags = ShaderStageFlags.VertexBit },
				new() { Name = vss.PixelFunc, KaShader = vss.Pixel, Flags = ShaderStageFlags.FragmentBit }
			],

			DepthShaderSet dss =>
			[
				new() { Name = dss.VertexFunc, KaShader = dss.Vertex, Flags = ShaderStageFlags.VertexBit }
			],

			AmplifiedMeshShaderSet amss =>
			[
				new() { Name = amss.AmplificationFunc, KaShader = amss.Amplification, Flags = ShaderStageFlags.TaskBitExt },
				new() { Name = amss.MeshFunc, KaShader = amss.Mesh, Flags = ShaderStageFlags.MeshBitExt },
				new() { Name = amss.PixelFunc, KaShader = amss.Pixel, Flags = ShaderStageFlags.FragmentBit }
			],

			MeshShaderSet mss =>
			[
				new() { Name = mss.MeshFunc, KaShader = mss.Mesh, Flags = ShaderStageFlags.MeshBitExt },
				new() { Name = mss.PixelFunc, KaShader = mss.Pixel, Flags = ShaderStageFlags.FragmentBit }
			],

			_ => []
		};

		PipelineShaderStageCreateInfo[] stages = shaderStages.Select( s => new PipelineShaderStageCreateInfo
		{
			SType = StructureType.PipelineShaderStageCreateInfo,
			Module = s.KaShader.VkShader,
			Stage = s.Flags,
			PName = s.NamePtr,
			Flags = PipelineShaderStageCreateFlags.None
		} ).ToArray();

		int tempBindingId = 0;
		VertexInputBindingDescription[] vertexBindings = options.VertexInputs switch
		{
			null => [],
			{ } vertexInputs => vertexInputs.Select( vi => new VertexInputBindingDescription
			{
				Binding = (uint)tempBindingId++,
				Stride = (uint)vi.Stride,
				InputRate = vi.Rate
			} ).ToArray()
		};

		uint GetBindingForLocation( int location )
		{
			int binding = 0;
			int soFar = 0;
			foreach ( var vertexInputLayout in options.VertexInputs )
			{
				int length = vertexInputLayout.Elements.Length;
				if ( location < soFar + length )
				{
					break;
				}

				binding++;
				soFar += length;
			}

			return (uint)binding;
		}

		int tempShaderLocation = 0;
		int tempShaderLocation2 = 0;
		VertexInputAttributeDescription[] vertexAttributes = options.VertexInputs switch
		{
			null => [],
			{ } vertexInputs => vertexInputs.SelectMany( vi => vi.Elements.Select( e => new VertexInputAttributeDescription
			{
				Binding = GetBindingForLocation( tempShaderLocation++ ),
				Location = (uint)tempShaderLocation2++,
				Format = e.Format,
				Offset = (uint)e.Offset
			} ) ).ToArray()
		};

		PipelineVertexInputStateCreateInfo vertexInput = new()
		{
			SType = StructureType.PipelineVertexInputStateCreateInfo,
			VertexBindingDescriptionCount = (uint)vertexBindings.Length,
			PVertexBindingDescriptions = vertexBindings.Length is 0 ? null : vertexBindings.AsPointer(),
			VertexAttributeDescriptionCount = (uint)vertexAttributes.Length,
			PVertexAttributeDescriptions = vertexAttributes.Length is 0 ? null : vertexAttributes.AsPointer()
		};

		PipelineInputAssemblyStateCreateInfo inputAssembly = new()
		{
			SType = StructureType.PipelineInputAssemblyStateCreateInfo,
			Topology = options.Topology,
			PrimitiveRestartEnable = options.PrimitiveRestart
		};

		PipelineDynamicStateCreateInfo dynamicState = new()
		{
			SType = StructureType.PipelineDynamicStateCreateInfo,
			DynamicStateCount = (uint)options.DynamicStates.Length,
			PDynamicStates = options.DynamicStates.Length is 0 ? null : options.DynamicStates.AsPointer()
		};

		PipelineViewportStateCreateInfo viewportState = new()
		{
			SType = StructureType.PipelineViewportStateCreateInfo,
			ViewportCount = 1,
			ScissorCount = 1
		};

		PipelineRasterizationStateCreateInfo rasterisation = new()
		{
			SType = StructureType.PipelineRasterizationStateCreateInfo,
			DepthClampEnable = options.Rasterizer.DepthClampEnable,
			RasterizerDiscardEnable = options.Rasterizer.RasterizerDiscardEnable,
			PolygonMode = options.Rasterizer.PolygonMode,
			CullMode = options.Rasterizer.CullMode,
			FrontFace = options.Rasterizer.FrontFace,
			DepthBiasEnable = options.Rasterizer.DepthBiasEnable,
			DepthBiasConstantFactor = options.Rasterizer.DepthBiasConstantFactor,
			DepthBiasClamp = options.Rasterizer.DepthBiasClamp,
			DepthBiasSlopeFactor = options.Rasterizer.DepthBiasSlopeFactor,
			LineWidth = options.Rasterizer.LineWidth,
		};

		uint[] multisampleSampleMask = options.Multisample?.SampleMask ?? [uint.MaxValue, uint.MaxValue];

		PipelineMultisampleStateCreateInfo multisample = options.Multisample switch
		{
			null => new()
			{
				SType = StructureType.PipelineMultisampleStateCreateInfo,
				RasterizationSamples = SampleCountFlags.Count1Bit,
				SampleShadingEnable = false
			},

			{ } ms => new()
			{
				SType = StructureType.PipelineMultisampleStateCreateInfo,
				RasterizationSamples = ms.RasterizationSamples,
				SampleShadingEnable = ms.MinSampleShading is not null,
				MinSampleShading = ms.MinSampleShading ?? 0.0f,
				PSampleMask = multisampleSampleMask.AsPointer(),
				AlphaToCoverageEnable = ms.AlphaToCoverage,
				AlphaToOneEnable = false // AMD does not seem to support this, so I won't bother :3c
			}
		};

		var colorAttachments = options.Color.Attachments.Select( a => a.BlendState ).ToArray();

		PipelineColorBlendStateCreateInfo colorBlendState = new()
		{
			SType = StructureType.PipelineColorBlendStateCreateInfo,
			AttachmentCount = (uint)colorAttachments.Length,
			PAttachments = colorAttachments.AsPointer(),
			LogicOpEnable = options.Color.BlendOperator is not null,
			LogicOp = options.Color.BlendOperator ?? LogicOp.Copy
		};

		colorBlendState.BlendConstants[0] = options.Color.BlendConstantsRgba.X;
		colorBlendState.BlendConstants[1] = options.Color.BlendConstantsRgba.Y;
		colorBlendState.BlendConstants[2] = options.Color.BlendConstantsRgba.Z;
		colorBlendState.BlendConstants[3] = options.Color.BlendConstantsRgba.W;

		PipelineDepthStencilStateCreateInfo depthStencilState = options.DepthStencil switch
		{
			null => new(),
			{ } ds => new()
			{
				DepthTestEnable = ds.DepthComparison is not null,
				DepthWriteEnable = ds.DepthComparison is not null && ds.DepthWrite,
				DepthCompareOp = ds.DepthComparison ?? CompareOp.Never,
				DepthBoundsTestEnable = ds.DepthBounds is not null,
				MinDepthBounds = ds.DepthBounds?.X ?? 0.0f,
				MaxDepthBounds = ds.DepthBounds?.Y ?? 0.0f,
				StencilTestEnable = ds.StencilState is not null,
				Front = ds.StencilState?.Front ?? default,
				Back = ds.StencilState?.Back ?? default
			}
		};
		depthStencilState.SType = StructureType.PipelineDepthStencilStateCreateInfo;

		var attachmentFormats = options.Color.Attachments.Select( a => a.Format ).ToArray();

		PipelineRenderingCreateInfo pipelineRendering = new()
		{
			SType = StructureType.PipelineRenderingCreateInfo,
			ViewMask = options.ViewMask,
			ColorAttachmentCount = (uint)options.Color.Attachments.Length,
			PColorAttachmentFormats = attachmentFormats.AsPointer(),
			DepthAttachmentFormat = options.DepthStencil?.DepthFormat ?? Format.Undefined,
			StencilAttachmentFormat = options.DepthStencil?.StencilFormat ?? Format.Undefined
		};

		GraphicsPipelineCreateInfo createInfo = new()
		{
			SType = StructureType.GraphicsPipelineCreateInfo,
			PNext = &pipelineRendering,
			StageCount = (uint)stages.Length,
			PStages = (uint)stages.Length is 0 ? null : stages.AsPointer(),
			PVertexInputState = &vertexInput,
			PInputAssemblyState = &inputAssembly,
			PViewportState = &viewportState,
			PRasterizationState = &rasterisation,
			PMultisampleState = &multisample,
			PColorBlendState = &colorBlendState,
			PDepthStencilState = options.DepthStencil is null ? null : &depthStencilState,
			PDynamicState = &dynamicState,
			Layout = options.ResourceLayout.VkLayout,
			RenderPass = default // Not needed since VK 1.3
		};

		VkPipeline pipeline = default;
		VkResult result = Vulkan.Vk.CreateGraphicsPipelines( device.VkDevice, default, 1, &createInfo, null, &pipeline );
		if ( result is not VkResult.Success )
		{
			return new Error( $"KaGraphicsPipeline.Create: Could not create pipeline - {result}" );
		}

		return new KaGraphicsPipeline
		{
			Device = device,
			Layout = options.ResourceLayout,
			VkPipeline = pipeline,
		};
	}

	public void Dispose()
	{
		Vulkan.Vk.DestroyPipeline( Device.VkDevice, VkPipeline, null );
	}
}
