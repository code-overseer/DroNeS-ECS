using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using DroNeS.Mapbox.Interfaces;
using DroNeS.Utils.Interfaces;
using DroNeS.Utils.Time;
using Mapbox.Unity.Map;
using Mapbox.Unity.MeshGeneration.Enums;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace DroNeS.Mapbox.Custom.Parallel
{
    public class ParallelMeshProcessor : IMeshProcessor
    {
	    private readonly HashSet<CustomTile> _processing = new HashSet<CustomTile>();
        private readonly Dictionary<CustomTile, MeshDataStruct> _accumulation = new Dictionary<CustomTile, MeshDataStruct>();
        private readonly Dictionary<CustomTile, int> _indices = new Dictionary<CustomTile, int>();
        private readonly Dictionary<CustomTile, Queue<CustomFeatureUnity>> _queue = new Dictionary<CustomTile, Queue<CustomFeatureUnity>>();
        private Material _buildingMaterial;
        private UVModifierOptions _uvOptions;
        private GeometryExtrusionWithAtlasOptions _atlasOptions;
        private WaitForFixedUpdate _fixed;
        
        public void SetOptions(UVModifierOptions uvOptions, GeometryExtrusionWithAtlasOptions extrusionOptions)
        {
	        _uvOptions = uvOptions;
	        _atlasOptions = extrusionOptions;
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
	        _fixed = new WaitForFixedUpdate();
        }

        private int _count = 0;
        public void Enqueue(CustomTile tile, CustomFeatureUnity feature)
        {
	        if (!_processing.Contains(tile))
            {
	            _processing.Add(tile);
                _accumulation.Add(tile, new MeshDataStruct(default, Allocator.Persistent));
                _indices.Add(tile, 0);
                _queue.Add(tile, new Queue<CustomFeatureUnity>());
                ++_count;
            }
			_queue[tile].Enqueue(feature);
        }

        public IEnumerator RunJob(CustomTile tile)
        {
	        if (!_queue.TryGetValue(tile, out var queue)) yield break;
	        var b = queue.Count == 254;
	        while (queue.Count > 0)
	        {
		        var feature = queue.Dequeue();
		        var meshData = new MeshDataStruct(tile.Rect, Allocator.Persistent);
		        var timer = new CustomTimer().Start();
		        var handle = PolygonMeshModifierJob.Schedule(default, _uvOptions, feature, ref meshData); 
			    handle = TextureSideWallModifierJob.Schedule(handle, _atlasOptions, feature, ref meshData);
			    while (!handle.IsCompleted)
			    {
				    if (b) Debug.Log($"Queue Count {queue.Count.ToString()}");
				    yield return _fixed;
				    timer.Restart();
			    }
		        handle.Complete();
		        
		        if (_accumulation[tile].Vertices.Length + meshData.Vertices.Length < 65000)
		        {
			        Append(tile, meshData);
		        }
		        else
		        {
			        Terminate(tile, meshData);
		        }
		        meshData.Dispose();
		        
		        if (timer.ElapsedMilliseconds > 8) yield return _fixed;
	        }
	        Terminate(tile);
        }
        
        [BurstCompile]
        private struct TriangleUpdateJob : IJobParallelFor
        {
	        private NativeArray<int> _triangles;
	        private readonly int _vert;

	        public static JobHandle Schedule(JobHandle dependencies, NativeArray<int> triangles, int verts)
	        {
		        return new TriangleUpdateJob(triangles, verts).Schedule(triangles.Length, 64, dependencies);
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
		    if (!_accumulation.TryGetValue(tile, out var value) || value.Vertices.Length <= 3) return;
		    
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
	    
	    private void Terminate(CustomTile tile)
	    {
		    if (!_accumulation.TryGetValue(tile, out var value) || value.Vertices.Length <= 3) return;
		    
		    MakeEntity(tile, value);
		    tile.VectorDataState = TilePropertyState.Loaded;
		    _accumulation[tile].Dispose();
		    _accumulation.Remove(tile);
		    _queue.Remove(tile);
		    _indices.Remove(tile);
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