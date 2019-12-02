using Mapbox.Utils;
using Unity.Mathematics;

namespace DroNeS.Utils
{
    public static class Vector2dExtension
    {
        // ReSharper disable once InconsistentNaming
        public static double2 ToSIMD(this Vector2d vector)
        {
            return new double2(vector.x, vector.y);
        }
    }
}
