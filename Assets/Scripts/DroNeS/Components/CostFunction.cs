using Unity.Entities;
using Unity.Mathematics;

namespace DroNeS.Components
{
    public struct CostFunction : IComponentData
    {
        public float Reward;
        public float Penalty;
        public float Guarantee;

        public static CostFunction GetEmergency(in Random rand)
        {
            return new CostFunction
            {
                Reward = 1,
                Penalty = 0,
                Guarantee = rand.NextFloat(0, 1) < 0.1347f ? 7 * 60 : 18 * 60
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
