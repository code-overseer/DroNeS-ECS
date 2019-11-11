using UnityEngine;

namespace DroNeS.MonoBehaviours
{
    public class CameraMotion : MonoBehaviour
    {
        private Transform _cameraTransform;

        private void Awake()
        {
            _cameraTransform = GetComponent<Camera>().transform;
        }

        private void Update()
        {
            MoveLongitudinal(Input.GetAxis("Vertical") * SpeedScale);
            MoveLateral(Input.GetAxis("Horizontal") * SpeedScale);
            Rotate(Input.GetAxis("Rotate") * 0.05f);

            Zoom(Input.GetAxis("Mouse ScrollWheel") * SpeedScale);
            //FPS mouse hold click
            if (Input.GetMouseButton(0))
            {
                Pitch(Input.GetAxis("Mouse Y"));
                Yaw(Input.GetAxis("Mouse X"));
            }
            // Bounds
            ClampVertical();
            
        }

        private float SpeedScale => 2 * transform.position.y + 5;

        #region Movement Implementation

        private void MoveLongitudinal(float input)
        {
            var positiveDirection = Vector3.Cross(_cameraTransform.right, Vector3.up).normalized;

            transform.position += input * Time.unscaledDeltaTime * positiveDirection;
        }

        private void MoveLateral(float input)
        {
            transform.position += input * Time.unscaledDeltaTime * _cameraTransform.right;
        }
        
        private void Zoom(float input)
        {
            var front = _cameraTransform.forward;
            // Cannot zoom when facing up
            if (front.y < 0)
            {
                transform.position += input * Time.unscaledDeltaTime * 3 * front;
            }
        }

        private void Pitch(float input)
        {
            transform.Rotate(-input * 30, 0, 0);
        }

        private void Yaw(float input)
        {
            transform.Rotate(0, input * 30, 0, Space.World);
        }

        private void Rotate(float input)
        {
            var pos = transform.position;
            var forward = _cameraTransform.forward;
            pos -= forward * pos.y / (forward.y > 0 ? forward.y : 0.01f);
            transform.RotateAround(pos, Vector3.up, input);
        }

        private void ClampVertical()
        {
            var position = transform.position;
            position.y = Mathf.Clamp(position.y, 0, 1000);
            transform.position = position;
        }

        #endregion

    }
}

