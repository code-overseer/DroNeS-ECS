using System;
using System.Collections.Generic;
using Mapbox.Map;
using Mapbox.Unity.Map;
using Mapbox.Unity.Map.Interfaces;
using Mapbox.Unity.Map.Strategies;
using Mapbox.Unity.Map.TileProviders;
using Mapbox.Unity.Utilities;
using Mapbox.Utils;
using UnityEngine;

namespace DroNeS.Mapbox.ECS
{
    [Serializable]
    public class DronesMap : IMap
    {
        #region Private Fields
        [SerializeField] private MapOptions options = new MapOptions();
        #endregion
        
        #region IMap Properties
        public Vector2d CenterMercator { get; private set; }
        public float WorldRelativeScale { get; private set; }
        public Vector2d CenterLatitudeLongitude { get; private set; }
        public float Zoom => options.locationOptions.zoom;
        public int InitialZoom => 16;

        public int AbsoluteZoom => (int) Math.Floor(Zoom);

        public Transform Root { get; }
        public float UnityTileSize { get; }
        public Texture2D LoadingTexture { get; }
        public Material TileMaterial { get; }
        public HashSet<UnwrappedTileId> CurrentExtent { get; }
        #endregion

        public static void Build()
        {
            var c = new DronesMap();
        }
        private DronesMap()
        {
            options.locationOptions.zoom = 16;
            MapOnStartRoutine();
        }
        
        private ManhattanVisualizer Visualizer { get; set; }
        public event Action OnInitialized;
        public event Action OnUpdated;
        public Vector2d WorldToGeoPosition(Vector3 point)
        {
            var sf = Mathf.Pow(2, (InitialZoom - AbsoluteZoom));

            return Vector3.zero.GetGeoPosition(CenterMercator, WorldRelativeScale * sf);
        }
        public Vector3 GeoToWorldPosition(Vector2d latitudeLongitude, bool queryHeight = true) => Vector3.zero;

        public void SetCenterMercator(Vector2d centerMercator)
        {
            CenterMercator = centerMercator;
        }

        public void SetCenterLatitudeLongitude(Vector2d latLong)
        {
            options.locationOptions.latitudeLongitude = $"{latLong.x}, {latLong.y}";
            CenterLatitudeLongitude = latLong;
        }

        public void SetWorldRelativeScale(float scale)
        {
            WorldRelativeScale = scale;
        }

        public void SetZoom(float zoom) { }

        public void UpdateMap(Vector2d latLon, float zoom) { }

        public void ResetMap()
        {
            MapOnStartRoutine(false);
        }

        private void MapOnStartRoutine(bool coroutine = true)
        {
            if (!Application.isPlaying) return;
            SetUpMap();
        }
        
        private void SetUpMap()
        {
            options.scalingOptions.scalingStrategy = new MapScalingAtWorldScaleStrategy();
            options.placementOptions.placementStrategy = new MapPlacementAtTileCenterStrategy();
            
            InitializeMap(options);
        }

        private void InitializeMap(MapOptions options)
        {
            CenterLatitudeLongitude = Conversions.StringToLatLon("40.764170691358686, -73.97670925665614");
            SetWorldRelativeScale(Mathf.Pow(2, AbsoluteZoom - InitialZoom) * Mathf.Cos(Mathf.Deg2Rad * (float)CenterLatitudeLongitude.x));
            SetCenterMercator(Conversions.TileBounds(TileCover.CoordinateToTileId(CenterLatitudeLongitude, AbsoluteZoom)).Center);

            Visualizer = new ManhattanVisualizer(this);

            ManhattanTileProvider.Initialize(this, TriggerTileRedrawForExtent);
        }
        
        private void TriggerTileRedrawForExtent(ExtentArgs currentExtent)
        {
            foreach (var tileId in currentExtent.activeTiles)
            {
                Visualizer.LoadTile(tileId);
            }
        }

    }
}
