using Unity.Mathematics;
using UnityEngine;

namespace Utils
{
    public struct Ray
    {
        public float3 Origin;
        public float3 Direction;

        public float3 GetPoint(float distance) => Origin + distance * Direction;

        private Ray(float3 origin, float3 direction)
        {
            Origin = origin;
            Direction = math.normalize(direction);
        }

        public static implicit operator UnityEngine.Ray(Ray r)
        {
            return new UnityEngine.Ray(r.Origin, r.Direction);
        }
        
        public static implicit operator Ray(UnityEngine.Ray r)
        {
            return new Ray(r.origin, r.direction);
        }
    }
}
