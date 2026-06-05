
# Warning

I do gotta tell you. This obviously ain't for production. Things *will* likely change as I figure stuff out, until a 0.9.0 or 1.0.0 is out.

You can totally grab it and mess around, though! It's a playground of GAPI ideas. That being said, I think ~60% of the code now is more or less set, shouldn't really change.

So, here are some considerations and design decisions:

# Spicy C#

There is a `Result<T>` and `Error` type. Their implementation is a little scuffed (until I switch to .NET 11 or whatever has unions), but it works. Success does not allocate, `Error` allocates.

```cs
Result<Object> result = CreateObject();
if ( result.Get( out var error, out var @value ) )
{
	return error.Prepend( "Failed to create object" );
}

@value.DoStuff();
```

As an end user, you may write extension methods to perform error checking, breaking the debugger etc.

```cs
Object obj = CreateObject().Checked();
```

There's also a `Box<T>` type to convert value types into reference types.

Ideally this would be moved elsewhere, into its own library or something. I haven't even checked if anyone made something like this, surely they have. But it's okay for now.

# Kaldera - Thin Vulkan object wrapper

Instead of calling functions like `vkCmdDrawIndexed( commandBuffer, ... )`...  
you call `commandBuffer.DrawIndexed( ... )`. That's basically it.

This layer will be kinda familiar to you if you've actually used Vulkan. It's a bit like Vulkan-Hpp but for C# (which was my initial plan!), so you could totally follow some Vulkan tutorials with this.

But, the **main purpose** of it is to make the Vulkan API itself comfy, for the implementation of `Kaldera.Abstractions`.

## Some details

Something like getting physical device properties is also extremely simple. You get proper lists of strings instead of a weird C-string. As such, checking for supported extensions, layers etc. is a breeze.

Resources (like samplers and textures) are **value types**, so they can be tightly packed into arrays for heavy CPU-driven scenarios. It's less meaningful for modern GPU-driven techniques, but still nice to have.

Objects have **minimal state**. Nearly all objects are just a device reference + their actual Vulkan object (an image, buffer, whatever). There are no secrets inside, they're just wrappers. There's one little exception with command buffers (they track which pipeline has been bound), but it's small and inconsequential.

Certain Vulkan features are also omitted entirely, as IMO they're not that necessary:
* Traditional descriptor sets (not to be confused with "descriptor indexing" kinds of sets). This should be a relic of the 2010s. Push descriptors + push constants are wonderful and still plenty fast.
* Combined image-samplers. I don't feel like maintaining a feature I never use.

## Kaldera examples

Instance and device initialisation:
```cs
Result<KaInstance> vulkanInstanceResult = KaInstance.Create( new()
{
    ApplicationName = "Framerate Decimator",
	EngineName = "Ethereal Smellgine 5",
	ApiVersion = new( 1, 4, 0 ),

    // Implicitly adds VK_EXT_debug_report
	LogLevel = VulkanDebugLogLevel.PerformanceWarning,

	EnabledInstanceExtensions = [InstanceExtensionNames.KhrSurface],
	EnabledLayers = [LayerNames.KhronosValidation]
} );

... // error checking

StructureChain deviceFeatures = StructureChain.Begin( new PhysicalDeviceFeatures2
    {
        RobustBufferAccess = true,
        ...
    } )
    .Add( new PhysicalDeviceVulkan11Features
    {
        Multiview = true,
        ...
    } );

Result<KaPhysicalDevice> physicalDeviceResult = DeviceSelection.ScoreBased( vulkanInstance, deviceExtensions, deviceFeatures );

... // error checking

uint queueFamily = physicalDevice.GetFirstQueueFamilyIndex( QueueFlags.GraphicsBit );

Result<KaDevice> gpuResult = KaDevice.Create( vulkanInstance, physicalDevice, new()
{
	EnabledFeatures = deviceFeatures,
	EnabledDeviceExtensions = deviceExtensions.ToArray(),
	// Just one queue for now :)
	QueueFamilies = [new( queueFamily )]
} );
```

Memory allocation:
```cs
Result<KaBuffer> result = resourceAllocator.CreateBuffer(
	sizeInBytes,
	BufferUsageFlags.TransferDstBit | BufferUsageFlags.StorageBufferBit,
	MemoryPropertyFlags.DeviceLocalBit );

...

ImageOptions imageOptions = ImageOptions.Common( format, width, height, depth, ... );
// Memory is bound by the allocator and whatnot
Result<KaImage> result = allocator.CreateImage( imageOptions );
```

