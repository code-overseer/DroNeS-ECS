using Mapbox.Utils;

namespace DroNeS.Mapbox.Custom
{
    public interface IMap
    {
        Vector2d CenterMercator { get; }
        float WorldRelativeScale { get; }
        Vector2d CenterLatitudeLongitude { get; }
        int InitialZoom { get; }
        int AbsoluteZoom { get; }
        
    }
}
