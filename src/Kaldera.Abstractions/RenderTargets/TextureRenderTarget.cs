// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Abstractions.Extensions;
using Kaldera.Abstractions.Interfaces;
using Kaldera.Abstractions.Textures;
using Kaldera.Abstractions.Utilities;
using Kaldera.Interfaces;
using Kaldera.Objects;

namespace Kaldera.Abstractions.RenderTargets;

public struct TextureRenderTargetOptions
{
	public TextureRenderTargetOptions()
	{
	}

	public required int Width { get; set; }
	public required int Height { get; set; }
	public SampleCountFlags Samples { get; set; } = SampleCountFlags.Count1Bit;
	public Format ColourFormat { get; set; } = Format.B8G8R8A8Unorm;
	public int ColourLayers { get; set; } = 1;
	public int DepthLayers { get; set; } = 1;
	public bool? DepthWithStencil { get; set; } = null;

	public static TextureRenderTargetOptions Colour( int width, int height, Format colourFormat )
		=> new()
		{
			Width = width,
			Height = height,
			ColourFormat = colourFormat
		};

	public static TextureRenderTargetOptions ColourDepthStencil( int width, int height, Format colourFormat )
		=> new()
		{
			Width = width,
			Height = height,
			ColourFormat = colourFormat,
			DepthWithStencil = true
		};

	public static TextureRenderTargetOptions Multiview( int views, int width, int height, Format colourFormat )
		=> new()
		{
			Width = width,
			Height = height,
			ColourFormat = colourFormat,
			ColourLayers = views,
			DepthLayers = views,
			DepthWithStencil = true
		};
}

public class TextureRenderTarget : IDisposable, IRenderTarget
{
	private Vector2 mSize;

	public SampleCountFlags Multisampling { get; private set; }
	public required Box<TextureArray<AttachmentColour>>? ColourAttachmentsMsaa { get; set; }
	public required Box<TextureArray<AttachmentColour>> ColourAttachments { get; set; }
	public required Box<TextureArray<AttachmentDepthStencil>>? DepthStencilAttachmentsMsaa { get; set; }
	public required Box<TextureArray<AttachmentDepthStencil>>? DepthStencilAttachments { get; set; }

	public Vector2 CurrentSize => mSize;

	public Extent2D Extent => new()
	{
		Width = (uint)mSize.X,
		Height = (uint)mSize.Y
	};

	public TextureRenderTarget( Vector2 initialSize )
	{
		mSize = initialSize;
	}

	private struct AttachmentBundle
	{
		public required Box<TextureArray<AttachmentColour>>? ColourAttachmentsMsaa { get; init; }
		public required Box<TextureArray<AttachmentColour>> ColourAttachments { get; init; }
		public required Box<TextureArray<AttachmentDepthStencil>>? DepthStencilAttachmentsMsaa { get; init; }
		public required Box<TextureArray<AttachmentDepthStencil>>? DepthStencilAttachments { get; init; }
	};

