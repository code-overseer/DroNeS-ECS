using System;
using System.Collections;
using DroNeS.Mapbox.Interfaces;
using Mapbox.Unity;
using UnityEngine;

namespace DroNeS.Mapbox.Custom
{
    public class MonoBehaviourMap : UnityEngine.MonoBehaviour
    {
        private MapboxAccess _access;
        private IMap _map;
        private CustomTileFactory _imageFactory;
        private CustomTileFactory _meshFactory;
        private SerialMeshProcessor _processor;
        private Material _terrainMaterial;
//        private Texture2DArray _textures;
        private Mesh _wholeMesh;
        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private readonly int _textureArray = Shader.PropertyToID("_TextureArray");

        private void Awake()
        {
            _access = MapboxAccess.Instance;
            _map = new DronesMap();
            _processor = new SerialMeshProcessor();
            _terrainMaterial = new Material(Shader.Find("Custom/TerrainShader"));
            _imageFactory  = new TerrainImageFactory();
            _meshFactory = new BuildingMeshFactory(new SerialMeshBuilder(_map.BuildingProperties, _processor));
            _filter = gameObject.AddComponent<MeshFilter>();
            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _terrainMaterial;
        }
        private IEnumerator Start()
        {
            while (!MapboxAccess.Configured) yield return null;
            var instance = new CombineInstance[ManhattanTileProvider.Tiles.Count];
            var i = 0;
            var root = transform;
            
            foreach (var tileId in _map.Tiles)
            {
                var tile = new CustomTile(root, _map, in tileId);
                instance[i].mesh = tile.QuadMesh;
                instance[i].transform = root.localToWorldMatrix * tile.Transform.localToWorldMatrix;
                tile.ClearMesh();
                i++;
                _imageFactory.Register(tile);
                _meshFactory.Register(tile);
            }
            _filter.sharedMesh = new Mesh();
            _filter.sharedMesh.CombineMeshes(instance);
            while (((TerrainImageFactory) _imageFactory).TextureArray == null) yield return null;
            
            _terrainMaterial.SetTexture(_textureArray, ((TerrainImageFactory) _imageFactory).TextureArray);
        }

        
    }
}
