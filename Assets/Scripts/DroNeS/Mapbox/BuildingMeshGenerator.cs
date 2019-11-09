using System;
using System.Collections.Generic;
using Mapbox.Map;
using Mapbox.Unity.Map;
using Mapbox.Unity.MeshGeneration.Data;
using Mapbox.Unity.MeshGeneration.Enums;
using Mapbox.Unity.MeshGeneration.Factories;
using Mapbox.Unity.MeshGeneration.Interfaces;
using UnityEngine;

namespace DroNeS.Mapbox
{
	/// <summary>
	///	Vector Tile Factory
	/// Vector data is much more detailed compared to terrain and image data so we have a different structure to process
	/// vector data(compared to other factories). First of all, how does the vector data itself structured? Vector tile
	/// data contains 'vector layers' as immediate children.And then each of these vector layers contains a number of
	/// 'features' inside.I.e.vector data for a tile has 'building', 'road', 'landuse' etc layers. Then building layer
	/// has a number of polygon features, road layer has line features etc.
	/// Similar to this, vector tile factory contains bunch of 'layer visualizers' and each one of them corresponds to
	/// one (or more) vector layers in data.So when data is received, factory goes through all layers inside and passes
	/// them to designated layer visualizers.We're using layer name as key here, to find the designated layer visualizer,
	/// like 'building', 'road'. (vector tile factory visual would help here). If it can't find a layer visualizer for
	/// that layer, it'll be skipped and not processed at all.If all you need is 1-2 layers, it's indeed a big waste to
	/// pull whole vector data and you can use 'Style Optimized Vector Tile Factory' to pull only the layer you want to use.
	/// </summary>
	//[CreateAssetMenu(menuName = "Mapbox/Factories/Vector Tile Factory")]
	public class BuildingMeshGenerator : AbstractTileFactory
	{
		#region Private/Protected Fields
		private Dictionary<string, List<LayerVisualizerBase>> _layerBuilder;
		private Dictionary<UnityTile, HashSet<LayerVisualizerBase>> _layerProgress;
		protected VectorDataFetcher DataFetcher;
		#endregion

		#region Properties

		private string TilesetId => Properties.sourceOptions.Id;

		private VectorLayerProperties Properties { get; set; }

		#endregion

		#region Public Layer Operation Api Methods for
		public virtual LayerVisualizerBase AddVectorLayerVisualizer(VectorSubLayerProperties subLayer)
		{
			//if its of type prefab item options then separate the visualizer type
			LayerVisualizerBase visualizer = CreateInstance<VectorLayerVisualizer>();

			subLayer.coreOptions.geometryType = VectorPrimitiveType.Polygon;
			subLayer.honorBuildingIdSetting = true;
			
			// Setup visualizer.
			((VectorLayerVisualizer)visualizer).SetProperties(subLayer);
			visualizer.Initialize();
			if (visualizer == null) return visualizer;

			if (_layerBuilder.ContainsKey(visualizer.Key))
			{
				_layerBuilder[visualizer.Key].Add(visualizer);
			}
			else
			{
				_layerBuilder.Add(visualizer.Key, new List<LayerVisualizerBase> { visualizer });
			}
			return visualizer;
		}

		public virtual LayerVisualizerBase FindVectorLayerVisualizer(VectorSubLayerProperties subLayer)
		{
			if (!_layerBuilder.ContainsKey(subLayer.Key)) return null;
			var visualizer = _layerBuilder[subLayer.Key].Find((obj) => obj.SubLayerProperties == subLayer);
			return visualizer;
		}
		#endregion

		#region AbstractFactoryOverrides
		/// <summary>
		/// Set up sublayers using VectorLayerVisualizers.
		/// </summary>
		protected override void OnInitialized()
		{
			_layerProgress = new Dictionary<UnityTile, HashSet<LayerVisualizerBase>>();
			_layerBuilder = new Dictionary<string, List<LayerVisualizerBase>>();

			DataFetcher = CreateInstance<VectorDataFetcher>();
			DataFetcher.DataRecieved += OnVectorDataRecieved;

			CreateLayerVisualizers();
		}

		protected override void OnRegistered(UnityTile tile)
		{
			if (string.IsNullOrEmpty(TilesetId) || !Properties.sourceOptions.isActive || 
			    Properties.vectorSubLayers.Count + Properties.locationPrefabList.Count == 0)
			{
				tile.VectorDataState = TilePropertyState.None;
				return;
			}
			tile.VectorDataState = TilePropertyState.Loading;
			_tilesWaitingResponse.Add(tile);
			var parameters = new VectorDataFetcherParameters()
			{
				canonicalTileId = tile.CanonicalTileId,
				tilesetId = TilesetId,
				tile = tile,
				useOptimizedStyle = Properties.useOptimizedStyle,
				style = Properties.optimizedStyle
			};
			DataFetcher.FetchData(parameters);
		}

		protected override void OnUnregistered(UnityTile tile)
		{
			if (_layerProgress != null && _layerProgress.ContainsKey(tile))
			{
				_layerProgress.Remove(tile);
			}
			if (_tilesWaitingResponse != null && _tilesWaitingProcessing.Contains(tile))
			{
				_tilesWaitingProcessing.Remove(tile);
			}

			if (_layerBuilder == null) return;
			foreach (var layer in _layerBuilder.Values)
			{
				foreach (var visualizer in layer)
				{
					visualizer.UnregisterTile(tile);
				}
			}
		}

