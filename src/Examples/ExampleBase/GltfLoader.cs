// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using System.Numerics;
using Kaldera;
using SharpGLTF.Schema2;
using SharpGLTF.Validation;

namespace ExampleBase;

public struct GltfSurface
{
	public PosNormTanColUv[] Vertices;
	public uint[] Indices;
	public string MaterialName;
	public string? DiffuseTexture;
}

public struct GltfMesh
{
	public string Name;
	public GltfSurface[] Surfaces;
}

public struct GltfEntity
{
	public string Name;
	public Matrix4x4 Transform;
	public int MeshId; // GltfResult.Meshes
}

public struct GltfResult
{
	public GltfMesh[] Meshes;
	public GltfEntity[] Entities;
}

public static class GltfLoader
{
	private static GltfSurface GetSurface( MeshPrimitive gltfSurface )
	{
		var va = gltfSurface.VertexAccessors;

		PosNormTanColUv[] vertices = gltfSurface.VertexAccessors["POSITION"].AsVector3Array().Select( p => new PosNormTanColUv
		{
			Position = p
		} ).ToArray();

		// Counter-clockwise vs. clockwise stuff, bleh...
		uint[] indices = gltfSurface.IndexAccessor.AsIndexArray().ToArray();
		for ( int i = 0; i < indices.Length / 3; i++ )
		{
			ref var a = ref indices[i * 3 + 1];
			ref var b = ref indices[i * 3 + 2];
			(a, b) = (b, a);
		}

		// This can be done much more cleanly, but I am showing you the very essence of the technique here
		Vector3[]? normals = va.ContainsKey( "NORMAL" ) ? va["NORMAL"].AsVector3Array().ToArray() : null;
		Vector4[]? tangents = va.ContainsKey( "TANGENT" ) ? va["TANGENT"].AsVector4Array().ToArray() : null;
		Vector2[]? uvs = va.ContainsKey( "TEXCOORD_0" ) ? va["TEXCOORD_0"].AsVector2Array().ToArray() : null;
		Vector4[]? colours = va.ContainsKey( "COLOR_0" ) ? va["COLOR_0"].AsColorArray().ToArray() : null;

		if ( normals is not null )
		{
			for ( int i = 0; i < vertices.Length; i++ )
			{
				vertices[i].Normal = new()
				{
					X = (sbyte)(normals[i].X * 127.0f),
					Y = (sbyte)(normals[i].Y * 127.0f),
					Z = (sbyte)(normals[i].Z * 127.0f),
					W = 0
				};
			}
		}

		if ( tangents is not null )
		{
			for ( int i = 0; i < vertices.Length; i++ )
			{
				vertices[i].Tangent = new()
				{
					X = (sbyte)(tangents[i].X * 127.0f),
					Y = (sbyte)(tangents[i].Y * 127.0f),
					Z = (sbyte)(tangents[i].Z * 127.0f),
					W = (sbyte)(tangents[i].W * 127.0f)
				};
			}
		}

		if ( uvs is not null )
		{
			for ( int i = 0; i < vertices.Length; i++ )
			{
				vertices[i].Uv = uvs[i];
			}
		}

		if ( colours is not null )
		{
			for ( int i = 0; i < vertices.Length; i++ )
			{
				vertices[i].Colour = new()
				{
					X = (byte)(colours[i].X * 256.0f),
					Y = (byte)(colours[i].Y * 256.0f),
					Z = (byte)(colours[i].Z * 256.0f),
					W = (byte)(colours[i].W * 256.0f),
				};
			}
		}

		string materialName = gltfSurface.Material.Name;
		string? diffuseTexture = null;
		foreach ( var channel in gltfSurface.Material.Channels )
		{
			if ( channel.Texture is null )
			{
				continue;
			}

			if ( channel.Key is "BaseColor" or "Emissive" )
			{
				var image = channel.Texture.PrimaryImage;
				diffuseTexture = image.Content.SourcePath;

				// Path fixup... we don't extract textures from GLTFs here
				if ( materialName.EndsWith( image.Name ) )
				{
					diffuseTexture = $"{materialName}.{image.Content.FileExtension}";
				}

				break;
			}
		}

		return new()
		{
			Vertices = vertices,
			Indices = indices,
			MaterialName = gltfSurface.Material.Name,
			DiffuseTexture = diffuseTexture
		};
	}

	public static Result<GltfResult> Parse( string path )
	{
		if ( !File.Exists( path ) )
		{
			return new Error( $"GltfLoader.Parse: File '{path}' does not exist" );
		}

		ModelRoot root;
		try
		{
			root = ModelRoot.Load( path, new()
			{
				Validation = ValidationMode.Skip
			} );
		}
		catch ( Exception ex )
		{
			return new Error( ex.Message );
		}

		List<GltfMesh> meshes = new( root.LogicalMeshes.Count );
		List<GltfEntity> entities = new( root.LogicalNodes.Count );

		foreach ( var node in root.LogicalNodes )
		{
			if ( node.Mesh is null )
			{
				continue;
			}

			int meshId = -1;
			// Let's see if we can reuse a mesh
			for ( int i = 0; i < meshes.Count; i++ )
			{
				if ( meshes[i].Name == node.Mesh.Name )
				{
					meshId = i;
					break;
				}
			}

			// Mesh not found, create a new one...
			if ( meshId is -1 )
			{
				List<GltfSurface> surfaces = new( node.Mesh.Primitives.Count );
				int surfaceId = 0;

				Console.WriteLine( $"Gltf: Mesh '{node.Mesh.Name}'" );
				foreach ( var surface in node.Mesh.Primitives )
				{
					Console.WriteLine( $"Gltf:  * Surface {surfaceId}" );
					Console.WriteLine( $"Gltf:    * Material {surface.Material.Name}" );

					surfaces.Add( GetSurface( surface ) );
					surfaceId++;
				}

				meshes.Add( new()
				{
					Name = node.Mesh.Name,
					Surfaces = surfaces.ToArray()
				} );

				meshId = meshes.Count - 1;
			}

			entities.Add( new()
			{
				Name = node.Name,
				Transform = node.WorldMatrix,
				MeshId = meshId,
			} );
		}

		return new GltfResult
		{
			Meshes = meshes.ToArray(),
			Entities = entities.ToArray()
		};
	}
}
