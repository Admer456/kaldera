// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using System.Numerics;
using Kaldera;
using Kaldera.Abstractions;
using Kaldera.Abstractions.Buffers;
using Kaldera.Abstractions.Extensions;
using Kaldera.Interfaces;
using Kaldera.Objects;
using SDL3;

namespace ExampleBase;

public class Camera3D : IDisposable
{
	private Matrix4x4 mProjectionMatrix = Matrix4x4.Identity;
	private Vector2 mMouseDelta = Vector2.Zero;
	private Vector3 mDesiredDirection = Vector3.Zero;

	public required StagingBuffer StagingBuffer { get; init; }
	public required StorageBuffer UniformBuffer { get; init; }

	public Vector3 Position { get; set; } = Vector3.Zero;
	public Vector3 PitchYawRoll { get; set; } = Vector3.Zero;
	public float FieldOfView { get; set; } = 90.0f;
	public float Near { get; set; } = 0.01f;
	public float Far { get; set; } = 400.0f;

	public ref Matrix4x4 ViewProjection => ref StagingBuffer.Mapping.AsSpan<Matrix4x4>()[0];

	public static Result<Camera3D> Create<TAllocator>( TAllocator allocator )
		where TAllocator : IResourceAllocator
	{
		var staging = StagingBuffer.Create( allocator, 64 );
		if ( !staging.Get( out var error, out var stagingValue ) )
		{
			return error.Prepend( "Camera3D.Create: Could not create staging buffer" );
		}

		var storage = StorageBuffer.Create( allocator, 64 );
		if ( !storage.Get( out error, out var storageValue ) )
		{
			return error.Prepend( "Camera3D.Create: Could not create uniform buffer" );
		}

		return new Camera3D
		{
			StagingBuffer = stagingValue,
			UniformBuffer = storageValue
		};
	}

	// Copied from my engine project!
	// https://github.com/ElegyEngine/ElegyEngine/blob/master/src/Core/Elegy.Common/Maths/Coords.Angles.cs#L39C3-L58C4
	// You can obtain forward & up vectors in many ways, e.g. from a 3D rotation matrix, or using quaternions, it's up to you really
	private static void DirectionsFromRadians( Vector3 angles, out Vector3 outForward, out Vector3 outUp )
	{
		// Based on: https://github.com/Admer456/adm-utils/blob/master/src/Maths/Mat4.cpp#L52
		(float SinPitch, float CosPitch) = MathF.SinCos( -angles.X );
		(float SinYaw, float CosYaw) = MathF.SinCos( angles.Y );
		(float SinRoll, float CosRoll) = MathF.SinCos( -angles.Z );

		// Original is X-forward. X and Y here are swapped so it can be Y-forward
		outForward = new(
			SinYaw * CosPitch,
			CosYaw * CosPitch,
			-SinPitch
		);

		outUp = new(
			CosYaw * -SinRoll + SinYaw * SinPitch * CosRoll,
			-SinYaw * -SinRoll + CosYaw * SinPitch * CosRoll,
			CosPitch * CosRoll
		);
	}

	public void UploadData( KaCommandBuffer commands )
	{
		DirectionsFromRadians( PitchYawRoll * MathF.PI / 180.0f, out Vector3 camForward, out Vector3 camUp );

		var viewMatrix = Matrix4x4.CreateLookTo(
			cameraPosition: Position,
			cameraDirection: camForward,
			cameraUpVector: camUp
		);

		// ViewProjection here is really just a ref to the mapped memory in the staging buffer:
		// Mapping.AsSpan<Matrix4x4>()[0] = ...
		ViewProjection = Matrix4x4.Multiply( viewMatrix, mProjectionMatrix );

		commands.CopyBuffer( StagingBuffer, UniformBuffer );
		commands.Barrier( BarrierStages.Transfer, BarrierStages.VertexShader );
	}

	public void OnFrame( float dt )
	{
		// This can be handled in a more correct way, like checking which
		// keys are down/up etc. but this is fine
		ReadOnlySpan<bool> keyboard = SDL.GetKeyboardState( out _ );
		float speed = 1.5f;

		if ( keyboard[(int)SDL.Scancode.W] )
		{
			// Y-forward
			mDesiredDirection += Vector3.UnitY * dt * 10.0f;
		}
		if ( keyboard[(int)SDL.Scancode.S] )
		{
			mDesiredDirection -= Vector3.UnitY * dt * 10.0f;
		}
		if ( keyboard[(int)SDL.Scancode.D] )
		{
			// X-right
			mDesiredDirection += Vector3.UnitX * dt * 10.0f;
		}
		if ( keyboard[(int)SDL.Scancode.A] )
		{
			mDesiredDirection -= Vector3.UnitX * dt * 10.0f;
		}
		if ( keyboard[(int)SDL.Scancode.Space] )
		{
			// Z-up
			mDesiredDirection += Vector3.UnitZ * dt * 10.0f;
		}
		if ( keyboard[(int)SDL.Scancode.LCtrl] )
		{
			mDesiredDirection -= Vector3.UnitZ * dt * 10.0f;
		}
		if ( keyboard[(int)SDL.Scancode.LShift] )
		{
			speed = 5.0f;
		}

		PitchYawRoll += new Vector3
		{
			// X is pitch
			X = -mMouseDelta.Y * 0.05f,
			// Y is yaw (and Z is roll)
			Y = mMouseDelta.X * 0.05f
		};
		mMouseDelta = Vector2.Zero;

		DirectionsFromRadians( PitchYawRoll * MathF.PI / 180.0f, out Vector3 camForward, out Vector3 camUp );
		Vector3 camRight = Vector3.Cross( camForward, camUp );

		Position += camForward * mDesiredDirection.Y * dt * speed +
		            camRight * mDesiredDirection.X * dt * speed +
		            Vector3.UnitZ * mDesiredDirection.Z * dt * speed;

		mDesiredDirection -= mDesiredDirection * dt * 5.0f;
	}

	public void UpdateProjection( float aspectRatio )
	{
		mProjectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
			fieldOfView: FieldOfView * (MathF.PI / 180.0f),
			aspectRatio: aspectRatio,
			nearPlaneDistance: Near,
			farPlaneDistance: Far
		);
	}

	public void OnEvent( IntPtr window, SDL.Event ev )
	{
		// If RMB+mouse moved -> update PYR
		// If window changed -> redo projection matrix (new aspect ratio)
		switch ( (SDL.EventType)ev.Type )
		{
			case SDL.EventType.MouseMotion:
				if ( ev.Motion.State.HasFlag( SDL.MouseButtonFlags.Right ) )
				{
					mMouseDelta += new Vector2( ev.Motion.XRel, ev.Motion.YRel );
				}

				break;

			case SDL.EventType.WindowResized:
				SDL.GetWindowSize( window, out int width, out int height );
				UpdateProjection( (float)width / height );
				break;
		}
	}

	public void Dispose()
	{
		UniformBuffer.Dispose();
		StagingBuffer.Dispose();
	}
}
