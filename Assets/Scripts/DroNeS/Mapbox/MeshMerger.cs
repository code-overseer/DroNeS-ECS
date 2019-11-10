using System.Collections.Generic;
using DroNeS.Systems;
using Mapbox.Unity.MeshGeneration.Data;
using Mapbox.Unity.MeshGeneration.Modifiers;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace DroNeS.Mapbox
{
	public class MeshMerger : ModifierStackBase
    {
        private readonly Dictionary<CustomTile, int> _cacheVertexCount = new Dictionary<CustomTile, int>();
		private readonly Dictionary<CustomTile, List<MeshData>> _cached = new Dictionary<CustomTile, List<MeshData>>();
		private readonly Dictionary<CustomTile, int> _buildingCount = new Dictionary<CustomTile, int>();

		private Dictionary<CustomTile, List<RenderMesh>> _activeObjects = new Dictionary<CustomTile, List<RenderMesh>>();
		private MeshData _tempMeshData;
		private MeshData _temp2MeshData;
		private RenderMesh _tempVectorEntity;
		private ObjectPool<List<MeshData>> _meshDataPool;
		private static Material _buildingMaterial;
		private int _counter, _counter2;

		private void OnEnable()
		{
			_buildingMaterial = Resources.Load("Materials/BuildingMaterial") as Material;
			_meshDataPool = new ObjectPool<List<MeshData>>(() => new List<MeshData>());
			_tempMeshData = new MeshData();
		}

	    public override void Initialize()
		{
			_cacheVertexCount.Clear();
			_cached.Clear();
			_buildingCount.Clear();
			_counter = MeshModifiers.Count;
			for (var i = 0; i < _counter; i++)
			{
				MeshModifiers[i].Initialize();
			}
		}

	    public void Execute(CustomTile tile, CustomFeatureUnity feature, MeshData meshData, string type = "")
	    {
		    if (!_cacheVertexCount.ContainsKey(tile))
		    {
			    _cacheVertexCount.Add(tile, 0);
			    _cached.Add(tile, _meshDataPool.GetObject());
			    _buildingCount.Add(tile, 0);
		    }
		    
		    _buildingCount[tile]++;
		    _counter = MeshModifiers.Count;
		    for (var i = 0; i < _counter; i++)
		    {
			    if (MeshModifiers[i] != null && MeshModifiers[i].Active)
			    {
				    MeshModifiers[i].Run((VectorFeatureUnity)feature, meshData);
			    }
		    }
		    
		    //65000 is the vertex limit for meshes, keep stashing it until that
		    _counter = meshData.Vertices.Count;
		    if (_cacheVertexCount[tile] + _counter < 65000)
		    {
			    _cacheVertexCount[tile] += _counter;
			    _cached[tile].Add(meshData);
		    }
		    else
		    {
			    End(tile);
		    }
	    }

	    public void End(CustomTile tile)
		{
			if (!_cached.ContainsKey(tile)) return;
			_tempMeshData.Clear();

			//concat mesh data into _tempMeshData
			_counter = _cached[tile].Count;
			for (var i = 0; i < _counter; i++)
			{
				_temp2MeshData = _cached[tile][i];
				if (_temp2MeshData.Vertices.Count <= 3)  continue;

				var st = _tempMeshData.Vertices.Count;
				_tempMeshData.Vertices.AddRange(_temp2MeshData.Vertices);
				_tempMeshData.Normals.AddRange(_temp2MeshData.Normals);

				var c2 = _temp2MeshData.UV.Count;
				for (var j = 0; j < c2; j++)
				{
					if (_tempMeshData.UV.Count <= j)
					{
						_tempMeshData.UV.Add(new List<Vector2>(_temp2MeshData.UV[j].Count));
					}
				}

				c2 = _temp2MeshData.UV.Count;
				for (var j = 0; j < c2; j++)
				{
					_tempMeshData.UV[j].AddRange(_temp2MeshData.UV[j]);
				}

				c2 = _temp2MeshData.Triangles.Count;
				for (var j = 0; j < c2; j++)
				{
					if (_tempMeshData.Triangles.Count <= j)
					{
						_tempMeshData.Triangles.Add(new List<int>(_temp2MeshData.Triangles[j].Count));
					}
				}

				for (var j = 0; j < c2; j++)
				{
					for (var k = 0; k < _temp2MeshData.Triangles[j].Count; k++)
					{
						_tempMeshData.Triangles[j].Add(_temp2MeshData.Triangles[j][k] + st);
					}
				}
			}

			//update pooled vector entity with new data
			if (_tempMeshData.Vertices.Count <= 3) return;
			
			_cached[tile].Clear();
			_cacheVertexCount[tile] = 0;
			_tempVectorEntity = new RenderMesh
			{
				mesh = new Mesh(),
				material = _buildingMaterial
			};
			_tempVectorEntity.mesh.subMeshCount = _tempMeshData.Triangles.Count;
			_tempVectorEntity.mesh.SetVertices(_tempMeshData.Vertices);
			_tempVectorEntity.mesh.SetNormals(_tempMeshData.Normals);

			_counter = _tempMeshData.Triangles.Count;
			for (var i = 0; i < _counter; i++)
			{
				_tempVectorEntity.mesh.SetTriangles(_tempMeshData.Triangles[i], i);
			}

			_counter = _tempMeshData.UV.Count;
			for (var i = 0; i < _counter; i++)
			{
				_tempVectorEntity.mesh.SetUVs(i, _tempMeshData.UV[i]);
			}
			_tempVectorEntity.mesh.SetTriangles(_tempVectorEntity.mesh.triangles, 0);
			_tempVectorEntity.mesh.subMeshCount = 1;

			var pos = tile.Position;
			
			CityBuilderSystem.MakeBuilding(in pos, in _tempVectorEntity);


		}
    }
}
