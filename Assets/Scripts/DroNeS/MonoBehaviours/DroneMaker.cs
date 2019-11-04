using UnityEngine;
using UnityEngine.UI;

namespace DroNeS
{
    public class DroneMaker : MonoBehaviour
    {
        // Start is called before the first frame update
        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(DroneBootstrap.AddDrone);
        }
    }
}
