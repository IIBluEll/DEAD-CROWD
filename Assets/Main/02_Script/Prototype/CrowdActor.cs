using Fusion;
using UnityEngine;
using UnityEngine.AI;

namespace DeadCrowd.Prototype
{
    [RequireComponent(typeof(NetworkObject), typeof(NetworkTransform), typeof(CharacterController))]
    public sealed class CrowdActor : NetworkBehaviour
    {
        [SerializeField] private Transform _bodyTrans;
        [SerializeField] private GameObject _weaponObj;
        [SerializeField] private Renderer _bodyRenderer;

        [Networked] public int StableId { get; set; }
        [Networked] public int Slot { get; set; }
        [Networked] public NetworkBool IsNpc { get; set; }
        [Networked] public NetworkBool Connected { get; set; }
        [Networked] public NetworkBool Ready { get; set; }
        [Networked] public int LoadedRoundId { get; set; }
        [Networked] public NetworkBool LoadFailed { get; set; }
        [Networked] public int ConfirmedMagazine { get; set; }
        [Networked] public int ConfirmedReserve { get; set; }
        [Networked] public ACTION_STATE ConfirmedAction { get; set; }
        [Networked] public int ConfirmedActionEndTick { get; set; }
        public void ConfirmPresentation()
        {
            if (!HasStateAuthority) return;
            ConfirmedMagazine = Magazine; ConfirmedReserve = Reserve;
            ConfirmedAction = Action; ConfirmedActionEndTick = ActionEndTick;
        }
        [Networked] public int Health { get; set; }
        [Networked] public float Exposure { get; set; }
        [Networked] public NetworkBool Exposed { get; set; }
        [Networked] public int LastDangerTick { get; set; }
        [Networked] public ACTION_STATE Action { get; set; }
        [Networked] public int ActionEndTick { get; set; }
        [Networked] public int HitTick { get; set; }
        [Networked] public int AttackId { get; set; }
        [Networked] public int SettledAttackId { get; set; }
        [Networked] public NetworkBool HitPending { get; set; }
        [Networked] public int Magazine { get; set; }
        [Networked] public int Reserve { get; set; }
        [Networked] public NetworkBool Aiming { get; set; }
        [Networked] public float PistolPitch { get; set; }
        [Networked] public int LastAttackSequence { get; set; }
        [Networked] public int LastReloadSequence { get; set; }
        [Networked] public int ActionSequence { get; set; }
        [Networked] public ACTION_STATE ActionEvent { get; set; }
        [Networked] public int DamageSequence { get; set; }
        [Networked] public int ShotSequence { get; set; }
        [Networked] public Vector3 ShotStart { get; set; }
        [Networked] public Vector3 ShotEnd { get; set; }
        [Networked] public int DeathTick { get; set; }
        [Networked] public DEATH_CAUSE DeathCause { get; set; }
        [Networked] public AI_STATE AiState { get; set; }

        private DeadCrowd.Content.CrowdActorPresentation _presentation;
        private CharacterController _controller;
        private NetworkTransform _networkTransform;
        private CrowdMatch _match;
        private NavMeshPath _path;
        private Vector3 _lastValidPosition;
        private Vector3[] _corners;
        private int _cornerIndex;
        private int _pathTick;
        private Vector3 _pathGoal;
        private int _lastDamageSequence;
        private int _lastShotSequence;
        private float _flashUntil;
        private float _shotUntil;
        private LineRenderer _shotLine;
        private MaterialPropertyBlock _propertyBlock;

        public bool DangerThisTick;
        public float ExposureAddition;
        public CrowdActor RememberedTarget;
        // Authority-only candidate for one melee attack; never chooses the actual impact target.
        public CrowdActor BackstabCandidate;
        public Vector3 Goal;
        public int WaitUntilTick;
        public int LastProgressTick;
        public float LastPathDistance;
        public bool WaitingAtGoal;
        public Vector3 ProgressPosition;
        public int RecoveryUntilTick;
        public bool PresentationReady { get; private set; }

        public bool Alive => Object != null && Object.IsValid && Health > 0;
        public Vector3 Center => transform.position + Vector3.up * 0.9f;
        public Quaternion PistolRotation => transform.rotation * Quaternion.Euler(PistolPitch, 0f, 0f);

        // Match the standard pistol rig's shoulder and barrel offsets without sampling animation in a simulation tick.
        private static readonly Vector3 PISTOL_SHOULDER_OFFSET = new Vector3(0.3f, 1.32f, 0f);
        private static readonly Vector3 PISTOL_BARREL_OFFSET = new Vector3(0f, -0.04f, 0.71f);

