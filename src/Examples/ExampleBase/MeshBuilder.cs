// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using System.Numerics;
using System.Runtime.InteropServices;
using Kaldera;
using Kaldera.Abstractions.Buffers;
using Kaldera.Abstractions.Utilities;
using Kaldera.Objects;
using Silk.NET.Vulkan;

namespace ExampleBase;

/// <summary>
/// Grid mesh building helper for examples.
/// </summary>
public static class GridMeshBuilder
{
	public delegate float HeightmapFunc( Vector2 xy );

	public delegate T PointGeneratorFunc<T>( Vector2 xy, HeightmapFunc z ) where T : unmanaged, IVertexData;

	public static Vector3 GetPosition( Vector2 xy, HeightmapFunc z )
		=> new( xy.X, xy.Y, z( xy ) );

	public static Vector3 GetNormal( Vector2 xy, HeightmapFunc z )
	{
		Vector3 a = new( xy.X, xy.Y + 0.1f, 0.0f );
		Vector3 b = new( xy.X + 0.1f, xy.Y, 0.0f );
		Vector3 c = new( xy.X - 0.1f, xy.Y, 0.0f );

		a.Z = z( new( a.X, a.Y ) );
		b.Z = z( new( b.X, b.Y ) );
		c.Z = z( new( c.X, c.Y ) );

		return -Plane.CreateFromVertices( a, b, c ).Normal;
	}

	private static T BuildVertex<T>( int width, int i, PointGeneratorFunc<T> generator, HeightmapFunc z )
		where T : unmanaged, IVertexData
	{
		float x = i % width;
		float y = (int)(i / width);

		// Centre -> (n-1)/2
		x -= (width - 1) / 2.0f;
		y -= (width - 1) / 2.0f;

		return generator( new( x, y ), z );
	}

	public static T[] BuildVertexData<T>( int width, int height, PointGeneratorFunc<T> generator, HeightmapFunc z )
		where T : unmanaged, IVertexData
	{
		if ( width * height < 128 * 128 )
		{
			return Enumerable
				.Range( 0, width * height )
				.Select( i => BuildVertex( width, i, generator, z ) )
				.ToArray();
		}

		T[][] workerCache = new T[Environment.ProcessorCount][];
		Parallel.For( 0, workerCache.Length, workerIndex =>
		{
			Utilities.CalculateWorkRange( workerIndex, workerCache.Length, width * height, out int start, out int end );

			workerCache[workerIndex] = Enumerable
				.Range( start, end - start )
				.Select( i => BuildVertex( width, i, generator, z ) )
				.ToArray();
		} );

		return workerCache.SelectMany( s => s ).ToArray();
	}

	public static uint[] BuildIndices( int width, int height )
		=> Enumerable.Range( 0, (width - 1) * (height - 1) ).SelectMany<int, uint>( i =>
		{
			int row = i / (width - 1);
			int column = i % (width - 1);

			int top = row * width;
			int bottom = (row + 1) * width;

			uint topLeft = (uint)(top + column);
			uint topRight = topLeft + 1;
			uint bottomLeft = (uint)(bottom + column);
			uint bottomRight = bottomLeft + 1;

			return [topLeft, bottomRight, topRight, topLeft, bottomLeft, bottomRight];
		} ).ToArray();
}

[StructLayout( LayoutKind.Sequential )]
public struct PosNormal : IVertexData
{
	public Vector3 Position;
	public Vector3 Normal;

	public static int SizeInBytes => VertexInputLayout.Stride;
	public static VertexAttribute[] VertexAttributes => VertexInputLayout.Elements;

	public static readonly VertexInputLayout VertexInputLayout = new()
	{
		Stride = 24, // sizeof(vec3) + sizeof(vec3)
		Elements =
		[
			new() { Offset = 0, Format = Format.R32G32B32Sfloat },
			// Normal is 12 bytes away from Position
			new() { Offset = 12, Format = Format.R32G32B32Sfloat }
		]
	};
}

[StructLayout( LayoutKind.Sequential )]
public struct PosNormalUv : IVertexData
{
	public Vector3 Position;
	public Vector3 Normal;
	public Vector2 Uv;

	public static int SizeInBytes => VertexInputLayout.Stride;
	public static VertexAttribute[] VertexAttributes => VertexInputLayout.Elements;

	public static readonly VertexInputLayout VertexInputLayout = new()
	{
		Stride = 32, // sizeof(vec3) + sizeof(vec3) + sizeof(vec2)
		Elements =
		[
			new() { Offset = 0, Format = Format.R32G32B32Sfloat },
			// Normal is 12 bytes away from Position
			new() { Offset = 12, Format = Format.R32G32B32Sfloat },
			// UV is 12 bytes away from Normal, or 24 bytes away from Position
			new() { Offset = 24, Format = Format.R32G32Sfloat }
		]
	};

