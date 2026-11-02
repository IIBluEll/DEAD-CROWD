using System.Collections.Generic;
using System.IO;
using DeadCrowd.Content;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadCrowd.Editor
{
    public static class CrowdPolishEditor
    {
        private const string ROOT = "Assets/Main/07_Polish";

        [MenuItem("DEAD CROWD/Content/Create Refined Content Variant")]
        public static void CreateVariant()
        {
            CrowdContentEditor.CreateDefaults();
            Directory.CreateDirectory(ROOT + "/Animation"); AssetDatabase.Refresh();
            Directory.CreateDirectory(ROOT + "/Audio");
            string profilePath = ROOT + "/RefinedPresentation.asset";
            var profile = AssetDatabase.LoadAssetAtPath<CrowdPresentationProfile>(profilePath);
            if (profile == null)
            {
                profile = Object.Instantiate(CrowdContent.Catalog.Presentation); profile.name = "RefinedPresentation";
                profile.CommonVisualPrefab = CreateCharacter(); profile.UseMaterialColors = true;
                profile.Volume = 0.4f; profile.AudioRange = 18f;
                profile.Footstep = Sound("Footstep", 0.16f, 0);
                profile.Melee = Sound("Swing", 0.3f, 1);
                profile.Shot = Sound("Pistol", 0.3f, 2);
                profile.Reload = Sound("Reload", 1.6f, 3);
                profile.Hit = Sound("Impact", 0.18f, 4);
                profile.Death = Sound("Collapse", 0.5f, 5);
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            if (AssetDatabase.GetAssetPath(CrowdContent.Catalog.Presentation) == CrowdContentEditor.ROOT + "/Resources/Crowd/PresentationProfile.asset")
            {
                CrowdContent.Catalog.Presentation = profile; EditorUtility.SetDirty(CrowdContent.Catalog);
            }
            if (AssetDatabase.GetAssetPath(CrowdContent.Catalog.Map) == CrowdContentEditor.ROOT + "/Resources/Crowd/MapProfile.asset")
            {
                string mapPath = ROOT + "/CityLayout.asset";
                var map = AssetDatabase.LoadAssetAtPath<CrowdMapProfile>(mapPath);
                if (map == null) { map = Object.Instantiate(CrowdContent.Catalog.Map); map.MapVersion = "city-layout-2"; AssetDatabase.CreateAsset(map, mapPath); }
                CrowdContent.Catalog.Map = map; EditorUtility.SetDirty(CrowdContent.Catalog);
            }
            AssetDatabase.SaveAssets();
        }

        private static AudioClip Sound(string name, float duration, int kind)
        {
            string path = ROOT + "/Audio/" + name + ".wav";
            if (!File.Exists(path))
            {
                const int RATE = 22050;
                int count = Mathf.CeilToInt(duration * RATE);
                var random = new System.Random(917 + kind); float low = 0f;
                using (var writer = new BinaryWriter(File.Create(path)))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                    writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(RATE); writer.Write(RATE * 2); writer.Write((short)2); writer.Write((short)16);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
                    for (int i = 0; i < count; i++)
                    {
                        float t = (float)i / RATE, u = t / duration;
                        float noise = (float)random.NextDouble() * 2f - 1f; low = Mathf.Lerp(low, noise, kind == 2 ? 0.45f : 0.12f);
                        float sample;
                        if (kind == 1) sample = (noise - low) * Mathf.Sin(Mathf.PI * u) * 0.5f;
                        else if (kind == 3)
                        {
                            float click = Mathf.Exp(-Mathf.Abs(t - 0.08f) * 130f) + Mathf.Exp(-Mathf.Abs(t - 0.72f) * 110f) + Mathf.Exp(-Mathf.Abs(t - 1.35f) * 120f);
                            sample = click * (noise * 0.5f + Mathf.Sin(t * 2f * Mathf.PI * 1300f) * 0.2f);
                        }
                        else
                        {
                            float hz = kind == 0 ? 95f : kind == 2 ? 65f : kind == 4 ? 75f : 50f;
                            float decay = Mathf.Exp(-u * (kind == 2 ? 9f : 6f));
                            sample = (low * (kind == 2 ? 0.9f : 0.6f) + Mathf.Sin(t * 2f * Mathf.PI * hz) * 0.35f) * decay;
                        }
                        sample *= Mathf.Clamp01(t * 2000f) * Mathf.Clamp01((duration - t) * 1000f);
                        writer.Write((short)(Mathf.Clamp(sample, -1f, 1f) * 24000));
                    }
                }
                AssetDatabase.ImportAsset(path);
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static Material Material(string name, Color color)
        {
            string path = ROOT + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.name = name;
                material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", 0.15f); AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }
        private static Transform Joint(string name, Transform parent, Vector3 position)
        {
            Transform t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = position; return t;
        }
        private static GameObject Shape(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material, List<Renderer> bodies = null, bool solid = false)
        {
            var obj = GameObject.CreatePrimitive(type); obj.name = name; obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position; obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) Object.DestroyImmediate(obj.GetComponent<Collider>());
            obj.layer = solid ? 0 : parent.gameObject.layer;
            bodies?.Add(obj.GetComponent<Renderer>()); return obj;
        }

        private static CrowdActorPresentation CreateCharacter()
        {
            string path = ROOT + "/CommonCitizen.prefab";
            var saved = AssetDatabase.LoadAssetAtPath<CrowdActorPresentation>(path);
            if (saved != null) return saved;
            Material shirt = Material("Slate Shirt", new Color(0.23f, 0.32f, 0.38f));
            Material pants = Material("Charcoal Trousers", new Color(0.13f, 0.17f, 0.2f));
            Material skin = Material("Warm Skin", new Color(0.63f, 0.46f, 0.34f));
            Material dark = Material("Dark Leather", new Color(0.075f, 0.065f, 0.055f));
            Material metal = Material("Weapon Steel", new Color(0.18f, 0.2f, 0.21f));
            var root = new GameObject("Common Citizen"); root.layer = 2;
            var bodies = new List<Renderer>();
            Transform hips = Joint("Hips", root.transform, new Vector3(0, 0.9f, 0));
            Transform torso = Joint("Torso", hips, new Vector3(0, 0.25f, 0));
            Shape("Shirt", PrimitiveType.Capsule, torso, Vector3.zero, new Vector3(0.46f, 0.33f, 0.28f), shirt, bodies);
            Shape("Neck", PrimitiveType.Cylinder, torso, new Vector3(0, 0.36f, 0), new Vector3(0.13f, 0.1f, 0.13f), skin, bodies);
            Transform head = Joint("Head", torso, new Vector3(0, 0.48f, 0));
            Shape("Face", PrimitiveType.Sphere, head, Vector3.zero, new Vector3(0.33f, 0.38f, 0.31f), skin, bodies);
            Shape("Hair", PrimitiveType.Sphere, head, new Vector3(0, 0.13f, -0.01f), new Vector3(0.35f, 0.17f, 0.32f), dark, bodies);
            Shape("Nose", PrimitiveType.Sphere, head, new Vector3(0, 0, 0.16f), Vector3.one * 0.065f, skin, bodies);
            foreach (float side in new[] { -1f, 1f })
            {
                Shape("Eye", PrimitiveType.Sphere, head, new Vector3(side * 0.067f, 0.045f, 0.142f), Vector3.one * 0.027f, dark);
                Transform arm = Joint(side < 0 ? "LeftArm" : "RightArm", torso, new Vector3(side * 0.3f, 0.17f, 0));
                Shape("Sleeve", PrimitiveType.Capsule, arm, new Vector3(0, -0.2f, 0), new Vector3(0.14f, 0.24f, 0.16f), shirt, bodies);
                Shape("Hand", PrimitiveType.Sphere, arm, new Vector3(0, -0.47f, 0), new Vector3(0.13f, 0.16f, 0.14f), skin, bodies);
                Transform leg = Joint(side < 0 ? "LeftLeg" : "RightLeg", hips, new Vector3(side * 0.14f, 0, 0));
                Shape("Trouser", PrimitiveType.Capsule, leg, new Vector3(0, -0.34f, 0), new Vector3(0.2f, 0.37f, 0.24f), pants, bodies);
                Shape("Shoe", PrimitiveType.Cube, leg, new Vector3(0, -0.77f, 0.055f), new Vector3(0.2f, 0.15f, 0.33f), dark, bodies);
            }
            Transform handRoot = root.transform.Find("Hips/Torso/RightArm");
            GameObject pistol = Joint("Pistol", handRoot, new Vector3(0, -0.46f, 0.06f)).gameObject;
            pistol.transform.localRotation = Quaternion.Euler(75f, 0f, 0f);
            Shape("Slide", PrimitiveType.Cube, pistol.transform, new Vector3(0, 0.02f, 0.1f), new Vector3(0.075f, 0.09f, 0.3f), metal);
            Shape("Grip", PrimitiveType.Cube, pistol.transform, new Vector3(0, -0.075f, 0.025f), new Vector3(0.065f, 0.15f, 0.09f), dark);
            GameObject melee = Shape("Melee Baton", PrimitiveType.Cylinder, handRoot, new Vector3(0, -0.59f, 0.1f), new Vector3(0.055f, 0.32f, 0.055f), metal);
            Animator animator = root.AddComponent<Animator>(); animator.applyRootMotion = false; animator.runtimeAnimatorController = CreateMotionVariant();
            var view = root.AddComponent<CrowdActorPresentation>(); var edit = new SerializedObject(view);
            edit.FindProperty("_bodyAnimator").objectReferenceValue = animator;
            edit.FindProperty("_weaponObj").objectReferenceValue = pistol;
            edit.FindProperty("_meleeWeaponObj").objectReferenceValue = melee;
            var list = edit.FindProperty("_bodyRenderers"); list.arraySize = bodies.Count;
            for (int i = 0; i < bodies.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = bodies[i];
            edit.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
            return prefab.GetComponent<CrowdActorPresentation>();
        }

        private static RuntimeAnimatorController CreateMotionVariant()
        {
            string path = ROOT + "/Animation/Citizen.overrideController";
            var saved = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            if (saved != null) return saved;
            var source = AssetDatabase.LoadAssetAtPath<AnimatorController>(CrowdContentEditor.ROOT + "/Animation/CommonCharacter.controller");
            var controller = new AnimatorOverrideController(source);
            var replacements = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            foreach (AnimationClip original in source.animationClips)
            {
                string clipPath = ROOT + "/Animation/" + original.name + ".anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                {
                    clip = Object.Instantiate(original); clip.name = original.name;
                    if (original.name == "Melee") ApplyMeleePose(clip);
                    bool walk = original.name.Contains("Walk"), run = original.name.Contains("Run"), idle = original.name.Contains("Idle");
                    if (walk || run || idle)
                    {
                        float duration = clip.length; float swing = run ? 38f : walk ? 22f : 1f;
                        foreach (string side in new[] { "Left", "Right" })
                        {
                            float angle = side == "Left" ? -swing : swing;
                            float baseline = original.name.StartsWith("Aim") ? -75f : 0f;
                            clip.SetCurve("Hips/Torso/" + side + "Arm", typeof(Transform), "localEulerAnglesRaw.x", new AnimationCurve(new Keyframe(0, baseline + angle), new Keyframe(duration * 0.5f, baseline - angle), new Keyframe(duration, baseline + angle)));
                        }
                        clip.SetCurve("Hips/Torso", typeof(Transform), "localPosition.y", new AnimationCurve(new Keyframe(0, 0.25f), new Keyframe(duration * 0.5f, 0.25f + (run ? 0.015f : 0.005f)), new Keyframe(duration, 0.25f)));
                    }
                    AssetDatabase.CreateAsset(clip, clipPath);
                }
                replacements.Add(new KeyValuePair<AnimationClip, AnimationClip>(original, clip));
            }
            controller.ApplyOverrides(replacements); AssetDatabase.CreateAsset(controller, path); return controller;
        }

        [MenuItem("DEAD CROWD/Content/Add City Detail Layer")]
        public static void DressCity()
        {
            CreateVariant(); EditorSceneManager.OpenScene(ProductionSceneEditor.SCENE_PATH);
            Transform world = GameObject.Find("City Blockout")?.transform;
            if (world == null || world.Find("City Details") != null) return;
            Transform details = Joint("City Details", world, Vector3.zero);
            Material asphalt = Material("Road", new Color(0.115f, 0.14f, 0.16f));
            Material stone = Material("Stone", new Color(0.43f, 0.43f, 0.38f));
            Material facade = Material("Facade", new Color(0.47f, 0.43f, 0.35f));
            Material roof = Material("Roof", new Color(0.17f, 0.22f, 0.24f));
            Material iron = Material("Street Iron", new Color(0.12f, 0.14f, 0.16f));
            Material cream = Material("Sign", new Color(0.72f, 0.69f, 0.53f));
            foreach (Renderer renderer in world.GetComponentsInChildren<Renderer>())
            {
                if (renderer.name.StartsWith("Ground")) renderer.sharedMaterial = asphalt;
                else if (renderer.name.StartsWith("Building")) renderer.sharedMaterial = facade;
                else if (renderer.name == "Roof") renderer.sharedMaterial = roof;
                else if (renderer.name == "Central Plaza") renderer.sharedMaterial = stone;
            }
            int index = 0; string[] names = { "MARKET", "WORKSHOP", "CLINIC", "CAFE" };
            foreach (int x in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
            {
                Transform lamp = Joint("Street Lamp", details, new Vector3(x * 3.3f, 0, z * 3.3f));
                Shape("Pole", PrimitiveType.Cylinder, lamp, new Vector3(0, 1.4f, 0), new Vector3(0.08f, 1.4f, 0.08f), iron, solid: true);
                Shape("Lantern", PrimitiveType.Cube, lamp, new Vector3(0, 2.85f, 0), new Vector3(0.25f, 0.25f, 0.25f), cream, solid: true);
                var sign = Shape("Shop Sign", PrimitiveType.Cube, details, new Vector3(x * 8f, 2.65f, z * 4.47f), new Vector3(2.8f, 0.65f, 0.07f), iron, solid: true);
                var textObj = new GameObject("Shop Label"); textObj.transform.SetParent(details, false);
                textObj.transform.localPosition = new Vector3(x * 8f, 2.65f, z * 4.42f); textObj.transform.localRotation = Quaternion.Euler(0, z > 0 ? 0 : 180, 0);
                var text = textObj.AddComponent<TextMeshPro>(); text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ProductionSceneEditor.FONT_PATH);
                text.text = names[index++]; text.fontSize = 2.4f; text.alignment = TextAlignmentOptions.Center; text.color = new Color(0.85f, 0.8f, 0.6f); text.rectTransform.sizeDelta = new Vector2(2.65f, 0.55f);
            }
            EditorSceneManager.SaveScene(world.gameObject.scene); AssetDatabase.SaveAssets();
        }

        public static void PrepareAndBuild()
        {
            DressCity(); CrowdContentEditor.RebuildNavMesh(); CrowdContentValidationEditor.ValidateActive();
            ProductionSceneEditor.VerifyAndBuild();
        }

        private static void ApplyMeleePose(AnimationClip clip)
        {
            clip.SetCurve("Hips/Torso/RightArm", typeof(Transform), "localEulerAnglesRaw.x", new AnimationCurve(new Keyframe(0, 45), new Keyframe(0.3f, -90), new Keyframe(0.85f, 0)));
            clip.SetCurve("Hips/Torso/LeftArm", typeof(Transform), "localEulerAnglesRaw.x", new AnimationCurve(new Keyframe(0, -25), new Keyframe(0.3f, -50), new Keyframe(0.85f, 0)));
            clip.SetCurve("Hips/Torso", typeof(Transform), "localEulerAnglesRaw.x", new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.3f, 10), new Keyframe(0.85f, 0)));
        }

        // One-time finishing pass for the newly generated variant, not called by normal content/build menus.
        public static void FinishVariantAndBuild()
        {
            string path = ROOT + "/CommonCitizen.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var pistol = root.transform.Find("Hips/Torso/RightArm/Pistol");
            if (pistol != null) pistol.localRotation = Quaternion.Euler(75f, 0f, 0f);
            PrefabUtility.SaveAsPrefabAsset(root, path); PrefabUtility.UnloadPrefabContents(root);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ROOT + "/Animation/Melee.anim");
            if (clip != null) { ApplyMeleePose(clip); EditorUtility.SetDirty(clip); }
            AssetDatabase.SaveAssets();
            ProductionSceneEditor.VerifyAndBuild();
        }
    }
}
