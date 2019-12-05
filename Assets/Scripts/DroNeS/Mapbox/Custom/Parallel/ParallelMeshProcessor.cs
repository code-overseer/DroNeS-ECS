using System.Collections.Generic;
using DroNeS.Mapbox.Interfaces;
using Mapbox.Unity.Map;
using Mapbox.Unity.MeshGeneration.Data;
using Mapbox.Unity.MeshGeneration.Enums;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace DroNeS.Mapbox.Custom.Parallel
{
    public class ParallelMeshProcessor : IMeshProcessor
    {
        private readonly Dictionary<CustomTile, MeshDataStruct> _accumulation = new Dictionary<CustomTile, MeshDataStruct>();
        private readonly CustomMeshModifier[] _modifiers;
        private readonly Dictionary<CustomTile, int> _indices = new Dictionary<CustomTile, int>();
        private Material _buildingMaterial;
        
        public void SetOptions(UVModifierOptions uvOptions, GeometryExtrusionWithAtlasOptions extrusionOptions)
        {
            _modifiers[0].SetProperties(uvOptions);
            _modifiers[1].SetProperties(extrusionOptions);
            _modifiers[0].Initialize();
            _modifiers[1].Initialize();
        }

        public Material BuildingMaterial
        {
            get
            {
                if (_buildingMaterial == null) _buildingMaterial = Resources.Load("Materials/BuildingMaterial") as Material;
                return _buildingMaterial;
            }
        }
        
        public ParallelMeshProcessor()
        {
	        Application.quitting += Destroy;
            _modifiers = new[]
            {
                (CustomMeshModifier)ScriptableObject.CreateInstance<StrippedPolygonMeshModifier>(),
                ScriptableObject.CreateInstance<JobifiedTextureWallModifier>(),
            };
        }
        public void Execute(CustomTile tile, CustomFeatureUnity feature)
        {
	        var meshData = new MeshDataStruct(tile.Rect, Allocator.TempJob);
            if (!_accumulation.ContainsKey(tile))
            {
                _accumulation.Add(tile, new MeshDataStruct(default, Allocator.Persistent));
                _indices.Add(tile, 0);
            }

            foreach (var modifier in _modifiers)
            {
                modifier.Run((VectorFeatureUnity)feature, ref meshData);
            }

            if (_accumulation[tile].Vertices.Length + meshData.Vertices.Length < 65000)
            {
                Append(tile, meshData);
            }
            else
            {
                Terminate(tile, meshData);
            }

            meshData.Dispose();
        }
        
        [BurstCompile]
        private struct TriangleUpdateJob : IJobParallelFor
        {
	        private NativeArray<int> _triangles;
	        private readonly int _vert;

	        public static JobHandle Schedule(JobHandle dependencies, NativeArray<int> triangles, int verts)
	        {
		        return new TriangleUpdateJob(triangles, verts).Schedule(triangles.Length, 128, dependencies);
	        }
	        
	        private TriangleUpdateJob(NativeArray<int> triangles, int verts)
	        {
		        _triangles = triangles;
		        _vert = verts;
	        } 
	        
	        public void Execute(int index)
	        {
		        _triangles[index] = _triangles[index] + _vert;
	        }
        }

        private void Append(CustomTile tile, in MeshDataStruct data)
	    {
		    if (!_accumulation.TryGetValue(tile, out var value))
		    {
			    _accumulation.Add(tile, new MeshDataStruct(default, Allocator.Persistent));
			    _accumulation[tile].CopyFrom(data);   
			    value = _accumulation[tile];
		    }
		    if (data.Vertices.Length <= 3) return;
		    
		    TriangleUpdateJob.Schedule(default, data.Triangles, value.Vertices.Length).Complete();
		    value.Vertices.AddRange(data.Vertices);
		    value.Normals.AddRange(data.Normals);
		    value.UV.AddRange(data.UV);
		    value.Triangles.AddRange(data.Triangles);
	    }
        
	    private void MakeEntity(CustomTile tile, in MeshDataStruct value)
	    {
		    var go = new GameObject($"Building {_indices[tile]++.ToString()}");
		    go.transform.position = tile.Position;
		    go.transform.SetParent(tile.Transform, true);
		    
		    var filter = go.AddComponent<MeshFilter>();
		    filter.sharedMesh = new Mesh();
		    go.AddComponent<MeshRenderer>().sharedMaterial = BuildingMaterial;
		    
		    filter.sharedMesh.subMeshCount = 1;
		    filter.sharedMesh.vertices = value.Vertices.ToArray();
		    filter.sharedMesh.normals = value.Normals.ToArray();
		    filter.sharedMesh.triangles = value.Triangles.ToArray();
		    filter.sharedMesh.uv = value.UV.ToArray();
		    go.layer = LayerMask.NameToLayer("Buildings");
	    }
	    private void Terminate(CustomTile tile, in MeshDataStruct data)
	    {
		    if (!_accumulation.TryGetValue(tile, out var value) || value.Vertices.Length <= 3) return;
		    
		    MakeEntity(tile, value);
		    value.CopyFrom(in data);
	    }
	    
	    public void Terminate(CustomTile tile)
	    {
		    if (!_accumulation.TryGetValue(tile, out var value) || value.Vertices.Length <= 3) return;
		    
		    MakeEntity(tile, value);
		    tile.VectorDataState = TilePropertyState.Loaded;
		    _accumulation[tile].Dispose();
		    _accumulation.Remove(tile);
	    }

	    private void Destroy()
	    {
		    foreach (var tile in _indices.Keys)
		    {
			    if (!_accumulation.TryGetValue(tile, out var value)) continue;
			    value.Dispose();
		    }
	    }
    }
}