	public static PosNormalUv[] From( PosNormTanColUv[] other )
		=> other.Select( v => new PosNormalUv
		{
			Position = v.Position,
			Normal = v.Normal.ToVector3(),
			Uv = v.Uv
		} ).ToArray();
}

[StructLayout( LayoutKind.Sequential )]
public struct SByte4
{
	public sbyte X, Y, Z, W;

	public Vector3 ToVector3()
		=> new( X / 255.0f, Y / 255.0f, Z / 255.0f );
}

[StructLayout( LayoutKind.Sequential )]
public struct Byte4
{
	public byte X, Y, Z, W;

	public Vector3 ToVector3()
		=> new( X / 256.0f, Y / 256.0f, Z / 256.0f );

	public Vector4 ToVector4()
		=> new( X / 256.0f, Y / 256.0f, Z / 256.0f, W / 256.0f );
}

/// <summary>
/// The full set. 32 bytes per vertex, quite GPU-friendly!
/// </summary>
[StructLayout( LayoutKind.Sequential )]
public struct PosNormTanColUv : IVertexData
{
	public Vector3 Position;
	public SByte4 Normal;
	public SByte4 Tangent;
	public Byte4 Colour;
	public Vector2 Uv;

	public static int SizeInBytes => VertexInputLayout.Stride;
	public static VertexAttribute[] VertexAttributes => VertexInputLayout.Elements;

	public static readonly VertexInputLayout VertexInputLayout = new()
	{
		Stride = 32, // sizeof(vec3) + 3*sizeof(byte4) + sizeof(vec2)
		Elements =
		[
			new() { Offset = 0, Format = Format.R32G32B32Sfloat },
			// Normal is 12 bytes away from Position
			new() { Offset = 12, Format = Format.R8G8B8A8SNorm },
			// Tangent is 4 bytes away from Normal
			new() { Offset = 16, Format = Format.R8G8B8A8SNorm },
			// Colour is 4 bytes away from Tangent
			new() { Offset = 20, Format = Format.R8G8B8A8Unorm },
			// UV is 12 bytes away from Normal, or 24 bytes away from Position
			new() { Offset = 24, Format = Format.R32G32Sfloat }
		]
	};
}

/// <summary>
/// Mesh-building helper for examples.
/// </summary>
public class MeshBuilder
{
	private List<PosNormal> mVertices = [];
	private List<uint> mIndices = [];

	public static MeshBuilder Begin()
		=> new();

	public MeshBuilder Plane( Vector3 position, Vector3 normal, Vector3 up, Vector3 right )
	{
		up *= 0.5f;
		right *= 0.5f;
		uint i = (uint)mVertices.Count;

		mVertices.Add( new()
		{
			Normal = normal,
			Position = position + up + right
		} );
		mVertices.Add( new()
		{
			Normal = normal,
			Position = position - up + right
		} );
		mVertices.Add( new()
		{
			Normal = normal,
			Position = position - up - right
		} );
		mVertices.Add( new()
		{
			Normal = normal,
			Position = position + up - right
		} );
		mIndices.AddRange( [i, i + 1, i + 2] );
		mIndices.AddRange( [i, i + 2, i + 3] );
		return this;
	}

	public MeshBuilder Cube( Vector3 position )
	{
		// Top
		Plane( position + Vector3.UnitZ * 0.5f, Vector3.UnitZ, Vector3.UnitY, Vector3.UnitX );
		// Bottom
		Plane( position - Vector3.UnitZ * 0.5f, -Vector3.UnitZ, -Vector3.UnitY, Vector3.UnitX );
		// Front 
		Plane( position + Vector3.UnitY * 0.5f, Vector3.UnitY, -Vector3.UnitZ, Vector3.UnitX );
		// Back
		Plane( position - Vector3.UnitY * 0.5f, -Vector3.UnitY, Vector3.UnitZ, Vector3.UnitX );
		// Left
		Plane( position - Vector3.UnitX * 0.5f, -Vector3.UnitX, Vector3.UnitZ, -Vector3.UnitY );
		// Right
		Plane( position + Vector3.UnitX * 0.5f, Vector3.UnitX, Vector3.UnitZ, Vector3.UnitY );

		return this;
	}

	public Result<(VertexBuffer<PosNormal> VertexBuffer, IndexBuffer IndexBuffer)> Export( UploadHelper builder )
	{
		var vertexBuffer = builder.CommitVertexBuffer( CollectionsMarshal.AsSpan( mVertices ) );
		if ( !vertexBuffer.Get( out var error, out var vertexBufferValue ) )
		{
			return error.Prepend( "Cannot create vertex buffer" );
		}

		var indexBuffer = builder.CommitIndexBuffer( CollectionsMarshal.AsSpan( mIndices ) );
		if ( !indexBuffer.Get( out error, out var indexBufferValue ) )
		{
			return error.Prepend( "Cannot create index buffer" );
		}

		return (vertexBufferValue, indexBufferValue);
	}
}
