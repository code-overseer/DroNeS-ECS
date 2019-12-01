using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DroNeS.Mapbox.Custom;
using DroNeS.Mapbox.JobSystem;
using DroNeS.Mapbox.MonoBehaviour;
using Mapbox.Unity.Map;
using Mapbox.Unity.MeshGeneration.Data;
using Mapbox.Unity.MeshGeneration.Enums;
using Mapbox.Unity.MeshGeneration.Filters;
using Mapbox.Unity.MeshGeneration.Modifiers;
using Mapbox.Unity.Utilities;
using Mapbox.VectorTile;
using Mapbox.VectorTile.Geometry;
using UnityEngine;
using Debug = UnityEngine.Debug;
using TextureSideWallModifier = Mapbox.Unity.MeshGeneration.Modifiers.TextureSideWallModifier;

namespace DroNeS.Mapbox.ECS
{
	// ReSharper disable once ClassNeverInstantiated.Global
	public class BuildingMeshBuilderProperties
    {
        public FeatureProcessingStage FeatureProcessingStage;
        public bool BuildingsWithUniqueIds;
        public VectorTileLayer VectorTileLayer;
        public ILayerFeatureFilterComparer[] LayerFeatureFilters;
        public ILayerFeatureFilterComparer LayerFeatureFilterCombiner;
        public int FeatureCount;
    }
	
	internal class ProcessingState : IEnumerator
	{
		private int _index;
		private readonly BuildingMeshBuilderProperties _properties;

		public ProcessingState(BuildingMeshBuilderProperties properties)
		{
			_properties = properties;
			_index = 0;
		}
		public bool MoveNext()
		{
			return ++_index < _properties.FeatureCount;
		}
		public void Reset()
		{
			_index = 0;
		}
		public object Current => this;
	}
    
    public class BuildingMeshBuilder
    {
	    public VectorSubLayerProperties SubLayerProperties { get; private set; }
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
			_defaultStack = ScriptableObject.CreateInstance<MeshMerger>();
			_defaultStack.MeshModifiers = new List<MeshModifier>();

			var poly = AddOrCreateMeshModifier<PolygonMeshModifier>();
			SubLayerProperties.materialOptions.SetDefaultMaterialOptions();
			
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
			return layerProperties.LayerFeatureFilters.Length < 1 || layerProperties.LayerFeatureFilterCombiner.Try((VectorFeatureUnity)feature);
		}
		private bool ShouldSkipProcessingFeatureWithId(ulong featureId, BuildingMeshBuilderProperties layerProperties)
		{
			return layerProperties.BuildingsWithUniqueIds && _activeIds.Contains(featureId);
		}

		#endregion
		
		
		public void Initialize()
		{
			_activeIds = new HashSet<ulong>();
			InitializeStack();
		}

		private void InitializeStack()
		{
			if (_defaultStack != null) _defaultStack.Initialize();
		}
		
		public void Create(VectorTileLayer layer, CustomTile tile)
		{
			if (tile == null || layer == null) return;
			CoroutineManager.Run(ProcessLayer(MakeProperties(layer), tile));
		}

		private BuildingMeshBuilderProperties MakeProperties(VectorTileLayer layer)
		{
			var output = new BuildingMeshBuilderProperties
			{
				VectorTileLayer = layer,
				FeatureCount = layer?.FeatureCount() ?? 0,
				FeatureProcessingStage = FeatureProcessingStage.PreProcess,
				LayerFeatureFilters =
					SubLayerProperties.filterOptions.filters.Select(m => m.GetFilterComparer()).ToArray(),
				LayerFeatureFilterCombiner = new LayerFilterComparer()
			};
			switch (SubLayerProperties.filterOptions.combinerType)
			{
				case LayerFilterCombinerOperationType.Any:
					output.LayerFeatureFilterCombiner = LayerFilterComparer.AnyOf(output.LayerFeatureFilters);
					break;
				case LayerFilterCombinerOperationType.All:
					output.LayerFeatureFilterCombiner = LayerFilterComparer.AllOf(output.LayerFeatureFilters);
					break;
				case LayerFilterCombinerOperationType.None:
					output.LayerFeatureFilterCombiner = LayerFilterComparer.NoneOf(output.LayerFeatureFilters);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
			output.BuildingsWithUniqueIds = SubLayerProperties.honorBuildingIdSetting && SubLayerProperties.buildingsWithUniqueIds;
			return output;
		}

		private IEnumerator ProcessLayer(BuildingMeshBuilderProperties properties, CustomTile tile)
		{
			for (var i = 0; i < properties.FeatureCount; ++i)
			{
				ProcessFeature(i, tile, properties);
				yield return null;
			}
			
			_defaultStack.Terminate(tile);
		}

		private bool ProcessFeature(int index, CustomTile tile, BuildingMeshBuilderProperties layerProperties)
		{
			var layerExtent = layerProperties.VectorTileLayer.Extent;
			var fe = layerProperties.VectorTileLayer.GetFeature(index);
			List<List<Point2d<float>>> geom;
			
			if (layerProperties.BuildingsWithUniqueIds)
			{
				geom = fe.Geometry<float>(); 

				if (geom[0][0].X < 0 || geom[0][0].X > layerExtent || geom[0][0].Y < 0 || geom[0][0].Y > layerExtent) return false;
			}
			else
			{
				geom = fe.Geometry<float>(0); //passing zero means clip at tile edge
			}

			var feature = new CustomFeatureUnity(
				layerProperties.VectorTileLayer.GetFeature(index),
				geom,
				tile,
				layerProperties.VectorTileLayer.Extent,
				layerProperties.BuildingsWithUniqueIds);


			if (!IsFeatureEligibleAfterFiltering(feature, layerProperties)) return true;
			if (tile == null || tile.VectorDataState == TilePropertyState.Cancelled) return true;
			if (layerProperties.FeatureProcessingStage == FeatureProcessingStage.PostProcess) return true;
			if (ShouldSkipProcessingFeatureWithId(feature.Data.Id, layerProperties)) return false;
			
			AddFeatureToTileObjectPool(feature);
			Build(feature, tile);
			return true;
		}

		private void Build(CustomFeatureUnity feature, CustomTile tile)
		{
			if (feature.Properties.ContainsKey("extrude") && !Convert.ToBoolean(feature.Properties["extrude"])) return;
			if (feature.Points.Count < 1) return;
			var styleSelectorKey = SubLayerProperties.coreOptions.sublayerName;
			_defaultStack.Execute(tile, feature, new MeshData {TileRect = tile.Rect}, styleSelectorKey);
		}
    }
}
