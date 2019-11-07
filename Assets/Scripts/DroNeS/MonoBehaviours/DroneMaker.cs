using DroNeS.Systems;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DroNeS.MonoBehaviours
{
    public class DroneMaker : MonoBehaviour
    {
        // Start is called before the first frame update
        private Button _button;
        private Button BuildDrone {
            get
            {
                if (_button == null) _button = GetComponent<Button>();
                return _button;
            }
        }
        
        private void Awake()
        {
            BuildDrone.onClick.AddListener(DroneBuilderSystem.AddDrone);
        }


    }
}
