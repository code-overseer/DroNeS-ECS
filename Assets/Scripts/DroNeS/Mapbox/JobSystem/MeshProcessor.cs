using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DroNeS.Mapbox.ECS;
using DroNeS.Utils;
using Mapbox.Unity.Map;
using Mapbox.Utils;
using Unity.Collections;
using Unity.Jobs;
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

		private readonly Dictionary<CustomTile, NativeMeshList> _accumulation = new Dictionary<CustomTile, NativeMeshList>();
		private readonly Dictionary<CustomTile, JobHandle> _jobs = new Dictionary<CustomTile, JobHandle>();
		private readonly Dictionary<CustomTile, NativePtr<int>> _currentIndex = new Dictionary<CustomTile, NativePtr<int>>();
		private readonly Dictionary<CustomTile, RenderMesh[]> _renderMeshes = new Dictionary<CustomTile, RenderMesh[]>();
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
			    _accumulation.Add(tile, new NativeMeshList(Allocator.Persistent));
			    _currentIndex.Add(tile, new NativePtr<int>(0, Allocator.Persistent));
			    _jobs[tile] = default;
		    }
		    
		    var data = new MeshDataStruct(in tileRect, Allocator.TempJob);
		    var polygonJob = new PolygonMeshModifierJob().SetProperties(uvOptions, feature, ref data);
		    var textureJob = new TextureSideWallModifierJob().SetProperties(uvOptions, feature, ref data);
		    var routeJob = new BranchingJob
		    {
			    Data = data,
			    Accumulation = _accumulation[tile],
			    Index = _currentIndex[tile],
			    VertCount = new NativePtr<int>(0, Allocator.TempJob),
			    Skip = new NativePtr<Bool>(false, Allocator.TempJob)
		    };
		    var trianglePlusJob = new TriangleUpdateJob
		    {
			    Triangles = data.Triangles,
			    VertCount = routeJob.VertCount,
			    Skip = routeJob.Skip,
		    };
		    // Dispose VertCount
		    var appendJob = new MeshAppendJob
		    {
			    Data = data,
			    Accumulation = _accumulation[tile],
			    Index = _currentIndex[tile],
			    Skip = trianglePlusJob.Skip
		    };
		    //Dispose Skip

		    _jobs[tile] = polygonJob.Schedule(_jobs[tile]);
		    _jobs[tile] = textureJob.Schedule(_jobs[tile]);
		    _jobs[tile] = routeJob.Schedule(_jobs[tile]);
		    _jobs[tile] = trianglePlusJob.Schedule(trianglePlusJob.Triangles.Length, 256, _jobs[tile]);
		    _jobs[tile] = JobHandle.CombineDependencies(trianglePlusJob.VertCount.Dispose(_jobs[tile]), appendJob.Schedule(_jobs[tile]));
		    _jobs[tile] = JobHandle.CombineDependencies(appendJob.Skip.Dispose(_jobs[tile]), appendJob.Data.Dispose(_jobs[tile]));
	    }

		public JobHandle CombineAllDependencies()
		{
			JobHandle handle = default;
			foreach (var job in _jobs.Values)
			{
				handle = JobHandle.CombineDependencies(job, handle);
			}

			return handle;
		}
		
		public unsafe void Terminate() // assume completed
		{
			foreach (var pair in _accumulation)
			{
				var count = pair.Value.Length;
				_renderMeshes.Add(pair.Key, new RenderMesh[count]);
				var gcHandles = new NativeArray<GCHandle>(count, Allocator.TempJob);
				for (var i = 0; i < count; ++i)
				{
					var rm = new RenderMesh
					{
						mesh = new Mesh(),
						material = BuildingMaterial,
						layer = LayerMask.NameToLayer("Buildings")
					};
					_renderMeshes[pair.Key][i] = rm; 
					gcHandles[i] = GCHandle.Alloc(new MeshAllocationTask
					{
						Mesh = rm.mesh,
						Native = pair.Value[i]
					});
				}
				_jobs[pair.Key] = new MeshAllocationJob
				{
					GcHandles = gcHandles
				}.Schedule(count, 2, _jobs[pair.Key]);


			}
		}

		private class MeshAllocationTask : ITask
		{
			public Mesh Mesh;
			public NativeMesh Native;
			public void Execute()
			{
				Mesh.subMeshCount = 1;
				Mesh.vertices = new Vector3[Native.VertexCount];
				Mesh.normals = new Vector3[Native.NormalCount];
				Mesh.uv = new Vector2[Native.UVCount];
				Mesh.triangles = new int[Native.TriangleCount];
			}
		}
		
		private struct MeshAllocationJob : IJobParallelFor
		{
			public NativeArray<GCHandle> GcHandles;
			public void Execute(int index)
			{
				var task = (ITask) GcHandles[index].Target;
				task.Execute();
				GcHandles[index].Free();
			}
		}

		private struct BranchingJob : IJob
		{
			public MeshDataStruct Data;
			public NativeMeshList Accumulation;
			public NativePtr<int> Index;
			public NativePtr<int> VertCount;
			public NativePtr<Bool> Skip;
			public void Execute()
			{
				var target = Accumulation[Index.Value];
				
				if (target.VertexCount + Data.Vertices.Length > 65000)
				{
					Index.Value += 1;
					target = Accumulation[Index.Value];
				}
				VertCount.Value = target.VertexCount;

				if (Data.Vertices.Length > 3) return;
				Skip.Value = true;
			}
		}

		private struct TriangleUpdateJob : IJobParallelFor
		{
			public NativeArray<int> Triangles;
			[ReadOnly, NativeDisableParallelForRestriction]
			public NativePtr<int> VertCount;
			[ReadOnly, NativeDisableParallelForRestriction]
			public NativePtr<Bool> Skip;
			public void Execute(int index)
			{
				if (Skip.Value) return;
				var count = VertCount.Value;
				Triangles[index] = Triangles[index] + count;
			}
		}

		private struct MeshAppendJob : IJob
		{
			[ReadOnly]
			public MeshDataStruct Data;
			public NativeMeshList Accumulation;
			[ReadOnly]
			public NativePtr<int> Index;
			[ReadOnly]
			public NativePtr<Bool> Skip;
			public void Execute()
			{
				if (Skip.Value) return;
				var target = Accumulation[Index.Value];
				target.AddRange(in Data);
			}
		}

		public void Dispose()
	    {
		    foreach (var job in _jobs.Values)
		    {
			    job.Complete();
		    }
			var handles = new JobHandle[_accumulation.Count];
			var i = 0;
		    foreach (var dataSet in _accumulation.Values)
		    {
			    handles[i] = dataSet.Dispose(default);
			    ++i;
		    }

		    foreach (var handle in handles)
		    {
			    handle.Complete();
		    }
	    }
    }
}
