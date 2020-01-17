using System.Collections;
using DroNeS.Systems.EventSystem;
using DroNeS.Systems.FixedUpdates;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace DroNeS.MonoBehaviours
{
    public class CameraChanger : MonoBehaviour
    {
        // TODO make this cleaner
        private Button _button;
        private CameraMovementSystem _camerasMovementSystem;
        private SunOrbitSystem _clockSystem;
        private Button Change {
            get
            {
                if (_button == null) _button = GetComponent<Button>();
                return _button;
            }
        }

        private IEnumerator Start()
        {
            while (World.Active.GetExistingSystem<CameraMovementSystem>() == null ||
                   World.Active.GetExistingSystem<SunOrbitSystem>() == null) yield return null;
            
            _camerasMovementSystem = World.Active.GetExistingSystem<CameraMovementSystem>();
            _clockSystem = World.Active.GetExistingSystem<SunOrbitSystem>();
            Change.onClick.AddListener(OnModeChange);
        }

        private void OnModeChange()
        {
            _clockSystem.ChangeTimeSpeed(Speed.Pause);
            _camerasMovementSystem.OnCameraSwap();

        }
    }
}
