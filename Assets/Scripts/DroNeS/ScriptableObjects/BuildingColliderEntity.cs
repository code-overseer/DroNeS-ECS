using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using UnityEngine;
using Material = UnityEngine.Material;

namespace DroNeS.ScriptableObjects
{
    public class BuildingColliderEntity : ScriptableObject
    {
        public GameObject buildingCollider;
        private Material _material;
        private Mesh _mesh;

        private Material Material
        {
            get
            {
                if (_material != null) return _material;
                _material = buildingCollider.transform.GetChild(0).GetComponent<MeshRenderer>().sharedMaterial;
                return _material;
            }
        }

        private Mesh Mesh
        {
            get
            {
                if (_mesh != null) return _mesh;
                _mesh = buildingCollider.transform.GetChild(0).GetComponent<MeshFilter>().sharedMesh;
                return _mesh;
            }
        }
        
        public RenderMesh ToRenderMesh()
        {
            return new RenderMesh
            {
                material = Material,
                mesh = Mesh,
                layer = LayerMask.NameToLayer("Colliders")
            };
        }
        
        public BoxGeometry BoxGeometry =>
            new BoxGeometry
            {
                Center = float3.zero,
                Size = new float3(1,1,1),
                Orientation = quaternion.identity,
                BevelRadius = 0.05f
            };

        public Transform Parent => buildingCollider.transform;
    }
}
