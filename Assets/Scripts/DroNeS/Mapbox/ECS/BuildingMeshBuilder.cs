using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mapbox.Map;
using Mapbox.Unity.Map;
using Mapbox.Unity.MeshGeneration.Data;
using Mapbox.Unity.MeshGeneration.Enums;
using Mapbox.Unity.MeshGeneration.Filters;
using Mapbox.Unity.MeshGeneration.Modifiers;
using Mapbox.Unity.Utilities;
using Mapbox.VectorTile;
using Mapbox.VectorTile.Geometry;
using UnityEngine;

namespace DroNeS.Mapbox
{
	// ReSharper disable once ClassNeverInstantiated.Global
	public class BuildingMeshBuilderProperties
    {
        public FeatureProcessingStage featureProcessingStage;
        public bool buildingsWithUniqueIds = false;
        public VectorTileLayer vectorTileLayer;
        public ILayerFeatureFilterComparer[] layerFeatureFilters;
        public ILayerFeatureFilterComparer layerFeatureFilterCombiner;
    }
    
    public class BuildingMeshBuilder
    {
	    public VectorSubLayerProperties SubLayerProperties { get; set; }

	    public LayerPerformanceOptions PerformanceOptions;
		public Dictionary<CustomTile, List<int>> ActiveCoroutines;
		private int _entityInCurrentCoroutine;
		private MeshMerger _defaultStack;
		private HashSet<ulong> _activeIds;
		private string _key;
		private HashSet<ModifierBase> _coreModifiers = new HashSet<ModifierBase>();
		public string Key
		{
			get => SubLayerProperties.coreOptions.layerName;
			set => SubLayerProperties.coreOptions.layerName = value;
		}
		private T AddOrCreateMeshModifier<T>() where T : MeshModifier
		{
			var mod = _defaultStack.MeshModifiers.FirstOrDefault(x => x.GetType() == typeof(T));
			if (mod != null) return (T) mod;
			
			mod = (MeshModifier)ScriptableObject.CreateInstance(typeof(T));
			_coreModifiers.Add(mod);
			_defaultStack.MeshModifiers.Add(mod);
			return (T)mod;
		}

		public void SetProperties(VectorSubLayerProperties properties)
		{
			_coreModifiers = new HashSet<ModifierBase>();
			SubLayerProperties = properties;
			PerformanceOptions = properties.performanceOptions;
			_defaultStack = ScriptableObject.CreateInstance<MeshMerger>();
			_defaultStack.MeshModifiers = new List<MeshModifier>();

			SubLayerProperties.materialOptions.SetDefaultMaterialOptions();
			var poly = AddOrCreateMeshModifier<PolygonMeshModifier>();
			//This may cause problems
			var uvModOptions = new UVModifierOptions
			{
				texturingType = UvMapType.Atlas,
				atlasInfo = Resources.Load("Atlases/BuildingAtlas") as AtlasInfo,
				style = StyleTypes.Custom
			};
			poly.SetProperties(uvModOptions);
			
			var atlasMod = AddOrCreateMeshModifier<TextureSideWallModifier>();
			SubLayerProperties.extrusionOptions.extrusionType = ExtrusionType.PropertyHeight;
			SubLayerProperties.extrusionOptions.extrusionScaleFactor = 1.3203f;
			SubLayerProperties.extrusionOptions.propertyName = "height";
			SubLayerProperties.extrusionOptions.extrusionGeometryType = ExtrusionGeometryType.RoofAndSide;
			var atlasOptions = new GeometryExtrusionWithAtlasOptions(SubLayerProperties.extrusionOptions, uvModOptions);
			atlasMod.SetProperties(atlasOptions);
			
			SubLayerProperties.filterOptions.RegisterFilters();
		}
		
		private void SetReplacementCriteria(IReplacementCriteria criteria)
		{
			foreach (var meshMod in _defaultStack.MeshModifiers)
			{
				if (meshMod is IReplaceable replaceable)
				{
					replaceable.Criteria.Add(criteria);
				}
			}
		}

		#region Private Helper Methods
		private void AddFeatureToTileObjectPool(CustomFeatureUnity feature)
		{
			_activeIds.Add(feature.Data.Id);
		}
		private static bool IsFeatureEligibleAfterFiltering(CustomFeatureUnity feature, BuildingMeshBuilderProperties layerProperties)
		{
			return !layerProperties.layerFeatureFilters.Any() || layerProperties.layerFeatureFilterCombiner.Try((VectorFeatureUnity)feature);
		}
		private bool ShouldSkipProcessingFeatureWithId(ulong featureId, BuildingMeshBuilderProperties layerProperties)
		{
			return layerProperties.buildingsWithUniqueIds && _activeIds.Contains(featureId);
		}
		private bool IsCoroutineBucketFull()
		{
			return PerformanceOptions != null && PerformanceOptions.isEnabled &&
			       _entityInCurrentCoroutine >= PerformanceOptions.entityPerCoroutine;
		}
		#endregion
		
		public void Initialize()
		{
			_entityInCurrentCoroutine = 0;
			ActiveCoroutines = new Dictionary<CustomTile, List<int>>();
			_activeIds = new HashSet<ulong>();
			InitializeStack();
		}

		private void InitializeStack()
		{
			if (_defaultStack != null) _defaultStack.Initialize();
		}
		
