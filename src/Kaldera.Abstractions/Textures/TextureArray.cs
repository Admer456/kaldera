// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Abstractions.Interfaces;
using Kaldera.Abstractions.Memory;
using Kaldera.Interfaces;
using Kaldera.Objects;

namespace Kaldera.Abstractions.Textures;

public readonly struct TextureArraySlice<T> : IGpuTexture
	where T : IGpuTexture, ITextureArrayCapable
{
	public required TextureArray<T> Owner { get; init; }
	public required VkImageView View { get; init; }

	public RawTextureState State => Owner.State with { VkImageView = View };
	public IResourceAllocator Allocator => Owner.Allocator;
	public ImageAspectFlags AspectFlags => Owner.AspectFlags;

	public ImageLayout CurrentLayout
	{
		get => Owner.CurrentLayout;
		set { } // Hack :3c
	}
}

public struct TextureArray<T> : IGpuTexture, IDisposable
	where T : IGpuTexture, ITextureArrayCapable
{
	public TextureArray()
	{
	}

	public required VkImageView[] ArrayViews { get; init; }
	public required RawTextureState State { get; init; }
	public required IResourceAllocator Allocator { get; init; }
	public required ImageAspectFlags AspectFlags { get; init; }
	public ImageLayout CurrentLayout { get; set; } = ImageLayout.Undefined;

	internal static VkImageView[] GetArrayViews( KaImage image, ImageViewOptions template )
	{
		int layers = (int)template.SubresourceRange.LayerCount;
		VkImageView[] result = new VkImageView[layers];

		template.ViewType = template.ViewType switch
		{
			ImageViewType.Type1DArray => ImageViewType.Type1D,
			ImageViewType.TypeCubeArray => ImageViewType.TypeCube,
			_ => ImageViewType.Type2D
		};

		for ( int i = 0; i < layers; i++ )
		{
			template.SubresourceRange.BaseArrayLayer = (uint)i;
			template.SubresourceRange.LayerCount = 1;
			result[i] = image.CreateView( template );
		}

		return result;
	}

	public static implicit operator TextureArray<T>?( T? self )
		=> self is null
			? null
			: new()
			{
				State = self.State,
				Allocator = self.Allocator,
				AspectFlags = self.AspectFlags,
				CurrentLayout = self.CurrentLayout,
				ArrayViews = [self.State.VkImageView]
			};

	public TextureArraySlice<T> this[ int i ]
		=> new()
		{
			Owner = this,
			View = ArrayViews[i]
		};

	public unsafe void Dispose()
	{
		foreach ( var view in ArrayViews )
		{
			// TODO: Wrap image view destruction
			Vulkan.Vk.DestroyImageView( Allocator.Device.VkDevice, view, null );
		}

		Texture.Destroy( Allocator, State );
	}
}
