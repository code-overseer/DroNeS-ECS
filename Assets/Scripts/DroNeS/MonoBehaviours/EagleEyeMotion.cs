using System;
using UnityEngine;

namespace DroNeS.MonoBehaviours
{
    public class EagleEyeMotion : MonoBehaviour, ICameraMotion
    {
        private Camera _camera;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void Update()
        {
            MoveLongitudinal(Input.GetAxis("Vertical") * SpeedScale);
            MoveLateral(Input.GetAxis("Horizontal") * SpeedScale);
            Rotate(Input.GetAxis("Rotate"));

            Zoom(Input.GetAxis("Mouse ScrollWheel") * 100);
        }

        public float SpeedScale => 2 * transform.position.y;
        public void MoveLongitudinal(float input)
        {
            var t = transform;
            t.position += input * Time.unscaledDeltaTime * t.up;
        }

        public void MoveLateral(float input)
        {
            var t = transform;
            t.position += input * Time.unscaledDeltaTime * t.right;
        }

        public void Zoom(float input)
        {
            var s = _camera.orthographicSize - input;
            s = Mathf.Clamp(s, 300, 4000f);
            _camera.orthographicSize = s;
        }

        public void Pitch(float input)
        { }

        public void Yaw(float input)
        { }

        public void Rotate(float input)
        {
            transform.RotateAround(transform.position, Vector3.up, -input);
        }
    }
}
