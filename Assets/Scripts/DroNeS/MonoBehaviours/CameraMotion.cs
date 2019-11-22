using UnityEngine;

namespace DroNeS.MonoBehaviours
{
    public class CameraMotion : MonoBehaviour, ICameraMotion
    {
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
        private void ClampVertical()
        {
            var position = transform.position;
            position.y = Mathf.Clamp(position.y, 0, 1000);
            transform.position = position;
        }
        
        #region Movement Implementation
        
        public float SpeedScale => 2 * transform.position.y + 5;

        public void MoveLongitudinal(float input)
        {
            var positiveDirection = Vector3.Cross(transform.right, Vector3.up).normalized;

            transform.position += input * Time.unscaledDeltaTime * positiveDirection;
        }

        public void MoveLateral(float input)
        {
            transform.position += input * Time.unscaledDeltaTime * transform.right;
        }
        
        public void Zoom(float input)
        {
            var front = transform.forward;
            if (front.y < 0)
            {
                transform.position += input * Time.unscaledDeltaTime * 3 * front;
            }
        }

        public void Pitch(float input)
        {
            transform.Rotate(-input * 30, 0, 0);
        }

        public void Yaw(float input)
        {
            transform.Rotate(0, input * 30, 0, Space.World);
        }

        public void Rotate(float input)
        {
            var t = transform;
            var pos = t.position;
            var forward = t.forward;
            pos -= forward * pos.y / (forward.y > 0 ? forward.y : 0.01f);
            transform.RotateAround(pos, Vector3.up, input);
        }

        #endregion

    }
}