	private static Result<AttachmentBundle> CreateInternal<T>( T allocator, TextureRenderTargetOptions options )
		where T : IResourceAllocator
	{
		var colour = AttachmentColour.CreateArray( allocator, options.ColourFormat, options.Width, options.Height, SampleCountFlags.Count1Bit,
			options.ColourLayers );
		if ( !colour.Get( out var error, out var colourValue ) )
		{
			return new Error( "TextureRenderTarget: Failed to create colour attachment", error );
		}

		TextureArray<AttachmentDepthStencil>? depthStencilAttachment = null;
		if ( options.DepthWithStencil is not null )
		{
			var depthStencil = AttachmentDepthStencil.CreateArray( allocator, options.Width, options.Height, options.DepthWithStencil.Value,
				SampleCountFlags.Count1Bit,
				options.DepthLayers );
			if ( !depthStencil.Get( out error, out var depthStencilValue ) )
			{
				return new Error( "TextureRenderTarget: Failed to create depth stencil attachment", error );
			}

			depthStencilAttachment = depthStencilValue;
		}

		TextureArray<AttachmentColour>? colourAttachmentMsaa = null;
		TextureArray<AttachmentDepthStencil>? depthStencilAttachmentMsaa = null;
		if ( options.Samples is not SampleCountFlags.Count1Bit )
		{
			var colourMsaa = AttachmentColour.CreateArray( allocator, options.ColourFormat, options.Width, options.Height, options.Samples,
				options.ColourLayers );
			if ( !colourMsaa.Get( out error, out var colourMsaaValue ) )
			{
				return new Error( "TextureRenderTarget: Failed to create MSAA colour attachment", error );
			}

			colourAttachmentMsaa = colourMsaaValue;

			if ( options.DepthWithStencil is not null )
			{
				var depthStencilMsaa = AttachmentDepthStencil.CreateArray( allocator, options.Width, options.Height, options.DepthWithStencil.Value,
					options.Samples,
					options.DepthLayers );
				if ( !depthStencilMsaa.Get( out error, out var depthStencilMsaaValue ) )
				{
					return new Error( "TextureRenderTarget: Failed to create MSAA depth stencil attachment", error );
				}

				depthStencilAttachmentMsaa = depthStencilMsaaValue;
			}
		}

		return new AttachmentBundle
		{
			ColourAttachments = colourValue,
			DepthStencilAttachments = depthStencilAttachment,
			ColourAttachmentsMsaa = colourAttachmentMsaa,
			DepthStencilAttachmentsMsaa = depthStencilAttachmentMsaa
		};
	}

	public static Result<TextureRenderTarget> Create<T>( T allocator, TextureRenderTargetOptions options )
		where T : IResourceAllocator
	{
		var bundle = CreateInternal( allocator, options );
		if ( !bundle.Get( out var error, out var bundleValue ) )
		{
			return new Error( "TextureRenderTarget: Failed to create", error );
		}

		return new TextureRenderTarget( initialSize: new( options.Width, options.Height ) )
		{
			ColourAttachmentsMsaa = bundleValue.ColourAttachmentsMsaa,
			ColourAttachments = bundleValue.ColourAttachments,
			DepthStencilAttachmentsMsaa = bundleValue.DepthStencilAttachmentsMsaa,
			DepthStencilAttachments = bundleValue.DepthStencilAttachments,
			Multisampling = options.Samples
		};
	}

	public Format GetColourImageFormat()
		=> ColourAttachments.Value.State.Format;

	public ClearRect GetClearInfo()
		=> new()
		{
			Rect = new()
			{
				Offset = new( 0, 0 ),
				Extent = new( (uint)mSize.X, (uint)mSize.Y )
			},
			BaseArrayLayer = 0,
			LayerCount = (uint)ColourAttachments.Value.State.ArrayLayers
		};

	private Result Rebuild( Vector2 newSize, SampleCountFlags samples )
	{
		var bundle = CreateInternal( ColourAttachments.Value.Allocator, new()
		{
			Width = (int)newSize.X,
			Height = (int)newSize.Y,
			ColourFormat = ColourAttachments.Value.State.Format,
			ColourLayers = ColourAttachments.Value.State.ArrayLayers,
			DepthLayers = DepthStencilAttachments?.Value.State.ArrayLayers ?? 0,
			DepthWithStencil = DepthStencilAttachments?.Value.State.Format switch
			{
				Format.D32Sfloat => false,
				Format.D32SfloatS8Uint => true,
				_ => null
			},
			Samples = samples
		} );
		if ( !bundle.Get( out var error, out var bundleValue ) )
		{
			return error;
		}

		// We dispose AFTER a successful recreation. Why? In case of failure, we still have a valid framebuffer
		// to fall back to, and it won't crash rendering afterwards
		Dispose();

		ColourAttachmentsMsaa = bundleValue.ColourAttachmentsMsaa;
		ColourAttachments = bundleValue.ColourAttachments;
		DepthStencilAttachmentsMsaa = bundleValue.DepthStencilAttachmentsMsaa;
		DepthStencilAttachments = bundleValue.DepthStencilAttachments;
		return Result.Success();
	}

