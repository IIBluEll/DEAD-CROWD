using System;
using System.Collections.Generic;
using System.IO;
using DeadCrowd.Content;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
using UnityEngine;

namespace DeadCrowd.Editor
{
    public static class CrowdContentEditor
    {
        private static bool s_createdDefaultVisual;
        public const string ROOT = "Assets/Main/06_Content";
        private const string RESOURCE = ROOT + "/Resources/Crowd";

        [MenuItem("DEAD CROWD/Content/Create Missing Default Assets")]
        public static void CreateDefaults()
        {
            s_createdDefaultVisual = false;
            Directory.CreateDirectory(RESOURCE);
            Directory.CreateDirectory(ROOT + "/Animation");
            Directory.CreateDirectory(ROOT + "/Audio");
            AssetDatabase.Refresh();
            var settings = LoadOrCreate<CrowdGameSettings>(RESOURCE + "/GameSettings.asset");
            string mapPath = RESOURCE + "/MapProfile.asset";
            var map = AssetDatabase.LoadAssetAtPath<CrowdMapProfile>(mapPath);
            if (map == null)
            {
                map = ScriptableObject.CreateInstance<CrowdMapProfile>();
                Vector3 a = new Vector3(-13, 0, -8), b = new Vector3(13, 0, 8), c = new Vector3(-8, 0, 13), d = new Vector3(8, 0, -13);
                map.StartSets = new[] { new CrowdStartSet { Name = "Opposite West East", Positions = new[] { a, b } }, new CrowdStartSet { Name = "Opposite North South", Positions = new[] { c, d } }, new CrowdStartSet { Name = "Four Districts", Positions = new[] { a, b, c, d } } };
                AssetDatabase.CreateAsset(map, mapPath);
            }
            string profilePath = RESOURCE + "/PresentationProfile.asset";
            if (AssetDatabase.LoadAssetAtPath<CrowdPresentationProfile>(profilePath) == null)
            {
                var profile = ScriptableObject.CreateInstance<CrowdPresentationProfile>();
                profile.CommonVisualPrefab = CreateVisual();
                profile.Footstep = Tone("Footstep", 105f, 0.08f, true);
                profile.Melee = Tone("Melee", 330f, 0.18f, true);
                profile.Shot = Tone("Shot", 65f, 0.18f, true);
                profile.Reload = Tone("Reload", 850f, 0.2f, false);
                profile.Hit = Tone("Hit", 170f, 0.1f, true);
                profile.Death = Tone("Death", 90f, 0.3f, false);
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            settings.Validate(); map.Validate();
            if (s_createdDefaultVisual) UpgradeGeneratedVisual();
            if (!File.Exists(ROOT + "/Audio/LocalWarning.wav"))
            {
                var profile = AssetDatabase.LoadAssetAtPath<CrowdPresentationProfile>(profilePath);
                profile.LocalWarning = Tone("LocalWarning", 1100f, 0.12f, false); EditorUtility.SetDirty(profile);
            }
            string catalogPath = RESOURCE + "/ContentCatalog.asset";
            if (AssetDatabase.LoadAssetAtPath<CrowdContentCatalog>(catalogPath) == null)
            {
                var catalog = ScriptableObject.CreateInstance<CrowdContentCatalog>();
                catalog.GameSettings = settings; catalog.Map = map;
                catalog.Presentation = AssetDatabase.LoadAssetAtPath<CrowdPresentationProfile>(profilePath);
                AssetDatabase.CreateAsset(catalog, catalogPath);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Content] Default assets ready. Existing customized assets were preserved.");
        }

        private static void UpgradeGeneratedVisual()
        {
            // Only extend our generated default; a replacement user prefab/controller is never rewritten.
            string path = ROOT + "/CommonCharacter.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<CrowdActorPresentation>(path);
            if (prefab == null) return;
            var serialized = new SerializedObject(prefab);
            if (serialized.FindProperty("_meleeWeaponObj").objectReferenceValue == null)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                Transform hand = root.transform.Find("Hips/Torso/RightArm");
                if (hand != null)
                {
                    var club = Box("Melee Weapon", hand, new Vector3(0, -0.6f, 0.15f), new Vector3(0.08f, 0.65f, 0.08f), AssetDatabase.LoadAssetAtPath<Material>(ROOT + "/CommonCharacter.mat"), new List<Renderer>());
                    var edit = new SerializedObject(root.GetComponent<CrowdActorPresentation>());
                    edit.FindProperty("_meleeWeaponObj").objectReferenceValue = club; edit.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                PrefabUtility.UnloadPrefabContents(root);
            }
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ROOT + "/Animation/CommonCharacter.controller");
            if (controller == null || Array.Exists(controller.layers, x => x.name == "Hit Reaction")) return;
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            var machine = new AnimatorStateMachine { name = "Hit Reaction" }; AssetDatabase.AddObjectToAsset(machine, controller);
            controller.AddLayer(new AnimatorControllerLayer { name = "Hit Reaction", stateMachine = machine, defaultWeight = 1f, blendingMode = AnimatorLayerBlendingMode.Additive });
            string clipPath = ROOT + "/Animation/Hit.anim";
            var hitClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (hitClip == null)
            {
                hitClip = new AnimationClip { name = "Hit" };
                hitClip.SetCurve("Hips/Torso", typeof(Transform), "localEulerAnglesRaw.z", new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.06f, 12), new Keyframe(0.18f, 0)));
                AssetDatabase.CreateAsset(hitClip, clipPath);
            }
            var idle = machine.AddState("Empty"); machine.defaultState = idle;
            var hit = machine.AddState("Hit"); hit.motion = hitClip;
            var begin = machine.AddAnyStateTransition(hit); begin.hasExitTime = false; begin.duration = 0f; begin.AddCondition(AnimatorConditionMode.If, 0, "Hit");
            var end = hit.AddTransition(idle); end.hasExitTime = true; end.exitTime = 1f; end.duration = 0.05f;
            EditorUtility.SetDirty(controller);
        }

        [MenuItem("DEAD CROWD/Content/Install Map Editing Tools")]
        public static void InstallMapTools()
        {
            CreateDefaults();
            EditorSceneManager.OpenScene(ProductionSceneEditor.SCENE_PATH);
            var authoring = UnityEngine.Object.FindFirstObjectByType<CrowdMapAuthoring>();
            if (authoring == null) authoring = new GameObject("Map Editing Tools").AddComponent<CrowdMapAuthoring>();
            if (authoring.Catalog == null) authoring.Catalog = AssetDatabase.LoadAssetAtPath<CrowdContentCatalog>(RESOURCE + "/ContentCatalog.asset");
            EditorSceneManager.SaveScene(authoring.gameObject.scene);
            Selection.activeGameObject = authoring.gameObject;
        }

        [MenuItem("DEAD CROWD/Content/Rebuild Main Scene NavMesh")]
        public static void RebuildNavMesh()
        {
            EditorSceneManager.OpenScene(ProductionSceneEditor.SCENE_PATH);
            var surface = UnityEngine.Object.FindFirstObjectByType<NavMeshSurface>();
            if (surface == null) throw new InvalidOperationException("Keep a NavMeshSurface on the editable world root.");
            surface.BuildNavMesh();
            string path = ROOT + "/MainCityNavMesh.asset";
            var saved = AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(path);
            if (saved == null) AssetDatabase.CreateAsset(surface.navMeshData, path);
            else
            {
                var generated = surface.navMeshData;
                EditorUtility.CopySerialized(generated, saved); surface.RemoveData(); surface.navMeshData = saved; surface.AddData();
            }
            EditorUtility.SetDirty(surface);
            EditorSceneManager.SaveScene(surface.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Content] Main scene NavMesh rebuilt. Prototype navigation asset preserved.");
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
            return asset;
        }

        [MenuItem("DEAD CROWD/Content/Select Game Settings")]
        private static void SelectSettings() { CreateDefaults(); Selection.activeObject = CrowdContent.Catalog.GameSettings; }
        [MenuItem("DEAD CROWD/Content/Select Active Content Catalog")]
        private static void SelectCatalog() { CreateDefaults(); Selection.activeObject = AssetDatabase.LoadAssetAtPath<CrowdContentCatalog>(RESOURCE + "/ContentCatalog.asset"); }
        [MenuItem("DEAD CROWD/Content/Select Map Profile")]
        private static void SelectMap() { CreateDefaults(); Selection.activeObject = CrowdContent.Catalog.Map; }
        [MenuItem("DEAD CROWD/Content/Select Presentation Profile")]
        private static void SelectPresentation() { CreateDefaults(); Selection.activeObject = CrowdContent.Catalog.Presentation; }

        private static AudioClip Tone(string name, float frequency, float seconds, bool noise)
        {
            string path = ROOT + "/Audio/" + name + ".wav";
            if (!File.Exists(path))
            {
                int count = Mathf.CeilToInt(22050 * seconds);
                var random = new System.Random(name.Length * 31);
                using (var writer = new BinaryWriter(File.Create(path)))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                    writer.Write(22050); writer.Write(44100); writer.Write((short)2); writer.Write((short)16);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
                    for (int i = 0; i < count; i++)
                    {
                        float envelope = Mathf.Pow(1f - (float)i / count, 3f);
                        float sample = Mathf.Sin(2 * Mathf.PI * frequency * i / 22050f) * (noise ? 0.3f : 0.8f);
                        if (noise) sample += ((float)random.NextDouble() * 2f - 1f) * 0.55f;
                        writer.Write((short)(sample * envelope * 20000));
                    }
                }
                AssetDatabase.ImportAsset(path);
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static CrowdActorPresentation CreateVisual()
        {
            string path = ROOT + "/CommonCharacter.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<CrowdActorPresentation>(path);
            if (existing != null) return existing;
            string materialPath = ROOT + "/CommonCharacter.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, materialPath); }
            s_createdDefaultVisual = true;
            var root = new GameObject("Common Character");
            root.layer = 2;
            var renderers = new List<Renderer>();
            Transform hips = Joint("Hips", root.transform, new Vector3(0, 0.9f, 0));
            Transform torso = Joint("Torso", hips, new Vector3(0, 0.25f, 0));
            Box("Chest", torso, Vector3.zero, new Vector3(0.46f, 0.5f, 0.28f), material, renderers);
            Box("Head", torso, new Vector3(0, 0.48f, 0), new Vector3(0.35f, 0.38f, 0.33f), material, renderers);
            Box("Nose", torso, new Vector3(0, 0.48f, 0.2f), new Vector3(0.1f, 0.08f, 0.12f), material, renderers);
            Transform left = Joint("LeftArm", torso, new Vector3(-0.32f, 0.17f, 0));
            Transform right = Joint("RightArm", torso, new Vector3(0.32f, 0.17f, 0));
            foreach (Transform arm in new[] { left, right }) Box("Arm", arm, new Vector3(0, -0.24f, 0), new Vector3(0.14f, 0.5f, 0.17f), material, renderers);
            foreach (int side in new[] { -1, 1 })
            {
                Transform leg = Joint(side < 0 ? "LeftLeg" : "RightLeg", hips, new Vector3(side * 0.14f, 0, 0));
                Box("Leg", leg, new Vector3(0, -0.42f, 0), new Vector3(0.19f, 0.8f, 0.23f), material, renderers);
            }
            GameObject weapon = Box("Weapon", right, new Vector3(0, -0.46f, 0.12f), new Vector3(0.08f, 0.12f, 0.36f), material, new List<Renderer>());
            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = CreateAnimator();
            var view = root.AddComponent<CrowdActorPresentation>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_bodyAnimator").objectReferenceValue = animator;
            serialized.FindProperty("_weaponObj").objectReferenceValue = weapon;
            var list = serialized.FindProperty("_bodyRenderers"); list.arraySize = renderers.Count;
            for (int i = 0; i < renderers.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab.GetComponent<CrowdActorPresentation>();
        }

        private static Transform Joint(string name, Transform parent, Vector3 position)
        {
            Transform joint = new GameObject(name).transform; joint.SetParent(parent, false); joint.localPosition = position; return joint;
        }
        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material, List<Renderer> renderers)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name; obj.layer = 2;
            obj.transform.SetParent(parent, false); obj.transform.localPosition = position; obj.transform.localScale = size;
            UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
            obj.GetComponent<Renderer>().sharedMaterial = material; renderers.Add(obj.GetComponent<Renderer>()); return obj;
        }

        private static RuntimeAnimatorController CreateAnimator()
        {
            string path = ROOT + "/Animation/CommonCharacter.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller != null) return controller;
            controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Aim", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Action", AnimatorControllerParameterType.Int);
            var machine = controller.layers[0].stateMachine;
            var locomotion = machine.AddState("Locomotion");
            locomotion.motion = Blend(controller, false);
            var aim = machine.AddState("Aim"); aim.motion = Blend(controller, true);
            machine.defaultState = locomotion;
            foreach (var state in new[] { locomotion, aim })
            {
                var transition = machine.AddAnyStateTransition(state); transition.hasExitTime = false; transition.duration = 0.08f; transition.canTransitionToSelf = false;
                transition.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead"); transition.AddCondition(AnimatorConditionMode.Equals, 0, "Action");
                transition.AddCondition(state == aim ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "Aim");
            }
            string[] names = { "Melee", "Shot", "Reload", "Death" };
            for (int i = 0; i < names.Length; i++)
            {
                var state = machine.AddState(names[i]); state.motion = Clip(names[i], 0f, i != 0, i + 1);
                var transition = machine.AddAnyStateTransition(state); transition.hasExitTime = false; transition.duration = 0.05f; transition.canTransitionToSelf = false;
                transition.AddCondition(i == 3 ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "Dead");
                if (i < 3) transition.AddCondition(AnimatorConditionMode.Equals, i + 1, "Action");
            }
            return controller;
        }

        private static Motion Blend(AnimatorController controller, bool aim)
        {
            var tree = new BlendTree { name = aim ? "Aim Movement" : "Movement", blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(Clip(aim ? "AimIdle" : "Idle", 0f, aim, 0), 0f);
            tree.AddChild(Clip(aim ? "AimWalk" : "Walk", 25f, aim, 0), 2.5f);
            tree.AddChild(Clip(aim ? "AimRun" : "Run", 45f, aim, 0), 5f);
            return tree;
        }

        private static AnimationClip Clip(string name, float swing, bool aim, int action)
        {
            string path = ROOT + "/Animation/" + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null) return clip;
            clip = new AnimationClip { name = name, frameRate = 30f };
            float duration = action == 1 ? 0.85f : action == 2 ? 0.35f : action == 3 ? 1.8f : 1f;
            foreach (string joint in new[] { "Hips/LeftLeg", "Hips/RightLeg", "Hips/Torso/LeftArm", "Hips/Torso/RightArm" })
            {
                bool arm = joint.Contains("Arm"); float angle = joint.Contains("Left") ? swing : -swing;
                float baseline = arm && aim ? -75f : 0f;
                var curve = AnimationCurve.Linear(0f, baseline + angle, duration, baseline + angle);
                curve.AddKey(duration * 0.5f, baseline - angle);
                if (action == 1 && arm) curve = new AnimationCurve(new Keyframe(0, -80f), new Keyframe(0.3f, 65f), new Keyframe(duration, 0));
                if (action == 2 && arm) curve = new AnimationCurve(new Keyframe(0, -95f), new Keyframe(duration, -75f));
                if (action == 3 && arm) curve = new AnimationCurve(new Keyframe(0, -55f), new Keyframe(duration * 0.5f, -100f), new Keyframe(duration, -55f));
                clip.SetCurve(joint, typeof(Transform), "localEulerAnglesRaw.x", curve);
            }
            clip.SetCurve("Hips", typeof(Transform), "localEulerAnglesRaw.z", AnimationCurve.Linear(0, 0, duration, action == 4 ? 90f : 0f));
            clip.SetCurve("Hips", typeof(Transform), "localPosition.y", AnimationCurve.Linear(0, 0.9f, duration, action == 4 ? 0.25f : 0.9f));
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = action == 0; AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, path); return clip;
        }
    }
}
