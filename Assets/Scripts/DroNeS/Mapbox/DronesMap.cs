using System;
using System.Collections.Generic;
using DroNeS.Mapbox;
using Mapbox.Map;
using Mapbox.Unity;
using Mapbox.Unity.Map;
using Mapbox.Unity.Map.Interfaces;
using Mapbox.Unity.Map.Strategies;
using Mapbox.Unity.Map.TileProviders;
using Mapbox.Unity.MeshGeneration.Data;
using Mapbox.Unity.MeshGeneration.Enums;
using Mapbox.Unity.MeshGeneration.Factories;
using Mapbox.Unity.Utilities;
using Mapbox.Utils;
using UnityEngine;
using TerrainLayer = Mapbox.Unity.Map.TerrainLayer;

namespace DroNeS
{
    [Serializable]
    public class DronesMap : IMap
    {
        #region Private Fields

        [SerializeField] private MapOptions _options = new MapOptions();
        [SerializeField] private ImageryLayer _imagery = new ImageryLayer();
        [SerializeField] private TerrainLayer _terrain = new TerrainLayer();
        [SerializeField] private VectorLayer _vectorData = new VectorLayer();
        private MapboxAccess _fileSource;

        #endregion
        
        #region IMap Properties
        public Vector2d CenterMercator { get; private set; }
        public float WorldRelativeScale { get; private set; }
        public Vector2d CenterLatitudeLongitude { get; private set; }
        public float Zoom => _options.locationOptions.zoom;
        public int InitialZoom => 16;
        public int AbsoluteZoom => (int) Math.Floor(Zoom);
        public Transform Root { get; }
        public float UnityTileSize { get; }
        public Texture2D LoadingTexture { get; }
        public Material TileMaterial { get; }
        public HashSet<UnwrappedTileId> CurrentExtent { get; }
        #endregion
        
        private MapVisualizer Visualizer { get; set; }

        public event Action OnInitialized;
        public event Action OnUpdated;
        public Vector2d WorldToGeoPosition(Vector3 point)
        {
            var sf = Mathf.Pow(2, (InitialZoom - AbsoluteZoom));

            return (Root.InverseTransformPoint(point)).GetGeoPosition(CenterMercator, WorldRelativeScale * sf);
        }
        
        public Vector3 GeoToWorldPosition(Vector2d latitudeLongitude, bool queryHeight = true) => Vector3.zero;

        public void SetCenterMercator(Vector2d centerMercator)
        {
            CenterMercator = centerMercator;
        }

        public void SetCenterLatitudeLongitude(Vector2d centerLatitudeLongitude)
        {
            _options.locationOptions.latitudeLongitude = $"{centerLatitudeLongitude.x}, {centerLatitudeLongitude.y}";
            CenterLatitudeLongitude = centerLatitudeLongitude;
        }

        public void SetWorldRelativeScale(float scale)
        {
            WorldRelativeScale = scale;
        }

        public void SetZoom(float zoom) { }

        public void UpdateMap(Vector2d latLon, float zoom) { }

        public void ResetMap()
        {
            MapOnAwakeRoutine();
            MapOnStartRoutine(false);
        }

        private void MapOnAwakeRoutine()
        {
            if(Visualizer == null)
            {
                Visualizer = ScriptableObject.CreateInstance<MapVisualizer>();
            }
        }
        
        private void MapOnStartRoutine(bool coroutine = true)
        {
            if (!Application.isPlaying) return;
            SetUpMap();
        }
        
        private void SetUpMap()
        {
            _options.scalingOptions.scalingStrategy = new MapScalingAtWorldScaleStrategy();
            _options.placementOptions.placementStrategy = new MapPlacementAtTileCenterStrategy();

            // Tile provider called on update
            _imagery.Initialize();
            _terrain.Initialize();
            _vectorData.Initialize();

            Visualizer.Factories = new List<AbstractTileFactory>
            {
                _terrain.Factory,
                _imagery.Factory,
                _vectorData.Factory
            };

            InitializeMap(_options);
        }

        private void TileProvider_OnTileAdded(UnwrappedTileId tileId)
        {
            var tile = Visualizer.LoadTile(tileId);
            if (!_options.placementOptions.snapMapToZero) return;
            if (tile.HeightDataState == TilePropertyState.Loaded)
            {
                ApplySnapWorldToZero(tile);
            }
        }
        
        private void InitializeMap(MapOptions options)
        {
            _fileSource = MapboxAccess.Instance;
            CenterLatitudeLongitude = Conversions.StringToLatLon(options.locationOptions.latitudeLongitude);
            
            SetWorldRelativeScale(Mathf.Pow(2, AbsoluteZoom - InitialZoom) * Mathf.Cos(Mathf.Deg2Rad * (float)CenterLatitudeLongitude.x));
            SetCenterMercator(Conversions.TileBounds(TileCover.CoordinateToTileId(CenterLatitudeLongitude, AbsoluteZoom)).Center);

            Visualizer.Initialize(this, _fileSource);

            ManhattanTileProvider.Initialize(this, TriggerTileRedrawForExtent);
        }
        
        private void TriggerTileRedrawForExtent(ExtentArgs currentExtent)
        {
            foreach (var tileId in currentExtent.activeTiles)
            {
                Visualizer.State = ModuleState.Working;
                TileProvider_OnTileAdded(tileId);
            }
        }

        private void ApplySnapWorldToZero(UnityTile referenceTile)
        {
            var h = referenceTile.QueryHeightData(.5f, .5f);
            var trans = Root.transform;
            var pos = trans.position;
            trans.localPosition = new Vector3(pos.x, -h, pos.z);
        }

    }
}
