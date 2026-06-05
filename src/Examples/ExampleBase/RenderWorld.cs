// SPDX-License-Identifier: MIT
// Copyright 2025-2026 Admer "Admer456" Šuko (admer456@gmail.com)

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Kaldera;
using Kaldera.Abstractions;
using Kaldera.Abstractions.Buffers;
using Kaldera.Abstractions.Extensions;
using Kaldera.Abstractions.Textures;
using Kaldera.Abstractions.Utilities;
using Kaldera.Objects;
using Silk.NET.Vulkan;

// Here is a simplistic "render world" representation. There is a concept of renderable objects (render entities), models and materials.
// Models are further composed of surfaces, and materials reference textures. Everything is organised neatly so it's CPU cache friendly.
// You can freely take this as a base and improve upon it.
//
// As an end user of this, you can load models, spawn entities and it is your responsibility to render them.
// There's no rendering code here, it is merely a "rendering structure".

namespace ExampleBase;

[Flags]
public enum RenderMaterialFlags
{
	None = 0,
	AlphaTest = 1,
	AlphaToCoverage = 2,
	AlphaBlend = 4,
	TwoSided = 8
}

public struct RenderMaterial
{
	public int DiffuseTextureId;
	public int NormalTextureId;
	public int ShadingTextureId;
	public RenderMaterialFlags Flags;
}

public struct RenderSurface
{
	// An optimisation that could be made here is to have one big shared vertex buffer,
	// similar to RenderEntity below
	public required VertexBuffer<PosNormTanColUv> VertexBuffer;
	public required IndexBuffer IndexBuffer;
	public required int MaterialId;
	public required Vector4 Bounds; // for occlusion culling

	public Vector3 BoxHalfExtents => new( Bounds.X, Bounds.Y, Bounds.Z );
	public float SphereRadius => Bounds.W;
}

public struct RenderModel
{
	public required string Name;
	public required int FirstRenderSurface;
	public required int NumRenderSurfaces;
}

[StructLayout( LayoutKind.Sequential )]
public struct RenderEntityState
{
	public required Matrix4x4 Transform;
	// These are generic rendering parametres. It's probably a bit too much,
	// but it satisfies the 64-bit alignment. You can view them as padding instead
	public Vector4 Parameters1;
	public Vector4 Parameters2;
	public Vector4 Parameters3;
	public Vector4 Parameters4;

	public Vector3 GetPosition()
		=> Transform.Translation;
}

public struct RenderEntity
{
	public required BufferSlice<StorageBuffer> EntityBuffer;
	public required int RenderModelId;
	public required Vector4 Bounds;

	public Vector3 BoxHalfExtents => new( Bounds.X, Bounds.Y, Bounds.Z );
	public float SphereRadius => Bounds.W;
}

public class RenderWorld
{
	private UploadHelper mUploader;
	private List<string> mTextureNames = [];
	private List<Texture> mTextures = [];
	private List<string> mMaterialNames = [];
	private List<RenderMaterial> mMaterials = [];
	private List<RenderSurface> mSurfaces = [];
	private List<RenderModel> mModels = [];
	private List<bool> mEntityStatesDirty = [];
	private StorageBuffer mEntitySharedBuffer;
	private StagingBuffer mEntityStagingBuffer;
	private List<RenderEntity> mEntities = [];

	public const int BuiltinTextureSize = 16;
	public const int MissingTexture = 0;
	public const int BlackTexture = 1;
	public const int GrayTexture = 2;
	public const int WhiteTexture = 3;
	public const int FlatTexture = 4;

	public RenderWorld( UploadHelper helper, int numEntities = 32768 )
	{
		mUploader = helper;

		mEntitySharedBuffer = StorageBuffer.Create( helper.Allocator, Unsafe.SizeOf<RenderEntityState>() * numEntities ).Checked();
		mEntityStagingBuffer = StagingBuffer.Create( helper.Allocator, Unsafe.SizeOf<RenderEntityState>() * numEntities ).Checked();
	}

	public Span<Texture> Textures => CollectionsMarshal.AsSpan( mTextures );
	public Span<RenderMaterial> Materials => CollectionsMarshal.AsSpan( mMaterials );
	public Span<RenderSurface> Surfaces => CollectionsMarshal.AsSpan( mSurfaces );
	public Span<RenderModel> Models => CollectionsMarshal.AsSpan( mModels );
	public Span<RenderEntityState> EntityStates => mEntityStagingBuffer.Mapping.AsSpan<RenderEntityState>();
	public Span<RenderEntity> Entities => CollectionsMarshal.AsSpan( mEntities );

