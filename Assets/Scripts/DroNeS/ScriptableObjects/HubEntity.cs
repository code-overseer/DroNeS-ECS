using DroNeS.Utils;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using UnityEngine;
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
        private BlobAssetReference<Unity.Physics.Collider> _collider;
        
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
        
        private BoxGeometry BoxGeometry
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
        
        public BlobAssetReference<Unity.Physics.Collider> BoxCollider
        {
            get
            {
                if (!_collider.IsCreated)
                {
                    _collider = Unity.Physics.BoxCollider.Create(BoxGeometry,
                        new CollisionFilter
                        {
                            BelongsTo = CollisionGroups.Hub,
                            CollidesWith = CollisionGroups.Cast,
                            GroupIndex = 0
                        });
                }

                return _collider;
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
