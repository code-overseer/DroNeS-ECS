﻿using Mapbox.Unity.Map;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace DroNeS.Mapbox.Custom
{

	public struct Cont<T> where T : unmanaged
	{
		
	}
    public struct PolygonMeshModifierJob : IJob
    {
	    
	    #region Atlas Fields
	    private Vector3 _v1, _v2;
		private Vector3 _vert;
		private Quaternion _textureDirection;
		private Vector3 _vertexRelativePos;
		private Vector3 _firstVert;

		private float _minx;
		private float _miny;
		private float _maxx;
		private float _maxy;
		#endregion
		#region Inputs
		
		private UvMapType _textureType;
		private AtlasEntityStruct _currentFacade;
		private MeshDataStruct _mesh;
		private NativeList<UnsafeListContainer> _points;
		#endregion

		public PolygonMeshModifierJob(UVModifierOptions properties, NativeList<UnsafeListContainer> points, ref MeshDataStruct md)
		{
			_points = points;
			_textureType = properties.texturingType;
			_currentFacade = properties.atlasInfo.Roofs[0];
			_mesh = md;

			_v1 = default;
			_v2 = default;
			_vert = default;
			_textureDirection = default;
			_vertexRelativePos = default;
			_firstVert = default;
			_minx = default;
			_miny = default;
			_maxx = default;
			_maxy = default;
		}

		private bool IsClockwise(UnsafeListContainer vertices)
		{
			var sum = 0.0;
			var counter = vertices.Length;
			for (var i = 0; i < counter; i++)
			{
				_v1 = vertices.Get<Vector3>(i);
				_v2 = vertices.Get<Vector3>((i + 1) % counter);
				sum += (_v2.x - _v1.x) * (_v2.z + _v1.z);
			}

			return sum > 0.0;
		}

		public void Execute()
		{
			var linkedList = new NativeList<Node>(64, Allocator.Temp);
			var subset = new NativeList<UnsafeListContainer>(128, Allocator.Temp);
			Data flatData;
			NativeList<int> result;
			var currentIndex = 0;
			var polygonVertexCount = 0;
			NativeList<int> triList = default;
			
			var counter = _points.Length;
			
			for (var i = 0; i < counter; i++)
			{
				var sub = _points[i];
				var vertCount = _mesh.Vertices.Length;
				if (IsClockwise(sub) && vertCount > 0)
				{
					flatData = EarcutLibrary.Flatten(subset);
					
					result = EarcutLibrary.Earcut(flatData.Vertices, flatData.Holes, flatData.Dim, ref linkedList);
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
				_mesh.Vertices.Capacity = _mesh.Vertices.Length + polygonVertexCount;
				_mesh.Normals.Capacity = _mesh.Normals.Length + polygonVertexCount;
				_mesh.Edges.Capacity = _mesh.Edges.Length + polygonVertexCount * 2;
				var size = _mesh.TileRect.Size;

				for (var j = 0; j < polygonVertexCount; j++)
				{
					_mesh.Edges.Add(vertCount + (j + 1) % polygonVertexCount);
					_mesh.Edges.Add(vertCount + j);
					_mesh.Vertices.Add(sub.Get<Vector3>(j));
					_mesh.Normals.Add(Vector3.up);

					if (_textureType != UvMapType.Tiled) continue;
					var val = sub.Get<Vector3>(j);
					_mesh.UV.Add(new Vector2(val.x, val.z));
				}

			}

			flatData = EarcutLibrary.Flatten(subset);
			result = EarcutLibrary.Earcut(flatData.Vertices, flatData.Holes, flatData.Dim, ref linkedList);
			polygonVertexCount = result.Length;

			if (_textureType == UvMapType.Atlas || _textureType == UvMapType.AtlasWithColorPalette)
			{
				_minx = float.MaxValue;
				_miny = float.MaxValue;
				_maxx = float.MinValue;
				_maxy = float.MinValue;

				var textureUvCoordinates = new NativeArray<Vector2>(_mesh.Vertices.Length, Allocator.Temp);
				_textureDirection = Quaternion.FromToRotation(_mesh.Vertices[0] - _mesh.Vertices[1], new Vector3(1,0,0));
				textureUvCoordinates[0] = Vector2.zero;
				_firstVert = _mesh.Vertices[0];
				for (var i = 1; i < _mesh.Vertices.Length; i++)
				{
					_vert = _mesh.Vertices[i];
					_vertexRelativePos = _vert - _firstVert;
					_vertexRelativePos = _textureDirection * _vertexRelativePos;
					textureUvCoordinates[i] = new Vector2(_vertexRelativePos.x, _vertexRelativePos.z);
					if (_vertexRelativePos.x < _minx)
						_minx = _vertexRelativePos.x;
					if (_vertexRelativePos.x > _maxx)
						_maxx = _vertexRelativePos.x;
					if (_vertexRelativePos.z < _miny)
						_miny = _vertexRelativePos.z;
					if (_vertexRelativePos.z > _maxy)
						_maxy = _vertexRelativePos.z;
				}

				var width = _maxx - _minx;
				var height = _maxy - _miny;

				for (var i = 0; i < _mesh.Vertices.Length; i++)
				{
					_mesh.UV.Add(new Vector2(
						(((textureUvCoordinates[i].x - _minx) / width) * _currentFacade.TextureRect.width) + _currentFacade.TextureRect.x,
						(((textureUvCoordinates[i].y - _miny) / height) * _currentFacade.TextureRect.height) + _currentFacade.TextureRect.y));
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
