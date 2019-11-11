using Mapbox.Unity.MeshGeneration.Data;
using Mapbox.Unity.MeshGeneration.Modifiers;
using UnityEngine;
using UnityEngine.Rendering;

namespace DroNeS.Mapbox.ECS
{
    [CreateAssetMenu(menuName = "Mapbox/Modifiers/Mesh Renderer Modifier")]
    public class MeshRendererModifier : GameObjectModifier
    {
        [SerializeField] private Material[] materials;
        private Material[] Materials
        {
            get
            {
                if (materials != null) return materials;
                materials = new Material[1];
                materials[0] = null;
                return materials;
            }
        }
        //public override void Run(VectorEntity ve, UnityTile tile)
        //{
        //  ve.MeshRenderer.enabled = false;

        //}
        public override void Run(VectorEntity ve, UnityTile tile)
        {

            tile.MeshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            tile.MeshRenderer.allowOcclusionWhenDynamic = true;
            tile.MeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            Mesh mesh;
            (mesh = ve.MeshFilter.mesh).SetTriangles(ve.MeshFilter.mesh.triangles, 0);
            mesh.subMeshCount = 1;
            ve.MeshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            ve.MeshRenderer.materials = Materials;
        }
    }
}