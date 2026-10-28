using System;
using System.Collections.Generic;
using System.IO;
using Fusion;
using Fusion.Editor;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DeadCrowd.Prototype.Editor
{
    public static class CrowdPrototypeEditor
    {
        public const string SCENE_PATH = "Assets/Main/01_Scene/DeadCrowdPrototype.unity";
        private const string PREFAB_PATH = "Assets/Main/03_Prefab/Prototype/CrowdActor.prefab";
        private const string MATERIAL_PATH = "Assets/Main/04_Material/Prototype";
        private const string NAV_PATH = "Assets/Main/01_Scene/DeadCrowdPrototypeNavMesh.asset";

        [MenuItem("DEAD CROWD/Prototype/Create or Open Prototype")]
        public static void CreatePrototype()
        {
            if (File.Exists(SCENE_PATH))
            {
                Scene existing = EditorSceneManager.OpenScene(SCENE_PATH);
                NetworkObjectPostprocessor.BakeScene(existing);
                EditorSceneManager.SaveScene(existing);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(PREFAB_PATH));
            Directory.CreateDirectory(MATERIAL_PATH);
            AssetDatabase.Refresh();
            Material ground = Material("Asphalt", new Color(0.12f, 0.15f, 0.17f));
            Material pavement = Material("Pavement", new Color(0.29f, 0.33f, 0.32f));
            Material walls = Material("Buildings", new Color(0.34f, 0.39f, 0.4f));
            Material roof = Material("Roofs", new Color(0.2f, 0.25f, 0.26f));
            Material trim = Material("Trim", new Color(0.7f, 0.67f, 0.48f));
            Material crowd = Material("Crowd", new Color(0.38f, 0.49f, 0.39f));
            Material weapon = Material("Weapon", new Color(0.18f, 0.2f, 0.21f));
            Material trail = Material("ShotTrail", new Color(1f, 0.82f, 0.35f));
            NetworkObject actorPrefab = CreateActor(crowd, weapon, trim, trail);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.6f, 0.65f, 0.68f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.2f, 0.25f, 0.28f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 25f;
            RenderSettings.fogEndDistance = 65f;
            var lightObj = new GameObject("Sun");
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.shadows = LightShadows.Soft;
            lightObj.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            var worldObj = new GameObject("City Blockout");
            Cube("Ground 40m", new Vector3(0f, -0.25f, 0f), new Vector3(40f, 0.5f, 40f), ground, worldObj.transform);
            Cube("North Boundary", new Vector3(0f, 2f, 20.5f), new Vector3(42f, 4f, 1f), walls, worldObj.transform);
            Cube("South Boundary", new Vector3(0f, 2f, -20.5f), new Vector3(42f, 4f, 1f), walls, worldObj.transform);
            Cube("West Boundary", new Vector3(-20.5f, 2f, 0f), new Vector3(1f, 4f, 40f), walls, worldObj.transform);
            Cube("East Boundary", new Vector3(20.5f, 2f, 0f), new Vector3(1f, 4f, 40f), walls, worldObj.transform);
            Cube("Central Plaza", new Vector3(0f, 0.006f, 0f), new Vector3(8f, 0.01f, 8f), pavement, worldObj.transform, false);
            for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 center = new Vector3(x * 8f, 2.5f, z * 8f);
                GameObject building = Cube($"Building {x} {z}", center, new Vector3(7f, 5f, 7f), walls, worldObj.transform);
                Cube("Roof", center + Vector3.up * 2.6f, new Vector3(7.3f, 0.2f, 7.3f), roof, worldObj.transform, false);
                for (int window = -1; window <= 1; window++)
                {
                    Cube("Window", center + new Vector3(window * 1.8f, 0.8f, -3.51f), new Vector3(0.8f, 1.2f, 0.02f), trim, worldObj.transform, false);
                    Cube("Window", center + new Vector3(window * 1.8f, 0.8f, 3.51f), new Vector3(0.8f, 1.2f, 0.02f), trim, worldObj.transform, false);
                }
            }
            for (int i = -3; i <= 3; i++)
            {
                Cube("Road Marking", new Vector3(i * 5f, 0.012f, 0f), new Vector3(2f, 0.01f, 0.12f), trim, worldObj.transform, false);
                Cube("Road Marking", new Vector3(0f, 0.012f, i * 5f), new Vector3(0.12f, 0.01f, 2f), trim, worldObj.transform, false);
            }
            var surface = worldObj.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = CrowdRules.WORLD_MASK;
            surface.BuildNavMesh();
            AssetDatabase.CreateAsset(surface.navMeshData, NAV_PATH);

            var spawnRoot = new GameObject("Approved Start Positions");
            Vector3[] positions = { new Vector3(-13f, 0f, -8f), new Vector3(13f, 0f, 8f), new Vector3(-8f, 0f, 13f), new Vector3(8f, 0f, -13f) };
            var starts = new Transform[positions.Length];
            for (int i = 0; i < starts.Length; i++)
            {
                starts[i] = new GameObject($"Start {i + 1}").transform;
                starts[i].SetParent(spawnRoot.transform);
                starts[i].position = positions[i];
                starts[i].rotation = Quaternion.LookRotation(-positions[i]);
            }
            var matchObj = new GameObject("Authoritative Match");
            matchObj.AddComponent<NetworkObject>();
            CrowdMatch match = matchObj.AddComponent<CrowdMatch>();
            match.Configure(actorPrefab, starts);

            var bootstrapObj = new GameObject("Prototype Session and UI");
            CrowdBootstrap bootstrap = bootstrapObj.AddComponent<CrowdBootstrap>();
            var cameraObj = new GameObject("Player Camera");
            cameraObj.tag = "MainCamera";
            cameraObj.transform.SetParent(bootstrapObj.transform);
            cameraObj.transform.SetPositionAndRotation(new Vector3(0f, 24f, -22f), Quaternion.Euler(48f, 0f, 0f));
            Camera camera = cameraObj.AddComponent<Camera>();
            camera.fieldOfView = 70f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RenderSettings.fogColor;
            cameraObj.AddComponent<AudioListener>();
            bootstrap.Configure(camera);
            bootstrapObj.AddComponent<CrowdSmokeTest>();

            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            // Scene GUIDs exist only after the first save. Bake again so Fusion assigns a nonzero SortKey.
            NetworkObjectPostprocessor.BakeScene(scene);
            EditorSceneManager.SaveScene(scene);
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(SCENE_PATH, true) };
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes) if (existing.path != SCENE_PATH) scenes.Add(existing);
            EditorBuildSettings.scenes = scenes.ToArray();
            NetworkProjectConfigUtilities.RebuildPrefabTable();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Crowd] Editable prototype scene, shared actor prefab and NavMesh created.");
        }

        private static NetworkObject CreateActor(Material crowd, Material weapon, Material trim, Material trail)
        {
            var root = new GameObject("CrowdActor");
            root.layer = 2; // Explicit combat queries include this layer; camera and NPC sight ignore bodies.
            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.3f;
            controller.center = Vector3.up * 0.9f;
            controller.skinWidth = 0.02f;
            controller.stepOffset = 0.1f;
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();
            CrowdActor actor = root.AddComponent<CrowdActor>();
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Common Body";
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = Vector3.up * 0.9f;
            body.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
            Renderer bodyRenderer = body.GetComponent<Renderer>();
            bodyRenderer.sharedMaterial = crowd;
            GameObject face = Cube("Common Facing Mark", Vector3.zero, Vector3.one, trim, body.transform, false);
            face.transform.localPosition = new Vector3(0f, 0.35f, 0.48f);
            face.transform.localScale = new Vector3(0.5f, 0.14f, 0.08f);
            GameObject weaponObj = Cube("Action Weapon", Vector3.zero, Vector3.one, weapon, root.transform, false);
            weaponObj.transform.localPosition = new Vector3(0.28f, 1.2f, 0.35f);
            weaponObj.transform.localScale = new Vector3(0.1f, 0.12f, 0.5f);
            weaponObj.SetActive(false);
            LineRenderer line = root.AddComponent<LineRenderer>();
            line.sharedMaterial = trail;
            line.positionCount = 2;
            line.startWidth = 0.025f;
            line.endWidth = 0.01f;
            line.useWorldSpace = true;
            line.enabled = false;
            var serialized = new SerializedObject(actor);
            serialized.FindProperty("_bodyTrans").objectReferenceValue = body.transform;
            serialized.FindProperty("_weaponObj").objectReferenceValue = weaponObj;
            serialized.FindProperty("_bodyRenderer").objectReferenceValue = bodyRenderer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab.GetComponent<NetworkObject>();
        }

        private static GameObject Cube(string name, Vector3 position, Vector3 scale, Material material, Transform parent, bool collision = true)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.position = position;
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
            return obj;
        }

        private static Material Material(string name, Color color)
        {
            string path = $"{MATERIAL_PATH}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            material.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        [MenuItem("DEAD CROWD/Prototype/Verify Rules and Scene")]
        public static void Verify()
        {
            int checks = 0;
            void Check(bool condition, string label) { if (!condition) throw new Exception("[Crowd] Verification failed: " + label); checks++; }
            Check(CrowdRules.Ticks(0.3f, 1f / 60f) == 18, "melee windup 18 ticks");
            Check(CrowdRules.Ticks(0.85f, 1f / 60f) == 51, "melee cycle 51 ticks");
            Check(CrowdRules.WithinCone(Vector3.forward, Quaternion.Euler(0f, 45f, 0f) * Vector3.forward, 45f), "front angle boundary included");
            Check(!CrowdRules.WithinCone(Vector3.forward, Quaternion.Euler(0f, 45.1f, 0f) * Vector3.forward, 45f), "outside front angle excluded");
            Check(CrowdRules.IsBackstab(Vector3.forward, Quaternion.Euler(0f, 130f, 0f) * Vector3.forward), "130 degree back boundary");
            Check(!CrowdRules.IsBackstab(Vector3.forward, Quaternion.Euler(0f, 129.9f, 0f) * Vector3.forward), "129.9 degrees normal");
            float exposure = 0f;
            bool exposed = false;
            for (int i = 0; i < 3; i++) CrowdRules.UpdateExposure(ref exposure, ref exposed, true, false, 35f, 1f / 60f);
            Check(exposure == 100f && exposed, "three uninterrupted melee starts expose");
            CrowdRules.UpdateExposure(ref exposure, ref exposed, false, true, 0f, 1f / 60f);
            Check(exposure < 100f && exposed, "exposed state survives decrease below 100");
            for (int i = 0; i < 500; i++) CrowdRules.UpdateExposure(ref exposure, ref exposed, false, true, 0f, 1f / 60f);
            Check(exposure == 0f && !exposed, "zero clears exposure");
            exposure = 50f;
            CrowdRules.UpdateExposure(ref exposure, ref exposed, true, true, 35f, 1f);
            Check(exposure == 85f, "danger wins over simultaneous decay");
            Check(CrowdRules.ExposureDisplay(99.5f) == 99 && CrowdRules.ExposureDisplay(0.2f) == 1 && CrowdRules.ExposureDisplay(0f) == 0, "HUD boundaries");
            Check(CrowdRules.ReloadAmount(2, 3) == 3, "partial reserve reload");
            Check(CrowdRules.ReloadAmount(6, 12) == 0, "full magazine");
            Check(CrowdRules.ResolveResult(0, -1, false) == -1, "mutual elimination draw");
            Check(CrowdRules.ResolveResult(1, 3, true) == 3, "last survivor wins at time boundary");
            Check(CrowdRules.ResolveResult(2, 3, true) == -2, "timeout draw");
            Check(CrowdRules.ResolveResult(2, 3, false) == -3, "ongoing round");
            EditorSceneManager.OpenScene(SCENE_PATH);
            Physics.SyncTransforms();
            CrowdMatch match = UnityEngine.Object.FindFirstObjectByType<CrowdMatch>();
            Check(match != null && match.GetComponent<NetworkObject>() != null, "network match scene object");
            Check(match.GetComponent<NetworkObject>().SortKey != 0, "Fusion scene object is baked and discoverable");
            Check(AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH).GetComponent<CrowdActor>() != null, "actor prefab");
            var spawnRoot = GameObject.Find("Approved Start Positions").transform;
            for (int i = 0; i < spawnRoot.childCount; i++)
            {
                Vector3 position = spawnRoot.GetChild(i).position;
                Check(NavMesh.SamplePosition(position, out _, 0.2f, NavMesh.AllAreas), "start on NavMesh " + i);
                for (int j = i + 1; j < spawnRoot.childCount; j++)
                {
                    Vector3 other = spawnRoot.GetChild(j).position;
                    Check(Vector3.Distance(position, other) >= 10f, "minimum start distance");
                    Check(Physics.Linecast(position + Vector3.up * 0.9f, other + Vector3.up * 0.9f, CrowdRules.WORLD_MASK), "no direct start visibility");
                }
            }
            Debug.Log($"[Crowd] PASS: {checks} rule and scene checks. These do not prove multiplayer gameplay.");
        }

        [MenuItem("DEAD CROWD/Prototype/Build Windows Prototype")]
        public static void VerifyAndBuild()
        {
            CreatePrototype();
            Verify();
            Directory.CreateDirectory("Builds/Prototype");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { SCENE_PATH },
                locationPathName = "Builds/Prototype/DEAD-CROWD.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("[Crowd] Windows build failed: " + report.summary.result);
            Debug.Log($"[Crowd] Windows prototype build succeeded. bytes={report.summary.totalSize}");
        }
    }
}