	private static byte[] CreateMissingTexturePattern()
	{
		byte[] palette =
		[
			0, 0, 0, 255, // Black
			0, 255, 255, 255, // Cyan
			255, 0, 255, 255, // Magenta
			255, 255, 0, 255 // Yellow
		];

		// "Missing texture" texture
		byte[] pixelData = new byte[BuiltinTextureSize * BuiltinTextureSize * 4];
		for ( int y = 0; y < BuiltinTextureSize; y++ )
		{
			for ( int x = 0; x < BuiltinTextureSize; x++ )
			{
				int i = y * BuiltinTextureSize + x;
				// Quick little "forward diagonal step" formula
				int colour = Math.Abs( (x % 4) + (y % -4) ) % 4;

				i *= 4;
				colour *= 4;

				pixelData[i] = palette[colour];
				pixelData[i + 1] = palette[colour + 1];
				pixelData[i + 2] = palette[colour + 2];
				pixelData[i + 3] = palette[colour + 3];
			}
		}

		return pixelData;
	}

	public static byte[] CreateFilledRgba( int width, int height, byte r, byte g, byte b, byte a )
		=> Enumerable.Range( 0, width * height * 4 ).Select( i => (i % 4) switch
		{
			0 => r,
			1 => g,
			2 => b,
			_ => a
		} ).ToArray();

	public void CreateDefaultResources()
	{
		int width = BuiltinTextureSize;
		int height = BuiltinTextureSize;
		mTextureNames.Add( "_missing" );
		mTextures.Add( mUploader.CommitTexture( CreateMissingTexturePattern(), Format.B8G8R8A8Unorm, width, height, 1 ).Checked() );
		mTextureNames.Add( "_black" );
		mTextures.Add( mUploader.CommitTexture( CreateFilledRgba( width, height, 0, 0, 0, 255 ), Format.B8G8R8A8Unorm, width, height, 1 ).Checked() );
		mTextureNames.Add( "_gray" );
		mTextures.Add( mUploader.CommitTexture( CreateFilledRgba( width, height, 127, 127, 127, 255 ), Format.B8G8R8A8Unorm, width, height, 1 ).Checked() );
		mTextureNames.Add( "_white" );
		mTextures.Add( mUploader.CommitTexture( CreateFilledRgba( width, height, 255, 255, 255, 255 ), Format.B8G8R8A8Unorm, width, height, 1 ).Checked() );
		mTextureNames.Add( "_flat" );
		mTextures.Add( mUploader.CommitTexture( CreateFilledRgba( width, height, 127, 127, 255, 255 ), Format.B8G8R8A8Unorm, width, height, 1 ).Checked() );
	}

	private static string? GetFirstExistingTextureExtension( string pathWithoutExtension )
	{
		if ( File.Exists( $"{pathWithoutExtension}.ktx" ) )
		{
			return ".ktx";
		}

		if ( File.Exists( $"{pathWithoutExtension}.png" ) )
		{
			return ".png";
		}

		if ( File.Exists( $"{pathWithoutExtension}.dds" ) )
		{
			return ".dds";
		}

		if ( File.Exists( $"{pathWithoutExtension}.jpg" ) )
		{
			return ".jpg";
		}

		if ( File.Exists( $"{pathWithoutExtension}.jpeg" ) )
		{
			return ".jpeg";
		}

		if ( File.Exists( $"{pathWithoutExtension}.tga" ) )
		{
			return ".tga";
		}

		if ( File.Exists( $"{pathWithoutExtension}.bmp" ) )
		{
			return ".bmp";
		}

		return null;
	}

