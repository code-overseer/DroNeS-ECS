using DroNeS;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public class DroneEntityMenu : EditorWindow
    {
    
        [MenuItem("Window/Entities/Drone")]
        private static void Initialize()
        {
            var sizeWindow = CreateInstance<DroneEntityMenu>();
            sizeWindow.autoRepaintOnSceneChange = true;
            sizeWindow.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            DroneBootstrap.mesh = (Mesh) EditorGUILayout.ObjectField(DroneBootstrap.mesh, 
                typeof(Mesh), false);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            DroneBootstrap.material = (Material) EditorGUILayout.ObjectField(DroneBootstrap.material, 
                typeof(Material), false);
            EditorGUILayout.EndHorizontal();

        }
    
    }
}
