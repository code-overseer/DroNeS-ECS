using System.Collections;
using DroNeS.Systems.EventSystem;
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
        private Button Change {
            get
            {
                if (_button == null) _button = GetComponent<Button>();
                return _button;
            }
        }

        private IEnumerator Start()
        {
            while (World.Active.GetExistingSystem<CameraMovementSystem>() == null) yield return null;
            
            _camerasMovementSystem = World.Active.GetExistingSystem<CameraMovementSystem>();
            Change.onClick.AddListener(_camerasMovementSystem.OnCameraSwap);
        }
    }
}
