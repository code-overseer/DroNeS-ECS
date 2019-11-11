using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Mapbox.Map;
using Mapbox.Unity.Map;
using Mapbox.Unity.Map.Interfaces;
using Mapbox.Unity.Map.Strategies;
using Mapbox.Unity.Map.TileProviders;
using Mapbox.Unity.MeshGeneration.Data;
using Mapbox.Unity.MeshGeneration.Modifiers;
using Mapbox.Unity.Utilities;
using Mapbox.Utils;
using UnityEngine;

namespace DroNeS.Mapbox.MonoBehaviour
{
    public class DronesMap : UnityEngine.MonoBehaviour, IMap
    {
        private readonly MapOptions _options = new MapOptions();
        private AbstractTileProvider _tileProvider;
        private List<UnwrappedTileId> _tilesToProcess;

        private AbstractMapVisualizer _mapVisualizer;

        public Vector2d CenterMercator { get; private set; }
        public float WorldRelativeScale { get; private set; }
        public Vector2d CenterLatitudeLongitude { get; private set; }
        public float Zoom => _options.locationOptions.zoom;
        public int InitialZoom { get; private set; }
        public int AbsoluteZoom { get; private set; }
        public Transform Root => transform;
        public float UnityTileSize { get; private set; } = 1;
        public Texture2D LoadingTexture { get; private set; }
        public Material TileMaterial { get; private set; }
        public HashSet<UnwrappedTileId> CurrentExtent { get; }
        public event Action OnInitialized;
        public event Action OnUpdated;

        public ManhattanVisualizer Visualizer
        {
            get
            {
                if(_mapVisualizer == null)
                {
                    _mapVisualizer = ScriptableObject.CreateInstance<ManhattanVisualizer>();
                }
                return (ManhattanVisualizer) _mapVisualizer;
            }
        }

        private Vector3 GeoToWorldPositionXz(Vector2d latitudeLongitude)
        {
            var scaleFactor = Mathf.Pow(2, (InitialZoom - AbsoluteZoom));
            var worldPos = Conversions.GeoToWorldPosition(latitudeLongitude, CenterMercator, WorldRelativeScale * scaleFactor).ToVector3xz();
            return Root.TransformPoint(worldPos);
        }

        private float QueryElevationAtInternal(Vector2d latlong, out float tileScale)
        {
            var meters = Conversions.LatLonToMeters(latlong.x, latlong.y);
            var foundTile = Visualizer.ActiveTiles.TryGetValue(Conversions.LatitudeLongitudeToTileId(latlong.x, latlong.y, (int)Zoom), out var tile);
            if (foundTile)
            {
                tileScale = tile.TileScale;
                var rect = tile.Rect;
                return tile.QueryHeightData((float)((meters - rect.Min).x / rect.Size.x), (float)((meters.y - rect.Max.y) / rect.Size.y));
            }
            tileScale = 1f;
            return 0f;
        }
        public Vector3 GeoToWorldPosition(Vector2d latitudeLongitude, bool queryHeight = true)
        {
            var worldPos = GeoToWorldPositionXz(latitudeLongitude);
            if (!queryHeight) return worldPos;
        
            var tileScale = 1f;
            var height = QueryElevationAtInternal(latitudeLongitude, out tileScale);
        
            if (!Visualizer.ActiveTiles.TryGetValue(
                Conversions.LatitudeLongitudeToTileId(latitudeLongitude.x, latitudeLongitude.y, (int) Zoom),
                out var tile)) return worldPos;
			
            if (tile == null) return worldPos;
        
            var localPos = tile.gameObject.transform.InverseTransformPoint(worldPos);
            localPos.y = height;
            worldPos = tile.gameObject.transform.TransformPoint(localPos);

            return worldPos;
        }
    
        public Vector2d WorldToGeoPosition(Vector3 point)
        {
            var scaleFactor = Mathf.Pow(2, (InitialZoom - AbsoluteZoom));

            return Root.InverseTransformPoint(point).GetGeoPosition(CenterMercator, WorldRelativeScale * scaleFactor);
        }

        public void SetCenterMercator(Vector2d centerMercator)
        {
            CenterMercator = centerMercator;
        }

        public void SetCenterLatitudeLongitude(Vector2d latLong)
        {
            _options.locationOptions.latitudeLongitude = $"{latLong.x}, {latLong.y}";
            CenterLatitudeLongitude = latLong;
        }

        public void SetZoom(float zoom)
        {
            _options.locationOptions.zoom = zoom;
        }

        public void SetWorldRelativeScale(float scale)
        {
            WorldRelativeScale = scale;
        }

        public void UpdateMap(Vector2d latLon, float zoom) { }

        public void ResetMap() { }

        private void Start()
        {
            _options.scalingOptions.scalingStrategy = new MapScalingAtWorldScaleStrategy();
            _options.placementOptions.placementStrategy = new MapPlacementAtTileCenterStrategy();
            
            InitializeMap();
            
        }

        private void InitializeMap()
        {
            TileMaterial = Resources.Load("Materials/Terrain") as Material;
            _options.locationOptions.zoom = 16;
            _options.extentOptions.extentType = MapExtentType.Custom;
            _options.scalingOptions.scalingType = MapScalingType.WorldScale;
            _options.placementOptions.placementType = MapPlacementType.AtTileCenter;
            _options.placementOptions.snapMapToZero = true;
            _options.tileMaterial = TileMaterial;
            AbsoluteZoom = 16;
            InitialZoom = 16;
            CenterLatitudeLongitude = Conversions.StringToLatLon("40.764170691358686, -73.97670925665614");
            SetWorldRelativeScale(Mathf.Pow(2, AbsoluteZoom - InitialZoom) * Mathf.Cos(Mathf.Deg2Rad * (float) CenterLatitudeLongitude.x));
            SetCenterMercator(Conversions.TileBounds(TileCover.CoordinateToTileId(CenterLatitudeLongitude, AbsoluteZoom)).Center);
            Visualizer.Initialize(this);
            _tileProvider = gameObject.AddComponent<ManhattanTileProvider>();
            _tileProvider.Initialize(this);
            _tileProvider.ExtentChanged += OnMapExtentChanged;
            _tileProvider.UpdateTileExtent();
        }
        
        private void OnMapExtentChanged(object sender, ExtentArgs currentExtent)
        {
            StartCoroutine(TriggerTileRedrawForExtent(currentExtent, Stopwatch.StartNew()));
            _tileProvider.ExtentChanged -= OnMapExtentChanged;
        }

        private IEnumerator TriggerTileRedrawForExtent(ExtentArgs extent, Stopwatch t)
        {
            foreach (var tileId in extent.activeTiles)
            {
                Visualizer.LoadTile(tileId);
                if (t.ElapsedMilliseconds <= 13) continue;
                yield return null;
                t.Restart();
            }
        }
    }
}
