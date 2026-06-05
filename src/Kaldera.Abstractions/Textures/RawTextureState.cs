// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Objects;

namespace Kaldera.Abstractions.Textures;

public struct RawTextureState
{
	public RawTextureState( in ImageOptions options, KaImage image, VkImageView imageView )
	{
		Image = image;
		VkImageView = imageView;
		Width = options.Width;
		Height = options.Height;
		Depth = options.Depth;
		ArrayLayers = (int)options.ArrayLayers;
		MipLevels = (int)options.MipLevels;
		Format = options.Format;
	}

	public KaImage Image;
	public VkImageView VkImageView;
	public int Width;
	public int Height;
	public int Depth;
	public int ArrayLayers;
	public int MipLevels;
	public Format Format;
}
