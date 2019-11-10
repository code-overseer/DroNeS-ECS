using System.Collections.Generic;
using Mapbox.Map;
using Mapbox.Unity.Map;
using Mapbox.Unity.Map.Interfaces;
using Mapbox.Unity.MeshGeneration.Factories;
using Unity.Mathematics;
using UnityEngine;

namespace DroNeS.Mapbox
{
    public class ManhattanVisualizer
    {
        public BuildingMeshFactory MeshFactory;
        public TerrainImageFactory ImageFactory;
        private IMapReadable _map;
        private Dictionary<UnwrappedTileId, CustomTile> _activeTiles = new Dictionary<UnwrappedTileId, CustomTile>();
        private int _counter;
        private ModuleState _state;
    
        public ManhattanVisualizer(IMapReadable map)
        {
            _map = map;
            _state = ModuleState.Initialized;
            MeshFactory = new BuildingMeshFactory();
            ImageFactory = new TerrainImageFactory();
        }

        public CustomTile LoadTile(UnwrappedTileId tileId)
        {
            var tile = new CustomTile(in _map, in tileId);
            _activeTiles.Add(tileId, tile); // to keep track
            ImageFactory.Register(tile);
            MeshFactory.Register(tile);

            return tile;
        }
        
    
    }
}
