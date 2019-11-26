using DroNeS.Components.Singletons;
using DroNeS.Systems.EventSystem;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DroNeS.Components
{
    public struct PlayerInput
    {
        private static CameraMovementSystem _cameraMovementSystem;

        private static CameraMovementSystem CameraMovementSystem =>
            _cameraMovementSystem ??
            (_cameraMovementSystem = World.Active.GetOrCreateSystem<CameraMovementSystem>());

        public static PlayerInput Get()
        {
            var output = new PlayerInput{ _value = float3x3.zero };
            output._value.c0.x = Input.GetAxis("Vertical");
            output._value.c0.y = Input.GetAxis("Horizontal");
            output._value.c0.z = Input.GetAxis("Rotate");
            output._value.c1.x = Input.GetAxis("Mouse X");
            output._value.c1.y = Input.GetAxis("Mouse Y");
            output._value.c1.z = Input.GetAxis("Mouse ScrollWheel");
            output._value.c2.x = Input.GetMouseButton(2) ? 1 : 0;
            output._value.c2.y = CameraMovementSystem.GetSingleton<View>().CameraType == CameraTypeValue.Main ? 1 : 0;
            
            return output;
        }
        
        private float3x3 _value;

        public float Vertical() => _value.c0.x;
        public float Horizontal() => _value.c0.y;
        public float Rotate() => _value.c0.z;
        public float MiddleMouse() => _value.c2.x;
        public float MouseX() => _value.c1.x;
        public float MouseY() => _value.c1.y;
        public float Scroll() => _value.c1.z;

        public float IsMainCamera() => _value.c2.y;
    }
}
