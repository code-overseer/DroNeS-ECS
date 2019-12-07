using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DroNeS.Utils;
using DroNeS.Utils.Time;
using Mapbox.Unity.Map;
using Mapbox.Unity.MeshGeneration.Data;
using Mapbox.Unity.MeshGeneration.Modifiers;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;


namespace DroNeS.Mapbox.Custom.Parallel
{

	public class StrippedPolygonMeshModifier : CustomMeshModifier
	{
		public override ModifierType Type => ModifierType.Preprocess;

		private UVModifierOptions _options;
		private AtlasEntity _currentFacade;

		public override void SetProperties(ModifierProperties properties)
		{
			_options = (UVModifierOptions) properties;
		}

		public override void Run(CustomFeatureUnity feature, ref MeshDataStruct md)
		{
			var counter = feature.Points.Count;
			var subset = new List<List<Vector3>>(counter);
			Assets.Mapbox.Unity.MeshGeneration.Modifiers.MeshModifiers.Data flatData;
			List<int> result;
			var currentIndex = 0;
			var polygonVertexCount = 0;
			NativeList<int> triList = default;

			for (var i = 0; i < counter; i++)
			{
				var sub = feature.Points[i];
				// ear cut is built to handle one polygon with multiple holes
				//point data can contain multiple polygons though, so we're handling them separately here

				var vertCount = md.Vertices.Length;
				if (IsClockwise(sub) && vertCount > 0)
				{
					flatData = Assets.Mapbox.Unity.MeshGeneration.Modifiers.MeshModifiers.EarcutLibrary.Flatten(subset);
					result = Assets.Mapbox.Unity.MeshGeneration.Modifiers.MeshModifiers.EarcutLibrary.Earcut(flatData.Vertices, flatData.Holes, flatData.Dim);
					polygonVertexCount = result.Count;

					if (!triList.IsCreated)
					{
						triList = new NativeList<int>(polygonVertexCount, Allocator.TempJob);
					}
					else
					{
						triList.Capacity = triList.Length + polygonVertexCount;
					}

					for (var j = 0; j < polygonVertexCount; j++)
					{
						triList.Add(result[j] + currentIndex);
					}

					currentIndex = vertCount;
					subset.Clear();
				}

				subset.Add(sub);

				polygonVertexCount = sub.Count;
				md.Vertices.Capacity = md.Vertices.Length + polygonVertexCount;
				md.Normals.Capacity = md.Normals.Length + polygonVertexCount;
				md.Edges.Capacity = md.Edges.Length + polygonVertexCount * 2;

				for (var j = 0; j < polygonVertexCount; j++)
				{
					md.Edges.Add(vertCount + ((j + 1) % polygonVertexCount));
					md.Edges.Add(vertCount + j);
					md.Vertices.Add(sub[j]);
					md.Normals.Add(Vector3.up);
					if (_options.texturingType != UvMapType.Tiled) continue;
					md.UV.Add(new Vector2(sub[j].x, sub[j].z));
				}
			}

			flatData = Assets.Mapbox.Unity.MeshGeneration.Modifiers.MeshModifiers.EarcutLibrary.Flatten(subset);
			result = Assets.Mapbox.Unity.MeshGeneration.Modifiers.MeshModifiers.EarcutLibrary.Earcut(flatData.Vertices, flatData.Holes, flatData.Dim);
			polygonVertexCount = result.Count;

			if (_options.texturingType == UvMapType.Atlas || _options.texturingType == UvMapType.AtlasWithColorPalette)
			{
				_currentFacade = _options.atlasInfo.Roofs[UnityEngine.Random.Range(0, _options.atlasInfo.Roofs.Count)];

				var minx = float.MaxValue;
				var miny = float.MaxValue;
				var maxx = float.MinValue;
				var maxy = float.MinValue;

				var textureUvCoordinates = new Vector2[md.Vertices.Length];
				var textureDirection = Quaternion.FromToRotation(md.Vertices[0] - md.Vertices[1], Vector3.right);
				textureUvCoordinates[0] = new Vector2(0, 0);

				for (var i = 1; i < md.Vertices.Length; i++)
				{
					var vert = md.Vertices[i];
					var vertexRelativePos = textureDirection * (vert - md.Vertices[0]);
					textureUvCoordinates[i] = new Vector2(vertexRelativePos.x, vertexRelativePos.z);
					if (vertexRelativePos.x < minx)
						minx = vertexRelativePos.x;
					if (vertexRelativePos.x > maxx)
						maxx = vertexRelativePos.x;
					if (vertexRelativePos.z < miny)
						miny = vertexRelativePos.z;
					if (vertexRelativePos.z > maxy)
						maxy = vertexRelativePos.z;
				}

				var width = maxx - minx;
				var height = maxy - miny;

				for (var i = 0; i < md.Vertices.Length; i++)
				{
					md.UV.Add(new Vector2(
						(((textureUvCoordinates[i].x - minx) / width) * _currentFacade.TextureRect.width) + _currentFacade.TextureRect.x,
						(((textureUvCoordinates[i].y - miny) / height) * _currentFacade.TextureRect.height) + _currentFacade.TextureRect.y));
				}
			}

			if (!triList.IsCreated)
			{
				triList = new NativeList<int>(polygonVertexCount, Allocator.TempJob);
			}
			else
			{
				triList.Capacity = triList.Length + polygonVertexCount;
			}

			for (var i = 0; i < polygonVertexCount; i++)
			{
				triList.Add(result[i] + currentIndex);
			}
			md.Triangles.AddRange(triList);

			triList.Dispose();
		}
		
