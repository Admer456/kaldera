using Kaldera.Abstractions.RenderTargets;
using Kaldera.Objects;

namespace Kaldera.Abstractions.Extensions;

public static class SwapchainQueueExtensions
{
	public static void Submit( this KaQueue self, KaCommandBuffer commandBuffer, SwapchainRenderTarget renderTarget )
		=> renderTarget.SubmitCommands( self, commandBuffer );

	public static Result Present( this KaQueue self, SwapchainRenderTarget renderTarget )
		=> renderTarget.Present( self );
}
