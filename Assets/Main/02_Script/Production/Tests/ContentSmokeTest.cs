using System;
using System.Threading.Tasks;
using DeadCrowd.Content;
using DeadCrowd.Prototype;
using Fusion;
using UnityEngine;

namespace DeadCrowd.Tests
{
    public sealed class ContentSmokeTest : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--content-smoke") < 0) return;
            CrowdBootstrap runtime = FindFirstObjectByType<CrowdBootstrap>();
            if (runtime != null && runtime.GetComponent<ContentSmokeTest>() == null) runtime.gameObject.AddComponent<ContentSmokeTest>();
        }
        private async void Start()
        {
            string code = "CUSTAM";
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "--content-code") code = args[i + 1];
            try
            {
                var profile = CrowdContent.Catalog.Presentation;
                Require(profile != null && profile.CommonVisualPrefab != null, "missing common presentation");
                Require(profile.CommonVisualPrefab.GetComponentsInChildren<Collider>(true).Length == 0, "visual prefab changes collision");
                var animator = profile.CommonVisualPrefab.GetComponent<Animator>();
                Require(animator != null && !animator.applyRootMotion && animator.runtimeAnimatorController.animationClips.Length >= 10, "missing animation rig/clips");
                foreach (AudioClip clip in new[] { profile.Footstep, profile.Melee, profile.Shot, profile.Reload, profile.Hit, profile.Death }) Require(clip != null && clip.length > 0f, "missing sound");
                var source = CrowdContent.Catalog.GameSettings;
                var invalid = Instantiate(source); invalid.MAX_HP = 0;
                bool rejected = false; try { invalid.Validate(); } catch (InvalidOperationException) { rejected = true; }
                Destroy(invalid); Require(rejected, "invalid settings accepted");
                CrowdContent.Freeze(); float frozen = CrowdRules.WALK_SPEED;
                source.WALK_SPEED += 0.25f;
                Require(CrowdRules.WALK_SPEED == frozen, "live asset edit changed running snapshot");
                var runtime = GetComponent<CrowdBootstrap>();
                await runtime.Connect_async(GameMode.Client, code);
                Require(!runtime.Busy && runtime.Runner == null && runtime.Match == null, "mismatched settings joined the host");
                Require(!runtime.Status.StartsWith("Room not found") && !runtime.Status.StartsWith("Enter 6"), "compatibility test did not find the host room");
                Debug.Log("[ContentSmoke] PASS: assets, invalid settings, frozen snapshot, mismatched rules rejected before a slot was created.");
                Application.Quit(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Debug.Log("[ContentSmoke] FAIL " + exception.Message); Application.Quit(1); }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
#endif
    }
}
