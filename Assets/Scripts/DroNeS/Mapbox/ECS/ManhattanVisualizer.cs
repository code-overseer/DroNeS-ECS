using Mapbox.Map;
using Mapbox.Unity.Map;
using Mapbox.Unity.Map.Interfaces;

namespace DroNeS.Mapbox.ECS
{
    public class ManhattanVisualizer
    {
        public BuildingMeshFactory MeshFactory;
        public TerrainImageFactory ImageFactory;
        private readonly DronesMap _map;
        private int _counter;

        public ManhattanVisualizer(DronesMap map)
        {
            _map = map;
            MeshFactory = new BuildingMeshFactory();
            ImageFactory = new TerrainImageFactory();
        }

        public void LoadTile(UnwrappedTileId tileId)
        {
            var tile = new CustomTile(in _map, in tileId);
            ImageFactory.Register(tile);
            MeshFactory.Register(tile);
        }
        
    
    }
}
