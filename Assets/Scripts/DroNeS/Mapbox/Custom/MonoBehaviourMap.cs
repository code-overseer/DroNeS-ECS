using System.Collections;
using DroNeS.Mapbox.Interfaces;
using Mapbox.Unity;

namespace DroNeS.Mapbox.Custom
{
    public class MonoBehaviourMap : UnityEngine.MonoBehaviour
    {
        private MapboxAccess _access;
        private IMap _map;
        private CustomTileFactory _imageFactory;
        private CustomTileFactory _meshFactory;
        private SerialMeshProcessor _processor;

        private void Awake()
        {
            _access = MapboxAccess.Instance;
            _map = new DronesMap();
            _processor = new SerialMeshProcessor();
            _imageFactory  = new TerrainImageFactory();
            _meshFactory = new BuildingMeshFactory(new SerialMeshBuilder(_map.BuildingProperties, _processor));

        }
        private IEnumerator Start()
        {
            while (!MapboxAccess.Configured) yield return null;
            foreach (var tileId in _map.Tiles)
            {
                var tile = new CustomTile(_map, in tileId);
                _imageFactory.Register(tile);
                _meshFactory.Register(tile);
            }
        }
        
    }
}
