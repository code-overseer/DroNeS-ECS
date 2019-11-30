using System;
using System.Collections.Generic;
using DroNeS.Mapbox.ECS;
using DroNeS.Systems;
using Mapbox.Unity.Map;
using Mapbox.Unity.MeshGeneration.Data;
using Mapbox.Unity.MeshGeneration.Enums;
using Mapbox.Utils;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace DroNeS.Mapbox.JobSystem
{
    public class MeshProcessor : IDisposable
    {
		private static Material _buildingMaterial;
		private static Material BuildingMaterial
		{
			get
			{
				if (_buildingMaterial == null) _buildingMaterial = Resources.Load("Materials/BuildingMaterial") as Material;
				return _buildingMaterial;
			}
		}

		private readonly Dictionary<CustomTile, MeshDataStruct> _accumulation = new Dictionary<CustomTile, MeshDataStruct>();
		private readonly Dictionary<CustomTile, JobHandle> _jobs = new Dictionary<CustomTile, JobHandle>();
		
		private readonly Dictionary<CustomTile, List<RenderMesh>> _renderMeshes = new Dictionary<CustomTile, List<RenderMesh>>();
		
		private readonly List<Vector3> _v3List = new List<Vector3>(65001);
		private readonly List<Vector2> _v2List = new List<Vector2>(65001);

		public MeshProcessor()
		{
			Application.quitting += Dispose;
		}

		public void Execute(in RectD tileRect, CustomTile tile, CustomFeatureUnity feature, UVModifierOptions uvOptions, GeometryExtrusionOptions extrudeOptions)
	    {
		    if (!_accumulation.ContainsKey(tile))
		    {
			    _accumulation.Add(tile, new MeshDataStruct(new RectD(), Allocator.Persistent));
		    }
		    
		    var data = new MeshDataStruct(in tileRect, Allocator.TempJob);
		    VectorFeatureStruct featureInput = feature; 
		    var polygonJob = new PolygonMeshModifierJob().SetProperties(uvOptions, ref featureInput, ref data);
		    var textureJob = new TextureSideWallModifierJob().SetProperties(uvOptions, feature);
		    var appendJob = new MeshAppendJob
		    {
			    Data = data,
			    Accumulation = _accumulation[tile],
			    UpperLowerLimitHit = new NativeArray<bool2>(1, Allocator.TempJob)
		    };
		    
		    if (_jobs.ContainsKey(tile))
		    {
			    _jobs[tile] = appendJob.Schedule(textureJob.Schedule(polygonJob.Schedule(_jobs[tile])));    
		    }
		    else
		    {
			    _jobs[tile] = appendJob.Schedule(textureJob.Schedule(polygonJob.Schedule()));
		    }
	    }

		private struct MeshAppendJob : IJob
		{
			public MeshDataStruct Data;
			public MeshDataStruct Accumulation;
			public NativeArray<bool2> UpperLowerLimitHit;
			public void Execute()
			{
				if (Accumulation.Vertices.Length + Data.Vertices.Length > 65000)
				{
					UpperLowerLimitHit[0] = new bool2(true, false);
				}
				if (Data.Vertices.Length <= 3)
				{
					UpperLowerLimitHit[0] = new bool2(false, true);
					return;
				}

				var st = Accumulation.Vertices.Length;
				Accumulation.Vertices.AddRange(Data.Vertices);
				Accumulation.Normals.AddRange(Data.Normals);
				Accumulation.UV.AddRange(Data.UV);
				for (var j = 0; j < Data.Triangles.Length; j++)
				{
					Accumulation.Triangles.Add(Data.Triangles[j] + st);
				}
			}
		}

	    private void Append(CustomTile tile, in MeshDataStruct data)
	    {
		    if (data.Vertices.Length <= 3) return;
		    _accumulation.TryGetValue(tile, out var value);
			    
		    var st = value.Vertices.Length;
		    value.Vertices.AddRange(data.Vertices);
		    value.Normals.AddRange(data.Normals);
		    value.UV.AddRange(data.UV);

		    for (var j = 0; j < data.Triangles.Length; j++)
		    {
			    value.Triangles.Add(data.Triangles[j] + st);
		    }
	    }

	    private void Terminate(CustomTile tile, MeshDataStruct data) // must be main thread
	    {
		    if (!_accumulation.TryGetValue(tile, out var value) || value.Vertices.Length <= 3) return;
		    
		    var renderMesh = new RenderMesh
		    {
			    mesh = new Mesh(),
			    material = BuildingMaterial
		    };
		    
		    renderMesh.mesh.subMeshCount = 1;
		    
		    _v3List.Clear();
		    _v3List.AddRange(value.Vertices);
		    renderMesh.mesh.SetVertices(_v3List);
		    
		    _v3List.Clear();
		    _v3List.AddRange(value.Normals);
		    renderMesh.mesh.SetNormals(_v3List);
		    
		    renderMesh.mesh.SetTriangles(value.Triangles.ToArray(), 0);

		    _v2List.Clear();
		    _v2List.AddRange(value.UV);
		    renderMesh.mesh.SetUVs(0, _v2List);
		    renderMesh.layer = LayerMask.NameToLayer("Buildings");

		    var pos = tile.Position;
		    CityBuilderSystem.MakeBuilding(in pos, in renderMesh);
		    
		    _accumulation[tile].CopyFrom(in data);
	    }
	    
	    public void Terminate(CustomTile tile)
	    {
		    if (_accumulation.TryGetValue(tile, out var value) && value.Vertices.Length > 3)
		    {
			    var renderMesh = new RenderMesh
			    {
				    mesh = new Mesh(),
				    material = BuildingMaterial
			    };
			    renderMesh.mesh.subMeshCount = 1;
			    
			    _v3List.Clear();
			    _v3List.AddRange(value.Vertices);
			    renderMesh.mesh.SetVertices(_v3List);
		    
			    _v3List.Clear();
			    _v3List.AddRange(value.Normals);
			    renderMesh.mesh.SetNormals(_v3List);
		    
			    renderMesh.mesh.SetTriangles(value.Triangles.ToArray(), 0);

			    _v2List.Clear();
			    _v2List.AddRange(value.UV);
			    renderMesh.mesh.SetUVs(0, _v2List);
			    renderMesh.layer = LayerMask.NameToLayer("Buildings");

			    var pos = tile.Position;
			
			    CityBuilderSystem.MakeBuilding(in pos, in renderMesh);
		    }
		    
		    _accumulation[tile].Dispose();
		    _accumulation.Remove(tile);
		    tile.VectorDataState = TilePropertyState.Loaded;
	    }

	    public void Dispose()
	    {
		    foreach (var job in _jobs.Values)
		    {
			    job.Complete();
		    }

		    foreach (var dataSet in _accumulation.Values)
		    {
			    dataSet.Dispose();
		    }
	    }
    }
}
