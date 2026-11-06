using DeadCrowd.Prototype;
using UnityEngine;

namespace DeadCrowd.Content
{
    public sealed class CrowdActorPresentation : MonoBehaviour
    {
        [SerializeField] private Animator _bodyAnimator;
        [SerializeField] private GameObject _weaponObj;
        [SerializeField] private GameObject _meleeWeaponObj;
        [SerializeField] private Renderer[] _bodyRenderers;
        [SerializeField] private SkinnedMeshRenderer[] _bodyVariants;
        [SerializeField] private bool _orientPistolToAim;
        private CrowdActor _actor;
        private Transform _pistolArmTrans;
        private Quaternion _pistolRestRotation;
        private CrowdMatch _match;
        private int _roundId;
        private CrowdPresentationProfile _profile;
        private AudioSource _audio;
        private AudioSource _warningAudio;
        private bool _wasExposed;
        private int _magazine;
        private Vector3 _previous;
        private float _distance;
        private float _flashUntil;
        private int _damage, _shot, _action;
        private bool _alive;
        private MaterialPropertyBlock _block;
        private SkinnedMeshRenderer _activeBodyVariant;
        public bool Configured => _bodyAnimator != null && _bodyAnimator.runtimeAnimatorController != null && _bodyRenderers != null &&
            GetComponentsInChildren<Collider>(true).Length == 0 && GetComponentsInChildren<Fusion.NetworkObject>(true).Length == 0;

        public void Initialize(CrowdActor actor, CrowdPresentationProfile profile)
        {
            _actor = actor; _profile = profile; _match = FindFirstObjectByType<CrowdMatch>(); _roundId = _match != null ? _match.RoundId : -1; _previous = actor.transform.position;
            if (_weaponObj != null && _weaponObj.name == "Pistol" && _weaponObj.transform.parent?.name == "RightArm") _pistolArmTrans = _weaponObj.transform.parent;
            if (_orientPistolToAim && _weaponObj != null) _pistolRestRotation = _weaponObj.transform.localRotation;
            _damage = actor.DamageSequence; _shot = actor.ShotSequence; _action = actor.ActionSequence; _alive = actor.Alive;
            _block = new MaterialPropertyBlock();
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 1f; _audio.rolloffMode = AudioRolloffMode.Linear;
            _audio.minDistance = 1f; _audio.maxDistance = profile.AudioRange;
            _bodyAnimator.applyRootMotion = false;
            _bodyAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _bodyAnimator.Rebind();
            _bodyAnimator.Update(0f);
            RequireParameter(profile.SpeedParameter, AnimatorControllerParameterType.Float);
            RequireParameter(profile.AimParameter, AnimatorControllerParameterType.Bool);
            RequireParameter(profile.DeadParameter, AnimatorControllerParameterType.Bool);
            RequireParameter(profile.ActionParameter, AnimatorControllerParameterType.Int);
            if (!string.IsNullOrWhiteSpace(profile.HitParameter)) RequireParameter(profile.HitParameter, AnimatorControllerParameterType.Trigger);
            foreach (string state in new[] { profile.MeleeState, profile.ShotState, profile.ReloadState })
                if (!_bodyAnimator.HasState(0, Animator.StringToHash(state)) && !_bodyAnimator.HasState(0, Animator.StringToHash(_bodyAnimator.GetLayerName(0) + "." + state))) throw new System.InvalidOperationException("Missing base-layer Animator state: " + state);
            _warningAudio = gameObject.AddComponent<AudioSource>(); _warningAudio.spatialBlend = 0f;
            _wasExposed = actor.Exposed; _magazine = actor.ConfirmedMagazine;
            SelectBodyVariant();
        }

        private void RequireParameter(string name, AnimatorControllerParameterType type)
        {
            foreach (AnimatorControllerParameter parameter in _bodyAnimator.parameters) if (parameter.name == name && parameter.type == type) return;
            throw new System.InvalidOperationException("Missing Animator parameter: " + name + " (" + type + ")");
        }

        public void ResetPresentation()
        {
            _roundId = _match != null ? _match.RoundId : -1;
            _distance = 0f; _flashUntil = 0f;
            _previous = _actor.transform.position;
            _alive = _actor.Alive; _wasExposed = _actor.Exposed; _magazine = _actor.ConfirmedMagazine;
            _damage = _actor.DamageSequence; _shot = _actor.ShotSequence; _action = _actor.ActionSequence;
            _bodyAnimator.Rebind(); _bodyAnimator.Update(0f);
            SelectBodyVariant();
        }

        private void SelectBodyVariant()
        {
            if (_bodyVariants == null || _bodyVariants.Length == 0) return;
            int seed = _match != null ? _match.Seed : 0;
            uint index = unchecked((uint)(_actor.StableId - 1 + seed)) % (uint)_bodyVariants.Length;
            _activeBodyVariant = _bodyVariants[index];
            foreach (SkinnedMeshRenderer variant in _bodyVariants)
                variant.gameObject.SetActive(variant == _activeBodyVariant);
        }

