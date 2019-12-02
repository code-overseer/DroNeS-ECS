using System.Collections.Generic;
using DroNeS.Mapbox.Interfaces;
using DroNeS.Utils;
using Mapbox.Map;
using Mapbox.Unity.Map;
using Mapbox.Unity.Map.Strategies;
using Mapbox.Unity.MeshGeneration.Modifiers;
using Mapbox.Unity.Utilities;
using Unity.Mathematics;

namespace DroNeS.Mapbox.Custom
{
    public class DronesMap : IMap
    {
        private readonly MapOptions _options = new MapOptions();
        public double2 CenterMercator { get; }
        public float WorldRelativeScale { get; }
        public double2 CenterLatitudeLongitude { get; }
        private float Zoom => _options.locationOptions.zoom;
        public int InitialZoom => 16;
        public int AbsoluteZoom => (int) math.floor(Zoom);
        public IEnumerable<UnwrappedTileId> Tiles { get; }
        public VectorSubLayerProperties BuildingProperties { get; }
        
        public const string LayerName = "building";

        public DronesMap()
        {
            _options.locationOptions.zoom = 16;
            _options.scalingOptions.scalingStrategy = new MapScalingAtWorldScaleStrategy();
            _options.placementOptions.placementStrategy = new MapPlacementAtTileCenterStrategy();
            
            CenterLatitudeLongitude = new double2(40.764170691358686, -73.97670925665614);
            WorldRelativeScale = math.pow(2.0f, AbsoluteZoom - InitialZoom) * (float)math.cos(math.radians(CenterLatitudeLongitude.x));
            CenterMercator = Conversions.TileBounds(FromCoordinates(CenterLatitudeLongitude)).Center.ToSIMD();

            Tiles = ManhattanTileProvider.GetTiles(this);
            
            BuildingProperties = new VectorSubLayerProperties
            {
                colliderOptions = {colliderType = ColliderType.None},
                coreOptions =
                {
                    geometryType = VectorPrimitiveType.Polygon,
                    layerName = LayerName,
                    snapToTerrain = true,
                    combineMeshes = true
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
            BuildingProperties.coreOptions.sublayerName = "Buildings";
            BuildingProperties.buildingsWithUniqueIds = true;
            BuildingProperties.coreOptions.geometryType = VectorPrimitiveType.Polygon;
            BuildingProperties.honorBuildingIdSetting = true;
        }
        
        private UnwrappedTileId FromCoordinates(double2 center)
        {
            var lat = center.x;
            var lng = center.y;
            
            var x = (int)math.floor((lng + 180.0) / 360.0 * math.pow(2.0, AbsoluteZoom));
            var y = (int)math.floor((1.0 - math.log(math.tan(lat * math.PI / 180.0)
                                                    + 1.0 / math.cos(lat * math.PI / 180.0)) / math.PI) / 2.0 * math.pow(2.0, AbsoluteZoom));

            return new UnwrappedTileId(AbsoluteZoom, x, y);
        }
        
    }
}
