using UnityEngine;

namespace DroNeS.MonoBehaviours
{
    public class CameraMotion : MonoBehaviour, ICameraMotion
    {
        private void Update()
        {
            MoveLongitudinal(Input.GetAxis("Vertical") * SpeedScale);
            MoveLateral(Input.GetAxis("Horizontal") * SpeedScale);
            Rotate(Input.GetAxis("Rotate") * 30);

            Zoom(Input.GetAxis("Mouse ScrollWheel") * SpeedScale);
            //FPS mouse hold click
            if (Input.GetMouseButton(2))
            {
                Pitch(Input.GetAxis("Mouse Y") * 30);
                Yaw(Input.GetAxis("Mouse X") * 30);
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
            transform.Rotate(-input, 0, 0);
        }

        public void Yaw(float input)
        {
            transform.Rotate(0, input, 0, Space.World);
        }

        public void Rotate(float input)
        {
            transform.Rotate(0, input, 0, Space.World);
        }

        #endregion

    }
}