		public override void Clear()
		{
			DestroyImmediate(DataFetcher);
			if (_layerBuilder == null) return;
			foreach (var layerList in _layerBuilder.Values)
			{
				foreach (var layerVisualizerBase in layerList)
				{
					layerVisualizerBase.Clear();
					DestroyImmediate(layerVisualizerBase);
				}
			}

			_layerProgress.Clear();
			_tilesWaitingResponse.Clear();
			_tilesWaitingProcessing.Clear();
		}

		public override void SetOptions(LayerProperties options)
		{
			Properties = (VectorLayerProperties)options;
			if (_layerBuilder == null) return;
			RemoveAllLayerVisualiers();

			CreateLayerVisualizers();
		}

		public override void UpdateTileProperty(UnityTile tile, LayerUpdateArgs updateArgs)
		{
			updateArgs.property.UpdateProperty(tile);

			if (updateArgs.property.NeedsForceUpdate())
			{
				Unregister(tile);
			}
			Register(tile);
		}

		protected override void UpdateTileFactory(object sender, EventArgs args)
		{
			var layerUpdateArgs = args as VectorLayerUpdateArgs;
			layerUpdateArgs.factory = this;
			base.UpdateTileFactory(sender, layerUpdateArgs);
		}

		/// <summary>
		/// Method to be called when a tile error has occurred.
		/// </summary>
		/// <param name="e"><see cref="T:Mapbox.Map.TileErrorEventArgs"/> instance/</param>
		protected override void OnErrorOccurred(UnityTile tile, TileErrorEventArgs e)
		{
			base.OnErrorOccurred(tile, e);
		}

		protected override void OnPostProcess(UnityTile tile)
		{

		}

		public override void UnbindEvents()
		{
			base.UnbindEvents();
		}

		protected override void OnUnbindEvents()
		{
			if (_layerBuilder == null) return;
			foreach (var layer in _layerBuilder.Values)
			{
				foreach (var visualizer in layer)
				{
					visualizer.LayerVisualizerHasChanged -= UpdateTileFactory;
					visualizer.UnbindSubLayerEvents();
				}
			}
		}
		#endregion

		#region DataFetcherEvents
		private void OnVectorDataRecieved(UnityTile tile, global::Mapbox.Map.VectorTile vectorTile)
		{
			if (tile == null) return;
			_tilesWaitingResponse.Remove(tile);
			tile.SetVectorData(vectorTile);
			
			CreateMeshes(tile);
		}

		private void DataChangedHandler(UnityTile tile)
		{
			if (tile.VectorDataState != TilePropertyState.Unregistered &&
				tile.RasterDataState != TilePropertyState.Loading &&
				tile.HeightDataState != TilePropertyState.Loading)
			{
				CreateMeshes(tile);
			}
		}
		#endregion

		#region Private Methods
		private void CreateMeshes(UnityTile tile)
		{
			var nameList = new List<string>();
			var builderList = new List<LayerVisualizerBase>();

			foreach (var layerName in tile.VectorData.Data.LayerNames())
			{
				if (!_layerBuilder.ContainsKey(layerName)) continue;
				//two loops; first one to add it to waiting/tracking list, second to start it
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

			if (!_layerProgress.ContainsKey(tile))
			{
				tile.VectorDataState = TilePropertyState.Loaded;
			}
		}

		private void TrackFeatureWithBuilder(UnityTile tile, string layerName, LayerVisualizerBase builder)
		{
			if (!builder.Active) return;
			if (_layerProgress.ContainsKey(tile))
			{
				_layerProgress[tile].Add(builder);
			}
			else
			{
				_layerProgress.Add(tile, new HashSet<LayerVisualizerBase> {builder});
				if (!_tilesWaitingProcessing.Contains(tile))
				{
					_tilesWaitingProcessing.Add(tile);
				}
			}
		}

		private void CreateFeatureWithBuilder(UnityTile tile, string layerName, LayerVisualizerBase builder)
		{
			if (!builder.Active) return;
			if (_layerProgress.ContainsKey(tile))
			{
				_layerProgress[tile].Add(builder);
			}
			else
			{
				_layerProgress.Add(tile, new HashSet<LayerVisualizerBase> { builder });
				if (!_tilesWaitingProcessing.Contains(tile)) _tilesWaitingProcessing.Add(tile);
			}

			builder.Create(tile.VectorData.Data.GetLayer(layerName), tile, DecreaseProgressCounter);
		}

		private void DecreaseProgressCounter(UnityTile tile, LayerVisualizerBase builder)
		{
			if (!_layerProgress.ContainsKey(tile)) return;
			if (_layerProgress[tile].Contains(builder))
			{
				_layerProgress[tile].Remove(builder);

			}

			if (_layerProgress[tile].Count != 0) return;
			_layerProgress.Remove(tile);
			_tilesWaitingProcessing.Remove(tile);
			tile.VectorDataState = TilePropertyState.Loaded;
		}

		private void CreateLayerVisualizers()
		{
			foreach (var sublayer in Properties.vectorSubLayers)
			{
				AddVectorLayerVisualizer(sublayer);
			}
		}

		private void RemoveAllLayerVisualiers()
		{
			//Clearing gameobjects pooled and managed by modifiers to prevent zombie gameobjects.
			foreach (var pairs in _layerBuilder)
			{
				foreach (var layerVisualizerBase in pairs.Value)
				{
					layerVisualizerBase.Clear();
				}
			}
			_layerBuilder.Clear();
		}
		#endregion

	}
}