		public void Create(VectorTileLayer layer, CustomTile tile, Action<CustomTile, BuildingMeshBuilder> callback)
		{
			if (!ActiveCoroutines.ContainsKey(tile))
				ActiveCoroutines.Add(tile, new List<int>());
			ActiveCoroutines[tile].Add(Runnable.Run(ProcessLayer(layer, tile, tile.UnwrappedTileId, callback)));
		}

		private IEnumerator ProcessLayer(VectorTileLayer layer, CustomTile tile, UnwrappedTileId tileId, Action<CustomTile, BuildingMeshBuilder> callback = null)
		{
			if (tile == null) yield break;

			var tempLayerProperties = new BuildingMeshBuilderProperties
			{
				vectorTileLayer = layer,
				featureProcessingStage = FeatureProcessingStage.PreProcess,
				layerFeatureFilters =
					SubLayerProperties.filterOptions.filters.Select(m => m.GetFilterComparer()).ToArray(),
				layerFeatureFilterCombiner = new LayerFilterComparer()
			};
			
			switch (SubLayerProperties.filterOptions.combinerType)
			{
				case LayerFilterCombinerOperationType.Any:
					tempLayerProperties.layerFeatureFilterCombiner = LayerFilterComparer.AnyOf(tempLayerProperties.layerFeatureFilters);
					break;
				case LayerFilterCombinerOperationType.All:
					tempLayerProperties.layerFeatureFilterCombiner = LayerFilterComparer.AllOf(tempLayerProperties.layerFeatureFilters);
					break;
				case LayerFilterCombinerOperationType.None:
					tempLayerProperties.layerFeatureFilterCombiner = LayerFilterComparer.NoneOf(tempLayerProperties.layerFeatureFilters);
					break;
			}

			tempLayerProperties.buildingsWithUniqueIds = (SubLayerProperties.honorBuildingIdSetting) && SubLayerProperties.buildingsWithUniqueIds;

			//find any replacement criteria and assign them
			foreach (var goModifier in _defaultStack.GoModifiers)
			{
				if (goModifier is IReplacementCriteria criteria && goModifier.Active)
				{
					SetReplacementCriteria(criteria);
				}
			}

			#region PreProcess & Process.

			var featureCount = tempLayerProperties.vectorTileLayer?.FeatureCount() ?? 0;
			do
			{
				for (var i = 0; i < featureCount; i++)
				{
					//checking if tile is recycled and changed
					if (tile.UnwrappedTileId != tileId 
					    || !ActiveCoroutines.ContainsKey(tile))
					{
						yield break;
					}

					ProcessFeature(i, tile, tempLayerProperties, layer.Extent);

					if (!IsCoroutineBucketFull() || Application.isEditor && !Application.isPlaying) continue;
					//Reset bucket..
					_entityInCurrentCoroutine = 0;
					yield return null;
				}
				// move processing to next stage.
				tempLayerProperties.featureProcessingStage++;
			} while (tempLayerProperties.featureProcessingStage == FeatureProcessingStage.PreProcess 
			         || tempLayerProperties.featureProcessingStage == FeatureProcessingStage.Process);

			#endregion

			_defaultStack.End(tile);
			callback?.Invoke(tile, this);
		}

		private bool ProcessFeature(int index, CustomTile tile, BuildingMeshBuilderProperties layerProperties, float layerExtent)
		{
			var fe = layerProperties.vectorTileLayer.GetFeature(index);
			List<List<Point2d<float>>> geom;
			if (layerProperties.buildingsWithUniqueIds) //ids from building dataset is big ulongs
			{
				geom = fe.Geometry<float>(); //and we're not clipping by passing no parameters

				if (geom[0][0].X < 0 || geom[0][0].X > layerExtent || geom[0][0].Y < 0 || geom[0][0].Y > layerExtent)
				{
					return false;
				}
			}
			else //streets ids, will require clipping
			{
				geom = fe.Geometry<float>(0); //passing zero means clip at tile edge
			}

			var feature = new CustomFeatureUnity(
				layerProperties.vectorTileLayer.GetFeature(index),
				geom,
				tile,
				layerProperties.vectorTileLayer.Extent,
				layerProperties.buildingsWithUniqueIds);


			if (!IsFeatureEligibleAfterFiltering(feature, layerProperties)) return true;
			if (tile == null || tile.VectorDataState == TilePropertyState.Cancelled) return true;

			if (layerProperties.featureProcessingStage != FeatureProcessingStage.PostProcess)
			{
				if (ShouldSkipProcessingFeatureWithId(feature.Data.Id, layerProperties)) return false;
			
				AddFeatureToTileObjectPool(feature);
				Build(feature, tile);
			}

			_entityInCurrentCoroutine++;
			return true;
		}
		
		private bool IsFeatureValid(CustomFeatureUnity feature)
		{
			if (feature.Properties.ContainsKey("extrude") && !bool.Parse(feature.Properties["extrude"].ToString()))
				return false;

			return feature.Points.Count >= 1;
		}
		private void Build(CustomFeatureUnity feature, CustomTile tile)
		{
			if (feature.Properties.ContainsKey("extrude") && !Convert.ToBoolean(feature.Properties["extrude"]))
				return;

			if (feature.Points.Count < 1)
				return;
			
			var styleSelectorKey = SubLayerProperties.coreOptions.sublayerName;

			if (_defaultStack != null)
			{
				_defaultStack.Execute(tile, feature, new MeshData {TileRect = tile.Rect}, styleSelectorKey);
			}
		}
    }
}
