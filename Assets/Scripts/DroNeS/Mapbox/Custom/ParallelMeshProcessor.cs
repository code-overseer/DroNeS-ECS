using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DroNeS.Mapbox.Interfaces;
using DroNeS.Utils;
using Mapbox.Unity.Map;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Rendering;
using UnityEngine;

namespace DroNeS.Mapbox.Custom
{
    public class ParallelMeshProcessor : IDisposable, IMeshProcessor
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
		private int _count = 0;
		public Dictionary<CustomTile, IEnumerable<RenderMesh>> RenderMeshes { get; } = new Dictionary<CustomTile, IEnumerable<RenderMesh>>();

		public ParallelMeshProcessor()
		{
			Application.quitting += Dispose;
		}

		private NativeList<UnsafeListContainer> Convert(CustomFeatureUnity feature)
		{
			var managed = feature.Points;
			var points = new NativeList<UnsafeListContainer>(managed.Count, Allocator.TempJob);
			var idx = 0;
			foreach (var list in managed)
			{
				points.Add(new UnsafeListContainer(list.Count, 
					UnsafeUtility.SizeOf<Vector3>(), 
					UnsafeUtility.AlignOf<Vector3>(), 
					Allocator.TempJob));
				foreach (var value in list)
				{
					points[idx].Add(value);
				}
				++idx;
			}

			return points;
		}

		public void Execute(CustomTile tile, CustomFeatureUnity feature, UVModifierOptions uvOptions, GeometryExtrusionWithAtlasOptions atlasOptions)
		{
			++_count;
			if (_count > 1)
			{
				CombineAllDependencies().Complete();
			}
			if (!_accumulation.ContainsKey(tile))
		    {
			    _accumulation.Add(tile, new NativeMeshList(Allocator.Persistent));
			    _currentIndex.Add(tile, new NativePtr<int>(0, Allocator.Persistent));
			    _jobs[tile] = default;
		    }
		    
		    var data = new MeshDataStruct(tile.Rect, Allocator.TempJob);
		    var points = Convert(feature);
		    var polygonJob = new PolygonMeshModifierJob(uvOptions, points, ref data);
		    var textureJob = new TextureSideWallModifierJob(atlasOptions, feature, points, ref data);
		    
		    var deallocationJob = new InternalListDeallocationJob
		    {
				Lists = points
		    };
		    var branchingJob = new BranchingJob
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
			    VertCount = branchingJob.VertCount,
			    Skip = branchingJob.Skip,
		    };
		    var appendJob = new MeshAppendJob
		    {
			    Data = data,
			    VertCount = trianglePlusJob.VertCount,
			    Accumulation = _accumulation[tile],
			    Index = _currentIndex[tile],
			    Skip = trianglePlusJob.Skip
		    };
		    //Dispose Skip & data & VertCount
		    
