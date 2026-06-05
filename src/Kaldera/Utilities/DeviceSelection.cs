// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using Kaldera.Objects;

namespace Kaldera.Utilities;

public static class DeviceSelection
{
	private static int CompareFeatures2( KaPhysicalDevice device, ref PhysicalDeviceFeatures2 features )
	{
		var devFeats = device.GetFeatures();
		ref var feats = ref features.Features;
		int result = 0;
		if ( devFeats.RobustBufferAccess == feats.RobustBufferAccess ) result++;
		if ( devFeats.FullDrawIndexUint32 == feats.FullDrawIndexUint32 ) result++;
		if ( devFeats.ImageCubeArray == feats.IndependentBlend ) result++;
		if ( devFeats.GeometryShader == feats.GeometryShader ) result++;
		if ( devFeats.TessellationShader == feats.TessellationShader ) result++;
		if ( devFeats.SampleRateShading == feats.SampleRateShading ) result++;
		if ( devFeats.DualSrcBlend == feats.DualSrcBlend ) result++;
		if ( devFeats.LogicOp == feats.LogicOp ) result++;
		if ( devFeats.MultiDrawIndirect == feats.MultiDrawIndirect ) result++;
		if ( devFeats.DrawIndirectFirstInstance == feats.DrawIndirectFirstInstance ) result++;
		if ( devFeats.DepthClamp == feats.DepthClamp ) result++;
		if ( devFeats.DepthBiasClamp == feats.DepthBiasClamp ) result++;
		if ( devFeats.FillModeNonSolid == feats.FillModeNonSolid ) result++;
		if ( devFeats.DepthBounds == feats.DepthBounds ) result++;
		if ( devFeats.WideLines == feats.WideLines ) result++;
		if ( devFeats.LargePoints == feats.LargePoints ) result++;
		if ( devFeats.AlphaToOne == feats.AlphaToOne ) result++;
		if ( devFeats.MultiViewport == feats.MultiViewport ) result++;
		if ( devFeats.SamplerAnisotropy == feats.SamplerAnisotropy ) result++;
		if ( devFeats.TextureCompressionEtc2 == feats.TextureCompressionEtc2 ) result++;
		if ( devFeats.TextureCompressionAstcLdr == feats.TextureCompressionAstcLdr ) result++;
		if ( devFeats.TextureCompressionBC == feats.TextureCompressionBC ) result++;
		if ( devFeats.OcclusionQueryPrecise == feats.OcclusionQueryPrecise ) result++;
		if ( devFeats.PipelineStatisticsQuery == feats.PipelineStatisticsQuery ) result++;
		if ( devFeats.VertexPipelineStoresAndAtomics == feats.VertexPipelineStoresAndAtomics ) result++;
		if ( devFeats.FragmentStoresAndAtomics == feats.FragmentStoresAndAtomics ) result++;
		if ( devFeats.ShaderTessellationAndGeometryPointSize == feats.ShaderTessellationAndGeometryPointSize ) result++;
		if ( devFeats.ShaderImageGatherExtended == feats.ShaderImageGatherExtended ) result++;
		if ( devFeats.ShaderStorageImageExtendedFormats == feats.ShaderStorageImageExtendedFormats ) result++;
		if ( devFeats.ShaderStorageImageMultisample == feats.ShaderStorageImageMultisample ) result++;
		if ( devFeats.ShaderStorageImageReadWithoutFormat == feats.ShaderStorageImageReadWithoutFormat ) result++;
		if ( devFeats.ShaderStorageImageWriteWithoutFormat == feats.ShaderStorageImageWriteWithoutFormat ) result++;
		if ( devFeats.ShaderUniformBufferArrayDynamicIndexing == feats.ShaderUniformBufferArrayDynamicIndexing ) result++;
		if ( devFeats.ShaderSampledImageArrayDynamicIndexing == feats.ShaderSampledImageArrayDynamicIndexing ) result++;
		if ( devFeats.ShaderStorageBufferArrayDynamicIndexing == feats.ShaderStorageBufferArrayDynamicIndexing ) result++;
		if ( devFeats.ShaderStorageImageArrayDynamicIndexing == feats.ShaderStorageImageArrayDynamicIndexing ) result++;
		if ( devFeats.ShaderClipDistance == feats.ShaderClipDistance ) result++;
		if ( devFeats.ShaderCullDistance == feats.ShaderCullDistance ) result++;
		if ( devFeats.ShaderFloat64 == feats.ShaderFloat64 ) result++;
		if ( devFeats.ShaderInt64 == feats.ShaderInt64 ) result++;
		if ( devFeats.ShaderResourceResidency == feats.ShaderResourceResidency ) result++;
		if ( devFeats.ShaderResourceMinLod == feats.ShaderResourceMinLod ) result++;
		if ( devFeats.SparseBinding == feats.SparseBinding ) result++;
		if ( devFeats.SparseResidencyBuffer == feats.SparseResidencyBuffer ) result++;
		if ( devFeats.SparseResidencyImage2D == feats.SparseResidencyImage2D ) result++;
		if ( devFeats.SparseResidencyImage3D == feats.SparseResidencyImage3D ) result++;
		if ( devFeats.SparseResidency2Samples == feats.SparseResidency2Samples ) result++;
		if ( devFeats.SparseResidency4Samples == feats.SparseResidency4Samples ) result++;
		if ( devFeats.SparseResidency8Samples == feats.SparseResidency8Samples ) result++;
		if ( devFeats.SparseResidency16Samples == feats.SparseResidency16Samples ) result++;
		if ( devFeats.SparseResidencyAliased == feats.SparseResidencyAliased ) result++;
		if ( devFeats.VariableMultisampleRate == feats.VariableMultisampleRate ) result++;
		if ( devFeats.InheritedQueries == feats.InheritedQueries ) result++;
		return result;
	}

