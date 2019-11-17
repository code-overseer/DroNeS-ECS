using UnityEngine;

namespace DroNeS.MonoBehaviours
{
    public class ClipSpaceTest : MonoBehaviour
    {
        private Camera _cam;

        private void Start()
        {
            _cam = GameObject.Find("Main Camera").GetComponent<Camera>();

            foreach (Transform child in transform)
            {
                var pos = child.position;
                var v = new Vector4(pos.x, pos.y, pos.z, 1);
                v = _cam.projectionMatrix *_cam.worldToCameraMatrix * v;
                Debug.Log(v);    
            }
            
        }


    }
}
