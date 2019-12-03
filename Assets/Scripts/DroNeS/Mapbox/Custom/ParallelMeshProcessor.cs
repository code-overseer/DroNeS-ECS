using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DroNeS.Mapbox.Interfaces;
using DroNeS.Utils;
using Mapbox.Unity.Map;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Rendering;
using UnityEngine;
using Debug = UnityEngine.Debug;

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
		private readonly HashSet<CustomTile> _tiles = new HashSet<CustomTile>();
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
			if (!_tiles.Contains(tile))
			{
				_tiles.Add(tile);
			    _accumulation.Add(tile, new NativeMeshList(Allocator.Persistent));
			    _currentIndex.Add(tile, new NativePtr<int>(0, Allocator.Persistent));
			    _jobs.Add(tile, default);
			}
			var data = new MeshDataStruct(tile.Rect, Allocator.TempJob);
			var points = Convert(feature);
		    
		    var polygonJob = new PolygonMeshModifierJob(uvOptions, points, ref data);
		    
		    //Dispose points
		    
		    var textureJob = new TextureSideWallModifierJob(atlasOptions, feature, points, ref data);
		    
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
			    VertCount = branchingJob.VertCount.AsParallel(),
			    Skip = branchingJob.Skip.AsParallel(),
		    };
		    var appendJob = new MeshAppendJob
		    {
			    Data = data,
			    VertCount = branchingJob.VertCount,
			    Accumulation = _accumulation[tile],
			    Index = _currentIndex[tile],
			    Skip = branchingJob.Skip
		    };
		    //Dispose Skip & data & VertCount
		    
		    var parPoints = points.AsParallelWriter();
		    
		    _jobs[tile] = UnsafeListDeallocationJob.Schedule(ref parPoints, polygonJob.Schedule(_jobs[tile]));
		    _jobs[tile] = JobHandle.CombineDependencies(
			    points.Dispose(_jobs[tile]),
				textureJob.Schedule(_jobs[tile]));
		    
		    _jobs[tile] = branchingJob.Schedule(_jobs[tile]);
		    _jobs[tile] = trianglePlusJob.Schedule(trianglePlusJob.Triangles.Length, 256, _jobs[tile]);
		    _jobs[tile] = appendJob.Schedule(_jobs[tile]);
		    _jobs[tile] = JobHandle.CombineDependencies(
			    data.Dispose(_jobs[tile]), 
			    branchingJob.Skip.Dispose(_jobs[tile]),
			    branchingJob.VertCount.Dispose(_jobs[tile]));
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
			foreach (var tile in _tiles)
			{
				var count = _accumulation[tile].Length;
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
						Native = _accumulation[tile][i]
					}.TaskHandle;
				}
				_jobs[tile] = new MeshAllocationJob
				{
					// deallocate on completion
					GcHandles = gcHandles
				}.Schedule(count, 2, _jobs[tile]);
				_jobs[tile] = MeshProxyArrayUtilities.GenerateArray((RenderMesh[])RenderMeshes[tile], Allocator.TempJob, _jobs[tile],
					out var meshProxies);

				_jobs[tile] = new NativeToManageJob
				{
					Managed = meshProxies,
					Native = _accumulation[tile].AsParallel(),
				}.Schedule(meshProxies.Length, 64, _jobs[tile]);

				var list = _accumulation[tile];
				var index = _currentIndex[tile];
				_jobs[tile] = JobHandle.CombineDependencies(
					list.Dispose(_jobs[tile]),
					index.Dispose(_jobs[tile]),
					meshProxies.Dispose(_jobs[tile])
				);
				_accumulation[tile] = list;
				_currentIndex[tile] = index;
				output = JobHandle.CombineDependencies(_jobs[tile], output);
			}

			return output;
		}
		
		public void Dispose()
		{
			JobHandle handles = default;
			foreach (var tile in _jobs.Keys)
			{
				handles = JobHandle.CombineDependencies(_accumulation[tile].Dispose(_jobs[tile]), _currentIndex[tile].Dispose(_jobs[tile]), handles);
			}
			handles.Complete();
		}
		
		
		private class MeshAllocationTask : ITask
		{
			private GCHandle _handle;
			public Mesh Mesh;
			public NativeMesh Native;
			public GCHandle TaskHandle 
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
			[WriteOnly]
			public NativePtr<int> VertCount;
			[WriteOnly]
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
			public NativePtr<int>.Parallel VertCount;
			[ReadOnly, NativeDisableParallelForRestriction]
			public NativePtr<Bool>.Parallel Skip;
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
			[ReadOnly, DeallocateOnJobCompletion]
			public NativePtr<Bool> Skip;
			[ReadOnly, DeallocateOnJobCompletion]
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

		private struct UnsafeListDeallocationJob : IJobParallelFor
		{
			public static JobHandle Schedule(ref NativeArray<UnsafeListContainer> list, JobHandle inputDeps)
			{
				var job = new UnsafeListDeallocationJob {List = list};
				
				return job.Schedule(list.Length, 32, inputDeps);
			}
			
			private NativeArray<UnsafeListContainer> List;
			
			public void Execute(int index)
			{
				List[index].Deallocate();
			}
		}
    }
}