		    _jobs[tile] = points.Dispose(deallocationJob.Schedule(points.Length, 64, polygonJob.Schedule(_jobs[tile])));
		    _jobs[tile] = textureJob.Schedule(_jobs[tile]);
		    _jobs[tile] = branchingJob.Schedule(_jobs[tile]);
		    _jobs[tile] = trianglePlusJob.Schedule(trianglePlusJob.Triangles.Length, 256, _jobs[tile]);
		    _jobs[tile] = appendJob.Schedule(_jobs[tile]);
		    _jobs[tile] = JobHandle.CombineDependencies(appendJob.Skip.Dispose(_jobs[tile]), data.Dispose(_jobs[tile]), trianglePlusJob.VertCount.Dispose(_jobs[tile]));
	    }

		public JobHandle CombineAllDependencies()
		{
			JobHandle output = default;
			foreach (var jobsValue in _jobs.Values)
			{
				output = JobHandle.CombineDependencies(jobsValue, output);
			}

			return output;
		}

		public JobHandle Terminate() 
		{
			JobHandle output = default;
			foreach (var pair in _accumulation)
			{
				var tile = pair.Key;
				_jobs[tile] = _currentIndex[tile].Dispose(_jobs[tile]);
				var count = pair.Value.Length;
				RenderMeshes.Add(tile, new RenderMesh[count]);
				var gcHandles = new NativeArray<GCHandle>(count, Allocator.TempJob);
				for (var i = 0; i < count; ++i)
				{
					((RenderMesh[])RenderMeshes[tile])[i] = new RenderMesh
					{
						mesh = new Mesh(),
						material = BuildingMaterial,
						layer = LayerMask.NameToLayer("Buildings")
					};
					
					gcHandles[i] = new MeshAllocationTask
					{
						Mesh = ((RenderMesh[])RenderMeshes[tile])[i].mesh,
						Native = pair.Value[i]
					}.Handle;
				}
				_jobs[tile] = new MeshAllocationJob
				{
					GcHandles = gcHandles
				}.Schedule(count, 2, _jobs[tile]);
				_jobs[tile] = MeshProxyArrayUtilities.GenerateArray(((RenderMesh[])RenderMeshes[tile]), Allocator.TempJob, _jobs[tile],
					out var meshProxies);

				_jobs[tile] = new NativeToManageJob
				{
					Managed = meshProxies,
					Native = pair.Value.AsParallel(),
				}.Schedule(meshProxies.Length, 64, _jobs[tile]);

				_jobs[tile] = pair.Value.Dispose(_jobs[tile]);
				output = JobHandle.CombineDependencies(_jobs[tile], output);
			}

			return output;
		}

		private struct InternalListDeallocationJob : IJobParallelFor
		{
			public NativeArray<UnsafeListContainer> Lists;
			public void Execute(int index)
			{
				Lists[index].Deallocate();
			}
		}
		
		private class MeshAllocationTask : ITask
		{
			private GCHandle _handle;
			public Mesh Mesh;
			public NativeMesh Native;
			public GCHandle Handle 
			{
				get
				{
					if (_handle == default) _handle = GCHandle.Alloc(this, GCHandleType.Pinned);
					return _handle;
				}
                
			}

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
			[DeallocateOnJobCompletion]
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
			[ReadOnly]
			public NativeMeshList Accumulation;
			public NativePtr<int> Index;
			public NativePtr<int> VertCount;
			public NativePtr<Bool> Skip;
			public void Execute()
			{
				if (Data.Vertices.Length <= 3)
				{
					Skip.Value = true;
					return;
				}
				var target = Accumulation[Index.Value];
				if (target.VertexCount + Data.Vertices.Length > 65000)
				{
					Index.Value += 1;
					VertCount.Value = 0;
				}
				else
				{
					VertCount.Value = target.VertexCount;	
				}
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
				if (Skip.Value || VertCount.Value == 0) return;
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
			[ReadOnly]
			public NativePtr<int> VertCount;
			public void Execute()
			{
				if (Skip.Value) return;
				if (VertCount.Value > 0)
				{
					var target = Accumulation[Index.Value];
					target.AddRange(in Data);
				}
				else
				{
					Accumulation.Add(Data);
				}
				
			}
		}
		
		private struct NativeToManageJob : IJobParallelFor
		{
			[ReadOnly]
			public NativeMeshList.Parallel Native;
			public MeshProxyArray Managed;
			
			public void Execute(int index)
			{
				Managed[index].CopyFrom(Native[index]);
			}
		}

		public void Dispose()
	    {
		    JobHandle handles = default;
		    foreach (var tile in _jobs.Keys)
		    {
			    if (_currentIndex[tile].IsCreated && _accumulation[tile].IsCreated)
					handles = JobHandle.CombineDependencies(_accumulation[tile].Dispose(_jobs[tile]), _currentIndex[tile].Dispose(_jobs[tile]), handles);
			    else if (_currentIndex[tile].IsCreated)
				    handles = JobHandle.CombineDependencies(_currentIndex[tile].Dispose(_jobs[tile]), handles);
			    else if (_accumulation[tile].IsCreated)
					handles = JobHandle.CombineDependencies(_accumulation[tile].Dispose(_jobs[tile]), handles);
		    }
		    handles.Complete();
	    }
    }
}
