using System.Collections.Generic;
using Mapbox.Map;
using Mapbox.Platform;
using Mapbox.Unity;
using Mapbox.Unity.Map;
using Mapbox.Unity.Map.Interfaces;
using Mapbox.Unity.MeshGeneration.Data;
using Mapbox.Unity.MeshGeneration.Factories;
using Mapbox.Unity.MeshGeneration.Modifiers;
using Mapbox.Unity.Utilities;
using UnityEngine;
using UnityEngine.Serialization;

namespace DroNeS.Mapbox.MonoBehaviour
{
    public class ManhattanVisualizer : AbstractMapVisualizer
    {
        public VectorTileFactory meshFactory;
        public MapImageFactory imageFactory;
        private int _counter;
        private Mesh QuadMesh { get; set; }

        public void Initialize(IMapReadable map)
        {
            _map = map;
            meshFactory = CreateInstance<VectorTileFactory>();
            var vectorSubLayerProperties = new VectorSubLayerProperties
            {
                colliderOptions = {colliderType = ColliderType.MeshCollider},
                coreOptions =
                {
                    combineMeshes = true,
                    geometryType = VectorPrimitiveType.Polygon,
                    layerName = "building",
                    snapToTerrain = true
                },
                extrusionOptions =
                {
                    extrusionType = ExtrusionType.PropertyHeight,
                    extrusionScaleFactor = 1.3203f,
                    propertyName = "height",
                    extrusionGeometryType = ExtrusionGeometryType.RoofAndSide
                },
                moveFeaturePositionTo = PositionTargetType.CenterOfVertices
            };
            vectorSubLayerProperties.coreOptions.sublayerName = "Buildings";
            var atlasInfo = Resources.Load("Atlases/BuildingAtlas") as AtlasInfo;
            var material = Resources.Load("Materials/BuildingMaterial") as Material;
            var materialOptions = new GeometryMaterialOptions();
            materialOptions.SetDefaultMaterialOptions();
            materialOptions.customStyleOptions.texturingType = UvMapType.Atlas;
            materialOptions.customStyleOptions.materials[0].Materials[0] = material;
            materialOptions.customStyleOptions.materials[1].Materials[0] = material;
            materialOptions.customStyleOptions.atlasInfo = atlasInfo;
            materialOptions.SetStyleType(StyleTypes.Custom);
            vectorSubLayerProperties.materialOptions = materialOptions;
            vectorSubLayerProperties.buildingsWithUniqueIds = true;
            meshFactory.SetOptions(new VectorLayerProperties
            {
                performanceOptions = new LayerPerformanceOptions
                {
                    entityPerCoroutine = 20,
                    isEnabled = true
                },
                vectorSubLayers = new List<VectorSubLayerProperties> {vectorSubLayerProperties},
                sourceType = VectorSourceType.MapboxStreetsV8WithBuildingIds,
            });
            meshFactory.Initialize(MapboxAccess.Instance);
            
            imageFactory = CreateInstance<MapImageFactory>();
            imageFactory.Initialize(MapboxAccess.Instance);
            imageFactory.SetOptions(new ImageryLayerProperties
            {
                rasterOptions = new ImageryRasterOptions
                {
                    useCompression = false,
                    useMipMap = true,
                    useRetina = false
                },
                sourceType = ImagerySourceType.Custom,
                sourceOptions = new LayerSourceOptions
                {
                    layerSource = new Style
                    {
                        Name = "Manhattan",
                        Id = "mapbox://styles/jw5514/cjr7loico0my12rnrzcm9qk2p",
                        UserName = "jw5514"
                    },
                    isActive = true
                }
            });
            
            
        }
        
        private void MakeFlatTerrain(UnityTile tile)
        {
            if (QuadMesh != null) return;
            var verts = new Vector3[4];
            var tileScale = tile.TileScale;
            var rect = tile.Rect;
            verts[0] = tileScale * (rect.Min - rect.Center).ToVector3xz();
            verts[1] = tileScale * new Vector3((float)(rect.Max.x - rect.Center.x), 0, (float)(rect.Min.y - rect.Center.y));
            verts[2] = tileScale * (rect.Max - rect.Center).ToVector3xz();
            verts[3] = tileScale * new Vector3((float)(rect.Min.x - rect.Center.x), 0, (float)(rect.Max.y - rect.Center.y));
            var norms = new [] {Vector3.up, Vector3.up, Vector3.up, Vector3.up};

            QuadMesh = new Mesh
            {
                vertices = verts,
                normals = norms,
                triangles = new[] {0, 1, 2, 0, 2, 3},
                uv = new[]
                {
                    new Vector2(0, 1),
                    new Vector2(1, 1),
                    new Vector2(1, 0),
                    new Vector2(0, 0)
                }
            };
        }

        public override UnityTile LoadTile(UnwrappedTileId tileId)
        {
            var tile = new GameObject().AddComponent<UnityTile>();
            tile.Initialize(_map, tileId, _map.WorldRelativeScale, _map.AbsoluteZoom);
            MakeFlatTerrain(tile);
            tile.MeshFilter.sharedMesh = QuadMesh;
            tile.MeshRenderer.sharedMaterial = Instantiate(_map.TileMaterial);
            tile.transform.SetParent(_map.Root, false);
            tile.Initialize(_map, tileId, _map.WorldRelativeScale, _map.AbsoluteZoom, _map.LoadingTexture);
            PlaceTile(tileId, tile, _map);
            
#if UNITY_EDITOR
            tile.gameObject.name = tile.CanonicalTileId.ToString();
#endif
            imageFactory.Register(tile);
            meshFactory.Register(tile);

            return tile;
        } 
        protected override void PlaceTile(UnwrappedTileId tileId, UnityTile tile, IMapReadable map)
        {
            var rect = tile.Rect;
            var scale = tile.TileScale;
            var scaleFactor = Mathf.Pow(2, map.InitialZoom - map.AbsoluteZoom);

            var position = new Vector3(
                (float)(rect.Center.x - map.CenterMercator.x) * scale * scaleFactor,
                0,
                (float)(rect.Center.y - map.CenterMercator.y) * scale * scaleFactor);
            tile.transform.localPosition = position;
        }
    }
}
