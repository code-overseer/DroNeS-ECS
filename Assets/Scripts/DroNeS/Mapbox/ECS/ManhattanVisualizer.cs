using Mapbox.Map;
using Mapbox.Unity.Map;
using Mapbox.Unity.Map.Interfaces;

namespace DroNeS.Mapbox.ECS
{
    public class ManhattanVisualizer
    {
        private readonly TerrainImageFactory _imageFactory;
        private readonly BuildingMeshFactory _meshFactory;
        private readonly DronesMap _map;
        private int _counter;

        public ManhattanVisualizer(DronesMap map)
        {
            _map = map;
            _imageFactory = new TerrainImageFactory();
            _meshFactory = new BuildingMeshFactory();
        }

        public void LoadTile(UnwrappedTileId tileId)
        {
            var tile = new CustomTile(in _map, in tileId);
            _imageFactory.Register(tile);
            _meshFactory.Register(tile);
        }
        
    
    }
}
