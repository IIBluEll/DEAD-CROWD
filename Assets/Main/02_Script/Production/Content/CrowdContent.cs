using System;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace DeadCrowd.Content
{
    public static class CrowdContent
    {
        private static CrowdGameSettings s_settings;
        private static CrowdMapProfile s_map;
        public static CrowdContentCatalog Catalog => Resources.Load<CrowdContentCatalog>("Crowd/ContentCatalog");
        public static CrowdGameSettings Settings { get { Ensure(); return s_settings; } }
        public static CrowdMapProfile Map { get { Ensure(); return s_map; } }
        public static string Signature { get; private set; }
        public static string SessionVersion => Prototype.CrowdRules.BUILD_VERSION + ":" + Signature;

        private static void Ensure() { if (s_settings == null || s_map == null) Freeze(); }
        public static void Freeze()
        {
            CrowdGameSettings source = Catalog != null ? Catalog.GameSettings : null;
            CrowdMapProfile map = Catalog != null ? Catalog.Map : null;
            if (source == null || map == null) throw new InvalidOperationException("Create the default Crowd content assets before playing.");
            source.Validate(); map.Validate();
            if (Catalog.Presentation == null || Catalog.Presentation.CommonVisualPrefab == null || !Catalog.Presentation.CommonVisualPrefab.Configured)
                throw new InvalidOperationException("Assign an Animator/body renderers to the visual prefab and remove its Collider/NetworkObject components.");
            Catalog.Presentation.Validate();
            if (s_settings != null) UnityEngine.Object.Destroy(s_settings);
            if (s_map != null) UnityEngine.Object.Destroy(s_map);
            s_settings = UnityEngine.Object.Instantiate(source);
            s_map = UnityEngine.Object.Instantiate(map);
            using (SHA256 hash = SHA256.Create())
                Signature = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(s_settings) + JsonUtility.ToJson(s_map) + SceneGeometry()))).Replace("-", "");
            Debug.Log("[Content] Session rules/map signature=" + Signature + ", map=" + s_map.MapVersion);
        }

        private static string SceneGeometry()
        {
            var shapes = new List<string>();
            foreach (Collider collider in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (!collider.enabled || collider.isTrigger || collider.GetComponentInParent<Prototype.CrowdActor>() != null) continue;
                string path = collider.name;
                for (Transform parent = collider.transform.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
                var data = new StringBuilder(collider.gameObject.scene.path + ":" + path + ":" + collider.GetType().Name + ":" + collider.gameObject.layer);
                data.Append(collider.transform.localToWorldMatrix.ToString("R", CultureInfo.InvariantCulture));
                data.Append(collider.bounds.center.ToString("R", CultureInfo.InvariantCulture)).Append(collider.bounds.size.ToString("R", CultureInfo.InvariantCulture));
                if (collider is MeshCollider mesh && mesh.sharedMesh != null && mesh.sharedMesh.isReadable)
                {
                    foreach (Vector3 vertex in mesh.sharedMesh.vertices) data.Append(vertex.ToString("R", CultureInfo.InvariantCulture));
                    foreach (int index in mesh.sharedMesh.triangles) data.Append(index).Append(',');
                }
                shapes.Add(data.ToString());
            }
            shapes.Sort(StringComparer.Ordinal);
            return string.Join("\n", shapes);
        }
    }
}