	public Result ChangeMultisampling( SampleCountFlags samples )
	{
		if ( Multisampling == samples )
		{
			return Result.Success();
		}

		if ( !Rebuild( mSize, samples ).Get( out var error ) )
		{
			return error.Prepend( "TextureRenderTarget.ChangeMultisampling: Failed to rebuild" );
		}

		Multisampling = samples;
		return Result.Success();
	}

	public Result RequestResize( Vector2 newSize )
	{
		// Optimisation: if the new size is smaller, we don't actually need to rebuild the framebuffer
		// Idea taken from Demez's Chocolate Engine
		// if ( newSize.X < mSize.X && newSize.Y < mSize.Y )
		// {
		// 	return Result.Success();
		// }
		// mSize = Vector2.Max( mSize, newSize );

		if ( mSize == newSize )
		{
			return Result.Success();
		}

		if ( !Rebuild( newSize, Multisampling ).Get( out var error ) )
		{
			return error;
		}

		mSize = newSize;
		return Result.Success();
	}

	public void GetRenderPassContinueInfo( ref CommandBufferInheritanceRenderingInfo info, Span<Format> colourFormats )
	{
		info.ColorAttachmentCount = 1;
		colourFormats[0] = ColourAttachments.Value.State.Format;
		info.DepthAttachmentFormat = DepthStencilAttachments?.Value.State.Format ?? Format.Undefined;
		// TODO: How do i tenfil,, xwx
		info.StencilAttachmentFormat = Format.Undefined;
		info.RasterizationSamples = Multisampling;
		// TODO: Multibiew :)
		info.ViewMask = 0;
	}

	public unsafe void BeginRenderPass( KaCommandBuffer commands, bool secondaryCommandsHint )
	{
		bool msaa = ColourAttachmentsMsaa is not null;
		bool depth = DepthStencilAttachments is not null;
		bool stencil = depth && DepthStencilAttachments!.Value.AspectFlags.HasFlag( ImageAspectFlags.StencilBit );
		ImageLayout depthLayout = stencil ? ImageLayout.DepthStencilAttachmentOptimal : ImageLayout.DepthAttachmentOptimal;

		commands.TransitionTextureLayout( ref ColourAttachments.Value, ImageLayout.ColorAttachmentOptimal );
		if ( depth )
		{
			commands.TransitionTextureLayout( ref DepthStencilAttachments!.Value, depthLayout );
		}

		if ( msaa )
		{
			commands.TransitionTextureLayout( ref ColourAttachmentsMsaa!.Value, ImageLayout.ColorAttachmentOptimal );
			if ( depth )
			{
				commands.TransitionTextureLayout( ref DepthStencilAttachmentsMsaa!.Value, depthLayout );
			}
		}

		RenderingAttachmentInfo colourAttachmentInfo = new()
		{
			SType = StructureType.RenderingAttachmentInfo,
			ImageView = ColourAttachmentsMsaa?.Value.State.VkImageView ?? ColourAttachments.Value.State.VkImageView,
			ImageLayout = ImageLayout.ColorAttachmentOptimal,
			ResolveImageView = msaa ? ColourAttachments.Value.State.VkImageView : default,
			ResolveImageLayout = ImageLayout.ColorAttachmentOptimal,
			// Spec states: non-integer colour formats -> AverageBit, integer formats -> SampleZeroBit
			// So uhh... yeah... there's a giant switch in there that takes care of that
			ResolveMode = msaa ? GetResolveMode( ColourAttachments.Value.State.Format ) : ResolveModeFlags.None,
			LoadOp = AttachmentLoadOp.DontCare,
			// This small detail saves a lot of bandwidth. 1080p60fps is about 500 MB/s of uncompressed pixels. At 4x MSAA we'd be doing way more,
			// like 2.5 GB/s. Multisampled textures can't be used in pixel shaders, so we only want to store a regular sampled texture
			StoreOp = msaa ? AttachmentStoreOp.DontCare : AttachmentStoreOp.Store,
			ClearValue = new()
		};

		RenderingAttachmentInfo depthAttachmentInfo = colourAttachmentInfo with
		{
			ImageView = DepthStencilAttachmentsMsaa?.Value.State.VkImageView ?? DepthStencilAttachments?.Value.State.VkImageView ?? default,
			ImageLayout = ImageLayout.DepthStencilAttachmentOptimal,
			ResolveImageView = msaa ? DepthStencilAttachments?.Value.State.VkImageView ?? default : default,
			ResolveImageLayout = ImageLayout.DepthStencilAttachmentOptimal,
			// Depth here is either D32S8 or D32, so average it is
			ResolveMode = msaa ? ResolveModeFlags.AverageBit : ResolveModeFlags.None
		};

		RenderingInfo renderingInfo = new()
		{
			SType = StructureType.RenderingInfo,
			// Secondary command buffers are special :3c
			Flags = secondaryCommandsHint ? RenderingFlags.ContentsSecondaryCommandBuffersBit : RenderingFlags.None,
			RenderArea = new()
			{
				Offset = new( 0, 0 ),
				Extent = Extent
			},
			LayerCount = (uint)ColourAttachments.Value.State.ArrayLayers,
			ColorAttachmentCount = 1,
			PColorAttachments = &colourAttachmentInfo,
			PDepthAttachment = depth ? &depthAttachmentInfo : null,
			// TODO: Stencil buffer. Should it also just be &depthAttachmentInfo or..?
			PStencilAttachment = null
		};

		commands.BeginRendering( ref renderingInfo );
	}

