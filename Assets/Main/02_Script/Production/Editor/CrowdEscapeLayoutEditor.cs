using System;
using System.IO;
using DeadCrowd.Content;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadCrowd.Editor
{
    public static class CrowdEscapeLayoutEditor
    {
        [MenuItem("DEAD CROWD/Content/Add Escape Routes")]
        public static void AddRoutes()
        {
            EditorSceneManager.OpenScene(ProductionSceneEditor.SCENE_PATH);
            Transform world = GameObject.Find("City Blockout")?.transform;
            if (world == null) throw new InvalidOperationException("Open a city map with a City Blockout root.");
            if (world.Find("Escape Routes") != null) { Selection.activeGameObject = world.Find("Escape Routes").gameObject; return; }
            string materialPath = "Assets/Main/07_Polish/Sight Blocker.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(materialPath));
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", new Color(0.32f, 0.35f, 0.3f)); material.SetFloat("_Smoothness", 0.1f);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            var routes = new GameObject("Escape Routes"); routes.transform.SetParent(world, false);
            for (int i = 0; i < 4; i++)
            {
                var corner = new GameObject("Two Exit Corner " + (i + 1)); corner.transform.SetParent(routes.transform, false);
                Quaternion rotation = Quaternion.Euler(0, i * 90f, 0);
                corner.transform.localPosition = rotation * new Vector3(0, 0, 15.5f); corner.transform.localRotation = rotation;
                Wall(corner.transform, "Long Screen", new Vector3(0, 1.2f, 0), new Vector3(3.4f, 2.4f, 0.28f), material);
                Wall(corner.transform, "Return Screen", new Vector3(1.56f, 1.2f, -0.96f), new Vector3(0.28f, 2.4f, 2.2f), material);
            }
            var map = CrowdContent.Catalog.Map;
            if (!map.MapVersion.Contains("escape-1")) { map.MapVersion += "+escape-1"; EditorUtility.SetDirty(map); }
            EditorSceneManager.SaveScene(world.gameObject.scene); AssetDatabase.SaveAssets();
            Selection.activeGameObject = routes;
        }
        private static void Wall(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = name;
            wall.transform.SetParent(parent, false); wall.transform.localPosition = position; wall.transform.localScale = size;
            wall.GetComponent<Renderer>().sharedMaterial = material;
            wall.isStatic = true;
        }
        public static void ApplyAndBuild()
        {
            AddRoutes(); CrowdContentEditor.RebuildNavMesh(); ProductionSceneEditor.VerifyAndBuild();
        }
    }
}
