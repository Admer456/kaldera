// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Abstractions.Interfaces;
using Kaldera.Extensions;
using Kaldera.Objects;

namespace Kaldera.Abstractions.RenderTargets;

public enum SwapchainResult
{
	Ready,
	Skip
}

public enum BufferingType
{
	Double,
	Triple
}

public struct SwapchainRenderTargetOptions
{
	public SwapchainRenderTargetOptions()
	{
	}

	public required SurfaceKHR Surface { get; set; }
	public required Vector2 InitialSize { get; set; }
	public BufferingType Buffering { get; set; } = BufferingType.Triple;
}

public sealed unsafe class SwapchainRenderTarget : IDisposable, IRenderTarget
{
	private KaSwapchain mSwapchain;
	private SurfaceFormatKHR mSwapchainSurfaceFormat;
	private Extent2D mSwapchainExtent;
	private VkImage[] mSwapchainImages = [];
	private VkImageView[] mSwapchainImageViews = [];
	private KaSemaphore[] mPresentCompleteSemaphores = [];
	private KaSemaphore[] mRenderFinishedSemaphores = [];
	private VkFence[] mInFlightFences = [];
	private int mFrameIndex;
	private int mImageIndex;
	private Vector2 mWindowSize = Vector2.Zero;
	private bool mFramebufferResized;

	public required KaQueue PresentQueue { get; init; }
	public required SwapchainRenderTargetOptions Options { get; init; }
	public required SurfaceKHR Surface { get; init; }
	public KaDevice Device => PresentQueue.Device;
	public KaPhysicalDevice PhysicalDevice => Device.Physical;
	public VkImage CurrentImage => mSwapchainImages[mImageIndex];
	public VkImageView CurrentImageView => mSwapchainImageViews[mImageIndex];
	public Extent2D Extent => mSwapchainExtent;

	public Vector2 CurrentSize => new()
	{
		X = mSwapchainExtent.Width,
		Y = mSwapchainExtent.Height
	};

	public int FramesInFlight => Options.Buffering is BufferingType.Triple ? 3 : 2;

	public static Result<SwapchainRenderTarget> Create( KaQueue presentQueue, SwapchainRenderTargetOptions options )
	{
		var renderTarget = new SwapchainRenderTarget
		{
			Options = options,
			PresentQueue = presentQueue,
			Surface = options.Surface
		};

		if ( !renderTarget.Init().Get( out var error ) )
		{
			return error.Prepend( "SwapchainRenderTarget.Create: Failed to create swapchain" );
		}

		return renderTarget;
	}

	private Result Init()
	{
		mWindowSize = Options.InitialSize;

		if ( !CreateSwapchain( null ).Get( out var error ) )
		{
			return error;
		}

		CreateImageViews();
		CreateSyncObjects();

		return Result.Success();
	}

	private void CleanupSwapchain()
	{
		for ( int i = 0; i < mSwapchainImages.Length; i++ )
		{
			if ( mSwapchainImageViews[i].Handle is not 0 )
			{
				// TODO: Wrap image view destruction
				Vulkan.Vk.DestroyImageView( Device.VkDevice, mSwapchainImageViews[i], null );
			}
		}
	}

	private static Result<(KaSwapchain Swapchain, VkImage[] Images)> BuildSwapchain( KaDevice device, KaPhysicalDevice physicalDevice,
		SurfaceKHR surface, Extent2D extent, SurfaceFormatKHR surfaceFormat, int framesInFlight, KaSwapchain? oldSwapchain )
	{
		var surfaceCapabilities = physicalDevice.GetSurfaceCapabilities( surface );
		SwapchainCreateInfoKHR swapChainCreateInfo = new()
		{
			SType = StructureType.SwapchainCreateInfoKhr,
			Surface = surface,
			OldSwapchain = oldSwapchain?.VkSwapchain ?? new(),
			MinImageCount = ChooseSwapMinImageCount( framesInFlight, surfaceCapabilities ),
			ImageFormat = surfaceFormat.Format,
			ImageColorSpace = surfaceFormat.ColorSpace,
			ImageExtent = extent,
			ImageArrayLayers = 1,
			ImageUsage = ImageUsageFlags.ColorAttachmentBit,
			ImageSharingMode = SharingMode.Exclusive,
			PreTransform = surfaceCapabilities.CurrentTransform,
			CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
			PresentMode = ChooseSwapPresentMode( physicalDevice.GetSurfacePresentModes( surface ) ),
			Clipped = true
		};

		if ( !KaSwapchain.Create( device, swapChainCreateInfo ).Get( out var error, out var swapchain ) )
		{
			return error.Prepend( "SwapchainRenderTarget.BuildSwapchain: Could not build swapchain" );
		}

		return (swapchain, swapchain.GetImages());
	}