	// TODO: Codegen these or something
	private static int CompareVulkan11Features( KaPhysicalDevice device, ref PhysicalDeviceVulkan11Features features )
	{
		int result = 0;
		return result;
	}

	private static int CompareVulkan12Features( KaPhysicalDevice device, ref PhysicalDeviceVulkan12Features features )
	{
		int result = 0;
		return result;
	}

	private static int CompareVulkan13Features( KaPhysicalDevice device, ref PhysicalDeviceVulkan13Features features )
	{
		int result = 0;
		return result;
	}

	private static int CompareVulkan14Features( KaPhysicalDevice device, ref PhysicalDeviceVulkan14Features features )
	{
		int result = 0;
		return result;
	}

	private static int GetScoreForFeatures( KaPhysicalDevice device, StructureChain features )
		=> features.Blobs.Sum( blob => blob switch
		{
			StructureChain.Blob<PhysicalDeviceFeatures2> unwrapped => CompareFeatures2( device, ref unwrapped.Structure ),
			StructureChain.Blob<PhysicalDeviceVulkan11Features> unwrapped => CompareVulkan11Features( device, ref unwrapped.Structure ),
			StructureChain.Blob<PhysicalDeviceVulkan12Features> unwrapped => CompareVulkan12Features( device, ref unwrapped.Structure ),
			StructureChain.Blob<PhysicalDeviceVulkan13Features> unwrapped => CompareVulkan13Features( device, ref unwrapped.Structure ),
			StructureChain.Blob<PhysicalDeviceVulkan14Features> unwrapped => CompareVulkan14Features( device, ref unwrapped.Structure ),
			_ => 0
		} );

	public static Result<KaPhysicalDevice> ScoreBased( KaInstance instance, string[]? deviceExtensions = null, StructureChain? features = null )
	{
		var devices = instance.EnumeratePhysicalDevices();
		int[] scores = new int[devices.Length];

		for ( int i = 0; i < devices.Length; i++ )
		{
			KaPhysicalDevice device = devices[i];
			var props = device.GetProperties();

			int score = props.DeviceType switch
			{
				PhysicalDeviceType.DiscreteGpu => 2000,
				PhysicalDeviceType.IntegratedGpu => 1000,
				PhysicalDeviceType.VirtualGpu => 500,
				PhysicalDeviceType.Cpu => 100,
				_ => 0
			};

			if ( deviceExtensions is not null )
			{
				string[] supportedExtensions = device.GetSupportedExtensions();

				foreach ( string extension in deviceExtensions )
				{
					if ( supportedExtensions.Contains( extension ) )
					{
						score += 10;
					}
				}
			}

			if ( features is not null )
			{
				score += GetScoreForFeatures( device, features );
			}

			scores[i] = score;
		}

		int max = scores.Max();
		for ( int i = 0; i < devices.Length; i++ )
		{
			if ( scores[i] == max )
			{
				return devices[i];
			}
		}

		return devices[0];
	}
}
