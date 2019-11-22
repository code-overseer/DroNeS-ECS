using UnityEngine;

namespace DroNeS.MonoBehaviours
{
    public interface ICameraMotion
    {
        float SpeedScale { get; }

        void MoveLongitudinal(float input);

        void MoveLateral(float input);
        
        void Zoom(float input);

        void Pitch(float input);

        void Yaw(float input);

        void Rotate(float input);
        
    }
}
