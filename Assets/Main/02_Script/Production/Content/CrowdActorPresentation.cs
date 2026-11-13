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
        private AudioSource _shotAudio;
        private AudioSource _localAudio;
        private bool _wasExposed;
        private int _magazine;
        private Vector3 _previous;
        private float _distance;
        private float _flashUntil;
        private int _damage, _shot, _action;
        private int _searchSite, _searchScore;
        private bool _alive;
        private bool _rummaging;
        private bool _hasRummageState;
        private static readonly int RUMMAGE_STATE = Animator.StringToHash("Base Layer.Rummage");
        private static readonly int LOCOMOTION_STATE = Animator.StringToHash("Base Layer.Locomotion");
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
            _audio = CreateAudioSource(profile.AudioRange);
            _bodyAnimator.applyRootMotion = false;
            _bodyAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _bodyAnimator.Rebind();
            _bodyAnimator.Update(0f);
            _hasRummageState = _bodyAnimator.HasState(0, RUMMAGE_STATE);
            RequireParameter(profile.SpeedParameter, AnimatorControllerParameterType.Float);
            RequireParameter(profile.AimParameter, AnimatorControllerParameterType.Bool);
            RequireParameter(profile.DeadParameter, AnimatorControllerParameterType.Bool);
            RequireParameter(profile.ActionParameter, AnimatorControllerParameterType.Int);
            if (!string.IsNullOrWhiteSpace(profile.HitParameter)) RequireParameter(profile.HitParameter, AnimatorControllerParameterType.Trigger);
            foreach (string state in new[] { profile.MeleeState, profile.ShotState, profile.ReloadState })
                if (!_bodyAnimator.HasState(0, Animator.StringToHash(state)) && !_bodyAnimator.HasState(0, Animator.StringToHash(_bodyAnimator.GetLayerName(0) + "." + state))) throw new System.InvalidOperationException("Missing base-layer Animator state: " + state);
            _wasExposed = actor.Exposed; _magazine = actor.ConfirmedMagazine;
            _searchSite = actor.SearchSiteId; _searchScore = actor.SearchScore;
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
            _searchSite = _actor.SearchSiteId; _searchScore = _actor.SearchScore;
            _audio.Stop();
            if (_shotAudio != null) _shotAudio.Stop();
            if (_localAudio != null) _localAudio.Stop();
            _rummaging = false;
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

        private AudioSource CreateAudioSource(float range)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.dopplerLevel = 0f;
            source.spatialBlend = range > 0f ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = Mathf.Min(1f, range);
            source.maxDistance = Mathf.Max(1f, range);
            return source;
        }

        private void Play(AudioClip clip, float gain = 1f)
        {
            if (clip != null) _audio.PlayOneShot(clip, _profile.Volume * gain);
        }

        private void PlayShot()
        {
            if (_profile.Shot == null) return;
            if (_profile.ShotAudioRange <= 0f) { Play(_profile.Shot); return; }
            if (_shotAudio == null)
            {
                _shotAudio = CreateAudioSource(_profile.ShotAudioRange);
                _shotAudio.priority = 64;
            }
            _shotAudio.PlayOneShot(_profile.Shot, _profile.Volume);
        }

        private void PlayLocal(AudioClip clip)
        {
            if (clip == null) return;
            if (_localAudio == null)
            {
                _localAudio = CreateAudioSource(0f);
                _localAudio.priority = 32;
            }
            _localAudio.PlayOneShot(clip, _profile.Volume);
        }

        private void UpdateFootsteps(bool alive, Vector3 displacement)
        {
            if (!alive || _match == null || _match.State != ROUND_STATE.PLAYING || displacement.sqrMagnitude >= 0.25f)
            {
                _distance = 0f;
                return;
            }

            float moved = CrowdRules.Flat(displacement).magnitude;
            _distance += moved;
            if (_distance < _profile.FootstepDistance) return;
            _distance -= _profile.FootstepDistance;
            bool sprinting = moved > (CrowdRules.WALK_SPEED + CrowdRules.SPRINT_SPEED) * 0.5f * Time.deltaTime;
            AudioClip clip = sprinting && _profile.SprintFootstep != null ? _profile.SprintFootstep : _profile.Footstep;
            Play(clip, _profile.FootstepVolume);
        }

        private void UpdateSearchSounds()
        {
            int searchSite = _actor.SearchSiteId;
            int searchScore = _actor.SearchScore;
            if (searchSite != 0 && searchSite != _searchSite) Play(_profile.SearchStart);
            if (_actor.HasInputAuthority && searchScore > _searchScore) PlayLocal(_profile.SearchPoint);
            _searchSite = searchSite;
            _searchScore = searchScore;
        }

        private void LateUpdate()
        {
            if (_actor == null || _actor.Object == null || !_actor.Object.IsValid) return;
            if (_match != null && _roundId != _match.RoundId) ResetPresentation();
            bool alive = _actor.Alive;
            Vector3 displacement = _actor.transform.position - _previous;
            float moved = displacement.magnitude;
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
            bool rummaging = _hasRummageState && alive && _actor.SearchSiteId != 0 && action == ACTION_STATE.NONE;
            if (rummaging)
            {
                float phase = Mathf.Clamp01(1f - (_actor.SearchEndTick - _actor.Runner.Tick.Raw) * _actor.Runner.DeltaTime / CrowdMatch.SEARCH_SECONDS);
                _bodyAnimator.Play(RUMMAGE_STATE, 0, phase);
                _bodyAnimator.Update(0f);
            }
            else if (_rummaging && alive && action == ACTION_STATE.NONE) _bodyAnimator.CrossFadeInFixedTime(LOCOMOTION_STATE, 0.12f, 0);
            _rummaging = rummaging;
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
            UpdateFootsteps(alive, displacement);
            UpdateSearchSounds();
            if (_damage != _actor.DamageSequence) { _damage = _actor.DamageSequence; _flashUntil = Time.time + _profile.HitFlashSeconds; if (!string.IsNullOrWhiteSpace(_profile.HitParameter)) _bodyAnimator.SetTrigger(_profile.HitParameter); Play(_profile.Hit); }
            if (_shot != _actor.ShotSequence) { _shot = _actor.ShotSequence; PlayShot(); }
            if (_action != _actor.ActionSequence)
            {
                _action = _actor.ActionSequence;
                if (_actor.ActionEvent == ACTION_STATE.MELEE) Play(_profile.Melee);
                if (_actor.ActionEvent == ACTION_STATE.RELOAD) Play(_profile.Reload);
            }
            if (_alive && !alive) Play(_profile.Death);
            if (_actor.HasInputAuthority && _profile.PlayLocalWarnings &&
                (!_wasExposed && _actor.Exposed || _magazine > 0 && _actor.ConfirmedMagazine == 0)) PlayLocal(_profile.LocalWarning);
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
