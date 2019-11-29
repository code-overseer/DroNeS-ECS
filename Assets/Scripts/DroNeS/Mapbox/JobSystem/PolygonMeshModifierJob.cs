using Mapbox.Unity.Map;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace DroNeS.Mapbox.JobSystem
{
    public struct PolygonMeshModifierJob : IJob
    {
	    
	    #region Atlas Fields
	    private NativeList<Node> _linkedList;
	    private float3 _v1, _v2;
	    private UvMapType _textureType;
		private float3 _vert;
		private AtlasEntityStruct _currentFacade;
		private quaternion _textureDirection;
		private NativeArray<float2> _textureUvCoordinates;
		private float3 _vertexRelativePos;
		private float3 _firstVert;

		private float minx;
		private float miny;
		private float maxx;
		private float maxy;
		#endregion

		#region Inputs

		private VectorFeatureStruct _feature;
		private MeshDataStruct _mesh;

		#endregion

		public void SetProperties(UVModifierOptions properties, in VectorFeatureStruct feature, in MeshDataStruct md)
		{
			_textureType = properties.texturingType;
			_currentFacade = properties.atlasInfo.Roofs[0];
			_feature = feature;
			_mesh = md;
		}

		private bool IsClockwise(in NativeMultiHashMap<int, float3> vertices, int idx)
		{
			var sum = 0.0;
			if (!vertices.TryGetFirstValue(idx, out var head, out var iterator)) return sum > 0.0;
			_v1 = head;
			
			while (!vertices.TryGetNextValue(out _v2, ref iterator))
			{
				sum += (_v2.x - _v1.x) * (_v2.z + _v1.z);
				_v1 = _v2;
			}
			_v2 = head;
			sum += (_v2.x - _v1.x) * (_v2.z + _v1.z);
			return sum > 0.0;
		}

		public void Execute()
		{
			var subset = new NativeMultiHashMap<int, float3>(128, Allocator.Temp);
			var lengths = new NativeList<int>(128, Allocator.Temp);
			Data flatData;
			NativeList<int> result;
			var currentIndex = 0;
			var polygonVertexCount = 0;
			NativeList<int> triList = default;
			
			var counter = _feature.PointCount.Length;
			
			for (var i = 0; i < counter; i++)
			{
				var vertCount = _mesh.Vertices.Length;
				if (IsClockwise(_feature.Points, i) && vertCount > 0)
				{
					flatData = EarcutLibrary.Flatten(subset, lengths);
					result = EarcutLibrary.Earcut(flatData.Vertices, flatData.Holes, flatData.Dim, ref _linkedList);
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
				if (_feature.Points.TryGetFirstValue(i, out var val, out var iterator))
				{
					do
					{
						subset.Add(lengths.Length, val);
					} while (_feature.Points.TryGetNextValue(out val, ref iterator));
					
				}
				lengths.Add(_feature.PointCount[i]);
				
				polygonVertexCount = _feature.PointCount[i];
				_mesh.Vertices.Capacity = _mesh.Vertices.Length + polygonVertexCount;
				_mesh.Normals.Capacity = _mesh.Normals.Length + polygonVertexCount;
				_mesh.Edges.Capacity = _mesh.Edges.Length + polygonVertexCount * 2;
				var size = new double2(_mesh.TileRect.Size.x, _mesh.TileRect.Size.y);

				if (!_feature.Points.TryGetFirstValue(i, out val, out iterator)) continue;
				{
					var j = 0;
					do
					{
						_mesh.Edges.Add(vertCount + ((j + 1) % polygonVertexCount));
						_mesh.Edges.Add(vertCount + j);
						_mesh.Vertices.Add(val);
						_mesh.Tangents.Add(new float4(math.forward(quaternion.identity), 0));
						_mesh.Normals.Add(math.up());
						
						if (_textureType == UvMapType.Tiled)
						{
							_mesh.UV.Add(new float2(val.x, val.z));
						}
						
						++j;
					} while (_feature.Points.TryGetNextValue(out val, ref iterator));
				}

			}

			flatData = EarcutLibrary.Flatten(subset, lengths);
			result = EarcutLibrary.Earcut(flatData.Vertices, flatData.Holes, flatData.Dim, ref _linkedList);
			polygonVertexCount = result.Length;

			if (_textureType == UvMapType.Atlas || _textureType == UvMapType.AtlasWithColorPalette)
			{
				minx = float.MaxValue;
				miny = float.MaxValue;
				maxx = float.MinValue;
				maxy = float.MinValue;

				_textureUvCoordinates = new NativeArray<float2>(_mesh.Vertices.Length, Allocator.Temp);
				var q = Quaternion.FromToRotation(_mesh.Vertices[0] - _mesh.Vertices[1], new float3(1,0,0));
				_textureDirection = new quaternion(q.x,q.y,q.z,q.w);
				_textureUvCoordinates[0] = float2.zero;
				_firstVert = _mesh.Vertices[0];
				for (var i = 1; i < _mesh.Vertices.Length; i++)
				{
					_vert = _mesh.Vertices[i];
					_vertexRelativePos = _vert - _firstVert;
					_vertexRelativePos = math.mul(_textureDirection, _vertexRelativePos);
					_textureUvCoordinates[i] = new Vector2(_vertexRelativePos.x, _vertexRelativePos.z);
					if (_vertexRelativePos.x < minx)
						minx = _vertexRelativePos.x;
					if (_vertexRelativePos.x > maxx)
						maxx = _vertexRelativePos.x;
					if (_vertexRelativePos.z < miny)
						miny = _vertexRelativePos.z;
					if (_vertexRelativePos.z > maxy)
						maxy = _vertexRelativePos.z;
				}

				var width = maxx - minx;
				var height = maxy - miny;

				for (var i = 0; i < _mesh.Vertices.Length; i++)
				{
					_mesh.UV.Add(new float2(
						(((_textureUvCoordinates[i].x - minx) / width) * _currentFacade.TextureRect.width) + _currentFacade.TextureRect.x,
						(((_textureUvCoordinates[i].y - miny) / height) * _currentFacade.TextureRect.height) + _currentFacade.TextureRect.y));
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
			
			_mesh.Triangles.AddRange(triList);
		}
    }
}

