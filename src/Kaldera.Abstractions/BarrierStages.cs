// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

namespace Kaldera.Abstractions;

[Flags]
public enum BarrierStages
{
	Transfer,
	ComputeShader,
	VertexShader,
	PixelShader,
	ColourOutput,
	DepthStencilOutput,
	Graphics,
	Everything
}

[Flags]
public enum BarrierHazards
{
	/// <summary> Memory is being read, then written to later. No special hazard. </summary>
	None = 0,

	/// <summary> Memory is being written to, memory will be read. </summary>
	ReadAfterWrite = 1,

	/// <summary> A shader is writing to draw arguments. </summary>
	DrawArguments = 2,

	/// <summary> Descriptors are being updated. </summary>
	Descriptors = 4
}

public static class BarrierStageAndHazardFlagExtensions
{
	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	private static PipelineStageFlags2 ToPipelineStageFlag( this BarrierStages self )
		=> self switch
		{
			BarrierStages.Transfer => PipelineStageFlags2.TransferBit,
			BarrierStages.ComputeShader => PipelineStageFlags2.ComputeShaderBit,
			BarrierStages.VertexShader => PipelineStageFlags2.VertexShaderBit | PipelineStageFlags2.IndexInputBit | PipelineStageFlags2.VertexInputBit,
			BarrierStages.PixelShader => PipelineStageFlags2.FragmentShaderBit,
			BarrierStages.ColourOutput => PipelineStageFlags2.ColorAttachmentOutputBit,
			BarrierStages.DepthStencilOutput => PipelineStageFlags2.EarlyFragmentTestsBit | PipelineStageFlags2.LateFragmentTestsBit,
			BarrierStages.Graphics => PipelineStageFlags2.AllGraphicsBit,
			_ => PipelineStageFlags2.AllCommandsBit
		};

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	private static void CheckStage( this BarrierStages flags, BarrierStages flag, ref PipelineStageFlags2 result )
	{
		if ( flags.HasFlag( flag ) )
		{
			result |= flag.ToPipelineStageFlag();
		}
	}

	public static PipelineStageFlags2 ToPipelineStageFlags( this BarrierStages self )
	{
		PipelineStageFlags2 flags = PipelineStageFlags2.None;
		self.CheckStage( BarrierStages.Transfer, ref flags );
		self.CheckStage( BarrierStages.ComputeShader, ref flags );
		self.CheckStage( BarrierStages.VertexShader, ref flags );
		self.CheckStage( BarrierStages.PixelShader, ref flags );
		self.CheckStage( BarrierStages.ColourOutput, ref flags );
		self.CheckStage( BarrierStages.DepthStencilOutput, ref flags );
		self.CheckStage( BarrierStages.Graphics, ref flags );
		return flags;
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	private static (AccessFlags2 Before, AccessFlags2 After) ToAccessFlag( this BarrierHazards self )
		=> self switch
		{
			BarrierHazards.ReadAfterWrite => (AccessFlags2.MemoryWriteBit, AccessFlags2.MemoryReadBit),
			BarrierHazards.DrawArguments => (AccessFlags2.ShaderWriteBit, AccessFlags2.IndirectCommandReadBit),
			BarrierHazards.Descriptors => (AccessFlags2.ShaderWriteBit, AccessFlags2.ShaderReadBit),
			_ => (AccessFlags2.None, AccessFlags2.None),
		};

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	private static void CheckHazard( this BarrierHazards flags, BarrierHazards flag, ref AccessFlags2 before, ref AccessFlags2 after )
	{
		if ( flags.HasFlag( flag ) )
		{
			(AccessFlags2 b, AccessFlags2 a) = flag.ToAccessFlag();
			before |= b;
			after |= a;
		}
	}

	public static (AccessFlags2 Before, AccessFlags2 After) ToAccessFlags( this BarrierHazards self )
	{
		if ( self is BarrierHazards.None )
		{
			return BarrierHazards.None.ToAccessFlag();
		}

		AccessFlags2 before = AccessFlags2.None, after = AccessFlags2.None;
		self.CheckHazard( BarrierHazards.ReadAfterWrite, ref before, ref after );
		self.CheckHazard( BarrierHazards.DrawArguments, ref before, ref after );
		self.CheckHazard( BarrierHazards.Descriptors, ref before, ref after );

		return (before, after);
	}
}
