using System;
using DeadCrowd.Content;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DeadCrowd.Editor
{
    public static class CrowdContentValidationEditor
    {
        [MenuItem("DEAD CROWD/Content/Validate Active Content")]
        public static void ValidateActive()
        {
            var catalog = CrowdContent.Catalog;
            if (catalog == null || catalog.GameSettings == null || catalog.Map == null || catalog.Presentation == null) throw new InvalidOperationException("Assign all ContentCatalog references.");
            catalog.GameSettings.Validate(); catalog.Map.Validate(); catalog.Presentation.Validate();
            ValidateAnimator(catalog.Presentation);
            Debug.Log("[Content] Active configuration and Animator bindings are valid. This is not a gameplay QA result.");
        }

        public static void ValidateAnimator(CrowdPresentationProfile profile)
        {
            profile.Validate();
            var serialized = new SerializedObject(profile.CommonVisualPrefab);
            var animator = (Animator)serialized.FindProperty("_bodyAnimator").objectReferenceValue;
            RuntimeAnimatorController runtime = animator.runtimeAnimatorController;
            while (runtime is AnimatorOverrideController overridden) runtime = overridden.runtimeAnimatorController;
            var controller = runtime as AnimatorController;
            if (controller == null || controller.layers.Length == 0) throw new InvalidOperationException("Use an AnimatorController or AnimatorOverrideController with a base layer.");
            Require(controller, profile.SpeedParameter, AnimatorControllerParameterType.Float);
            Require(controller, profile.AimParameter, AnimatorControllerParameterType.Bool);
            Require(controller, profile.DeadParameter, AnimatorControllerParameterType.Bool);
            Require(controller, profile.ActionParameter, AnimatorControllerParameterType.Int);
            if (!string.IsNullOrWhiteSpace(profile.HitParameter)) Require(controller, profile.HitParameter, AnimatorControllerParameterType.Trigger);
            foreach (string name in new[] { profile.MeleeState, profile.ShotState, profile.ReloadState })
                if (!Contains(controller.layers[0].stateMachine, controller.layers[0].name, name)) throw new InvalidOperationException("Missing base-layer state: " + name);
        }
        private static void Require(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            foreach (var parameter in controller.parameters) if (parameter.name == name && parameter.type == type) return;
            throw new InvalidOperationException("Missing parameter " + name + " of type " + type);
        }
        private static bool Contains(AnimatorStateMachine machine, string path, string name)
        {
            foreach (var child in machine.states) if (child.state.name == name || path + "." + child.state.name == name) return true;
            foreach (var child in machine.stateMachines) if (Contains(child.stateMachine, path + "." + child.stateMachine.name, name)) return true;
            return false;
        }
        public static void ShowError(Action validate)
        {
            try { validate(); }
            catch (Exception exception) { EditorGUILayout.HelpBox(exception.Message, MessageType.Error); }
        }
    }
    [CustomEditor(typeof(CrowdGameSettings))]
    public sealed class CrowdGameSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI() { DrawDefaultInspector(); CrowdContentValidationEditor.ShowError(((CrowdGameSettings)target).Validate); }
    }
    [CustomEditor(typeof(CrowdMapProfile))]
    public sealed class CrowdMapProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI() { DrawDefaultInspector(); CrowdContentValidationEditor.ShowError(((CrowdMapProfile)target).Validate); }
    }
    [CustomEditor(typeof(CrowdPresentationProfile))]
    public sealed class CrowdPresentationProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI() { DrawDefaultInspector(); CrowdContentValidationEditor.ShowError(() => CrowdContentValidationEditor.ValidateAnimator((CrowdPresentationProfile)target)); }
    }
}