	private Result CreateSwapchain( KaSwapchain? oldSwapchain )
	{
		var surfaceCapabilities = PhysicalDevice.GetSurfaceCapabilities( Surface );
		mSwapchainExtent = ChooseSwapExtent( surfaceCapabilities );
		// TODO: Optimise this here, it's allocating each time
		mSwapchainSurfaceFormat = ChooseSwapSurfaceFormat( PhysicalDevice.GetSurfaceFormats( Surface ) );

		var result = BuildSwapchain( Device, PhysicalDevice, Surface, mSwapchainExtent, mSwapchainSurfaceFormat, FramesInFlight, oldSwapchain );
		if ( !result.Get( out var error, out var bundle ) )
		{
			return error.Prepend( "SwapchainRenderTarget.CreateSwapchain: Could not build swapchain" );
		}

		// TODO: Wrap swapchain destruction
		Vulkan.Swapchain.DestroySwapchain( Device.VkDevice, mSwapchain.VkSwapchain, null );

		mSwapchain = bundle.Swapchain;
		mSwapchainImages = bundle.Images;

		return Result.Success();
	}

	private void CreateImageViews()
	{
		ImageViewOptions imageViewCreateInfo = new()
		{
			ViewType = ImageViewType.Type2D,
			Format = mSwapchainSurfaceFormat.Format,
			Flags = ImageViewCreateFlags.None,
			Components = new(),
			SubresourceRange = new()
			{
				AspectMask = ImageAspectFlags.ColorBit,
				BaseMipLevel = 0,
				LevelCount = 1,
				BaseArrayLayer = 0,
				LayerCount = 1
			}
		};

		if ( mSwapchainImageViews.Length < mSwapchainImages.Length )
		{
			mSwapchainImageViews = new VkImageView[mSwapchainImages.Length];
		}

		for ( int i = 0; i < mSwapchainImages.Length; i++ )
		{
			mSwapchainImageViews[i] = KaImage.ViewFromExisting( Device, mSwapchainImages[i], imageViewCreateInfo );
		}
	}

	public Result<SwapchainResult> RecreateSwapchain()
	{
		Device.WaitIdle();
		CleanupSwapchain();
		if ( !CreateSwapchain( mSwapchain ).Get( out var error ) )
		{
			return error.Prepend( "SwapchainRenderTarget.RecreateSwapchain: Failed to rebuild swapchain" );
		}

		CreateImageViews();
		mFramebufferResized = false;
		return SwapchainResult.Skip;
	}

	public Result<SwapchainResult> Prepare()
	{
		// If we're gonna resize, just skip rendering this frame entirely, it's the simplest
		if ( mFramebufferResized )
		{
			return RecreateSwapchain();
		}

		// Waiting loop
		int secondsPassed = 0;
		while ( VkResult.Timeout == Device.WaitForFences( mInFlightFences[mFrameIndex], true, 100 * 1000 * 1000 ) )
		{
			// Tolerate up to 10 seconds, then bail out
			if ( ++secondsPassed > 10 )
			{
				return new Error( "SwapchainRenderTarget.Prepare: Timed out after 10 seconds" );
			}
		}

		Device.ResetFences( mInFlightFences[mFrameIndex] ); // TODO: Is this needed?

		VkResult result = mSwapchain.AcquireNextImage(
			ulong.MaxValue,
			mPresentCompleteSemaphores[mFrameIndex].VkSemaphore,
			out mImageIndex
		);

		if ( result is VkResult.ErrorOutOfDateKhr )
		{
			return RecreateSwapchain();
		}

		if ( result is not VkResult.Success and VkResult.SuboptimalKhr )
		{
			return new Error( $"SwapchainRenderTarget.Prepare: Failed to acquire swap chain image! {result}" );
		}

		Device.ResetFences( mInFlightFences[mFrameIndex] );
		return SwapchainResult.Ready;
	}

