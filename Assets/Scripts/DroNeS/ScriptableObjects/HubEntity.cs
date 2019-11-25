using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using UnityEngine;
using Utils;
using BoxCollider = UnityEngine.BoxCollider;
using Material = UnityEngine.Material;

namespace DroNeS.ScriptableObjects
{
    public class HubEntity : ScriptableObject
    {
        public GameObject hub;
        private Material _material;
        private Mesh _mesh;
        private Material _highlightMaterial;
        private BoxCollider _geometry;
        
        private Material Material
        {
            get
            {
                if (_material != null) return _material;
                _material = hub.GetComponent<MeshRenderer>().sharedMaterial;
                return _material;
            }
        }

        private Mesh Mesh
        {
            get
            {
                if (_mesh != null) return _mesh;
                _mesh = hub.GetComponent<MeshFilter>().sharedMesh;
                return _mesh;
            }
        }
        
        public Material HighlightMaterial
        {
            get
            {
                if (_highlightMaterial != null) return _highlightMaterial;
                _highlightMaterial = Instantiate(Material);
                _highlightMaterial.shader = Shader.Find("Custom/Highlight");
                return _highlightMaterial;
            }
        }
        
        public BoxGeometry BoxGeometry
        {
            get
            {
                if (_geometry == null)
                {
                    _geometry = hub.GetComponent<BoxCollider>();
                }

                return new BoxGeometry
                {
                    Center = _geometry.center,
                    Size = _geometry.size,
                    Orientation = quaternion.identity,
                    BevelRadius = 0.05f * _geometry.size.MinComponent()
                };
            }
        }

        public RenderMesh ToRenderMesh()
        {
            return new RenderMesh
            {
                material = _material,
                mesh = _mesh
            };
        }
        
        public RenderMesh ToHighlightMesh()
        {
            return new RenderMesh
            {
                material = HighlightMaterial,
                mesh = _mesh
            };
        }
        
    }
}
