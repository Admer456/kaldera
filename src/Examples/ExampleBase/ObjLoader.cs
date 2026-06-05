// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using System.Numerics;
using Kaldera;

namespace ExampleBase;

// Warning: This is one of the messiest parsers I've ever had the misfortune of writing.
// But it's good enough! And it reuses the same vertices :)

public struct ObjSurface
{
	public PosNormalUv[] Vertices;
	public uint[] Indices;
	public string MaterialName;
	public string? DiffuseTexture;
}

public struct ObjResult
{
	public ObjSurface[] Surfaces;
}

public static class ObjLoader
{
	private struct Vertex : IEquatable<Vertex>
	{
		public int PositionId;
		public int NormalId;
		public int UvId;

		public bool Equals( Vertex other )
		{
			return PositionId == other.PositionId && NormalId == other.NormalId && UvId == other.UvId;
		}

		public override bool Equals( object? obj )
		{
			return obj is Vertex other && Equals( other );
		}

		public override int GetHashCode()
		{
			return HashCode.Combine( PositionId, NormalId, UvId );
		}
	}

	private struct Face
	{
		public int MaterialId;
		public Vertex PointA;
		public Vertex PointB;
		public Vertex PointC;
	}

	private struct Material
	{
		public required string Name;
		public string? DiffuseTexture;
	}

	private class ObjSurfaceInternal
	{
		private uint mLastIndex;

		public Dictionary<Vertex, uint> VertexMap = new();
		public List<PosNormalUv> Vertices = new();
		public List<uint> Indices = new();
		public required string MaterialName;
		public string? DiffuseTexture;

		public uint GetVertexIndexOrAdd( Vertex vertex, List<Vector3> positions, List<Vector3> normals, List<Vector2> uvs )
		{
			if ( VertexMap.TryGetValue( vertex, out uint key ) )
			{
				return key;
			}

			VertexMap[vertex] = mLastIndex;
			mLastIndex++;
			Vertices.Add( GetVertexData( vertex, positions, normals, uvs ) );
			return mLastIndex - 1;
		}

		public void AddFace( Face face, List<Vector3> positions, List<Vector3> normals, List<Vector2> uvs )
		{
			Indices.Add( GetVertexIndexOrAdd( face.PointC, positions, normals, uvs ) );
			Indices.Add( GetVertexIndexOrAdd( face.PointB, positions, normals, uvs ) );
			Indices.Add( GetVertexIndexOrAdd( face.PointA, positions, normals, uvs ) );
		}
	}

	private static PosNormalUv GetVertexData( Vertex vertex, List<Vector3> positions, List<Vector3> normals, List<Vector2> uvs )
		=> new()
		{
			Position = positions[vertex.PositionId],
			Normal = normals[vertex.NormalId],
			Uv = uvs[vertex.UvId] * new Vector2( 1.0f, -1.0f )
		};