	public int LoadMaterial( string materialName, string materialDefinitionPath )
	{
		int diffuseTexture = MissingTexture;
		int normalTexture = FlatTexture;
		int shadingTexture = BlackTexture;
		RenderMaterialFlags flags = RenderMaterialFlags.None;

		string[] lines = File.ReadAllLines( materialDefinitionPath );
		Span<Range> ranges = stackalloc Range[2];
		foreach ( var line in lines )
		{
			ReadOnlySpan<char> span = line;
			int numWords = span.Split( ranges, ' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
			if ( numWords is 0 )
			{
				continue;
			}

			ReadOnlySpan<char> firstWord = span[ranges[0]];
			ReadOnlySpan<char> secondWord = numWords >= 2 ? span[ranges[1]] : default;

			switch ( firstWord )
			{
				case "albedoMap":
				case "baseColorMap":
				case "baseMap":
				case "diffuseMap":
					diffuseTexture = GetOrLoadTexture( secondWord.ToString() );
					break;
				case "bumpMap":
				case "normalMap":
					normalTexture = GetOrLoadTexture( secondWord.ToString() );
					break;
				case "shadingMap":
				case "specularMap":
				case "roughnessMap":
					shadingTexture = GetOrLoadTexture( secondWord.ToString() );
					break;
				case "alphaBlend":
					flags |= RenderMaterialFlags.AlphaBlend;
					break;
				case "alphaTest":
					flags |= RenderMaterialFlags.AlphaTest;
					break;
				case "alphaToCoverage":
					flags |= RenderMaterialFlags.AlphaToCoverage;
					break;
				case "twoSided":
					flags |= RenderMaterialFlags.TwoSided;
					break;
			}
		}

		mMaterialNames.Add( materialName );
		mMaterials.Add( new()
		{
			DiffuseTextureId = diffuseTexture,
			NormalTextureId = normalTexture,
			ShadingTextureId = shadingTexture,
			Flags = flags
		} );

		return mMaterials.Count - 1;
	}

	public int LoadSimplePbrMaterial( string materialName, string textureBasePath )
	{
		int diffuseTexture = MissingTexture;
		int normalTexture = FlatTexture;
		int shadingTexture = BlackTexture;

		string? diffuseExtension = GetFirstExistingTextureExtension( $"{textureBasePath}_d" );
		string? normalExtension = GetFirstExistingTextureExtension( $"{textureBasePath}_n" );
		string? shadingExtension = GetFirstExistingTextureExtension( $"{textureBasePath}_s" );

		if ( diffuseExtension is not null )
		{
			diffuseTexture = GetOrLoadTexture( $"{textureBasePath}_d{diffuseExtension}" );
		}

		if ( normalExtension is not null )
		{
			normalTexture = GetOrLoadTexture( $"{textureBasePath}_n{normalExtension}" );
		}

		if ( shadingExtension is not null )
		{
			shadingTexture = GetOrLoadTexture( $"{textureBasePath}_s{shadingExtension}" );
		}

		mMaterialNames.Add( materialName );
		mMaterials.Add( new()
		{
			DiffuseTextureId = diffuseTexture,
			NormalTextureId = normalTexture,
			ShadingTextureId = shadingTexture,
			Flags = RenderMaterialFlags.None
		} );

		return mMaterials.Count - 1;
	}

	public int LoadSimpleMaterial( string materialName, string imagePath )
	{
		int diffuseTexture = GetOrLoadTexture( imagePath );
		mMaterialNames.Add( materialName );
		mMaterials.Add( new()
		{
			DiffuseTextureId = diffuseTexture,
			NormalTextureId = FlatTexture,
			ShadingTextureId = BlackTexture
		} );

		return mMaterials.Count - 1;
	}

	public int GetOrLoadMaterial( string path )
	{
		if ( mMaterialNames.IndexOf( path ) is var id and not -1 )
		{
			return id;
		}

		// For a given input: "textures/example"
		// ...three configurations can be had:
		// 1) textures/example.mat which references example_d.png, example_n.png etc.
		string materialDefinitionPath = $"{path}.mat";
		if ( File.Exists( materialDefinitionPath ) )
		{
			return LoadMaterial( path, materialDefinitionPath );
		}

		// 2) textures/example_d.png, accompanied by others
		string diffusePath = $"{path}_d";
		string? diffuseExtension = GetFirstExistingTextureExtension( diffusePath );
		if ( diffuseExtension is not null )
		{
			return LoadSimplePbrMaterial( path, path );
		}

		// 3) textures/example.png which is a single texture
		string? firstExtension = GetFirstExistingTextureExtension( path );
		if ( firstExtension is not null )
		{
			return LoadSimpleMaterial( path, $"{path}{firstExtension}" );
		}

		return 0;
	}

	public int GetOrLoadTexture( string path )
	{
		if ( mTextureNames.IndexOf( path ) is var id and not -1 )
		{
			return id;
		}

		if ( !File.Exists( path ) )
		{
			Console.WriteLine( $"WARNING: Texture '{path}' is missing" );
			return MissingTexture;
		}

		(int width, int height, byte[] data) = Utilities.LoadTexture( path ).Checked();
		Texture texture = mUploader.CommitTexture( data.AsSpan(), Format.R8G8B8A8Unorm, width, height, 1 ).Checked();
		mTextures.Add( texture );
		mTextureNames.Add( path );
		return mTextures.Count - 1;
	}

	public List<int> GetOrLoadModel( string path )
	{
		string ext = Path.GetExtension( path );
		if ( ext is ".glb" or ".gltf" )
		{
			return LoadGltf( path );
		}

		new Error( $"WARNING: RenderWorld.GetOrLoadModel: Asset format not supported: {path}" ).PrintError();
		return [];
	}

	public List<int> LoadGltf( string path )
	{
		var gltfResultResult = GltfLoader.Parse( path );
		if ( !gltfResultResult.Get( out var error, out var gltfResult ) )
		{
			error.PrintError();
			return [];
		}

		Debug.Assert( gltfResult.Meshes.Length is not 0 );

		List<int> renderModelIds = new( gltfResult.Meshes.Length );
		foreach ( GltfMesh mesh in gltfResult.Meshes )
		{
			int? firstSurface = null;
			foreach ( GltfSurface surface in mesh.Surfaces )
			{
				int materialId = GetOrLoadMaterial( surface.MaterialName );
				int surfaceId = AddRenderSurface( surface.Vertices, surface.Indices, materialId );
				firstSurface ??= surfaceId;
			}

			Debug.Assert( firstSurface is not null );

			renderModelIds.Add( AddRenderModel( mesh.Name, firstSurface.Value, mesh.Surfaces.Length ) );
		}

		return renderModelIds;
	}

	public List<int> LoadGltfEntities( string path, int firstModelId )
	{
		var gltfResultResult = GltfLoader.Parse( path );
		if ( !gltfResultResult.Get( out var error, out var gltfResult ) )
		{
			error.PrintError();
			return [];
		}

		Debug.Assert( gltfResult.Entities.Length is not 0 );

		List<int> entityIds = new( gltfResult.Entities.Length );
		foreach ( GltfEntity entity in gltfResult.Entities )
		{
			entityIds.Add( AddRenderEntity( firstModelId + entity.MeshId, new()
			{
				Transform = entity.Transform
			} ) );
		}

		return entityIds;
	}

	public int AddRenderSurface( PosNormTanColUv[] vertices, uint[] indices, int materialId )
	{
		Span<PosNormTanColUv> vertexSpan = vertices;

		VertexBuffer<PosNormTanColUv> vertexBuffer = mUploader.CommitVertexBuffer( vertexSpan ).Checked();
		IndexBuffer indexBuffer = mUploader.CommitIndexBuffer( indices.AsSpan() ).Checked();

		Vector3 bounds = Vector3.Zero;
		for ( int i = 0; i < vertexSpan.Length; i++ )
		{
			Vector3 pos = Vector3.Abs( vertexSpan[i].Position );
			bounds = Vector3.Max( bounds, pos );
		}

		mSurfaces.Add( new()
		{
			VertexBuffer = vertexBuffer,
			IndexBuffer = indexBuffer,
			MaterialId = materialId,
			Bounds = new( bounds.X, bounds.Y, bounds.Z, bounds.Length() )
		} );

		return mSurfaces.Count - 1;
	}

	public int AddRenderModel( string name, int firstSurface, int numSurfaces )
	{
		mModels.Add( new()
		{
			Name = name,
			FirstRenderSurface = firstSurface,
			NumRenderSurfaces = numSurfaces
		} );

		return mModels.Count - 1;
	}

	public int AddRenderEntity( int renderModelId, RenderEntityState state )
	{
		mEntities.Add( new()
		{
			EntityBuffer = new()
			{
				Buffer = mEntitySharedBuffer,
				Length = (ulong)Unsafe.SizeOf<RenderEntityState>(),
				Start = (ulong)(Unsafe.SizeOf<RenderEntityState>() * mEntities.Count)
			},
			RenderModelId = renderModelId,
			Bounds = Surfaces[Models[renderModelId].FirstRenderSurface].Bounds
		} );

		int entityId = mEntities.Count - 1;
		EntityStates[entityId] = state;
		mEntityStatesDirty.Add( true );
		return entityId;
	}

	public void SetRenderEntityState( int id, in RenderEntityState state )
	{
		EntityStates[id] = state;
		mEntityStatesDirty[id] = true;
	}

	public void UpdateBuffers( KaCommandBuffer commands )
	{
		if ( !mEntityStatesDirty.TrueForAll( s => !s ) )
		{
			commands.CopyBuffer( mEntityStagingBuffer, mEntitySharedBuffer );
			commands.Barrier( BarrierStages.Transfer, BarrierStages.VertexShader );

			Span<bool> entityStatesDirty = CollectionsMarshal.AsSpan( mEntityStatesDirty );
			for ( int i = 0; i < entityStatesDirty.Length; i++ )
			{
				entityStatesDirty[i] = false;
			}
		}
	}

	public void Dispose()
	{
		foreach ( var texture in mTextures )
		{
			texture.Dispose();
		}

		foreach ( var surface in mSurfaces )
		{
			surface.IndexBuffer.Dispose();
			surface.VertexBuffer.Dispose();
		}

		mEntityStagingBuffer.Dispose();
		mEntitySharedBuffer.Dispose();
	}
}
