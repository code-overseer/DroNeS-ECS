using System;
using DroNeS.Mapbox.Custom;
using DroNeS.Utils;
using Mapbox.Unity.Map;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace DroNeS.Mapbox.JobSystem
{
    public struct TextureSideWallModifierJob : IJob
	{
		#region ModifierOptions

		private float _scaledFirstFloorHeight;

		private float _scaledTopFloorHeight;

		private float _scaledPreferredWallLength;

		#endregion

		#region Fields
		private float _currentWallLength;
		private float3 _start; //zero default
		private float3 _wallDirection; //zero default

		private float3 _wallSegmentFirstVertex;
		private float3 _wallSegmentSecondVertex;
		private float3 _wallSegmentDirection;
		private float _wallSegmentLength;
		private Rect _currentTextureRect;

		private float _finalFirstHeight;
		private float _finalTopHeight;
		private float _finalMidHeight;
		private float _finalLeftOverRowHeight;
		private float _scaledFloorHeight;
		private int _triIndex;
		private float3 _wallNormal;
		private float _columnScaleRatio;
		private float _rightOfEdgeUv;
		private float _currentY1;
		private float _currentY2;
		private int _counter;
		private float _minWallLength;
		private float _singleFloorHeight;
		private float _currentMidHeight;
		private float _midUvInCurrentStep;
		private float _singleColumnLength;
		private float _leftOverColumnLength;
		
		#endregion
		
		private static readonly Bool CenterSegments = true;
		private const float WallSizeEpsilon = 0.99f;
		private const float NarrowWallWidthDelta = 0.01f;
		private const float ShortRowHeightDelta = 0.015f;
		private const float Scale = 0.7571877f;
		private static readonly ExtrusionType ExtrusionType = ExtrusionType.PropertyHeight;
		private static readonly ExtrusionGeometryType ExtrusionGeometryType = ExtrusionGeometryType.RoofAndSide;
		
		#region Inputs

		private Bool _pointsEmpty;
		private MeshDataStruct _mesh;
		private AtlasEntityStruct _currentFacade;
		private float _height;
		private float _maxHeight;
		private float _minHeight;
		private float _extrusionScaleFactor;

		#endregion

		public TextureSideWallModifierJob(GeometryExtrusionWithAtlasOptions options, 
			CustomFeatureUnity feature,
			NativeList<UnsafeListContainer> points, 
			ref MeshDataStruct md)
		{
			_currentFacade = options.atlasInfo.Textures[0];
			_currentFacade.CalculateParameters();
			_extrusionScaleFactor = options.extrusionScaleFactor;
			_minHeight = 0.0f;
			_maxHeight = 0.0f;
			_height = 0.0f;
			
			_maxHeight = Convert.ToSingle(feature.Properties["height"]);
			if (feature.Properties.ContainsKey("min_height"))
			{
				_minHeight = Convert.ToSingle(feature.Properties["min_height"]);
			}

			_pointsEmpty = points.Length < 1;
			_mesh = md;
			_scaledFirstFloorHeight = default;
			_scaledTopFloorHeight = default;
			_scaledPreferredWallLength = default;
			_currentWallLength = default;
			_start = default;
			_wallDirection = default;
			_wallSegmentFirstVertex = default;
			_wallSegmentSecondVertex = default;
			_wallSegmentDirection = default;
			_wallSegmentLength = default;
			_currentTextureRect = default;
			_finalFirstHeight = default;
			_finalTopHeight = default;
			_finalMidHeight = default;
			_finalLeftOverRowHeight = default;
			_scaledFloorHeight = default;
			_triIndex = default;
			_wallNormal = default;
			_columnScaleRatio = default;
			_rightOfEdgeUv = default;
			_currentY1 = default;
			_currentY2 = default;
			_counter = default;
			_minWallLength = default;
			_singleFloorHeight = default;
			_currentMidHeight = default;
			_midUvInCurrentStep = default;
			_singleColumnLength = default;
			_leftOverColumnLength = default;
		}

		public void Execute()
		{
			if (_mesh.Vertices.Length == 0 || _pointsEmpty) return;
			
			//rect is a struct so we're caching this
			_currentTextureRect = _currentFacade.TextureRect;

			//this can be moved to initialize or in an if clause if you're sure all your tiles will be same level/scale
			_singleFloorHeight = (Scale * _currentFacade.FloorHeight) / _currentFacade.MidFloorCount;
			_scaledFirstFloorHeight = Scale * _currentFacade.FirstFloorHeight;
			_scaledTopFloorHeight = Scale * _currentFacade.TopFloorHeight;
			_scaledPreferredWallLength = Scale * _currentFacade.PreferredEdgeSectionLength;
			_scaledFloorHeight = _scaledPreferredWallLength * _currentFacade.WallToFloorRatio;
			_singleColumnLength = _scaledPreferredWallLength / _currentFacade.ColumnCount;

			_maxHeight = _maxHeight * _extrusionScaleFactor * Scale;
			_minHeight = _minHeight * _extrusionScaleFactor * Scale;
			_height = _maxHeight - _minHeight;
			
			GenerateRoofMesh(_mesh);
			
			_finalFirstHeight = Mathf.Min(_height, _scaledFirstFloorHeight);
			_finalTopHeight = (_height - _finalFirstHeight) < _scaledTopFloorHeight ? 0 : _scaledTopFloorHeight;
			_finalMidHeight = Mathf.Max(0, _height - (_finalFirstHeight + _finalTopHeight));
			var wallTriangles = new NativeList<int>(32, Allocator.Temp);
			
			_currentWallLength = 0;
			_start = float3.zero;
			_wallSegmentDirection = float3.zero;

			_finalLeftOverRowHeight = 0f;
			if (_finalMidHeight > 0)
			{
				_finalLeftOverRowHeight = _finalMidHeight;
				_finalLeftOverRowHeight %= _singleFloorHeight;
				_finalMidHeight -= _finalLeftOverRowHeight;
			}
			else
			{
				_finalLeftOverRowHeight = _finalTopHeight;
			}

			for (var i = 0; i < _mesh.Edges.Length; i += 2)
			{
				var v1 = _mesh.Vertices[_mesh.Edges[i]];
				var v2 = _mesh.Vertices[_mesh.Edges[i + 1]];

				_wallDirection = v2 - v1;

				_currentWallLength = math.distance(v1, v2);
				_leftOverColumnLength = _currentWallLength % _singleColumnLength;
				_start = v1;
				_wallSegmentDirection = math.normalize(v2 - v1);

				//half of leftover column (if _centerSegments ofc) at the beginning
				if (CenterSegments && _currentWallLength > _singleColumnLength)
				{
					//save left,right vertices and wall length
					_wallSegmentFirstVertex = _start;
					_wallSegmentLength = (_leftOverColumnLength / 2);
					_start += _wallSegmentDirection * _wallSegmentLength;
					_wallSegmentSecondVertex = _start;

					_leftOverColumnLength /= 2;
					CreateWall(_mesh, ref wallTriangles);
				}

				while (_currentWallLength > _singleColumnLength)
				{
					_wallSegmentFirstVertex = _start;
					//columns fitting wall / max column we have in texture
					var stepRatio =
						(float)Math.Min(_currentFacade.ColumnCount,
							Math.Floor(_currentWallLength / _singleColumnLength)) / _currentFacade.ColumnCount;
					_wallSegmentLength = stepRatio * _scaledPreferredWallLength;
					_start += _wallSegmentDirection * _wallSegmentLength;
					_wallSegmentSecondVertex = _start;

					_currentWallLength -= (stepRatio * _scaledPreferredWallLength);
					CreateWall(_mesh, ref wallTriangles);
				}

				//left over column at the end
				if (!(_leftOverColumnLength > 0)) continue;
				_wallSegmentFirstVertex = _start;
				_wallSegmentSecondVertex = v2;
				_wallSegmentLength = _leftOverColumnLength;
				CreateWall(_mesh, ref wallTriangles);
			}
			
			var newCap = _mesh.Triangles.Length + wallTriangles.Length;
			if (_mesh.Triangles.Capacity < newCap) _mesh.Triangles.Capacity = 2 * newCap; 
			_mesh.Triangles.AddRange(wallTriangles);
		}

		private void CreateWall(MeshDataStruct md, ref NativeList<int> wallTriangles)
		{
			//need to keep track of this for triangulation indices
			_triIndex = md.Vertices.Length;

			//this part minimizes stretching for narrow columns
			//if texture has 3 columns, 33% (of preferred edge length) wide walls will get 1 window.
			//0-33% gets 1 window, 33-66 gets 2, 66-100 gets all three
			//we're not wrapping/repeating texture as it won't work with atlases
			_columnScaleRatio = math.min(1, _wallSegmentLength / _scaledPreferredWallLength);
			_rightOfEdgeUv =
				_currentTextureRect.xMin +
				_currentTextureRect.size.x *
				_columnScaleRatio; // Math.Min(1, ((float)(Math.Floor(columnScaleRatio * _currentFacade.ColumnCount) + 1) / _currentFacade.ColumnCount));

			_minWallLength = (_scaledPreferredWallLength / _currentFacade.ColumnCount) * WallSizeEpsilon;
			//common for all top/mid/bottom segments
			_wallNormal = math.normalize(new float3(-(_wallSegmentFirstVertex.z - _wallSegmentSecondVertex.z), 0,
				_wallSegmentFirstVertex.x - _wallSegmentSecondVertex.x));
			//height of the left/right edges
			_currentY1 = _wallSegmentFirstVertex.y;
			_currentY2 = _wallSegmentSecondVertex.y;

			//moving leftover row to top
			LeftOverRow(md, _finalLeftOverRowHeight, ref wallTriangles);

			FirstFloor(md, _height, ref wallTriangles);
			TopFloor(md, _finalLeftOverRowHeight, ref wallTriangles);
			MidFloors(md, ref wallTriangles);
		}

		private void LeftOverRow(MeshDataStruct md, float leftOver, ref NativeList<int> wallTriangles)
		{
			//leftover. we're moving small leftover row to top of the building
			if (!(leftOver > 0)) return;
			
			md.Vertices.Add(new float3(_wallSegmentFirstVertex.x, _currentY1, _wallSegmentFirstVertex.z));
			md.Vertices.Add(new float3(_wallSegmentSecondVertex.x, _currentY2, _wallSegmentSecondVertex.z));
			//move offsets bottom
			_currentY1 -= leftOver;
			_currentY2 -= leftOver;
			//bottom two vertices
			md.Vertices.Add(new float3(_wallSegmentFirstVertex.x, _currentY1, _wallSegmentFirstVertex.z));
			md.Vertices.Add(new float3(_wallSegmentSecondVertex.x, _currentY2, _wallSegmentSecondVertex.z));

			if (_wallSegmentLength >= _minWallLength)
			{
				md.UV.Add( new float2(_currentTextureRect.xMin, _currentTextureRect.yMax));
				md.UV.Add( new float2(_rightOfEdgeUv, _currentTextureRect.yMax));
				md.UV.Add( new float2(_currentTextureRect.xMin,
					_currentTextureRect.yMax - ShortRowHeightDelta));
				md.UV.Add( new float2(_rightOfEdgeUv, _currentTextureRect.yMax - ShortRowHeightDelta));
			}
			else
			{
				md.UV.Add( new float2(_currentTextureRect.xMin, _currentTextureRect.yMax));
				md.UV.Add( new float2(_currentTextureRect.xMin + NarrowWallWidthDelta, _currentTextureRect.yMax));
				md.UV.Add( new float2(_currentTextureRect.xMin,
					_currentTextureRect.yMax - ShortRowHeightDelta));
				md.UV.Add( new float2(_currentTextureRect.xMin + NarrowWallWidthDelta,
					_currentTextureRect.yMax - ShortRowHeightDelta));
			}

			md.Normals.Add(_wallNormal);
			md.Normals.Add(_wallNormal);
			md.Normals.Add(_wallNormal);
			md.Normals.Add(_wallNormal);

			wallTriangles.Add(_triIndex);
			wallTriangles.Add(_triIndex + 1);
			wallTriangles.Add(_triIndex + 2);

			wallTriangles.Add(_triIndex + 1);
			wallTriangles.Add(_triIndex + 3);
			wallTriangles.Add(_triIndex + 2);

			_triIndex += 4;
		}

		private void MidFloors(MeshDataStruct md, ref NativeList<int> wallTriangles)
		{
			_currentMidHeight = _finalMidHeight;
			while (_currentMidHeight >= _singleFloorHeight - 0.01f)
			{
				//first part is the number of floors fitting current wall segment. You can fit max of "row count in mid". Or if wall
				//is smaller and it can only fit i.e. 3 floors instead of 5; we use 3/5 of the mid section texture as well.
				_midUvInCurrentStep =
					((float)Math.Min(_currentFacade.MidFloorCount,
						Math.Round(_currentMidHeight / _singleFloorHeight))) / _currentFacade.MidFloorCount;

				//top two vertices
				md.Vertices.Add(new float3(_wallSegmentFirstVertex.x, _currentY1, _wallSegmentFirstVertex.z));
				md.Vertices.Add(new float3(_wallSegmentSecondVertex.x, _currentY2, _wallSegmentSecondVertex.z));
				//move offsets bottom
				_currentY1 -= (_scaledFloorHeight * _midUvInCurrentStep);
				_currentY2 -= (_scaledFloorHeight * _midUvInCurrentStep);
				//bottom two vertices
				md.Vertices.Add(new float3(_wallSegmentFirstVertex.x, _currentY1, _wallSegmentFirstVertex.z));
				md.Vertices.Add(new float3(_wallSegmentSecondVertex.x, _currentY2, _wallSegmentSecondVertex.z));

				//we uv narrow walls different so they won't have condensed windows
				if (_wallSegmentLength >= _minWallLength)
				{
					md.UV.Add(new float2(_currentTextureRect.xMin, _currentFacade.TopOfMidUv));
					md.UV.Add(new float2(_rightOfEdgeUv, _currentFacade.TopOfMidUv));
					md.UV.Add(new float2(_currentTextureRect.xMin,
						_currentFacade.TopOfMidUv - _currentFacade.MidUvHeight * _midUvInCurrentStep));
					md.UV.Add(new float2(_rightOfEdgeUv,
						_currentFacade.TopOfMidUv - _currentFacade.MidUvHeight * _midUvInCurrentStep));
				}
				else
				{
					md.UV.Add(new float2(_currentTextureRect.xMin, _currentFacade.TopOfMidUv));
					md.UV.Add(new float2(_currentTextureRect.xMin + NarrowWallWidthDelta,
						_currentFacade.TopOfMidUv));
					md.UV.Add(new float2(_currentTextureRect.xMin,
						_currentFacade.TopOfMidUv - _currentFacade.MidUvHeight * _midUvInCurrentStep));
					md.UV.Add(new float2(_currentTextureRect.xMin + NarrowWallWidthDelta,
						_currentFacade.TopOfMidUv - _currentFacade.MidUvHeight * _midUvInCurrentStep));
				}

				md.Normals.Add(_wallNormal);
				md.Normals.Add(_wallNormal);
				md.Normals.Add(_wallNormal);
				md.Normals.Add(_wallNormal);

				wallTriangles.Add(_triIndex);
				wallTriangles.Add(_triIndex + 1);
				wallTriangles.Add(_triIndex + 2);

				wallTriangles.Add(_triIndex + 1);
				wallTriangles.Add(_triIndex + 3);
				wallTriangles.Add(_triIndex + 2);

				_triIndex += 4;
				_currentMidHeight -= Math.Max(0.1f, (_scaledFloorHeight * _midUvInCurrentStep));
			}
		}

		private void TopFloor(MeshDataStruct md, float leftOver, ref NativeList<int> wallTriangles)
		{
			//top floor start
			_currentY1 -= _finalTopHeight;
			_currentY2 -= _finalTopHeight;
			md.Vertices.Add(new float3(_wallSegmentFirstVertex.x, _wallSegmentFirstVertex.y - leftOver,
				_wallSegmentFirstVertex.z));
			md.Vertices.Add(new float3(_wallSegmentSecondVertex.x, _wallSegmentSecondVertex.y - leftOver,
				_wallSegmentSecondVertex.z));
			md.Vertices.Add(new float3(_wallSegmentFirstVertex.x, _wallSegmentFirstVertex.y - leftOver - _finalTopHeight,
				_wallSegmentFirstVertex.z));
			md.Vertices.Add(new float3(_wallSegmentSecondVertex.x,
				_wallSegmentSecondVertex.y - leftOver - _finalTopHeight, _wallSegmentSecondVertex.z));

			if (_wallSegmentLength >= _minWallLength)
			{
				md.UV.Add(new float2(_currentTextureRect.xMin, _currentTextureRect.yMax));
				md.UV.Add(new float2(_rightOfEdgeUv, _currentTextureRect.yMax));
				md.UV.Add(new float2(_currentTextureRect.xMin, _currentFacade.BottomOfTopUv));
				md.UV.Add(new float2(_rightOfEdgeUv, _currentFacade.BottomOfTopUv));
			}
			else
			{
				md.UV.Add(new float2(_currentTextureRect.xMin, _currentTextureRect.yMax));
				md.UV.Add(new float2(_currentTextureRect.xMin + NarrowWallWidthDelta, _currentTextureRect.yMax));
				md.UV.Add(new float2(_currentTextureRect.xMin, _currentFacade.BottomOfTopUv));
				md.UV.Add(
					new float2(_currentTextureRect.xMin + NarrowWallWidthDelta, _currentFacade.BottomOfTopUv));
			}

			md.Normals.Add(_wallNormal);
			md.Normals.Add(_wallNormal);
			md.Normals.Add(_wallNormal);
			md.Normals.Add(_wallNormal);

			wallTriangles.Add(_triIndex);
			wallTriangles.Add(_triIndex + 1);
			wallTriangles.Add(_triIndex + 2);

			wallTriangles.Add(_triIndex + 1);
			wallTriangles.Add(_triIndex + 3);
			wallTriangles.Add(_triIndex + 2);

			_triIndex += 4;
		}

		private void FirstFloor(MeshDataStruct md, float hf, ref NativeList<int> wallTriangles)
		{
			md.Vertices.Add(new float3(_wallSegmentFirstVertex.x, _wallSegmentFirstVertex.y - hf + _finalFirstHeight,
				_wallSegmentFirstVertex.z));
			md.Vertices.Add(new float3(_wallSegmentSecondVertex.x, _wallSegmentSecondVertex.y - hf + _finalFirstHeight,
				_wallSegmentSecondVertex.z));
			md.Vertices.Add(new float3(_wallSegmentFirstVertex.x, _wallSegmentFirstVertex.y - hf,
				_wallSegmentFirstVertex.z));
			md.Vertices.Add(new float3(_wallSegmentSecondVertex.x, _wallSegmentSecondVertex.y - hf,
				_wallSegmentSecondVertex.z));

			md.Normals.Add(_wallNormal);
			md.Normals.Add(_wallNormal);
			md.Normals.Add(_wallNormal);
			md.Normals.Add(_wallNormal);


			if (_wallSegmentLength >= _minWallLength)
			{
				md.UV.Add(new float2(_currentTextureRect.xMin, _currentFacade.TopOfBottomUv));
				md.UV.Add(new float2(_rightOfEdgeUv, _currentFacade.TopOfBottomUv));
				md.UV.Add(new float2(_currentTextureRect.xMin, _currentTextureRect.yMin));
				md.UV.Add(new float2(_rightOfEdgeUv, _currentTextureRect.yMin));
			}
			else
			{
				md.UV.Add(new float2(_currentTextureRect.xMin, _currentFacade.TopOfBottomUv));
				md.UV.Add(
					new float2(_currentTextureRect.xMin + NarrowWallWidthDelta, _currentFacade.TopOfBottomUv));
				md.UV.Add(new float2(_currentTextureRect.xMin, _currentTextureRect.yMin));
				md.UV.Add(new float2(_currentTextureRect.xMin + NarrowWallWidthDelta, _currentTextureRect.yMin));
			}

			wallTriangles.Add(_triIndex);
			wallTriangles.Add(_triIndex + 1);
			wallTriangles.Add(_triIndex + 2);

			wallTriangles.Add(_triIndex + 1);
			wallTriangles.Add(_triIndex + 3);
			wallTriangles.Add(_triIndex + 2);

			_triIndex += 4;
		}

		private void GenerateRoofMesh(MeshDataStruct md)
		{
			_counter = md.Vertices.Length;
			for (var i = 0; i < _counter; i++)
			{
				md.Vertices[i] = new float3(md.Vertices[i].x, md.Vertices[i].y + _maxHeight, md.Vertices[i].z);
			}
		}
	}
}