	private static Result ParseMaterials( ReadOnlySpan<char> objPath, ReadOnlySpan<char> mtlPath, List<Material> materials )
	{
		string finalPath = Path.ChangeExtension( objPath.ToString(), ".mtl" );
		if ( !finalPath.AsSpan().EndsWith( mtlPath ) )
		{
			return new Error( $"ObjLoader.ParseMaterials: Non-standard .mtl declaration: {mtlPath}" );
		}

		if ( !File.Exists( finalPath ) )
		{
			return new Error( $"ObjLoader.ParseMaterials: File '{mtlPath}' does not exist" );
		}

		Span<Range> ranges = stackalloc Range[8];

		string[] lines = File.ReadAllLines( finalPath );
		for ( int i = 0; i < lines.Length; i++ )
		{
			ReadOnlySpan<char> line = lines[i];
			int numWords = line.Split( ranges, ' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
			if ( numWords < 2 )
			{
				continue;
			}

			ReadOnlySpan<char> firstWord = line[ranges[0]];
			ReadOnlySpan<char> secondWord = line[ranges[1]];
			switch ( firstWord )
			{
				case "newmtl":
					materials.Add( new()
					{
						Name = secondWord.ToString()
					} );
					break;

				case "map_Kd":
					materials[^1] = materials.Last() with
					{
						DiffuseTexture = secondWord.ToString()
					};
					break;
			}
		}

		return Result.Success();
	}

	private static Vertex ParseVertex( ReadOnlySpan<char> bundle )
	{
		Span<Range> ranges = stackalloc Range[3];
		bundle.Split( ranges, '/', StringSplitOptions.TrimEntries );

		return new()
		{
			// OBJ indices are not zero-based
			PositionId = int.Parse( bundle[ranges[0]] ) - 1,
			UvId = int.Parse( bundle[ranges[1]] ) - 1,
			NormalId = int.Parse( bundle[ranges[2]] ) - 1
		};
	}

	public static Result<ObjResult> Parse( string path )
	{
		if ( !File.Exists( path ) )
		{
			return new Error( $"ObjLoader.Parse: File '{path}' does not exist" );
		}

		string[] lines = File.ReadAllLines( path );

		List<Material> materials = new( 16 );
		string currentMaterial = string.Empty;
		List<Vector3> positions = new( 512 );
		List<Vector3> normals = new( 512 );
		List<Vector2> uvs = new( 512 );
		List<Face> faces = new( 512 );

		int FindMaterial( ReadOnlySpan<char> name )
		{
			for ( int i = 0; i < materials.Count; i++ )
			{
				if ( name.SequenceEqual( materials[i].Name ) )
				{
					return i;
				}
			}

			return -1;
		}

		Span<Range> ranges = stackalloc Range[8];

		// Step 1: Plainly parse everything
		for ( int i = 0; i < lines.Length; i++ )
		{
			ReadOnlySpan<char> line = lines[i];

			int numWords = line.Split( ranges, ' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
			if ( numWords < 2 )
			{
				continue;
			}

			ReadOnlySpan<char> firstWord = line[ranges[0]];
			ReadOnlySpan<char> secondWord = line[ranges[1]];
			ReadOnlySpan<char> thirdWord = numWords >= 3 ? line[ranges[2]] : default;
			ReadOnlySpan<char> fourthWord = numWords >= 4 ? line[ranges[3]] : default;
			switch ( firstWord )
			{
				case "#": continue;
				case "mtllib":
					ParseMaterials( path, secondWord, materials ).Check();
					break;
				//case "o":
				//	currentObject = secondWord.ToString();
				//	break;
				case "v":
					positions.Add( new()
					{
						X = float.Parse( secondWord ),
						Y = float.Parse( thirdWord ),
						Z = float.Parse( fourthWord )
					} );
					break;
				case "vn":
					normals.Add( new()
					{
						X = float.Parse( secondWord ),
						Y = float.Parse( thirdWord ),
						Z = float.Parse( fourthWord )
					} );
					break;
				case "vt":
					uvs.Add( new()
					{
						X = float.Parse( secondWord ),
						Y = float.Parse( thirdWord )
					} );
					break;
				case "usemtl":
					currentMaterial = secondWord.ToString();
					break;
				case "f":
					// TODO: To make this a slightly more flexible OBJ parser, you'd also want to support polygons (verts > 3)
					faces.Add( new()
					{
						MaterialId = FindMaterial( currentMaterial ),
						PointA = ParseVertex( secondWord ),
						PointB = ParseVertex( thirdWord ),
						PointC = ParseVertex( fourthWord )
					} );
					break;
			}
		}

		// Step 2: Make sense of what's been parsed and connect stuff
		List<ObjSurfaceInternal> surfaces = new( 64 );

		ObjSurfaceInternal FindOrCreateSurface( int materialId )
		{
			bool useMissingMaterial = materialId < 0 || materials.Count is 0;
			Material material = !useMissingMaterial
				? materials[materialId]
				: new()
				{
					Name = "unknown",
					DiffuseTexture = null
				};

			foreach ( ObjSurfaceInternal surface in surfaces )
			{
				if ( surface.MaterialName == material.Name )
				{
					return surface;
				}
			}

			surfaces.Add( new()
			{
				MaterialName = material.Name!, // I dunno why but the compiler insists that this may potentially be null...
				DiffuseTexture = material.DiffuseTexture
			} );

			return surfaces.Last();
		}

		for ( int i = 0; i < faces.Count; i++ )
		{
			Face face = faces[i];
			var surface = FindOrCreateSurface( face.MaterialId );
			surface.AddFace( face, positions, normals, uvs );
		}

		return new ObjResult
		{
			Surfaces = surfaces.Select( s => new ObjSurface
			{
				Vertices = s.Vertices.ToArray(),
				Indices = s.Indices.ToArray(),
				MaterialName = s.MaterialName,
				DiffuseTexture = s.DiffuseTexture
			} ).ToArray()
		};
	}
}
