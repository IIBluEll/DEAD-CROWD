using UnityEngine;

namespace DeadCrowd.Content
{
    [CreateAssetMenu(menuName = "DEAD CROWD/Presentation Profile")]
    public sealed class CrowdPresentationProfile : ScriptableObject
    {
        public CrowdActorPresentation CommonVisualPrefab;
        [Header("Sound effects")]
        public AudioClip Footstep, Melee, Shot, Reload, Hit, Death;
        public AudioClip LocalWarning;
        [Tooltip("Uses Footstep when left empty.")]
        public AudioClip SprintFootstep;
        public AudioClip SearchStart;
        [Tooltip("Only the player who earned the point hears this clip.")]
        public AudioClip SearchPoint;
        public bool PlayLocalWarnings = true;
        [Range(0f, 1f)] public float Volume = 0.5f;
        [Range(0f, 1f)] public float FootstepVolume = 1f;
        [Min(0.1f)] public float AudioRange = 22f;
        [Tooltip("Uses Audio Range when left at zero.")]
        [Min(0f)] public float ShotAudioRange;
        [Min(0.1f)] public float FootstepDistance = 1.2f;
        [Header("Animator parameter names")]
        public string SpeedParameter = "Speed", AimParameter = "Aim", ActionParameter = "Action", DeadParameter = "Dead", HitParameter = "Hit";
        [Header("Animator state names")]
        public string MeleeState = "Melee", ShotState = "Shot", ReloadState = "Reload";
        [Range(0f, 1f)] public float MeleeClipHitPhase = 0.3f / 0.85f;
        public Color BodyColor = new Color(0.38f, 0.49f, 0.39f);
        public bool UseMaterialColors;
        public Color HitColor = new Color(0.9f, 0.32f, 0.23f);
        [Min(0.01f)] public float HitFlashSeconds = 0.15f;

        public void Validate()
        {
            if (CommonVisualPrefab == null || !CommonVisualPrefab.Configured) throw new System.InvalidOperationException("Common visual: assign Animator/renderers and remove Collider/NetworkObject components.");
            if (string.IsNullOrWhiteSpace(SpeedParameter) || string.IsNullOrWhiteSpace(AimParameter) || string.IsNullOrWhiteSpace(DeadParameter) || string.IsNullOrWhiteSpace(ActionParameter) || string.IsNullOrWhiteSpace(MeleeState) || string.IsNullOrWhiteSpace(ShotState) || string.IsNullOrWhiteSpace(ReloadState)) throw new System.InvalidOperationException("Animator parameter/state names cannot be empty.");
            if (!float.IsFinite(Volume) || Volume < 0f || Volume > 1f || !float.IsFinite(AudioRange) || AudioRange <= 0f || !float.IsFinite(FootstepDistance) || FootstepDistance <= 0f || !float.IsFinite(HitFlashSeconds) || HitFlashSeconds < 0f || !float.IsFinite(MeleeClipHitPhase) || MeleeClipHitPhase < 0f || MeleeClipHitPhase > 1f) throw new System.InvalidOperationException("Invalid presentation volume, distance or timing.");
            if (!float.IsFinite(FootstepVolume) || FootstepVolume < 0f || FootstepVolume > 1f || !float.IsFinite(ShotAudioRange) || ShotAudioRange < 0f) throw new System.InvalidOperationException("Invalid footstep volume or gunshot range.");
        }
    }
}