		public override void Run(VectorFeatureUnity feature, MeshData md, UnityTile tile = null)
		{
			var counter = feature.Points.Count;
			var subset = new List<List<Vector3>>(counter);
			Assets.Mapbox.Unity.MeshGeneration.Modifiers.MeshModifiers.Data flatData;
			List<int> result;
			var currentIndex = 0;
			var polygonVertexCount = 0;
			List<int> triList = null;

			for (var i = 0; i < counter; i++)
			{
				var sub = feature.Points[i];
				// ear cut is built to handle one polygon with multiple holes
				//point data can contain multiple polygons though, so we're handling them separately here

				var vertCount = md.Vertices.Count;
				if (IsClockwise(sub) && vertCount > 0)
				{
					flatData = Assets.Mapbox.Unity.MeshGeneration.Modifiers.MeshModifiers.EarcutLibrary.Flatten(subset);
					result = Assets.Mapbox.Unity.MeshGeneration.Modifiers.MeshModifiers.EarcutLibrary.Earcut(flatData.Vertices, flatData.Holes, flatData.Dim);
					polygonVertexCount = result.Count;

					if (triList == null)
					{
						triList = new List<int>(polygonVertexCount);
					}
					else
					{
						triList.Capacity = triList.Count + polygonVertexCount;
					}

					for (var j = 0; j < polygonVertexCount; j++)
					{
						triList.Add(result[j] + currentIndex);
					}

					currentIndex = vertCount;
					subset.Clear();
				}

				subset.Add(sub);

				polygonVertexCount = sub.Count;
				md.Vertices.Capacity = md.Vertices.Count + polygonVertexCount;
				md.Normals.Capacity = md.Normals.Count + polygonVertexCount;
				md.Edges.Capacity = md.Edges.Count + polygonVertexCount * 2;

				for (var j = 0; j < polygonVertexCount; j++)
				{
					md.Edges.Add(vertCount + ((j + 1) % polygonVertexCount));
					md.Edges.Add(vertCount + j);
					md.Vertices.Add(sub[j]);
					md.Normals.Add(Vector3.up);
					if (_options.texturingType != UvMapType.Tiled) continue;
					md.UV[0].Add(new Vector2(sub[j].x, sub[j].z));
				}
			}

			flatData = Assets.Mapbox.Unity.MeshGeneration.Modifiers.MeshModifiers.EarcutLibrary.Flatten(subset);
			result = Assets.Mapbox.Unity.MeshGeneration.Modifiers.MeshModifiers.EarcutLibrary.Earcut(flatData.Vertices, flatData.Holes, flatData.Dim);
			polygonVertexCount = result.Count;

			if (_options.texturingType == UvMapType.Atlas || _options.texturingType == UvMapType.AtlasWithColorPalette)
			{
				_currentFacade = _options.atlasInfo.Roofs[UnityEngine.Random.Range(0, _options.atlasInfo.Roofs.Count)];

				var minx = float.MaxValue;
				var miny = float.MaxValue;
				var maxx = float.MinValue;
				var maxy = float.MinValue;

				var textureUvCoordinates = new Vector2[md.Vertices.Count];
				var textureDirection = Quaternion.FromToRotation((md.Vertices[0] - md.Vertices[1]), Vector3.right);
				textureUvCoordinates[0] = new Vector2(0, 0);
				for (var i = 1; i < md.Vertices.Count; i++)
				{
					var vert = md.Vertices[i];
					var vertexRelativePos = textureDirection * (vert - md.Vertices[0]);
					textureUvCoordinates[i] = new Vector2(vertexRelativePos.x, vertexRelativePos.z);
					if (vertexRelativePos.x < minx)
						minx = vertexRelativePos.x;
					if (vertexRelativePos.x > maxx)
						maxx = vertexRelativePos.x;
					if (vertexRelativePos.z < miny)
						miny = vertexRelativePos.z;
					if (vertexRelativePos.z > maxy)
						maxy = vertexRelativePos.z;
				}

				var width = maxx - minx;
				var height = maxy - miny;

				for (var i = 0; i < md.Vertices.Count; i++)
				{
					md.UV[0].Add(new Vector2(
						(((textureUvCoordinates[i].x - minx) / width) * _currentFacade.TextureRect.width) + _currentFacade.TextureRect.x,
						(((textureUvCoordinates[i].y - miny) / height) * _currentFacade.TextureRect.height) + _currentFacade.TextureRect.y));
				}
			}

			if (triList == null)
			{
				triList = new List<int>(polygonVertexCount);
			}
			else
			{
				triList.Capacity = triList.Count + polygonVertexCount;
			}

			for (var i = 0; i < polygonVertexCount; i++)
			{
				triList.Add(result[i] + currentIndex);
			}
			md.Triangles.Add(triList);
		}