        private void Play(AudioClip clip) { if (clip != null) _audio.PlayOneShot(clip, _profile.Volume); }
        private void LateUpdate()
        {
            if (_actor == null || _actor.Object == null || !_actor.Object.IsValid) return;
            if (_match != null && _roundId != _match.RoundId) ResetPresentation();
            bool alive = _actor.Alive;
            float moved = Vector3.Distance(_actor.transform.position, _previous);
            _previous = _actor.transform.position;
            float speed = Time.deltaTime > 0f ? Mathf.Clamp(moved / Time.deltaTime, 0f, CrowdRules.SPRINT_SPEED) : 0f;
            _bodyAnimator.SetFloat(_profile.SpeedParameter, alive ? speed : 0f, 0.12f, Time.deltaTime);
            _bodyAnimator.SetBool(_profile.DeadParameter, !alive);
            _bodyAnimator.SetBool(_profile.AimParameter, _actor.Aiming);
            _bodyAnimator.SetInteger(_profile.ActionParameter, (int)_actor.ConfirmedAction);
            // NPCs have the same action presentation contract as players.
            if (_actor.IsNpc) _bodyAnimator.SetInteger(_profile.ActionParameter, (int)_actor.Action);
            ACTION_STATE action = _actor.IsNpc ? _actor.Action : _actor.ConfirmedAction;
            int endTick = _actor.IsNpc ? _actor.ActionEndTick : _actor.ConfirmedActionEndTick;
            float seconds = action == ACTION_STATE.MELEE ? (_actor.IsNpc ? CrowdRules.AI_CYCLE : CrowdRules.MELEE_CYCLE) : action == ACTION_STATE.SHOT ? CrowdRules.GUN_INTERVAL : CrowdRules.RELOAD_SECONDS;
            if (alive && action != ACTION_STATE.NONE && seconds > 0f)
            {
                // Sample the clip using the host timer, including late snapshots and customized action durations.
                float phase = Mathf.Clamp01(1f - (endTick - _actor.Runner.Tick.Raw) * _actor.Runner.DeltaTime / seconds);
                if (action == ACTION_STATE.MELEE)
                {
                    float windup = _actor.IsNpc ? CrowdRules.AI_WINDUP : CrowdRules.MELEE_WINDUP;
                    float hitPhase = Mathf.Clamp01(windup / seconds);
                    phase = hitPhase <= 0f ? Mathf.Lerp(_profile.MeleeClipHitPhase, 1f, phase) : phase <= hitPhase ? phase / hitPhase * _profile.MeleeClipHitPhase : Mathf.Lerp(_profile.MeleeClipHitPhase, 1f, (phase - hitPhase) / Mathf.Max(0.001f, 1f - hitPhase));
                }
                string state = action == ACTION_STATE.MELEE ? _profile.MeleeState : action == ACTION_STATE.SHOT ? _profile.ShotState : _profile.ReloadState;
                _bodyAnimator.Play(state, 0, phase);
                _bodyAnimator.Update(0f);
            }
            bool pistolPose = alive && (action == ACTION_STATE.SHOT || action == ACTION_STATE.NONE && _actor.Aiming);
            if (_orientPistolToAim && _weaponObj != null)
            {
                if (pistolPose) _weaponObj.transform.rotation = _actor.PistolRotation;
                else _weaponObj.transform.localRotation = _pistolRestRotation;
            }
            else if (pistolPose && _pistolArmTrans != null)
            {
                Quaternion weaponLocalRotation = _weaponObj.transform.localRotation;
                Quaternion weaponRotation = _actor.PistolRotation;
                if (action == ACTION_STATE.SHOT) weaponRotation *= _pistolArmTrans.localRotation * weaponLocalRotation;
                _pistolArmTrans.rotation = weaponRotation * Quaternion.Inverse(weaponLocalRotation);
            }
            if (_weaponObj != null) _weaponObj.SetActive(alive && (_actor.Aiming || _actor.Action == ACTION_STATE.SHOT || _actor.Action == ACTION_STATE.RELOAD));
            if (_meleeWeaponObj != null) _meleeWeaponObj.SetActive(alive && _actor.Action == ACTION_STATE.MELEE);
            if (alive && moved < 0.5f) { _distance += moved; if (_distance >= _profile.FootstepDistance) { _distance = 0f; Play(_profile.Footstep); } }
            if (_damage != _actor.DamageSequence) { _damage = _actor.DamageSequence; _flashUntil = Time.time + _profile.HitFlashSeconds; if (!string.IsNullOrWhiteSpace(_profile.HitParameter)) _bodyAnimator.SetTrigger(_profile.HitParameter); Play(_profile.Hit); }
            if (_shot != _actor.ShotSequence) { _shot = _actor.ShotSequence; Play(_profile.Shot); }
            if (_action != _actor.ActionSequence)
            {
                _action = _actor.ActionSequence;
                if (_actor.ActionEvent == ACTION_STATE.MELEE) Play(_profile.Melee);
                if (_actor.ActionEvent == ACTION_STATE.RELOAD) Play(_profile.Reload);
            }
            if (_alive && !alive) Play(_profile.Death);
            if (_actor.HasInputAuthority && _profile.PlayLocalWarnings && _profile.LocalWarning != null &&
                (!_wasExposed && _actor.Exposed || _magazine > 0 && _actor.ConfirmedMagazine == 0)) _warningAudio.PlayOneShot(_profile.LocalWarning, _profile.Volume);
            _wasExposed = _actor.Exposed; _magazine = _actor.ConfirmedMagazine;
            _alive = alive;
            bool flashing = Time.time < _flashUntil;
            _block.SetColor("_BaseColor", flashing ? _profile.HitColor : _profile.BodyColor);
            MaterialPropertyBlock block = _profile.UseMaterialColors && !flashing ? null : _block;
            if (_activeBodyVariant != null) _activeBodyVariant.SetPropertyBlock(block);
            else foreach (Renderer body in _bodyRenderers) if (body != null) body.SetPropertyBlock(block);
        }
    }
}