	public Format GetColourImageFormat()
		=> mSwapchainSurfaceFormat.Format;

	public ClearRect GetClearInfo()
		=> new()
		{
			Rect = new()
			{
				Offset = new( 0, 0 ),
				Extent = Extent
			},
			BaseArrayLayer = 0,
			LayerCount = 1
		};

	#region Command buffer extensions

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	internal void SubmitCommands( KaQueue presentQueue, KaCommandBuffer cb )
	{
		presentQueue.Submit(
			commandBuffer: cb,
			waitSemaphore: mPresentCompleteSemaphores[mFrameIndex].VkSemaphore,
			signalSemaphore: mRenderFinishedSemaphores[mImageIndex].VkSemaphore,
			fence: mInFlightFences[mFrameIndex],
			waitStage: PipelineStageFlags.ColorAttachmentOutputBit
		);
	}

	internal Result Present( KaQueue presentQueue )
	{
		VkResult result = presentQueue.Present( mRenderFinishedSemaphores[mImageIndex].VkSemaphore, mSwapchain, mImageIndex );

		if ( result is VkResult.SuboptimalKhr or VkResult.ErrorOutOfDateKhr || mFramebufferResized )
		{
			Result<SwapchainResult> swapchainResult = RecreateSwapchain();
			if ( !swapchainResult.Get( out var error, out var swapchainResultValue ) )
			{
				return error;
			}

			// Set this to true AFTER the swapchain has been SUCCESSFULLY recreated
			// Otherwise, we'll be left with an invalid swapchain
			//mFramebufferResized = false;
		}
		else if ( result is not VkResult.Success )
		{
			return new Error( $"Failed to present swapchain image: {result}" );
		}

		mFrameIndex = (mFrameIndex + 1) % FramesInFlight;
		return Result.Success();
	}

	#endregion

	public Result RequestResize( Vector2 newSize )
	{
		mFramebufferResized = true;
		mWindowSize = newSize;
		return Result.Success();
	}

	public void GetRenderPassContinueInfo( ref CommandBufferInheritanceRenderingInfo info, Span<Format> colourFormats )
	{
		info.ColorAttachmentCount = 1;
		colourFormats[0] = mSwapchainSurfaceFormat.Format;
		info.DepthAttachmentFormat = Format.Undefined;
		info.StencilAttachmentFormat = Format.Undefined;
		info.RasterizationSamples = SampleCountFlags.Count1Bit;
		info.ViewMask = 0;
	}

	public void BeginRenderPass( KaCommandBuffer commands, bool secondaryCommandsHint )
	{
		commands.TransitionImageLayout(
			CurrentImage,
			ImageLayout.Undefined,
			ImageLayout.ColorAttachmentOptimal,
			srcAccessMask: AccessFlags2.None, // no need to wait for previous operations
			dstAccessMask: AccessFlags2.ColorAttachmentWriteBit,
			srcStageMask: PipelineStageFlags2.ColorAttachmentOutputBit,
			dstStageMask: PipelineStageFlags2.ColorAttachmentOutputBit
		);

		RenderingAttachmentInfo attachmentInfo = new()
		{
			SType = StructureType.RenderingAttachmentInfo,
			ImageView = CurrentImageView,
			ImageLayout = ImageLayout.ColorAttachmentOptimal,
			LoadOp = AttachmentLoadOp.DontCare,
			StoreOp = AttachmentStoreOp.Store,
			ClearValue = new()
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
			LayerCount = 1,
			ColorAttachmentCount = 1,
			PColorAttachments = &attachmentInfo
		};

		commands.BeginRendering( ref renderingInfo );
	}

	public void EndRenderPass( KaCommandBuffer commands )
	{
		commands.EndRendering();
		commands.TransitionImageLayout(
			CurrentImage,
			ImageLayout.ColorAttachmentOptimal,
			ImageLayout.PresentSrcKhr,
			srcAccessMask: AccessFlags2.ColorAttachmentWriteBit,
			dstAccessMask: AccessFlags2.None,
			srcStageMask: PipelineStageFlags2.ColorAttachmentOutputBit,
			dstStageMask: PipelineStageFlags2.BottomOfPipeBit
		);
	}

