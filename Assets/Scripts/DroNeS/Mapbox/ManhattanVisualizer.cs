using System;
using System.Collections.Generic;
using System.Linq;
using Mapbox.Map;
using Mapbox.Platform;
using Mapbox.Unity.Map;
using Mapbox.Unity.Map.Interfaces;
using Mapbox.Unity.MeshGeneration.Data;
using Mapbox.Unity.MeshGeneration.Enums;
using Mapbox.Unity.MeshGeneration.Factories;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DroNeS.Mapbox
{
    public static class ManhattanVisualizer
    {
        public static List<AbstractTileFactory> factories;
        private static IMapReadable _map;
        private static Dictionary<UnwrappedTileId, UnityTile> _activeTiles = new Dictionary<UnwrappedTileId, UnityTile>();
        private static Queue<UnityTile> _inactiveTiles = new Queue<UnityTile>();
        private static int _counter;
        private static ModuleState _state;
    
        public static void Initialize(IMapReadable map, IFileSource fileSource)
        {
            _map = map;
            _state = ModuleState.Initialized;

            foreach (var factory in factories)
            {
                factory.Initialize(fileSource);
                UnregisterEvents(factory);
                RegisterEvents(factory);
            }
        }
        private static void RegisterEvents(AbstractTileFactory factory)
        {
            factory.OnTileError += Factory_OnTileError;
        }
        private static void UnregisterEvents(AbstractTileFactory factory)
        {
            factory.OnTileError -= Factory_OnTileError;
        }
        private static void Factory_OnTileError(object sender, TileErrorEventArgs e)
        {
            Debug.LogError(e.Exceptions.Count > 0 ? e.Exceptions[0].Message : "Unknown Error Occured");
        }
        
        private static void PlaceTile(UnityTile tile)
        {
            var rect = tile.Rect;
            var scale = tile.TileScale;
            var scaleFactor = Mathf.Pow(2, (_map.InitialZoom - _map.AbsoluteZoom));
            var position = new Vector3(
                (float)(rect.Center.x - _map.CenterMercator.x) * scale * scaleFactor,
                0,
                (float)(rect.Center.y - _map.CenterMercator.y) * scale * scaleFactor);
            tile.transform.localPosition = position;
            // Create entity component position here
        }
        
        public static UnityTile LoadTile(UnwrappedTileId tileId)
        {
            UnityTile unityTile = null;

            if (_inactiveTiles.Count > 0)
            {
                unityTile = _inactiveTiles.Dequeue(); // might not need since no gameobject
            }

            if (unityTile == null)
            {
                unityTile = new GameObject().AddComponent<UnityTile>();
                unityTile.MeshRenderer.sharedMaterial = Object.Instantiate(_map.TileMaterial);
                unityTile.transform.SetParent(_map.Root, false);
            }

            unityTile.Initialize(_map, tileId, _map.WorldRelativeScale, _map.AbsoluteZoom, _map.LoadingTexture);
            PlaceTile(unityTile);

            unityTile.TileState = TilePropertyState.Loading;
            _activeTiles.Add(tileId, unityTile); // to keep track

            foreach (var factory in factories)
            {
                factory.Register(unityTile);
            }

            return unityTile;
        }
        
    
    }
}
