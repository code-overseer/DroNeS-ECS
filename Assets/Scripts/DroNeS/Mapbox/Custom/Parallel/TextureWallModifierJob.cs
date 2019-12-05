using System;
using System.Runtime.InteropServices;
using DroNeS.Utils.Time;
using Mapbox.Unity.Map;
using Mapbox.Unity.MeshGeneration.Data;
using Mapbox.Unity.MeshGeneration.Modifiers;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace DroNeS.Mapbox.Custom.Parallel
{
	[BurstCompile]
	public struct TextureSideWallModifierJob : IJob
	{
		private const float WallSizeEpsilon = 0.99f;
		private const float NarrowWallWidthDelta = 0.01f;
		private const float ShortRowHeightDelta = 0.015f;
		private const float Scale = 0.7571877f;

		private AtlasEntityStruct _currentFacade;
		private ModifierVectors _vectors;
		private ModifierFloats _floats;
		private readonly float _maxHeight;

		private MeshDataStruct _output;

		private TextureSideWallModifierJob(GeometryExtrusionWithAtlasOptions options, VectorFeatureUnity feature, ref MeshDataStruct output)
		{
			_vectors = default;
			_floats = default;
			var minHeight = 0.0f;
			_maxHeight = 0.0f;
			if (feature.Properties.ContainsKey(options.propertyName))
			{
				_maxHeight = Convert.ToSingle(feature.Properties[options.propertyName]);
				if (feature.Properties.ContainsKey("min_height"))
				{
					minHeight = Convert.ToSingle(feature.Properties["min_height"]);
				}
			}

			_currentFacade = options.atlasInfo.Textures[0];
			_currentFacade.CalculateParameters();
			_floats.extrusionScaleFactor = options.extrusionScaleFactor;
			_maxHeight = _maxHeight * _floats.extrusionScaleFactor * Scale;
			minHeight = minHeight * _floats.extrusionScaleFactor * Scale;
			
			_floats.height = _maxHeight - minHeight;
			_output = output;
		}

		public static JobHandle Schedule(JobHandle dependencies, GeometryExtrusionWithAtlasOptions options, VectorFeatureUnity feature,
			ref MeshDataStruct output)
		{
			if (output.Vertices.Length == 0 || feature == null || feature.Points.Count < 1) return default;

			return new TextureSideWallModifierJob(options, feature, ref output).Schedule(dependencies);
		}
		
		public void Execute()
		{
			_floats.singleFloorHeight = Scale * _currentFacade.FloorHeight / _currentFacade.MidFloorCount;
			
			var scaledPreferredWallLength = Scale * _currentFacade.PreferredEdgeSectionLength;
			_floats.scaledFloorHeight = scaledPreferredWallLength * _currentFacade.WallToFloorRatio;

			for (var i = 0; i < _output.Vertices.Length; i++) _output.Vertices[i] = _output.Vertices[i] + Vector3.up * _maxHeight;

			var scaledTopFloorHeight = Scale * _currentFacade.TopFloorHeight;

			//limiting section heights, first floor gets priority, then we draw top floor, then mid if we still have space
			_floats.finalFirstHeight = math.min(_floats.height, Scale * _currentFacade.FirstFloorHeight);
			_floats.finalTopHeight = _floats.height - _floats.finalFirstHeight < scaledTopFloorHeight ? 0 : scaledTopFloorHeight;
			_floats.finalMidHeight = math.max(0, _floats.height - (_floats.finalFirstHeight + _floats.finalTopHeight));


			if (_floats.finalMidHeight > 0)
			{
				_floats.finalLeftOverRowHeight = _floats.finalMidHeight;
				_floats.finalLeftOverRowHeight %= _floats.singleFloorHeight;
				_floats.finalMidHeight -= _floats.finalLeftOverRowHeight;
			}
			else
			{
				_floats.finalLeftOverRowHeight = _floats.finalTopHeight;
			}
			
			var singleColumnLength = scaledPreferredWallLength / _currentFacade.ColumnCount;
			
			for (var i = 0; i < _output.Edges.Length; i += 2)
			{
				var v1 = _output.Vertices[_output.Edges[i]];
				var v2 = _output.Vertices[_output.Edges[i + 1]];

				var currentWallLength = Vector3.Distance(v1, v2);
				var leftOverColumnLength = currentWallLength % singleColumnLength;
				var start = v1;
				var wallSegmentDirection = (v2 - v1).normalized;

				//half of leftover column (if _centerSegments ofc) at the beginning
				if (currentWallLength > singleColumnLength)
				{
					//save left,right vertices and wall length
					_vectors.wallSegmentFirstVertex = start;
					_floats.wallSegmentLength = leftOverColumnLength / 2;
					start += wallSegmentDirection * _floats.wallSegmentLength;
					_vectors.wallSegmentSecondVertex = start;

					leftOverColumnLength /= 2;
					CreateWall();
				}

				while (currentWallLength > singleColumnLength)
				{
					_vectors.wallSegmentFirstVertex = start;
					//columns fitting wall / max column we have in texture
					var stepRatio =
						math.min(_currentFacade.ColumnCount,
							math.floor(currentWallLength / singleColumnLength)) / _currentFacade.ColumnCount;
					_floats.wallSegmentLength = stepRatio * scaledPreferredWallLength;
					start += wallSegmentDirection * _floats.wallSegmentLength;
					_vectors.wallSegmentSecondVertex = start;

					currentWallLength -= stepRatio * scaledPreferredWallLength;
					CreateWall();
				}

				//left over column at the end
				if (!(leftOverColumnLength > 0)) continue;
				_vectors.wallSegmentFirstVertex = start;
				_vectors.wallSegmentSecondVertex = v2;
				_floats.wallSegmentLength = leftOverColumnLength;
				CreateWall();
			}

		}
		
		private void CreateWall()
		{
			var s = Scale * _currentFacade.PreferredEdgeSectionLength;
			_floats.columnScaleRatio = math.min(1, _floats.wallSegmentLength / s);
			_floats.rightOfEdgeUv =
				_currentFacade.TextureRect.xMin +
				_currentFacade.TextureRect.size.x *
				_floats.columnScaleRatio;

			_floats.minWallLength = s * WallSizeEpsilon / _currentFacade.ColumnCount;
			
			//common for all top/mid/bottom segments
			_vectors.wallNormal = new Vector3(-(_vectors.wallSegmentFirstVertex.z - _vectors.wallSegmentSecondVertex.z), 0,
				(_vectors.wallSegmentFirstVertex.x - _vectors.wallSegmentSecondVertex.x)).normalized;
			//height of the left/right edges
			_floats.currentY1 = _vectors.wallSegmentFirstVertex.y;
			_floats.currentY2 = _vectors.wallSegmentSecondVertex.y;

			//moving leftover row to top
			LeftOverRow();

			FirstFloor();
			TopFloor();
			MidFloors();
		}

		private void LeftOverRow()
		{
			//leftover. we're moving small leftover row to top of the building
			if (!(_floats.finalLeftOverRowHeight > 0)) return;

			var triIndex = _output.Vertices.Length;
			
			_output.Vertices.Add(new Vector3(_vectors.wallSegmentFirstVertex.x, _floats.currentY1, _vectors.wallSegmentFirstVertex.z));
			_output.Vertices.Add(new Vector3(_vectors.wallSegmentSecondVertex.x, _floats.currentY2, _vectors.wallSegmentSecondVertex.z));
			//move offsets bottom
			_floats.currentY1 -= _floats.finalLeftOverRowHeight;
			_floats.currentY2 -= _floats.finalLeftOverRowHeight;
			//bottom two vertices
			_output.Vertices.Add(new Vector3(_vectors.wallSegmentFirstVertex.x, _floats.currentY1, _vectors.wallSegmentFirstVertex.z));
			_output.Vertices.Add(new Vector3(_vectors.wallSegmentSecondVertex.x, _floats.currentY2, _vectors.wallSegmentSecondVertex.z));

			if (_floats.wallSegmentLength >= _floats.minWallLength)
			{
				_output.UV.Add(new Vector2(_currentFacade.TextureRect.xMin, _currentFacade.TextureRect.yMax));
				_output.UV.Add(new Vector2(_floats.rightOfEdgeUv, _currentFacade.TextureRect.yMax));
				_output.UV.Add(new Vector2(_currentFacade.TextureRect.xMin,
					_currentFacade.TextureRect.yMax - ShortRowHeightDelta));
				_output.UV.Add(new Vector2(_floats.rightOfEdgeUv, _currentFacade.TextureRect.yMax - ShortRowHeightDelta));
			}
			else
			{
				_output.UV.Add(new Vector2(_currentFacade.TextureRect.xMin, _currentFacade.TextureRect.yMax));
				_output.UV.Add(
					new Vector2(_currentFacade.TextureRect.xMin + NarrowWallWidthDelta, _currentFacade.TextureRect.yMax));
				_output.UV.Add(new Vector2(_currentFacade.TextureRect.xMin,
					_currentFacade.TextureRect.yMax - ShortRowHeightDelta));
				_output.UV.Add(new Vector2(_currentFacade.TextureRect.xMin + NarrowWallWidthDelta,
					_currentFacade.TextureRect.yMax - ShortRowHeightDelta));
			}

			_output.Normals.Add(_vectors.wallNormal);
			_output.Normals.Add(_vectors.wallNormal);
			_output.Normals.Add(_vectors.wallNormal);
			_output.Normals.Add(_vectors.wallNormal);

			_output.Triangles.Add(triIndex);
			_output.Triangles.Add(triIndex + 1);
			_output.Triangles.Add(triIndex + 2);

			_output.Triangles.Add(triIndex + 1);
			_output.Triangles.Add(triIndex + 3);
			_output.Triangles.Add(triIndex + 2);
		}

		private void MidFloors()
		{
			var currentMidHeight = _floats.finalMidHeight;
			while (currentMidHeight >= _floats.singleFloorHeight - 0.01f)
			{
				var triIndex = _output.Vertices.Length;
				var midUvInCurrentStep =
					math.min(_currentFacade.MidFloorCount,
						math.round(currentMidHeight / _floats.singleFloorHeight)) / _currentFacade.MidFloorCount;

				//top two vertices
				_output.Vertices.Add(new Vector3(_vectors.wallSegmentFirstVertex.x, _floats.currentY1, _vectors.wallSegmentFirstVertex.z));
				_output.Vertices.Add(new Vector3(_vectors.wallSegmentSecondVertex.x, _floats.currentY2, _vectors.wallSegmentSecondVertex.z));
				//move offsets bottom
				_floats.currentY1 -= (_floats.scaledFloorHeight * midUvInCurrentStep);
				_floats.currentY2 -= (_floats.scaledFloorHeight * midUvInCurrentStep);
				//bottom two vertices
				_output.Vertices.Add(new Vector3(_vectors.wallSegmentFirstVertex.x, _floats.currentY1, _vectors.wallSegmentFirstVertex.z));
				_output.Vertices.Add(new Vector3(_vectors.wallSegmentSecondVertex.x, _floats.currentY2, _vectors.wallSegmentSecondVertex.z));

				//we uv narrow walls different so they won't have condensed windows
				if (_floats.wallSegmentLength >= _floats.minWallLength)
				{
					_output.UV.Add(new Vector2(_currentFacade.TextureRect.xMin, _currentFacade.TopOfMidUv));
					_output.UV.Add(new Vector2(_floats.rightOfEdgeUv, _currentFacade.TopOfMidUv));
					_output.UV.Add(new Vector2(_currentFacade.TextureRect.xMin,
						_currentFacade.TopOfMidUv - _currentFacade.MidUvHeight * midUvInCurrentStep));
					_output.UV.Add(new Vector2(_floats.rightOfEdgeUv,
						_currentFacade.TopOfMidUv - _currentFacade.MidUvHeight * midUvInCurrentStep));
				}
				else
				{
					_output.UV.Add(new Vector2(_currentFacade.TextureRect.xMin, _currentFacade.TopOfMidUv));
					_output.UV.Add(new Vector2(_currentFacade.TextureRect.xMin + NarrowWallWidthDelta,
						_currentFacade.TopOfMidUv));
					_output.UV.Add(new Vector2(_currentFacade.TextureRect.xMin,
						_currentFacade.TopOfMidUv - _currentFacade.MidUvHeight * midUvInCurrentStep));
					_output.UV.Add(new Vector2(_currentFacade.TextureRect.xMin + NarrowWallWidthDelta,
						_currentFacade.TopOfMidUv - _currentFacade.MidUvHeight * midUvInCurrentStep));
				}

				_output.Normals.Add(_vectors.wallNormal);
				_output.Normals.Add(_vectors.wallNormal);
				_output.Normals.Add(_vectors.wallNormal);
				_output.Normals.Add(_vectors.wallNormal);
				

				_output.Triangles.Add(triIndex);
				_output.Triangles.Add(triIndex + 1);
				_output.Triangles.Add(triIndex + 2);

				_output.Triangles.Add(triIndex + 1);
				_output.Triangles.Add(triIndex + 3);
				_output.Triangles.Add(triIndex + 2);
				
				currentMidHeight -= math.max(0.1f, (_floats.scaledFloorHeight * midUvInCurrentStep));
			}
		}

		private void FirstFloor()
		{
			var triIndex = _output.Vertices.Length;
			_output.Vertices.Add(new Vector3(_vectors.wallSegmentFirstVertex.x, 
				_vectors.wallSegmentFirstVertex.y - _floats.height + _floats.finalFirstHeight,
				_vectors.wallSegmentFirstVertex.z));
			_output.Vertices.Add(new Vector3(_vectors.wallSegmentSecondVertex.x, 
				_vectors.wallSegmentSecondVertex.y - _floats.height + _floats.finalFirstHeight,
				_vectors.wallSegmentSecondVertex.z));
			_output.Vertices.Add(new Vector3(_vectors.wallSegmentFirstVertex.x, 
				_vectors.wallSegmentFirstVertex.y - _floats.height,
				_vectors.wallSegmentFirstVertex.z));
			_output.Vertices.Add(new Vector3(_vectors.wallSegmentSecondVertex.x, 
				_vectors.wallSegmentSecondVertex.y - _floats.height,
				_vectors.wallSegmentSecondVertex.z));

			_output.Normals.Add(_vectors.wallNormal);
			_output.Normals.Add(_vectors.wallNormal);
			_output.Normals.Add(_vectors.wallNormal);
			_output.Normals.Add(_vectors.wallNormal);

			if (_floats.wallSegmentLength >= _floats.minWallLength)
			{
				_output.UV.Add(new Vector2(_currentFacade.TextureRect.xMin, _currentFacade.TopOfBottomUv));
				_output.UV.Add(new Vector2(_floats.rightOfEdgeUv, _currentFacade.TopOfBottomUv));
				_output.UV.Add(new Vector2(_currentFacade.TextureRect.xMin, _currentFacade.TextureRect.yMin));
				_output.UV.Add(new Vector2(_floats.rightOfEdgeUv, _currentFacade.TextureRect.yMin));
			}
			else
			{
				_output.UV.Add(new Vector2(_currentFacade.TextureRect.xMin, _currentFacade.TopOfBottomUv));
				_output.UV.Add(
					new Vector2(_currentFacade.TextureRect.xMin + NarrowWallWidthDelta, _currentFacade.TopOfBottomUv));
				_output.UV.Add(new Vector2(_currentFacade.TextureRect.xMin, _currentFacade.TextureRect.yMin));
				_output.UV.Add(new Vector2(_currentFacade.TextureRect.xMin + NarrowWallWidthDelta, _currentFacade.TextureRect.yMin));
			}

			_output.Triangles.Add(triIndex);
			_output.Triangles.Add(triIndex + 1);
			_output.Triangles.Add(triIndex + 2);

			_output.Triangles.Add(triIndex + 1);
			_output.Triangles.Add(triIndex + 3);
			_output.Triangles.Add(triIndex + 2);

		}
		
		private void TopFloor()
		{
			var triIndex = _output.Vertices.Length;
			_floats.currentY1 -= _floats.finalTopHeight;
			_floats.currentY2 -= _floats.finalTopHeight;
			_output.Vertices.Add(new Vector3(_vectors.wallSegmentFirstVertex.x, 
				_vectors.wallSegmentFirstVertex.y - _floats.finalLeftOverRowHeight,
				_vectors.wallSegmentFirstVertex.z));
			_output.Vertices.Add(new Vector3(_vectors.wallSegmentSecondVertex.x, 
				_vectors.wallSegmentSecondVertex.y - _floats.finalLeftOverRowHeight,
				_vectors.wallSegmentSecondVertex.z));
			_output.Vertices.Add(new Vector3(_vectors.wallSegmentFirstVertex.x, 
				_vectors.wallSegmentFirstVertex.y - _floats.finalLeftOverRowHeight - _floats.finalTopHeight,
				_vectors.wallSegmentFirstVertex.z));
			_output.Vertices.Add(new Vector3(_vectors.wallSegmentSecondVertex.x,
				_vectors.wallSegmentSecondVertex.y - _floats.finalLeftOverRowHeight - _floats.finalTopHeight, 
				_vectors.wallSegmentSecondVertex.z));

			if (_floats.wallSegmentLength >= _floats.minWallLength)
			{
				_output.UV.Add(new Vector2(_currentFacade.TextureRect.xMin, _currentFacade.TextureRect.yMax));
				_output.UV.Add(new Vector2(_floats.rightOfEdgeUv, _currentFacade.TextureRect.yMax));
				_output.UV.Add(new Vector2(_currentFacade.TextureRect.xMin, _currentFacade.BottomOfTopUv));
				_output.UV.Add(new Vector2(_floats.rightOfEdgeUv, _currentFacade.BottomOfTopUv));
			}
			else
			{
				_output.UV.Add(new Vector2(_currentFacade.TextureRect.xMin, _currentFacade.TextureRect.yMax));
				_output.UV.Add(new Vector2(_currentFacade.TextureRect.xMin + NarrowWallWidthDelta, _currentFacade.TextureRect.yMax));
				_output.UV.Add(new Vector2(_currentFacade.TextureRect.xMin, _currentFacade.BottomOfTopUv));
				_output.UV.Add(
					new Vector2(_currentFacade.TextureRect.xMin + NarrowWallWidthDelta, _currentFacade.BottomOfTopUv));
			}

			_output.Normals.Add(_vectors.wallNormal);
			_output.Normals.Add(_vectors.wallNormal);
			_output.Normals.Add(_vectors.wallNormal);
			_output.Normals.Add(_vectors.wallNormal);

			_output.Triangles.Add(triIndex);
			_output.Triangles.Add(triIndex + 1);
			_output.Triangles.Add(triIndex + 2);

			_output.Triangles.Add(triIndex + 1);
			_output.Triangles.Add(triIndex + 3);
			_output.Triangles.Add(triIndex + 2);
		}
	}

	[StructLayout(LayoutKind.Sequential)]
    public struct ModifierFloats
    {
	    public float wallSegmentLength;
	    public float finalFirstHeight;
	    public float finalTopHeight;
	    public float finalMidHeight;
	    public float finalLeftOverRowHeight;
	    public float scaledFloorHeight;
	    public float columnScaleRatio;
	    public float rightOfEdgeUv;
	    public float currentY1;
	    public float currentY2;
	    public float height;
	    public float minWallLength;
	    public float singleFloorHeight;
	    public float extrusionScaleFactor;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ModifierVectors
    {
	    public Vector3 wallSegmentFirstVertex;
	    public Vector3 wallSegmentSecondVertex;
	    public Vector3 wallNormal;
    }

    public abstract class CustomMeshModifier : MeshModifier
    {
	    public virtual void Run(VectorFeatureUnity feature, ref MeshDataStruct md)
	    {
		    
	    }
    }
    
    public class JobifiedTextureWallModifier : CustomMeshModifier
    {
	    private GeometryExtrusionWithAtlasOptions _options;
	    public override void SetProperties(ModifierProperties properties)
	    {
		    if (!(properties is GeometryExtrusionWithAtlasOptions options))
			    throw new ArgumentException("Expected GeometryExtrusionWithAtlasOptions");
		    _options = options;
	    }

	    public override void Run(VectorFeatureUnity feature, ref MeshDataStruct md)
	    {
		    TextureSideWallModifierJob.Schedule(default, _options, feature, ref md).Complete();
	    }
    }
    
    
    public class StrippedTextureSideWallModifier : MeshModifier
	{
		private const float WallSizeEpsilon = 0.99f;
		private const float NarrowWallWidthDelta = 0.01f;
		private const float ShortRowHeightDelta = 0.015f;
		private const float Scale = 0.7571877f;
		
		private AtlasEntity _currentFacade;
		private Rect _currentTextureRect;
		private GeometryExtrusionWithAtlasOptions _options;
		private ModifierVectors _vectors;
		private ModifierFloats _floats;
		private MeshData Output;

		public override void SetProperties(ModifierProperties properties)
		{
			if (!(properties is GeometryExtrusionWithAtlasOptions options))
				throw new ArgumentException("Expected GeometryExtrusionWithAtlasOptions");
			_currentFacade = options.atlasInfo.Textures[0];
			_currentFacade.CalculateParameters();
			_floats.extrusionScaleFactor = options.extrusionScaleFactor;
			_options = options;
		}

		public override void UnbindProperties() { }

		public override void UpdateModifier(object sender, EventArgs layerArgs)
		{
			SetProperties((ModifierProperties)sender);
			NotifyUpdateModifier(new VectorLayerUpdateArgs { property = (MapboxDataProperty) sender, modifier = this });
		}

		public override void Run(VectorFeatureUnity feature, MeshData md, UnityTile tile = null)
		{
			if (md.Vertices.Count == 0 || feature == null || feature.Points.Count < 1) return;
			Output = md;
			
			//rect is a struct so we're caching this
			_currentTextureRect = _currentFacade.TextureRect;

			//this can be moved to initialize or in an if clause if you're sure all your tiles will be same level/scale
			_floats.singleFloorHeight = Scale * _currentFacade.FloorHeight / _currentFacade.MidFloorCount;
			
			var scaledPreferredWallLength = Scale * _currentFacade.PreferredEdgeSectionLength;
			_floats.scaledFloorHeight = scaledPreferredWallLength * _currentFacade.WallToFloorRatio;
			//query height and push polygon up to create roof
			//can we do this vice versa and create roof at last?
			QueryHeight(feature, out var maxHeight, out var minHeight);
			maxHeight = maxHeight * _floats.extrusionScaleFactor * Scale;
			minHeight = minHeight * _floats.extrusionScaleFactor * Scale;
			_floats.height = maxHeight - minHeight;
			
			for (var i = 0; i < Output.Vertices.Count; i++) Output.Vertices[i] += Vector3.up * maxHeight;

			var scaledTopFloorHeight = Scale * _currentFacade.TopFloorHeight;

			//limiting section heights, first floor gets priority, then we draw top floor, then mid if we still have space
			_floats.finalFirstHeight = math.min(_floats.height, Scale * _currentFacade.FirstFloorHeight);
			_floats.finalTopHeight = _floats.height - _floats.finalFirstHeight < scaledTopFloorHeight ? 0 : scaledTopFloorHeight;
			_floats.finalMidHeight = math.max(0, _floats.height - (_floats.finalFirstHeight + _floats.finalTopHeight));


			if (_floats.finalMidHeight > 0)
			{
				_floats.finalLeftOverRowHeight = _floats.finalMidHeight;
				_floats.finalLeftOverRowHeight %= _floats.singleFloorHeight;
				_floats.finalMidHeight -= _floats.finalLeftOverRowHeight;
			}
			else
			{
				_floats.finalLeftOverRowHeight = _floats.finalTopHeight;
			}
			
			var singleColumnLength = scaledPreferredWallLength / _currentFacade.ColumnCount;
			for (var i = 0; i < Output.Edges.Count; i += 2)
			{
				var v1 = Output.Vertices[Output.Edges[i]];
				var v2 = Output.Vertices[Output.Edges[i + 1]];

				var currentWallLength = Vector3.Distance(v1, v2);
				var leftOverColumnLength = currentWallLength % singleColumnLength;
				var start = v1;
				var wallSegmentDirection = (v2 - v1).normalized;

				//half of leftover column (if _centerSegments ofc) at the begining
				if (currentWallLength > singleColumnLength)
				{
					//save left,right vertices and wall length
					_vectors.wallSegmentFirstVertex = start;
					_floats.wallSegmentLength = leftOverColumnLength / 2;
					start += wallSegmentDirection * _floats.wallSegmentLength;
					_vectors.wallSegmentSecondVertex = start;

					leftOverColumnLength /= 2;
					CreateWall();
				}

				while (currentWallLength > singleColumnLength)
				{
					_vectors.wallSegmentFirstVertex = start;
					//columns fitting wall / max column we have in texture
					var stepRatio =
						(float)math.min(_currentFacade.ColumnCount,
							math.floor(currentWallLength / singleColumnLength)) / _currentFacade.ColumnCount;
					_floats.wallSegmentLength = stepRatio * scaledPreferredWallLength;
					start += wallSegmentDirection * _floats.wallSegmentLength;
					_vectors.wallSegmentSecondVertex = start;

					currentWallLength -= stepRatio * scaledPreferredWallLength;
					CreateWall();
				}

				//left over column at the end
				if (!(leftOverColumnLength > 0)) continue;
				_vectors.wallSegmentFirstVertex = start;
				_vectors.wallSegmentSecondVertex = v2;
				_floats.wallSegmentLength = leftOverColumnLength;
				CreateWall();
			}
		}

		private void CreateWall()
		{
			var s = Scale * _currentFacade.PreferredEdgeSectionLength;
			_floats.columnScaleRatio = math.min(1, _floats.wallSegmentLength / s);
			_floats.rightOfEdgeUv =
				_currentTextureRect.xMin +
				_currentTextureRect.size.x *
				_floats.columnScaleRatio;

			_floats.minWallLength = s * WallSizeEpsilon / _currentFacade.ColumnCount;
			
			//common for all top/mid/bottom segments
			_vectors.wallNormal = new Vector3(-(_vectors.wallSegmentFirstVertex.z - _vectors.wallSegmentSecondVertex.z), 0,
				(_vectors.wallSegmentFirstVertex.x - _vectors.wallSegmentSecondVertex.x)).normalized;
			//height of the left/right edges
			_floats.currentY1 = _vectors.wallSegmentFirstVertex.y;
			_floats.currentY2 = _vectors.wallSegmentSecondVertex.y;

			//moving leftover row to top
			LeftOverRow();

			FirstFloor();
			TopFloor();
			MidFloors();
		}

		private void LeftOverRow()
		{
			//leftover. we're moving small leftover row to top of the building
			if (!(_floats.finalLeftOverRowHeight > 0)) return;

			var triIndex = Output.Vertices.Count;
			
			Output.Vertices.Add(new Vector3(_vectors.wallSegmentFirstVertex.x, _floats.currentY1, _vectors.wallSegmentFirstVertex.z));
			Output.Vertices.Add(new Vector3(_vectors.wallSegmentSecondVertex.x, _floats.currentY2, _vectors.wallSegmentSecondVertex.z));
			//move offsets bottom
			_floats.currentY1 -= _floats.finalLeftOverRowHeight;
			_floats.currentY2 -= _floats.finalLeftOverRowHeight;
			//bottom two vertices
			Output.Vertices.Add(new Vector3(_vectors.wallSegmentFirstVertex.x, _floats.currentY1, _vectors.wallSegmentFirstVertex.z));
			Output.Vertices.Add(new Vector3(_vectors.wallSegmentSecondVertex.x, _floats.currentY2, _vectors.wallSegmentSecondVertex.z));

			if (_floats.wallSegmentLength >= _floats.minWallLength)
			{
				Output.UV[0].Add(new Vector2(_currentTextureRect.xMin, _currentTextureRect.yMax));
				Output.UV[0].Add(new Vector2(_floats.rightOfEdgeUv, _currentTextureRect.yMax));
				Output.UV[0].Add(new Vector2(_currentTextureRect.xMin,
					_currentTextureRect.yMax - ShortRowHeightDelta));
				Output.UV[0].Add(new Vector2(_floats.rightOfEdgeUv, _currentTextureRect.yMax - ShortRowHeightDelta));
			}
			else
			{
				Output.UV[0].Add(new Vector2(_currentTextureRect.xMin, _currentTextureRect.yMax));
				Output.UV[0].Add(
					new Vector2(_currentTextureRect.xMin + NarrowWallWidthDelta, _currentTextureRect.yMax));
				Output.UV[0].Add(new Vector2(_currentTextureRect.xMin,
					_currentTextureRect.yMax - ShortRowHeightDelta));
				Output.UV[0].Add(new Vector2(_currentTextureRect.xMin + NarrowWallWidthDelta,
					_currentTextureRect.yMax - ShortRowHeightDelta));
			}

			Output.Normals.Add(_vectors.wallNormal);
			Output.Normals.Add(_vectors.wallNormal);
			Output.Normals.Add(_vectors.wallNormal);
			Output.Normals.Add(_vectors.wallNormal);

			Output.Triangles[0].Add(triIndex);
			Output.Triangles[0].Add(triIndex + 1);
			Output.Triangles[0].Add(triIndex + 2);

			Output.Triangles[0].Add(triIndex + 1);
			Output.Triangles[0].Add(triIndex + 3);
			Output.Triangles[0].Add(triIndex + 2);
		}

		private void MidFloors()
		{
			var currentMidHeight = _floats.finalMidHeight;
			while (currentMidHeight >= _floats.singleFloorHeight - 0.01f)
			{
				var triIndex = Output.Vertices.Count;
				var midUvInCurrentStep =
					((float)math.min(_currentFacade.MidFloorCount,
						math.round(currentMidHeight / _floats.singleFloorHeight))) / _currentFacade.MidFloorCount;

				//top two vertices
				Output.Vertices.Add(new Vector3(_vectors.wallSegmentFirstVertex.x, _floats.currentY1, _vectors.wallSegmentFirstVertex.z));
				Output.Vertices.Add(new Vector3(_vectors.wallSegmentSecondVertex.x, _floats.currentY2, _vectors.wallSegmentSecondVertex.z));
				//move offsets bottom
				_floats.currentY1 -= (_floats.scaledFloorHeight * midUvInCurrentStep);
				_floats.currentY2 -= (_floats.scaledFloorHeight * midUvInCurrentStep);
				//bottom two vertices
				Output.Vertices.Add(new Vector3(_vectors.wallSegmentFirstVertex.x, _floats.currentY1, _vectors.wallSegmentFirstVertex.z));
				Output.Vertices.Add(new Vector3(_vectors.wallSegmentSecondVertex.x, _floats.currentY2, _vectors.wallSegmentSecondVertex.z));

				//we uv narrow walls different so they won't have condensed windows
				if (_floats.wallSegmentLength >= _floats.minWallLength)
				{
					Output.UV[0].Add(new Vector2(_currentTextureRect.xMin, _currentFacade.topOfMidUv));
					Output.UV[0].Add(new Vector2(_floats.rightOfEdgeUv, _currentFacade.topOfMidUv));
					Output.UV[0].Add(new Vector2(_currentTextureRect.xMin,
						_currentFacade.topOfMidUv - _currentFacade.midUvHeight * midUvInCurrentStep));
					Output.UV[0].Add(new Vector2(_floats.rightOfEdgeUv,
						_currentFacade.topOfMidUv - _currentFacade.midUvHeight * midUvInCurrentStep));
				}
				else
				{
					Output.UV[0].Add(new Vector2(_currentTextureRect.xMin, _currentFacade.topOfMidUv));
					Output.UV[0].Add(new Vector2(_currentTextureRect.xMin + NarrowWallWidthDelta,
						_currentFacade.topOfMidUv));
					Output.UV[0].Add(new Vector2(_currentTextureRect.xMin,
						_currentFacade.topOfMidUv - _currentFacade.midUvHeight * midUvInCurrentStep));
					Output.UV[0].Add(new Vector2(_currentTextureRect.xMin + NarrowWallWidthDelta,
						_currentFacade.topOfMidUv - _currentFacade.midUvHeight * midUvInCurrentStep));
				}

				Output.Normals.Add(_vectors.wallNormal);
				Output.Normals.Add(_vectors.wallNormal);
				Output.Normals.Add(_vectors.wallNormal);
				Output.Normals.Add(_vectors.wallNormal);
				

				Output.Triangles[0].Add(triIndex);
				Output.Triangles[0].Add(triIndex + 1);
				Output.Triangles[0].Add(triIndex + 2);

				Output.Triangles[0].Add(triIndex + 1);
				Output.Triangles[0].Add(triIndex + 3);
				Output.Triangles[0].Add(triIndex + 2);
				
				currentMidHeight -= math.max(0.1f, (_floats.scaledFloorHeight * midUvInCurrentStep));
			}
		}

		private void FirstFloor()
		{
			var triIndex = Output.Vertices.Count;
			Output.Vertices.Add(new Vector3(_vectors.wallSegmentFirstVertex.x, 
				_vectors.wallSegmentFirstVertex.y - _floats.height + _floats.finalFirstHeight,
				_vectors.wallSegmentFirstVertex.z));
			Output.Vertices.Add(new Vector3(_vectors.wallSegmentSecondVertex.x, 
				_vectors.wallSegmentSecondVertex.y - _floats.height + _floats.finalFirstHeight,
				_vectors.wallSegmentSecondVertex.z));
			Output.Vertices.Add(new Vector3(_vectors.wallSegmentFirstVertex.x, 
				_vectors.wallSegmentFirstVertex.y - _floats.height,
				_vectors.wallSegmentFirstVertex.z));
			Output.Vertices.Add(new Vector3(_vectors.wallSegmentSecondVertex.x, 
				_vectors.wallSegmentSecondVertex.y - _floats.height,
				_vectors.wallSegmentSecondVertex.z));

			Output.Normals.Add(_vectors.wallNormal);
			Output.Normals.Add(_vectors.wallNormal);
			Output.Normals.Add(_vectors.wallNormal);
			Output.Normals.Add(_vectors.wallNormal);

			if (_floats.wallSegmentLength >= _floats.minWallLength)
			{
				Output.UV[0].Add(new Vector2(_currentTextureRect.xMin, _currentFacade.topOfBottomUv));
				Output.UV[0].Add(new Vector2(_floats.rightOfEdgeUv, _currentFacade.topOfBottomUv));
				Output.UV[0].Add(new Vector2(_currentTextureRect.xMin, _currentTextureRect.yMin));
				Output.UV[0].Add(new Vector2(_floats.rightOfEdgeUv, _currentTextureRect.yMin));
			}
			else
			{
				Output.UV[0].Add(new Vector2(_currentTextureRect.xMin, _currentFacade.topOfBottomUv));
				Output.UV[0].Add(
					new Vector2(_currentTextureRect.xMin + NarrowWallWidthDelta, _currentFacade.topOfBottomUv));
				Output.UV[0].Add(new Vector2(_currentTextureRect.xMin, _currentTextureRect.yMin));
				Output.UV[0].Add(new Vector2(_currentTextureRect.xMin + NarrowWallWidthDelta, _currentTextureRect.yMin));
			}

			Output.Triangles[0].Add(triIndex);
			Output.Triangles[0].Add(triIndex + 1);
			Output.Triangles[0].Add(triIndex + 2);

			Output.Triangles[0].Add(triIndex + 1);
			Output.Triangles[0].Add(triIndex + 3);
			Output.Triangles[0].Add(triIndex + 2);

		}
		
		private void TopFloor()
		{
			var triIndex = Output.Vertices.Count;
			_floats.currentY1 -= _floats.finalTopHeight;
			_floats.currentY2 -= _floats.finalTopHeight;
			Output.Vertices.Add(new Vector3(_vectors.wallSegmentFirstVertex.x, 
				_vectors.wallSegmentFirstVertex.y - _floats.finalLeftOverRowHeight,
				_vectors.wallSegmentFirstVertex.z));
			Output.Vertices.Add(new Vector3(_vectors.wallSegmentSecondVertex.x, 
				_vectors.wallSegmentSecondVertex.y - _floats.finalLeftOverRowHeight,
				_vectors.wallSegmentSecondVertex.z));
			Output.Vertices.Add(new Vector3(_vectors.wallSegmentFirstVertex.x, 
				_vectors.wallSegmentFirstVertex.y - _floats.finalLeftOverRowHeight - _floats.finalTopHeight,
				_vectors.wallSegmentFirstVertex.z));
			Output.Vertices.Add(new Vector3(_vectors.wallSegmentSecondVertex.x,
				_vectors.wallSegmentSecondVertex.y - _floats.finalLeftOverRowHeight - _floats.finalTopHeight, 
				_vectors.wallSegmentSecondVertex.z));

			if (_floats.wallSegmentLength >= _floats.minWallLength)
			{
				Output.UV[0].Add(new Vector2(_currentTextureRect.xMin, _currentTextureRect.yMax));
				Output.UV[0].Add(new Vector2(_floats.rightOfEdgeUv, _currentTextureRect.yMax));
				Output.UV[0].Add(new Vector2(_currentTextureRect.xMin, _currentFacade.bottomOfTopUv));
				Output.UV[0].Add(new Vector2(_floats.rightOfEdgeUv, _currentFacade.bottomOfTopUv));
			}
			else
			{
				Output.UV[0].Add(new Vector2(_currentTextureRect.xMin, _currentTextureRect.yMax));
				Output.UV[0].Add(new Vector2(_currentTextureRect.xMin + NarrowWallWidthDelta, _currentTextureRect.yMax));
				Output.UV[0].Add(new Vector2(_currentTextureRect.xMin, _currentFacade.bottomOfTopUv));
				Output.UV[0].Add(
					new Vector2(_currentTextureRect.xMin + NarrowWallWidthDelta, _currentFacade.bottomOfTopUv));
			}

			Output.Normals.Add(_vectors.wallNormal);
			Output.Normals.Add(_vectors.wallNormal);
			Output.Normals.Add(_vectors.wallNormal);
			Output.Normals.Add(_vectors.wallNormal);

			Output.Triangles[0].Add(triIndex);
			Output.Triangles[0].Add(triIndex + 1);
			Output.Triangles[0].Add(triIndex + 2);

			Output.Triangles[0].Add(triIndex + 1);
			Output.Triangles[0].Add(triIndex + 3);
			Output.Triangles[0].Add(triIndex + 2);
		}

		private void QueryHeight(VectorFeatureUnity feature, out float maxHeight, out float minHeight)
		{
			minHeight = 0.0f;
			maxHeight = 0.0f;
			if (!feature.Properties.ContainsKey(_options.propertyName)) return;
			
			maxHeight = Convert.ToSingle(feature.Properties[_options.propertyName]);
			if (feature.Properties.ContainsKey("min_height"))
			{
				minHeight = Convert.ToSingle(feature.Properties["min_height"]);
			}
		}
	}
}
