using Unity.Mathematics;

namespace DroNeS.Utils
{
    public static class RandomExtension
    {
        public static float2 InsideUnitCircle(this Random rand)
        {
            var a = rand.NextFloat() * 2 * math.PI;
            var r = math.sqrt(rand.NextFloat());
            return new float2(r * math.cos(a),r * math.sin(a));
        }

        public static float3 InsideUnitSphere(this Random rand)
        {
            var a = rand.NextFloat() * 2 * math.PI;
            var b = rand.NextFloat() * math.PI;
            var r = math.sqrt(rand.NextFloat());

            var c = math.sin(b);
            
            return new float3(r * c * math.cos(a), r * c * math.sin(a), r * math.cos(b));
        }
        
        public static float3 OnUnitSphere(this Random rand)
        {
            var a = rand.NextFloat() * 2 * math.PI;
            var b = rand.NextFloat() * math.PI;

            var c = math.sin(b);
            
            return new float3( c * math.cos(a), c * math.sin(a), math.cos(b));
        }

        public static float3 InsideUnitCube(this Random rand)
        {
            return new float3(rand.NextFloat(-0.5f, 0.5f), rand.NextFloat(-0.5f, 0.5f), rand.NextFloat(-0.5f, 0.5f));
        }
        
        public static float2 InsideUnitSquare(this Random rand)
        {
            return new float2(rand.NextFloat(-0.5f, 0.5f), rand.NextFloat(-0.5f, 0.5f));
        }
        
    }
}
