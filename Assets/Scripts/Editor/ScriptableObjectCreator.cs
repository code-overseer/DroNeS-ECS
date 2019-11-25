using DroNeS.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public static class ScriptableObjectCreator
    {
        [MenuItem("Assets/Create/DroneData")]
        public static void CreateDroneData()
        {
            var asset = ScriptableObject.CreateInstance<DroneEntity> ();

            asset.drone = Resources.Load("Prefabs/DronePrefab") as GameObject;

            ProjectWindowUtil.CreateAsset (asset, "Assets/Resources/EntityData/DroneData.asset");
        }
        
        [MenuItem("Assets/Create/HubData")]
        public static void CreateHubData()
        {
            var asset = ScriptableObject.CreateInstance<HubEntity> ();

            asset.hub = Resources.Load("Prefabs/HubPrefab") as GameObject;

            ProjectWindowUtil.CreateAsset (asset, "Assets/Resources/EntityData/HubData.asset");
        } 
    }
}