		private static bool IsClockwise(IList<Vector3> vertices)
		{
			var sum = 0.0;
			var counter = vertices.Count;
			
			for (var i = 0; i < counter; i++)
			{
				var v1 = vertices[i];
				var v2 = vertices[(i + 1) % counter];
				sum += (v2.x - v1.x) * (v2.z + v1.z);
			}

			return sum > 0.0;
		}
	}
	
	public class JobifiedPolygonMeshModifier : CustomMeshModifier
	{
		private UVModifierOptions _options;
		public override void SetProperties(ModifierProperties properties)
		{
			if (!(properties is UVModifierOptions options))
				throw new ArgumentException("Expected UVModifierOptions");
			_options = options;
		}

		public override void Run(CustomFeatureUnity feature, ref MeshDataStruct md)
		{
			PolygonMeshModifierJob.Schedule(default, _options, feature, ref md).Complete();
		}
	}
	
	[BurstCompile]
	public struct PolygonMeshModifierJob : IJob
    {
	    #region Inputs
		private readonly UvMapType _textureType;
		private readonly AtlasEntityStruct _currentFacade;
		private MeshDataStruct _output;
		private readonly NativeList<UnsafeListContainer> _points;
		#endregion

		public static JobHandle Schedule(JobHandle dependencies, UVModifierOptions options, CustomFeatureUnity feature,
			ref MeshDataStruct output)
		{
			var points = new NativeList<UnsafeListContainer>(feature.Points.Count, Allocator.Persistent);
			var i = 0;
			foreach (var list in feature.Points)
			{
				points.Add(new UnsafeListContainer(list.Count, UnsafeUtility.SizeOf<Vector3>(), UnsafeUtility.AlignOf<Vector3>(), Allocator.Persistent));
				CopyList(list, points[i++]);
			}
			
			return points.Dispose(new UnsafeListContainerDisposal(points).Schedule(
				new PolygonMeshModifierJob(options, points, ref output).Schedule(dependencies)));
		}

		private static unsafe void CopyList(List<Vector3> list, UnsafeListContainer dst)
		{
			var array = list.GetInternalArray();
			var gcHandle = GCHandle.Alloc(array, GCHandleType.Pinned);
			var ptr = gcHandle.AddrOfPinnedObject();
			UnsafeUtility.MemCpy(dst.m_ListData->Ptr, (void*) ptr, list.Count * sizeof(Vector3));
			dst.m_ListData->Length = list.Count;
			gcHandle.Free();
		}

		[BurstCompile]
		private struct UnsafeListContainerDisposal : IJob
		{
			private NativeList<UnsafeListContainer> _list;
			public UnsafeListContainerDisposal(NativeList<UnsafeListContainer> list)
			{
				_list = list;
			}
			public void Execute()
			{
				for (var i = 0; i < _list.Length; ++i)
				{
					_list[i].Dispose();
				}
			}
		}
		private PolygonMeshModifierJob(UVModifierOptions properties, NativeList<UnsafeListContainer> points, ref MeshDataStruct md)
		{
			_points = points;
			_textureType = properties.texturingType;
			_currentFacade = properties.atlasInfo.Roofs[0];
			_output = md;
		}

