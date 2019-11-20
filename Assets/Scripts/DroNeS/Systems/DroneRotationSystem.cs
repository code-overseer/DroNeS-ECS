using DroNeS.Components;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace DroNeS.Systems
{
    public class DroneRotationSystem : JobComponentSystem
    {
        private EntityQuery _droneQuery;
        protected override void OnCreate()
        {
            base.OnCreate();
            _droneQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<DroneTag>(), 
                    typeof(Rotation),
                    typeof(Translation),
                    typeof(Waypoint),
                    typeof(LocalToWorld)
                }
            });
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var job = new DroneRotationJob
            {
                Rotations = GetArchetypeChunkComponentType<Rotation>(),
                Translations = GetArchetypeChunkComponentType<Translation>(),
                Waypoints = GetArchetypeChunkComponentType<Waypoint>(),
                Models = GetArchetypeChunkComponentType<LocalToWorld>()
            };

            return job.Schedule(_droneQuery, inputDeps);
        }

        private struct DroneRotationJob : IJobChunk
        {
            public ArchetypeChunkComponentType<Rotation> Rotations;
            public ArchetypeChunkComponentType<Translation> Translations;
            public ArchetypeChunkComponentType<Waypoint> Waypoints;
            public ArchetypeChunkComponentType<LocalToWorld> Models;
            private const float RotationDelta = 0.01f; 
            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                var waypoints = chunk.GetNativeArray(Waypoints);
                var rotations = chunk.GetNativeArray(Rotations);
                var translations = chunk.GetNativeArray(Translations);
                var models = chunk.GetNativeArray(Models);
                for (var i = 0; i < chunk.Count; ++i)
                {
                    if (waypoints[i].index < 0 || waypoints[i].length < 1) continue;
                    var forward = math.mul(models[i].Value, new float4(1, 0, 0, 0)); //TODO change forward
                    var angle = SignedAngle(forward.xyz, waypoints[i].waypoint - translations[i].Value);
                    var q = rotations[i].Value;
                    rotations[i] = new Rotation {Value = RotateTowards(in q, angle)};
                }
            }

            private static float Angle(in float3 from, in float3 to)
            {
                var num = (float) math.sqrt(math.distancesq(from, float3.zero) * math.distancesq(to, float3.zero));
                if ((double) num < 1.00000000362749E-15)
                    return 0.0f;
                return (float) math.acos((double) math.clamp(math.dot(from, to) / num, -1f, 1f)); //radians
            }

            private static float SignedAngle(in float3 from, in float3 to)
            {
                var axis = new float3(0,1,0);
                var num1 = Angle(in from, in to);
                var num2 = (float) ((double) from.y * (double) to.z - (double) from.z * (double) to.y);
                var num3 = (float) ((double) from.z * (double) to.x - (double) from.x * (double) to.z);
                var num4 = (float) ((double) from.x * (double) to.y - (double) from.y * (double) to.x);
                var num5 = math.sign((float) ((double) axis.x * (double) num2 + (double) axis.y * (double) num3 + (double) axis.z * (double) num4));
                return num1 * num5;
            }
            
            private static quaternion RotateTowards(in quaternion rotation, in float angle)
            {
                var q = quaternion.AxisAngle(new float3(0, 1, 0), math.abs(angle) <= RotationDelta ? angle : math.sign(angle)*RotationDelta);
                return math.mul(q, rotation);
            }
        }
    }
}
