using System;
using System.Collections;
using System.Collections.Generic;
using DroNeS.Mapbox.Custom;
using DroNeS.Mapbox.ECS;
using DroNeS.Mapbox.MonoBehaviour;
using Mapbox.Map;
using Mapbox.Unity.Map;
using Mapbox.Unity.Map.Strategies;
using Mapbox.Unity.Utilities;
using Mapbox.Utils;
using Unity.Jobs;
using Unity.Rendering;
using UnityEngine;
using ManhattanTileProvider = DroNeS.Mapbox.Custom.ManhattanTileProvider;

namespace DroNeS.Mapbox.JobSystem
{
    public class DronesMap : IMap
    {
        private readonly MapOptions _options = new MapOptions();
        public Vector2d CenterMercator { get; private set; }
        public float WorldRelativeScale { get; private set; }
        public Vector2d CenterLatitudeLongitude { get; private set; }
        private float Zoom => _options.locationOptions.zoom;
        public int InitialZoom => 16;
        public int AbsoluteZoom => (int) Math.Floor(Zoom);

        private readonly TerrainImageFactory _imageFactory;
        private readonly BuildingMeshFactory _meshFactory;
        public JobHandle Termination { get; }
        private readonly MeshProcessor _processor;
        public Dictionary<CustomTile, RenderMesh[]> RenderMeshes => _processor.RenderMeshes;

        public DronesMap()
        {
            _options.locationOptions.zoom = 16;
            if (!Application.isPlaying) return;
            _options.scalingOptions.scalingStrategy = new MapScalingAtWorldScaleStrategy();
            _options.placementOptions.placementStrategy = new MapPlacementAtTileCenterStrategy();
            _processor = new MeshProcessor();
            _imageFactory = new TerrainImageFactory();
            _meshFactory = new BuildingMeshFactory(_processor);
            InitializeMap();
            Termination = _processor.Terminate();
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
            

            var currentExtent = ManhattanTileProvider.GetTiles(this);
            var tiles = new List<CustomTile>(16);
            foreach (var tileId in currentExtent)
            {
                var tile = new CustomTile(this, in tileId);
                _imageFactory.Register(tile);
                if (tiles.Count < 2) tiles.Add(tile);
            }
            
            foreach (var tile in tiles)
            {
                _meshFactory.Register(tile);
            }
        }

    }
}
