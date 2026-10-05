using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class TileCreateMenu {
    [MenuItem("Assets/Create/2D/Tiles/Tile", false, 1)]
    public static void CreateTile() {
        string folder = "Assets";
        string selected = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (!string.IsNullOrEmpty(selected)) {
            if (AssetDatabase.IsValidFolder(selected)) {
                folder = selected;
            } else {
                string dir = Path.GetDirectoryName(selected);
                if (!string.IsNullOrEmpty(dir)) {
                    folder = dir.Replace('\\', '/');
                }
            }
        }

        string assetPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/New Tile.asset");
        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.name = Path.GetFileNameWithoutExtension(assetPath);
        AssetDatabase.CreateAsset(tile, assetPath);
        AssetDatabase.SaveAssets();

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = tile;
        EditorGUIUtility.PingObject(tile);
    }
}
