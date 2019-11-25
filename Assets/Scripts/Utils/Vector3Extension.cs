using System;
using UnityEngine;

namespace Utils
{
    public static class Vector3Extension
    {
        public static float MinComponent(this Vector3 v)
        {
            return Math.Min(v.x, Math.Min(v.y, v.z));
        }
        
        public static float MaxComponent(this Vector3 v)
        {
            return Math.Max(v.x, Math.Max(v.y, v.z));
        }
    
    }
}
