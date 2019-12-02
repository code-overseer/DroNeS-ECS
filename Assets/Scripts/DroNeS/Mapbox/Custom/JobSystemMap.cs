using System.Collections.Generic;
using DroNeS.Mapbox.Interfaces;
using Unity.Jobs;
using Unity.Rendering;
using UnityEngine;

namespace DroNeS.Mapbox.Custom
{
    public class JobSystemMap
    {
        private readonly IMap _map;
        private readonly ParallelMeshProcessor _processor;
        private readonly CustomTileFactory _imageFactory;
        private readonly CustomTileFactory _meshFactory;
        public JobHandle Termination { get; }
        public Dictionary<CustomTile, IEnumerable<RenderMesh>> RenderMeshes => _processor.RenderMeshes;

        public JobSystemMap()
        {
            if (!Application.isPlaying) return;
            _map = new DronesMap();
            _processor = new ParallelMeshProcessor();
            _imageFactory = new TerrainImageFactory();
            _meshFactory = new BuildingMeshFactory(new ParallelMeshBuilder(_map.BuildingProperties, _processor));
            InitializeMap();
            Termination = _processor.Terminate();
        }

        private void InitializeMap()
        {
            var tiles = new List<CustomTile>(16);
            foreach (var tileId in _map.Tiles)
            {
                var tile = new CustomTile(_map, in tileId);
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