Rendering:
```cs
commands.Begin();
... // begin render pass
commands.BindPipeline( pipeline );
commands.SetViewport( 0, window.Extent, 0.0f, 1.0f );
commands.SetScissor( 0, window.Extent );
commands.SetPolygonMode( polygonMode ); // provided by ext. dyn. state 3
...
commands.DrawIndexed( indexCount, instanceCount );
... // end render pass
commands.End();
```

# Kaldera.Abstractions - Utilities and helpers

This is what I think will be the main attraction. Very basic utilities to make life easier. No need to mess with staging buffers, there's a concept of render targets etc.

Code in this layer should not touch raw Vulkan calls, only `Kaldera` objects. That way, if any core functionality is not covered by `Kaldera` above, it is immediately obvious.

## Render targets

*TODO: Write.*

## Memory utilities

*TODO: Write about UploadHelper etc.*

## API and runtime restrictions

Some GPUs support blitting/compute RW/sampling etc. on some formats, not on others, in varying degrees of support. For now, I've roughly compiled a few GPU profiles and found what they had in common.

This is why different texture types are planned: `Texture`, `Texture3D`, `TextureArray<T>`, `TextureCompressed` and such. They will each support different subsets of texture formats. In the future, this could be made more flexible, e.g. downloading GPU profile JSONs and generating some C# code from them.

The plan is to enforce some API restrictions (and a few GPU limitations) using the type system. This is why there is a `StagingBuffer` and why you cannot call `commands.BindVertexBuffer()` on it. I mean, duh, of course you wouldn't do that with a staging buffer.

Right now, the texture format stuff is not enforced. Buffer types are, as those are super simple.

The end goal is peace of mind and subconscious guidance for beginners. You shouldn't have to worry if your GPU supports some format-operation combo or not.

## Kaldera.Abstractions examples

Instance, device and queue creation:
```cs
var graphicsContextResult = Startup.CreateVulkan14Context( new()
{
	ApplicationName = "Slop Aggregator",
	EngineName = "Disarray 6.6",

	InstanceExtensions = [InstanceExtensionNames.KhrSurface],

#if DEBUG
	LogMethod = VulkanDebugLog,
	LogLevel = VulkanDebugLogLevel.Warning,
	OptionalLayers = [LayerNames.KhronosValidation]
#endif
} );

if ( !graphicsContextResult.Get( out var error, out var graphicsContext ) )
{
    // error
}

graphicsContext.Device.Instance ...
graphicsContext.Device ...
graphicsContext.Queue ...
```

Uploading data:
```cs
// Naive allocator, VMA allocator (coming soon), or one of yours!
// Usually there is only one, created at the start
IResourceAllocator allocator = SimpleAllocator.Create( queue );

UploadHelper uploader = UploadHelper.Create( allocator, capacityInBytes );

var vertexBuffer = builder.CommitVertexBuffer<PosNormalUv>( vertices );
var indexBuffer = builder.CommitIndexBuffer( indices );
var texture = builder.CommitTexture( pixelData, format, width, height, mips );

// Places barriers and submits the work to the queue
uploader.Upload();
```

Building pipeline layouts:
```cs
LayoutOptions layoutOptions = LayoutBuilder.Begin()
	.StructuredSet( s => s  // Set #0
        .UniformBuffer()    // Set #0 binding #0
        .StorageBuffer() )  // Set #0 binding #1
    .StructuredSet( s => s  // Set #1
		.SampledTexture()   // Set #1 binding #0
		.Sampler() )        // Set #1 binding #1
	.Build();
```

Render targets:
```cs
var rtOptions = TextureRenderTargetOptions.ColourDepthStencil( width, height, format );
var renderTargetResult = TextureRenderTarget.Create( allocator, rtOptions );

...

commands.Begin();
commands.RenderPass( renderTarget, () =>
{
	commands.ClearColour( renderTarget, 0, new( 0.0f, 0.13f, 0.13f, 1.0f ) );
	commands.ClearDepth( renderTarget, 0, 1.0f );
	commands.BindPipeline( pipeline );
	commands.SetViewport( 0, renderTarget.Extent, 0.0f, 1.0f );
	commands.SetScissor( 0, renderTarget.Extent );
	commands.PushUniformBuffer( camera.UniformBuffer, 0 );
	commands.BindVertexBuffer( vertexBuffer, 0 );
	commands.BindIndexBuffer( indexBuffer );
	commands.DrawIndexed( indexBuffer.Count, instanceCount: 1 );
} );
commands.End();
```