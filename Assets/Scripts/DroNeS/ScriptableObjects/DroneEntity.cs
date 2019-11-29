using DroNeS.Utils;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using UnityEngine;
using BoxCollider = UnityEngine.BoxCollider;
using Material = UnityEngine.Material;

namespace DroNeS.ScriptableObjects
{
    public class DroneEntity : ScriptableObject
    {
        public GameObject drone;
        private Material _material;
        private Mesh _mesh;
        private BoxCollider _geometry;
        
        private Material _highlightMaterial;
        private Material _propellerMaterial;
        private Mesh _propellerMesh;
        private float3[] _propellerPositions;

        private Material Material
        {
            get
            {
                if (_material != null) return _material;
                _material = drone.GetComponent<MeshRenderer>().sharedMaterial;
                return _material;
            }
        }

        private Mesh Mesh
        {
            get
            {
                if (_mesh != null) return _mesh;
                _mesh = drone.GetComponent<MeshFilter>().sharedMesh;
                return _mesh;
            }
        }

        private Material PropellerMaterial
        {
            get
            {
                if (_propellerMaterial != null) return _propellerMaterial;
                _propellerMaterial = drone.transform.GetChild(0).GetComponent<MeshRenderer>().sharedMaterial;
                return _propellerMaterial;
            }
        }

        private Mesh PropellerMesh
        {
            get
            {
                if (_propellerMesh != null) return _propellerMesh;
                _propellerMesh = drone.transform.GetChild(0).GetComponent<MeshFilter>().sharedMesh;
                return _propellerMesh;
            }
        }

        public float3[] PropellerPositions
        {
            get
            {
                if (_propellerPositions != null && _propellerPositions.Length == 4) return _propellerPositions;
                _propellerPositions = new float3[4];
                for (var i = 0; i < 4; ++i)
                {
                    _propellerPositions[i] = drone.transform.GetChild(i).localPosition;
                }
                return _propellerPositions;
            }
        }

        public BoxGeometry BoxGeometry
        {
            get
            {
                if (_geometry == null)
                {
                    _geometry = drone.GetComponent<BoxCollider>();
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

        public RenderMesh ToRenderMesh()
        {
            return new RenderMesh
            {
                material = Material,
                mesh = Mesh
            };
        }
        
        public RenderMesh ToHighlightMesh()
        {
            return new RenderMesh
            {
                material = HighlightMaterial,
                mesh = Mesh
            };
        }

        public RenderMesh ToPropellerMesh()
        {
            return new RenderMesh
            {
                material = PropellerMaterial,
                mesh = PropellerMesh
            };
        }
        
        
    }
}