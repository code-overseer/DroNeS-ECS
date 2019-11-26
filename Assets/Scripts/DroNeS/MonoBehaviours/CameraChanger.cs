using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DroNeS.MonoBehaviours
{
    public class CameraChanger : MonoBehaviour
    {
        // TODO make this cleaner
        private Button _button;
        private Camera[] _cameras;
        private Button Change {
            get
            {
                if (_button == null) _button = GetComponent<Button>();
                return _button;
            }
        }
        
        private IEnumerable<Camera> Cameras => _cameras ?? (_cameras = FindObjectsOfType<Camera>());

        private void Start()
        {
            Change.onClick.AddListener(Swap);
        }

        private void Swap()
        {
            foreach (var cam in Cameras)
            {
                cam.enabled = !cam.enabled;
            }
        }
    }
}
