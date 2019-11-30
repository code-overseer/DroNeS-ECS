using System.Collections.Generic;
using DroNeS.Systems;
using Mapbox.Unity.MeshGeneration.Data;
using Mapbox.Unity.MeshGeneration.Enums;
using Mapbox.Unity.MeshGeneration.Modifiers;
using Unity.Rendering;
using UnityEngine;

namespace DroNeS.Mapbox.ECS
{
	public class MeshMerger : ModifierStackBase
    {
	    private readonly Dictionary<CustomTile, int> _buildingCount = new Dictionary<CustomTile, int>();
		private readonly Dictionary<CustomTile, MeshData> _accumulation = new Dictionary<CustomTile, MeshData>();
		
		private static Material _buildingMaterial;

		private void OnEnable()
		{
			_buildingMaterial = Resources.Load("Materials/BuildingMaterial") as Material;
		}

	    public override void Initialize()
	    {
		    _buildingCount.Clear();
		    foreach (var modifier in MeshModifiers)
		    {
			    modifier.Initialize();
		    }
	    }

	    public void Execute(CustomTile tile, CustomFeatureUnity feature, MeshData meshData, string type = "")
	    {
		    if (!_accumulation.ContainsKey(tile))
		    {
			    _buildingCount.Add(tile, 0);
			    _accumulation.Add(tile, new MeshData
			    {
					Edges = new List<int>(),
					Normals = new List<Vector3>(),
					Tangents = new List<Vector4>(),
					Triangles = new List<List<int>>{new List<int>()},
					UV = new List<List<Vector2>>{new List<Vector2>()},
					Vertices = new List<Vector3>()
			    });
		    }
		    
		    _buildingCount[tile]++;

		    foreach (var modifier in MeshModifiers)
		    {
			    if (modifier != null && modifier.Active)
			    {
				    modifier.Run((VectorFeatureUnity)feature, meshData);
			    }
		    }
		    
		    if (_accumulation[tile].Vertices.Count + meshData.Vertices.Count < 65000)
		    {
			    Append(tile, meshData);
		    }
		    else
		    {
			    Terminate(tile, meshData);
		    }
	    }

	    private void Append(CustomTile tile, MeshData data)
	    {
		    if (!_accumulation.TryGetValue(tile, out var value))
		    {
			    value = _accumulation[tile] = data;
		    }
		    if (data.Vertices.Count <= 3) return;
		    var st = value.Vertices.Count;
		    value.Vertices.AddRange(data.Vertices);
		    value.Normals.AddRange(data.Normals);
		    
		    for (var j = 0; j < data.UV.Count; j++)
		    {
			    if (value.UV.Count <= j)
			    {
				    value.UV.Add(new List<Vector2>(data.UV[j].Count));
			    }
		    }
		    
		    for (var j = 0; j < data.UV.Count; j++)
		    {
			    value.UV[j].AddRange(data.UV[j]);
		    }
		    
		    for (var j = 0; j < data.Triangles.Count; j++)
		    {
			    if (value.Triangles.Count <= j)
			    {
				    value.Triangles.Add(new List<int>(data.Triangles[j].Count));
			    }
		    }
		    
		    for (var j = 0; j < data.Triangles.Count; j++)
		    {
			    for (var k = 0; k < data.Triangles[j].Count; k++)
			    {
				    value.Triangles[j].Add(data.Triangles[j][k] + st);
			    }
		    }
		    
	    }

	    private void Terminate(CustomTile tile, MeshData data)
	    {
		    if (!_accumulation.TryGetValue(tile, out var value) || value.Vertices.Count <= 3) return;
		    var renderMesh = new RenderMesh
		    {
			    mesh = new Mesh(),
			    material = _buildingMaterial
		    };
		    renderMesh.mesh.subMeshCount = value.Triangles.Count;
		    renderMesh.mesh.SetVertices(value.Vertices);
		    renderMesh.mesh.SetNormals(value.Normals);
		    
		    for (var i = 0; i < value.Triangles.Count; i++)
		    {
			    renderMesh.mesh.SetTriangles(value.Triangles[i], i);
		    }

		    for (var i = 0; i < value.UV.Count; i++)
		    {
			    renderMesh.mesh.SetUVs(i, value.UV[i]);
		    }
		    renderMesh.layer = LayerMask.NameToLayer("Buildings");

		    var pos = tile.Position;
			
		    CityBuilderSystem.MakeBuilding(in pos, in renderMesh);

		    _accumulation[tile] = data;
	    }
	    
	    public void Terminate(CustomTile tile)
	    {
		    if (_accumulation.TryGetValue(tile, out var value) && value.Vertices.Count > 3)
		    {
			    var renderMesh = new RenderMesh
			    {
				    mesh = new Mesh(),
				    material = _buildingMaterial
			    };
			    renderMesh.mesh.subMeshCount = value.Triangles.Count;
			    renderMesh.mesh.SetVertices(value.Vertices);
			    renderMesh.mesh.SetNormals(value.Normals);
		    
			    for (var i = 0; i < value.Triangles.Count; i++)
			    {
				    renderMesh.mesh.SetTriangles(value.Triangles[i], i);
			    }

			    for (var i = 0; i < value.UV.Count; i++)
			    {
				    renderMesh.mesh.SetUVs(i, value.UV[i]);
			    }
			    renderMesh.layer = LayerMask.NameToLayer("Buildings");

			    var pos = tile.Position;
			
			    CityBuilderSystem.MakeBuilding(in pos, in renderMesh);
		    }
		    
		    tile.VectorDataState = TilePropertyState.Loaded;
		    _accumulation.Remove(tile);
	    }
	    
    }
}
