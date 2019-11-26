using Unity.Entities;
using Unity.Mathematics;

namespace DroNeS.Components
{
    public struct CostFunction : IComponentData
    {
        private static Random _rand = new Random(1u);
        public float Reward;
        public float Penalty;
        public float Guarantee;

        public static CostFunction GetEmergency()
        {
            return new CostFunction
            {
                Reward = 1,
                Penalty = 0,
                Guarantee = _rand.NextFloat(0, 1) < 0.1347f ? 7 * 60 : 18 * 60
            };
        }
        
        public static CostFunction GetDelivery(float reward)
        {
            return new CostFunction
            {
                Reward = reward,
                Penalty = -5,
                Guarantee = 1800f
            };
        }
    }
}
