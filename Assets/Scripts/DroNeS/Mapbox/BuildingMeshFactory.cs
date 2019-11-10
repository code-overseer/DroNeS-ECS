using System;
using System.Collections.Generic;
using DroNeS.Systems;
using Mapbox.Map;
using Mapbox.Unity.Map;
using Mapbox.Unity.MeshGeneration.Data;
using Mapbox.Unity.MeshGeneration.Enums;
using Mapbox.Unity.MeshGeneration.Factories;
using Mapbox.Unity.MeshGeneration.Interfaces;
using Mapbox.Unity.MeshGeneration.Modifiers;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace DroNeS.Mapbox
{
	public class BuildingMeshFactory : CustomTileFactory
	{
		#region Private/Protected Fields
		private readonly Dictionary<string, List<BuildingMeshBuilder>> _layerBuilder;
		private readonly Dictionary<CustomTile, HashSet<BuildingMeshBuilder>> _layerProgress;
		private readonly BuildingMeshFetcher _dataFetcher;
		#endregion

		#region Properties
		public BuildingMeshFactory()
		{
			_layerProgress = new Dictionary<CustomTile, HashSet<BuildingMeshBuilder>>();
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

		private string TilesetId => Properties.sourceOptions.Id;

		private VectorLayerProperties Properties { get; }
		#endregion

		#region Public Layer
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

		public BuildingMeshBuilder FindVectorLayerVisualizer(VectorSubLayerProperties subLayer)
		{
			if (!_layerBuilder.ContainsKey(subLayer.Key)) return null;
			var visualizer = _layerBuilder[subLayer.Key].Find((obj) => obj.SubLayerProperties == subLayer);
			return visualizer;
		}
		#endregion

		#region AbstractFactoryOverrides

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
			UnityEngine.Object.DestroyImmediate(_dataFetcher);
			if (_layerBuilder == null) return;
			_layerProgress.Clear();
			TilesWaitingResponse.Clear();
			TilesWaitingProcessing.Clear();
		}

		#endregion

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
					TrackFeatureWithBuilder(tile, layerName, builder);
				}
			}
			
			for (var i = 0; i < nameList.Count; i++)
			{
				CreateFeatureWithBuilder(tile, nameList[i], builderList[i]);
			}

			if (!_layerProgress.ContainsKey(tile)) tile.VectorDataState = TilePropertyState.Loaded;
			
		}

		private void TrackFeatureWithBuilder(CustomTile tile, string layerName, BuildingMeshBuilder builder)
		{
			if (_layerProgress.ContainsKey(tile))
			{
				_layerProgress[tile].Add(builder);
			}
			else
			{
				_layerProgress.Add(tile, new HashSet<BuildingMeshBuilder> {builder});
				if (!TilesWaitingProcessing.Contains(tile))
				{
					TilesWaitingProcessing.Add(tile);
				}
			}
		}

		private void CreateFeatureWithBuilder(CustomTile tile, string layerName, BuildingMeshBuilder builder)
		{
			if (_layerProgress.ContainsKey(tile))
			{
				_layerProgress[tile].Add(builder);
			}
			else
			{
				_layerProgress.Add(tile, new HashSet<BuildingMeshBuilder> { builder });
				if (!TilesWaitingProcessing.Contains(tile)) TilesWaitingProcessing.Add(tile);
			}

			builder.Create(tile.VectorData.Data.GetLayer(layerName), tile, DecreaseProgressCounter);
		}

		private void DecreaseProgressCounter(CustomTile tile, BuildingMeshBuilder builder)
		{
			if (!_layerProgress.ContainsKey(tile)) return;
			if (_layerProgress[tile].Contains(builder)) _layerProgress[tile].Remove(builder);
			if (_layerProgress[tile].Count != 0) return;
			_layerProgress.Remove(tile);
			TilesWaitingProcessing.Remove(tile);
			tile.VectorDataState = TilePropertyState.Loaded;
		}

		private void CreateLayerVisualizers()
		{
			foreach (var sublayer in Properties.vectorSubLayers)
			{
				AddVectorLayerVisualizer(sublayer);
			}
		}

		private void RemoveAllLayerVisualizers() => _layerBuilder.Clear();
		#endregion

	}
}