        public Vector3 GetPistolMuzzle(Quaternion pistolRotation) => transform.position + transform.rotation * PISTOL_SHOULDER_OFFSET + pistolRotation * PISTOL_BARREL_OFFSET;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _networkTransform = GetComponent<NetworkTransform>();
            _shotLine = GetComponent<LineRenderer>();
            _path = new NavMeshPath();
            _propertyBlock = new MaterialPropertyBlock();
        }

        public override void Spawned()
        {
            _match = FindFirstObjectByType<CrowdMatch>();
            _match.Register(this);
            _lastValidPosition = transform.position;
            _lastDamageSequence = DamageSequence;
            _lastShotSequence = ShotSequence;
            var profile = DeadCrowd.Content.CrowdContent.Presentation;
            if (profile != null && profile.CommonVisualPrefab != null)
            {
                try
                {
                    _presentation = Instantiate(profile.CommonVisualPrefab, transform);
                    _presentation.Initialize(this, profile);
                    PresentationReady = true;
                }
                catch (System.Exception exception)
                {
                    Debug.LogError("[Content] Character presentation failed: " + exception.Message);
                    if (_presentation != null) Destroy(_presentation.gameObject);
                    _presentation = null;
                }
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (_match != null) _match.Unregister(this);
        }

        public void Initialize(int id, int slot, bool npc)
        {
            StableId = id;
            Slot = slot;
            IsNpc = npc;
            Connected = true;
            Health = CrowdRules.MAX_HP;
            Magazine = CrowdRules.MAGAZINE;
            Reserve = CrowdRules.RESERVE;
            ConfirmPresentation();
        }

        public void ResetRound(Vector3 position, Quaternion rotation)
        {
            Health = CrowdRules.MAX_HP;
            Exposure = 0f;
            Exposed = false;
            Action = ACTION_STATE.NONE;
            HitPending = false;
            Aiming = false;
            PistolPitch = 0f;
            Magazine = CrowdRules.MAGAZINE;
            Reserve = CrowdRules.RESERVE;
            ConfirmPresentation();
            DeathTick = 0;
            DeathCause = DEATH_CAUSE.NONE;
            Ready = false;
            LoadedRoundId = -1;
            LoadFailed = false;
            RememberedTarget = null;
            BackstabCandidate = null;
            AiState = AI_STATE.IDLE;
            WaitingAtGoal = false;
            WaitUntilTick = 0;
            RecoveryUntilTick = 0;
            ProgressPosition = position;
            _corners = null;
            _lastValidPosition = position;
            Teleport(position, rotation);
            if (_presentation != null) _presentation.ResetPresentation();
        }

        public void Move(Vector3 direction, float speed, float turnSpeed, Vector3 facing, float deltaTime)
        {
            if (!Alive) return;
            // Rebind the native controller to the Transform restored by NetworkTransform on rollback.
            _controller.enabled = false;
            _controller.enabled = true;
            if (facing.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(facing), turnSpeed * deltaTime);
            _controller.Move((Vector3.ClampMagnitude(direction, 1f) * speed + Vector3.down * 2f) * deltaTime);
            if (HasStateAuthority) RecoverOutsideMap();
        }

        private void RecoverOutsideMap()
        {
            Vector3 position = transform.position;
            bool grounded = _controller.isGrounded;
            // Spawn bounds are inset from the arena walls. The baked NavMesh covers the outer walkways.
            bool insideSpawnArea = grounded && Mathf.Abs(position.y - _lastValidPosition.y) <= _controller.stepOffset && DeadCrowd.Content.CrowdContent.Map.ContainsSpawnPosition(position);
            bool finite = float.IsFinite(position.x) && float.IsFinite(position.y) && float.IsFinite(position.z);
            if (finite && (insideSpawnArea || NavMesh.SamplePosition(position, out _, _controller.radius * 2f, NavMesh.AllAreas)))
            {
                if (grounded) _lastValidPosition = position;
                return;
            }
            Debug.LogWarning($"[Crowd] Recovered out-of-map actor. round={_match.RoundId}, tick={_match.CurrentTick}, actor={StableId}, position={position}, lastValid={_lastValidPosition}");
            Teleport(_lastValidPosition, transform.rotation);
            _corners = null;
        }

        private void Teleport(Vector3 position, Quaternion rotation)
        {
            _controller.enabled = false;
            _networkTransform.Teleport(position, rotation);
            transform.SetPositionAndRotation(position, rotation);
            _controller.enabled = true;
        }

        public Vector3 PathDirection(Vector3 goal, int tick)
        {
            if (_corners == null || tick >= _pathTick || (goal - _pathGoal).sqrMagnitude > 1f)
            {
                _pathTick = tick + CrowdRules.Ticks(CrowdRules.PATH_REFRESH_SECONDS, Runner.DeltaTime);
                _pathGoal = goal;
                if (!NavMesh.CalculatePath(transform.position, goal, NavMesh.AllAreas, _path) || _path.status == NavMeshPathStatus.PathInvalid)
                {
                    _corners = null;
                    return Vector3.zero;
                }
                _corners = _path.corners;
                _cornerIndex = 1;
            }
            while (_cornerIndex < _corners.Length && CrowdRules.Flat(_corners[_cornerIndex] - transform.position).sqrMagnitude < 0.09f) _cornerIndex++;
            return _cornerIndex < _corners.Length ? CrowdRules.Flat(_corners[_cornerIndex] - transform.position).normalized : Vector3.zero;
        }

        public float RemainingPathDistance()
        {
            if (_corners == null || _cornerIndex >= _corners.Length) return CrowdRules.Flat(Goal - transform.position).magnitude;
            float distance = Vector3.Distance(transform.position, _corners[_cornerIndex]);
            for (int i = _cornerIndex; i + 1 < _corners.Length; i++) distance += Vector3.Distance(_corners[i], _corners[i + 1]);
            return distance;
        }

        public void ApplyDamage(int damage, DEATH_CAUSE cause, int tick)
        {
            if (!Alive) return;
            Health = Mathf.Max(0, Health - damage);
            DamageSequence++;
            if (Health > 0) return;
            DeathCause = cause;
            DeathTick = tick;
            Action = ACTION_STATE.NONE;
            Aiming = false;
            HitPending = false;
            _controller.enabled = false;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_ReportLoaded(int roundId, string version, bool success)
        {
            if (!Connected || IsNpc || _match.RoundId != roundId || _match.State != ROUND_STATE.LOADING) return;
            if (LoadedRoundId == roundId || LoadFailed) return;
            LoadFailed = !success || version != DeadCrowd.Content.CrowdContent.SessionVersion;
            if (!LoadFailed) LoadedRoundId = roundId;
            Debug.Log($"[Crowd] Load response round={roundId}, slot={Slot}, success={!LoadFailed}");
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_SetReady(bool ready, int roundId)
        {
            if (_match.RoundId == roundId && _match.State == ROUND_STATE.LOBBY && Connected) Ready = ready;
        }

        public override void Render()
        {
            bool alive = Health > 0;
            _controller.enabled = alive;
            bool visible = alive || Runner.Tick.Raw - DeathTick < CrowdRules.Ticks(CrowdRules.CORPSE_SECONDS, Runner.DeltaTime);
            _bodyTrans.gameObject.SetActive(visible && _presentation == null);
            if (_presentation != null) _presentation.gameObject.SetActive(visible);
            _bodyTrans.localRotation = alive ? Quaternion.identity : Quaternion.Euler(0f, 0f, 90f);
            _bodyTrans.localPosition = alive ? Vector3.up * 0.9f : Vector3.up * 0.3f;
            _weaponObj.SetActive(_presentation == null && alive && (Aiming || Action != ACTION_STATE.NONE));
            _weaponObj.transform.localRotation = Action == ACTION_STATE.MELEE ? Quaternion.Euler(0f, Mathf.Sin((ActionEndTick - Runner.Tick.Raw) * 0.3f) * 70f, 0f) : Quaternion.identity;
            if (_lastDamageSequence != DamageSequence)
            {
                _lastDamageSequence = DamageSequence;
                _flashUntil = Time.time + 0.15f;
            }
            _propertyBlock.SetColor("_BaseColor", Time.time < _flashUntil ? new Color(0.9f, 0.32f, 0.23f) : new Color(0.38f, 0.49f, 0.39f));
            _bodyRenderer.SetPropertyBlock(_propertyBlock);
            if (_lastShotSequence != ShotSequence)
            {
                _lastShotSequence = ShotSequence;
                _shotUntil = Time.time + 0.08f;
            }
            if (_shotLine != null)
            {
                _shotLine.enabled = Time.time < _shotUntil;
                if (_shotLine.enabled)
                {
                    _shotLine.SetPosition(0, ShotStart);
                    _shotLine.SetPosition(1, ShotEnd);
                }
            }
        }
    }
}
