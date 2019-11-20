using UnityEngine;

namespace DroNeS.MonoBehaviours
{
    public class Propeller : MonoBehaviour
    {
        // Start is called before the first frame update

        // Update is called once per frame
        private void Update()
        {
            
            transform.Rotate(Vector3.up, 1.0f/Time.deltaTime, Space.World);
        }
    }
}