	public void EndRenderPass( KaCommandBuffer commands )
	{
		commands.EndRendering();
		commands.TransitionTextureLayout( ref ColourAttachments.Value, ImageLayout.ReadOnlyOptimal );
		if ( DepthStencilAttachments is not null )
		{
			commands.TransitionTextureLayout( ref DepthStencilAttachments.Value, ImageLayout.ReadOnlyOptimal );
		}
	}

	public void Dispose()
	{
		ColourAttachmentsMsaa?.Dispose();
		ColourAttachments.Dispose();
		DepthStencilAttachmentsMsaa?.Dispose();
		DepthStencilAttachments?.Dispose();
	}

	internal static ResolveModeFlags GetResolveMode( Format imageFormat )
		=> imageFormat switch
		{
			// Yes, I did this all manually.
			// No, it only took 4 minutes.
			Format.R32Uint => ResolveModeFlags.SampleZeroBit,
			Format.R32Sint => ResolveModeFlags.SampleZeroBit,
			Format.R32G32Uint => ResolveModeFlags.SampleZeroBit,
			Format.R32G32Sint => ResolveModeFlags.SampleZeroBit,
			Format.R32G32B32Uint => ResolveModeFlags.SampleZeroBit,
			Format.R32G32B32Sint => ResolveModeFlags.SampleZeroBit,
			Format.R32G32B32A32Uint => ResolveModeFlags.SampleZeroBit,
			Format.R32G32B32A32Sint => ResolveModeFlags.SampleZeroBit,
			Format.R64Uint => ResolveModeFlags.SampleZeroBit,
			Format.R64Sint => ResolveModeFlags.SampleZeroBit,
			Format.R64G64Uint => ResolveModeFlags.SampleZeroBit,
			Format.R64G64Sint => ResolveModeFlags.SampleZeroBit,
			Format.R64G64B64Uint => ResolveModeFlags.SampleZeroBit,
			Format.R64G64B64Sint => ResolveModeFlags.SampleZeroBit,
			Format.R64G64B64A64Uint => ResolveModeFlags.SampleZeroBit,
			Format.R64G64B64A64Sint => ResolveModeFlags.SampleZeroBit,
			_ => ResolveModeFlags.AverageBit
		};
}
