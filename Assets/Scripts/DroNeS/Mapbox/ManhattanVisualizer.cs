using System.Collections.Generic;
using Mapbox.Map;
using Mapbox.Platform;
using Mapbox.Unity.Map;
using Mapbox.Unity.Map.Interfaces;
using Mapbox.Unity.MeshGeneration.Factories;
using Unity.Mathematics;
using UnityEngine;

namespace DroNeS.Mapbox
{
    public static class ManhattanVisualizer
    {
        public static BuildingMeshFactory MeshFactory;
        public static TerrainImageFactory ImageFactory;
        private static IMapReadable _map;
        private static Dictionary<UnwrappedTileId, CustomTile> _activeTiles = new Dictionary<UnwrappedTileId, CustomTile>();
        private static int _counter;
        private static ModuleState _state;
    
        public static void Initialize(IMapReadable map, IFileSource fileSource)
        {
            _map = map;
            _state = ModuleState.Initialized;
            MeshFactory = new BuildingMeshFactory();
            ImageFactory = new TerrainImageFactory();
        }

        private static float3 GeneratePosition(in CustomTile tile)
        {
            var rect = tile.Rect;
            var scale = tile.TileScale;
            var scaleFactor = math.pow(2, _map.InitialZoom - _map.AbsoluteZoom);
            return new float3(
                (float)(rect.Center.x - _map.CenterMercator.x) * scale * scaleFactor,
                0,
                (float)(rect.Center.y - _map.CenterMercator.y) * scale * scaleFactor);
            // Create entity component position here
        }
        
        public static CustomTile LoadTile(UnwrappedTileId tileId)
        {
            var tile = new CustomTile(in _map, in tileId, _map.AbsoluteZoom);
            GeneratePosition(tile); // do something
            _activeTiles.Add(tileId, tile); // to keep track

            MeshFactory.Register(tile);
            ImageFactory.Register(tile);

            return tile;
        }
        
    
    }
}
