using System.Collections;
using System.Globalization;
using DroNeS.Mapbox.Interfaces;
using DroNeS.Utils.Time;
using Mapbox.Unity;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DroNeS.Mapbox.Custom.Parallel
{
    public class AsynchronousMap : MonoBehaviour
    {
        private MapboxAccess _access;
        private IMap _map;
        private CustomTileFactory _imageFactory;
        private CustomTileFactory _meshFactory;
        private Material _terrainMaterial;
        private Mesh _terrainMesh;
        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private readonly int _textureArray = Shader.PropertyToID("_TextureArray");

        private void Awake()
        {
            _access = MapboxAccess.Instance;
            _map = new DronesMap();
            _terrainMaterial = new Material(Shader.Find("Custom/TerrainShader"));
            _imageFactory  = new TerrainImageFactory(OnAllImageLoaded);
            var builder = new AsynchronousMeshBuilder(_map.BuildingProperties);
            _meshFactory = new BuildingMeshFactory(builder);
            _filter = gameObject.AddComponent<MeshFilter>();
            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _terrainMaterial;
        }

        private void OnAllImageLoaded(Texture array, Mesh mesh)
        {
            _terrainMaterial.SetTexture(_textureArray, array);
            _filter.sharedMesh = mesh;
        }

        private IEnumerator Start()
        {
            while (!MapboxAccess.Configured) yield return null;
            var profiler = new CustomTimer().Start();

            foreach (var tileId in _map.Tiles)
            {
                var tile = new CustomTile(transform, _map, in tileId);
                _imageFactory.Register(tile);
                _meshFactory.Register(tile);
            }

            while (CoroutineManager.Count > 0) yield return null; 
            Debug.Log(profiler.ElapsedSeconds.ToString(CultureInfo.CurrentCulture));
            profiler.Stop();
        }
    }
}
