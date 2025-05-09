using UnityEditor;
using UnityEngine;

namespace MultiSet
{
    [CustomEditor(typeof(MapMeshDownloader))]
    public class MapMeshDownloaderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            MapMeshDownloader mapMeshDownloader = (MapMeshDownloader)target;

            GUILayout.Space(20);
            EditorGUILayout.HelpBox("Download Mesh in Editor Mode. Helps to setup AR Scene", MessageType.Info);

            GUIContent buttonContent = new GUIContent("Download Mesh", "This downloads the mesh file for the specified Map or MapSet.");
            GUILayout.Space(20);

            if (mapMeshDownloader.isDownloading)
            {
                GUI.backgroundColor = Color.green;
                buttonContent.text = "Downloading Mesh...";
            }
            else
            {
                GUI.backgroundColor = GUI.backgroundColor; // Reset to default color
                buttonContent.text = "Download Mesh";
            }

            if (GUILayout.Button(buttonContent, GUILayout.Height(30))) // Increase the height to 30
            {
                if (!mapMeshDownloader.isDownloading)
                {
                    mapMeshDownloader.DownloadMesh();
                }
            }
        }
    }
}