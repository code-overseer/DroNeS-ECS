using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using DroNeS.Mapbox.Interfaces;
using DroNeS.Utils;
using DroNeS.Utils.Interfaces;
using DroNeS.Utils.Time;
using Mapbox.Unity.Map;
using Mapbox.Unity.MeshGeneration.Data;
using Mapbox.Unity.MeshGeneration.Enums;
using Mapbox.Unity.MeshGeneration.Filters;
using Mapbox.VectorTile;
using Mapbox.VectorTile.Geometry;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace DroNeS.Mapbox.Custom
{
    public class AsynchronousMeshBuilder : IMeshBuilder
    {
        public VectorSubLayerProperties SubLayerProperties { get; }
	    public IMeshProcessor Processor => _processor;
	    private readonly MeshProcessor _processor;
	    private NativeRoutines<ProcessingRoutine> _routines;
	    private void Destroy()
	    {
		    Debug.Log(_routines.Length.ToString());
		    _routines.Dispose();
	    }

	    public AsynchronousMeshBuilder(VectorSubLayerProperties subLayerProperties)
	    {
		    _routines = new NativeRoutines<ProcessingRoutine>(ManhattanTileProvider.Tiles.Count, Allocator.Persistent);
		    Application.quitting += Destroy;
		    SubLayerProperties = subLayerProperties;
		    SubLayerProperties.materialOptions.SetDefaultMaterialOptions();
		    SubLayerProperties.extrusionOptions.extrusionType = ExtrusionType.PropertyHeight;
		    SubLayerProperties.extrusionOptions.extrusionScaleFactor = 1.3203f;
		    SubLayerProperties.extrusionOptions.propertyName = "height";
		    SubLayerProperties.extrusionOptions.extrusionGeometryType = ExtrusionGeometryType.RoofAndSide;
		    
		    _processor = new MeshProcessor();
            
		    var uvOptions = new UVModifierOptions
		    {
			    texturingType = UvMapType.Atlas,
			    atlasInfo = Resources.Load("Atlases/BuildingAtlas") as AtlasInfo,
			    style = StyleTypes.Custom
		    };
		    var atlasOptions = new GeometryExtrusionWithAtlasOptions(SubLayerProperties.extrusionOptions, uvOptions);
		    
		    _processor.SetOptions(uvOptions, atlasOptions);

		    SubLayerProperties.filterOptions.RegisterFilters();
	    }

	    public void Create(VectorTileLayer layer, CustomTile tile)
        {
            if (tile == null || layer == null) return;
            var properties = MakeProperties(layer);
            _routines.Add(new ProcessingRoutine(ProcessingFunction(properties, tile)));
        }

		private struct ProcessingRoutine : IRoutine
		{
			private GCHandle _routine;
			public Period Period { get; }
			public CustomTimer Timer { get; }

			public ProcessingRoutine(IEnumerator routine)
			{
				Period = new Period(0);
				Timer = new CustomTimer();
				_routine = GCHandle.Alloc(routine, GCHandleType.Pinned);
			}

			public bool MoveNext() => ((IEnumerator) _routine.Target).MoveNext();

			public void Reset() { }

			public object Current => _routine.Target;
			public void Dispose() => _routine.Free();
		}

		private IEnumerator ProcessingFunction(BuildingMeshBuilderProperties properties, CustomTile tile)
		{
			for (var i = 0; i < properties.FeatureCount; ++i)
			{
				ProcessFeature(i, tile, properties);
				yield return null;
			}
			_processor.Terminate(tile);
		}

		public IEnumerator Manager()
		{
			JobHandle handle = default;
			do
			{
				handle = _routines.MoveNext(handle);
				while (!handle.IsCompleted) yield return null;
				handle.Complete();
			} while (_routines.Length > 0);

			handle = _routines.Dispose(handle);
			while (!handle.IsCompleted) yield return null;
			handle.Complete();
		}
		
        private BuildingMeshBuilderProperties MakeProperties(VectorTileLayer layer)
        {
            var output = new BuildingMeshBuilderProperties
            {
                VectorTileLayer = layer,
                FeatureCount = layer?.FeatureCount() ?? 0,
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

        private void ProcessFeature(int index, CustomTile tile, BuildingMeshBuilderProperties layerProperties)
        {
            var layerExtent = layerProperties.VectorTileLayer.Extent;
            var fe = layerProperties.VectorTileLayer.GetFeature(index);
            List<List<Point2d<float>>> geom;
			
            if (layerProperties.BuildingsWithUniqueIds)
            {
                geom = fe.Geometry<float>(); 

                if (geom[0][0].X < 0 || geom[0][0].X > layerExtent || geom[0][0].Y < 0 || geom[0][0].Y > layerExtent) return;
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


            if (!IsFeatureEligibleAfterFiltering(feature, layerProperties) ||
                tile == null || tile.VectorDataState == TilePropertyState.Cancelled) return;
            
            if (feature.Properties.ContainsKey("extrude") && !Convert.ToBoolean(feature.Properties["extrude"])) return;
            if (feature.Points.Count < 1) return;
            
            _processor.Execute(tile, feature);
            
        }
        
        private static bool IsFeatureEligibleAfterFiltering(CustomFeatureUnity feature, BuildingMeshBuilderProperties layerProperties)
        {
            return layerProperties.LayerFeatureFilters.Length < 1 || layerProperties.LayerFeatureFilterCombiner.Try((VectorFeatureUnity)feature);
        }
    }
}
