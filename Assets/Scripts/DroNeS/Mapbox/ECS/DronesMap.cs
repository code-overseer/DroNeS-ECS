using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DroNeS.Mapbox.Custom;
using Mapbox.Map;
using Mapbox.Unity;
using Mapbox.Unity.Map;
using Mapbox.Unity.Map.Interfaces;
using Mapbox.Unity.Map.Strategies;
using Mapbox.Unity.Map.TileProviders;
using Mapbox.Unity.Utilities;
using Mapbox.Utils;
using UnityEngine;

namespace DroNeS.Mapbox.ECS
{
    public class DronesMap 
    {
        private readonly MapOptions _options = new MapOptions();
        public Vector2d CenterMercator { get; private set; }
        public float WorldRelativeScale { get; private set; }
        public Vector2d CenterLatitudeLongitude { get; private set; }
        private float Zoom => _options.locationOptions.zoom;
        public int InitialZoom => 16;
        public int AbsoluteZoom => (int) Math.Floor(Zoom);
        private ManhattanVisualizer Visualizer { get; set; }

        public static void Build()
        {
            var c = new DronesMap();
        }

        private DronesMap()
        {
            _options.locationOptions.zoom = 16;
            if (!Application.isPlaying) return;
            _options.scalingOptions.scalingStrategy = new MapScalingAtWorldScaleStrategy();
            _options.placementOptions.placementStrategy = new MapPlacementAtTileCenterStrategy();
            
            InitializeMap();
        }

        private void SetCenterMercator(Vector2d centerMercator)
        {
            CenterMercator = centerMercator;
        }

        private void SetWorldRelativeScale(float scale)
        {
            WorldRelativeScale = scale;
        }

        private void InitializeMap()
        {
            CenterLatitudeLongitude = Conversions.StringToLatLon("40.764170691358686, -73.97670925665614");
            SetWorldRelativeScale(Mathf.Pow(2, AbsoluteZoom - InitialZoom) * Mathf.Cos(Mathf.Deg2Rad * (float)CenterLatitudeLongitude.x));
            SetCenterMercator(Conversions.TileBounds(TileCover.CoordinateToTileId(CenterLatitudeLongitude, AbsoluteZoom)).Center);

            Visualizer = new ManhattanVisualizer(this);

            var currentExtent = ManhattanTileProvider.GetTiles(this);
            foreach (var tileId in currentExtent)
            {
                Visualizer.LoadTile(tileId);
            }
        }

    }
}
