﻿using System.Collections.Generic;
using System.Diagnostics;
using DroNeS.Mapbox.Custom;
using Mapbox.Map;
using Mapbox.Unity.Map;
using Mapbox.Unity.MeshGeneration.Enums;
using Mapbox.Unity.MeshGeneration.Modifiers;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DroNeS.Mapbox.ECS
{
	public class BuildingMeshFactory : CustomTileFactory
	{
		#region Private/Protected Fields
		private readonly Dictionary<string, List<BuildingMeshBuilder>> _layerBuilder;
		private readonly BuildingMeshFetcher _dataFetcher;
		#endregion

		#region Properties
		private string TilesetId => Properties.sourceOptions.Id;
		private VectorLayerProperties Properties { get; }
		#endregion
		
		public BuildingMeshFactory()
		{
			_layerBuilder = new Dictionary<string, List<BuildingMeshBuilder>>();

			_dataFetcher = ScriptableObject.CreateInstance<BuildingMeshFetcher>();
			_dataFetcher.dataReceived += OnVectorDataReceived;
			
			Properties = new VectorLayerProperties();
			var vslp = new VectorSubLayerProperties
			{
				colliderOptions = {colliderType = ColliderType.None},
				coreOptions =
				{
					geometryType = VectorPrimitiveType.Polygon,
					layerName = "building",
					snapToTerrain = true,
					combineMeshes = true
				},
				extrusionOptions =
				{
					extrusionType = ExtrusionType.PropertyHeight,
					extrusionScaleFactor = 1.3203f,
					propertyName = "height",
					extrusionGeometryType = ExtrusionGeometryType.RoofAndSide
				},
				moveFeaturePositionTo = PositionTargetType.CenterOfVertices
			};
			vslp.coreOptions.sublayerName = "Buildings";
            vslp.buildingsWithUniqueIds = true;
            Properties.vectorSubLayers.Add(vslp);
            CreateLayerVisualizers();
		}
		
		private void AddVectorLayerVisualizer(VectorSubLayerProperties subLayer)
		{
			//if its of type prefab item options then separate the visualizer type
			var visualizer = new BuildingMeshBuilder();

			subLayer.coreOptions.geometryType = VectorPrimitiveType.Polygon;
			subLayer.honorBuildingIdSetting = true;

			// Setup visualizer.
			visualizer.SetProperties(subLayer);
			visualizer.Initialize();

			if (_layerBuilder.ContainsKey(visualizer.Key))
			{
				_layerBuilder[visualizer.Key].Add(visualizer);
			}
			else
			{
				_layerBuilder.Add(visualizer.Key, new List<BuildingMeshBuilder> { visualizer });
			}
		}

		protected override void OnRegistered(CustomTile tile)
		{
			if (!Properties.sourceOptions.isActive || 
			    Properties.vectorSubLayers.Count + Properties.locationPrefabList.Count == 0)
			{
				tile.VectorDataState = TilePropertyState.None;
				return;
			}
			
			tile.VectorDataState = TilePropertyState.Loading;
			TilesWaitingResponse.Add(tile);
			
			var parameters = new BuildingMeshFetcherParameters()
			{
				canonicalTileId = tile.CanonicalTileId,
				tilesetId = TilesetId,
				cTile = tile,
				useOptimizedStyle = Properties.useOptimizedStyle,
				style = Properties.optimizedStyle
			};
			_dataFetcher.FetchData(parameters);
		}

		public void Clear()
		{
			Object.DestroyImmediate(_dataFetcher);
			if (_layerBuilder == null) return;
			TilesWaitingResponse.Clear();
			TilesWaitingProcessing.Clear();
		}

		private void OnVectorDataReceived(CustomTile tile, VectorTile vectorTile)
		{
			if (tile == null) return;
			TilesWaitingResponse.Remove(tile);
			tile.SetVectorData(vectorTile);
			
			CreateMeshes(tile);
		}
		
		#region Private Methods
		private void CreateMeshes(CustomTile tile)
		{
			var nameList = new List<string>();
			var builderList = new List<BuildingMeshBuilder>();

			foreach (var layerName in tile.VectorData.Data.LayerNames())
			{
				if (!_layerBuilder.ContainsKey(layerName)) continue;
				foreach (var builder in _layerBuilder[layerName])
				{
					nameList.Add(layerName);
					builderList.Add(builder);
				}
			}
			
			for (var i = 0; i < nameList.Count; i++)
			{
				CreateFeatureWithBuilder(tile, nameList[i], builderList[i]);
			}
			
		}

		private static void CreateFeatureWithBuilder(CustomTile tile, string layerName, BuildingMeshBuilder builder)
		{
			builder.Create(tile.VectorData.Data.GetLayer(layerName), tile);
		}

		private void CreateLayerVisualizers()
		{
			foreach (var sublayer in Properties.vectorSubLayers)
			{
				AddVectorLayerVisualizer(sublayer);
			}
		}

		#endregion

	}
}
