using DroNeS.Components;
using DroNeS.SharedComponents;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace DroNeS.Systems
{
    public class PropellerRotationSystem : JobComponentSystem
    {
        private EntityQuery _propellerQuery;

        protected override void OnCreate()
        {
            base.OnCreate();
            _propellerQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Parent>(),
                    ComponentType.ReadOnly<PropellerTag>(),
                    typeof(Rotation)
                }
            });
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var job = new PropellerRotationJob
            {
                Rotations = GetArchetypeChunkComponentType<Rotation>()
            };

            return job.Schedule(_propellerQuery, inputDeps);
        }

        [BurstCompile]
        private struct PropellerRotationJob : IJobChunk
        {
            public ArchetypeChunkComponentType<Rotation> Rotations;
            private const float RotationSpeed = math.PI / 4;
            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                var rotations = chunk.GetNativeArray(Rotations);
                for (var i = 0; i < chunk.Count; ++i)
                {
                    var q = quaternion.AxisAngle(new float3(0, 1, 0), RotationSpeed); 
                    rotations[i] = new Rotation
                    {
                        Value =  math.mul(q, rotations[i].Value) 
                    };
                }
            }
        }
    }
}