	private static uint ChooseSwapMinImageCount( int framesInFlight, in SurfaceCapabilitiesKHR surfaceCapabilities )
	{
		var minImageCount = Math.Max( (uint)framesInFlight, surfaceCapabilities.MinImageCount );
		if ( surfaceCapabilities.MaxImageCount > 0 && surfaceCapabilities.MaxImageCount < minImageCount )
		{
			minImageCount = surfaceCapabilities.MaxImageCount;
		}

		return minImageCount;
	}

	private static SurfaceFormatKHR ChooseSwapSurfaceFormat( SurfaceFormatKHR[] availableFormats )
	{
		foreach ( var format in availableFormats )
		{
			if ( format is
			    {
				    Format: Format.B8G8R8A8Unorm,
				    ColorSpace: ColorSpaceKHR.SpaceSrgbNonlinearKhr
			    } )
			{
				return format;
			}
		}

		return availableFormats[0];
	}

	private static PresentModeKHR ChooseSwapPresentMode( PresentModeKHR[] availablePresentModes )
	{
		PresentModeKHR[] priorities =
		[
			//PresentModeKHR.FifoKhr,
			//PresentModeKHR.ImmediateKhr,
			//PresentModeKHR.FifoRelaxedKhr,

			PresentModeKHR.MailboxKhr,
			PresentModeKHR.FifoRelaxedKhr,
			PresentModeKHR.FifoKhr,
			PresentModeKHR.ImmediateKhr
		];

		foreach ( var mode in priorities )
		{
			if ( availablePresentModes.Contains( mode ) )
			{
				return mode;
			}
		}

		return availablePresentModes[0];
	}

	private Extent2D ChooseSwapExtent( in SurfaceCapabilitiesKHR capabilities )
	{
		if ( capabilities.CurrentExtent.Width != 0xFFFFFFFF )
		{
			return capabilities.CurrentExtent;
		}

		return new()
		{
			Width = Math.Clamp( (uint)mWindowSize.X, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width ),
			Height = Math.Clamp( (uint)mWindowSize.Y, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height )
		};
	}

	private void CreateSyncObjects()
	{
		if ( mRenderFinishedSemaphores.Length != mSwapchainImages.Length )
		{
			mRenderFinishedSemaphores = new KaSemaphore[mSwapchainImages.Length];
		}

		if ( mPresentCompleteSemaphores.Length != FramesInFlight )
		{
			mPresentCompleteSemaphores = new KaSemaphore[FramesInFlight];
			mInFlightFences = new VkFence[FramesInFlight];
		}

		for ( int i = 0; i < mSwapchainImages.Length; i++ )
		{
			mRenderFinishedSemaphores[i] = KaSemaphore.Create( Device );
		}

		for ( int i = 0; i < FramesInFlight; i++ )
		{
			mPresentCompleteSemaphores[i] = KaSemaphore.Create( Device );
			mInFlightFences[i] = Device.CreateFence( FenceCreateFlags.SignaledBit );
		}
	}

	public void Dispose()
	{
		mSwapchain.Dispose();

		foreach ( KaSemaphore renderFinishedSemaphore in mRenderFinishedSemaphores )
		{
			renderFinishedSemaphore.Dispose();
		}

		foreach ( KaSemaphore presentCompleteSemaphore in mPresentCompleteSemaphores )
		{
			presentCompleteSemaphore.Dispose();
		}

		foreach ( VkFence fence in mInFlightFences )
		{
			// TODO: Wrap fence destruction
			Vulkan.Vk.DestroyFence( Device.VkDevice, fence, null );
		}

		foreach ( VkImageView view in mSwapchainImageViews )
		{
			// TODO: Wrap image view destruction
			Vulkan.Vk.DestroyImageView( Device.VkDevice, view, null );
		}

		mSwapchainImages = [];
		mSwapchainImageViews = [];
		mRenderFinishedSemaphores = [];
		mPresentCompleteSemaphores = [];
		mInFlightFences = [];
		Device.Instance.DestroySurface( Surface );
	}
}