		private static bool IsClockwise(UnsafeListContainer vertices)
		{
			var sum = 0.0;
			var counter = vertices.Length;
			for (var i = 0; i < counter; i++)
			{
				var v1 = vertices.Get<Vector3>(i);
				var v2 = vertices.Get<Vector3>((i + 1) % counter);
				sum += (v2.x - v1.x) * (v2.z + v1.z);
			}

			return sum > 0.0;
		}

		public void Execute()
		{
			var subset = new NativeList<UnsafeListContainer>(4, Allocator.Temp);
			NativeList<int> result;
			var currentIndex = 0;
			int polygonVertexCount;
			NativeList<int> triList = default;

			var counter = _points.Length;
			
			for (var i = 0; i < counter; i++)
			{
				var sub = _points[i];
				var vertCount = _output.Vertices.Length;
				if (IsClockwise(sub) && vertCount > 0)
				{
					result = EarcutStruct.Earcut(subset);
					polygonVertexCount = result.Length;
					if (!triList.IsCreated)
					{
						triList = new NativeList<int>(polygonVertexCount, Allocator.Temp);
					}
					else
					{
						triList.Capacity = triList.Length + polygonVertexCount;
					}

					for (var j = 0; j < polygonVertexCount; j++)
					{
						triList.Add(result[j] + currentIndex);
					}

					currentIndex = vertCount;
					subset.Clear();
				}
				
				subset.Add(sub);

				polygonVertexCount = sub.Length;
				_output.Vertices.Capacity = _output.Vertices.Length + polygonVertexCount;
				_output.Normals.Capacity = _output.Normals.Length + polygonVertexCount;
				_output.Edges.Capacity = _output.Edges.Length + polygonVertexCount * 2;

				for (var j = 0; j < polygonVertexCount; j++)
				{
					_output.Edges.Add(vertCount + (j + 1) % polygonVertexCount);
					_output.Edges.Add(vertCount + j);
					_output.Vertices.Add(sub.Get<Vector3>(j));
					_output.Normals.Add(Vector3.up);

					if (_textureType != UvMapType.Tiled) continue;
					var val = sub.Get<Vector3>(j);
					_output.UV.Add(new Vector2(val.x, val.z));
				}

			}
			
			result = EarcutStruct.Earcut(subset);
			polygonVertexCount = result.Length;

			if (_textureType == UvMapType.Atlas || _textureType == UvMapType.AtlasWithColorPalette)
			{
				var minx = float.MaxValue;
				var miny = float.MaxValue;
				var maxx = float.MinValue;
				var maxy = float.MinValue;

				var textureUvCoordinates =
					new NativeArray<Vector2>(_output.Vertices.Length, Allocator.Temp) {[0] = Vector2.zero};
				
				var textureDirection = Quaternion.FromToRotation(_output.Vertices[0] - _output.Vertices[1], new Vector3(1,0,0));
				for (var i = 1; i < _output.Vertices.Length; i++)
				{
					var vertexRelativePos = textureDirection * (_output.Vertices[i] - _output.Vertices[0]);
					textureUvCoordinates[i] = new Vector2(vertexRelativePos.x, vertexRelativePos.z);
					if (vertexRelativePos.x < minx)
						minx = vertexRelativePos.x;
					if (vertexRelativePos.x > maxx)
						maxx = vertexRelativePos.x;
					if (vertexRelativePos.z < miny)
						miny = vertexRelativePos.z;
					if (vertexRelativePos.z > maxy)
						maxy = vertexRelativePos.z;
				}

				var width = maxx - minx;
				var height = maxy - miny;

				for (var i = 0; i < _output.Vertices.Length; i++)
				{
					_output.UV.Add(new Vector2(
						(textureUvCoordinates[i].x - minx) / width * _currentFacade.TextureRect.width + _currentFacade.TextureRect.x,
						(textureUvCoordinates[i].y - miny) / height * _currentFacade.TextureRect.height + _currentFacade.TextureRect.y));
				}
			}

			if (!triList.IsCreated)
			{
				triList = new NativeList<int>(polygonVertexCount, Allocator.Temp);
			}
			else
			{
				triList.Capacity = triList.Length + polygonVertexCount;
			}

			for (var i = 0; i < polygonVertexCount; i++)
			{
				triList.Add(result[i] + currentIndex);
			}
			
			_output.Triangles.AddRange(triList);
		}
    }

}

