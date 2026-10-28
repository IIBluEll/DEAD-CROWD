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
        [Networked] public int Health { get; set; }
        [Networked] public float Exposure { get; set; }
        [Networked] public NetworkBool Exposed { get; set; }
        [Networked] public int LastDangerTick { get; set; }
        [Networked] public ACTION_STATE Action { get; set; }
        [Networked] public int ActionEndTick { get; set; }
        [Networked] public int HitTick { get; set; }
        [Networked] public NetworkBool HitPending { get; set; }
        [Networked] public int Magazine { get; set; }
        [Networked] public int Reserve { get; set; }
        [Networked] public NetworkBool Aiming { get; set; }
        [Networked] public int LastAttackSequence { get; set; }
        [Networked] public int LastReloadSequence { get; set; }
        [Networked] public int DamageSequence { get; set; }
        [Networked] public int ShotSequence { get; set; }
        [Networked] public Vector3 ShotEnd { get; set; }
        [Networked] public int DeathTick { get; set; }
        [Networked] public DEATH_CAUSE DeathCause { get; set; }
        [Networked] public AI_STATE AiState { get; set; }

        private CharacterController _controller;
        private NetworkTransform _networkTransform;
        private CrowdMatch _match;
        private NavMeshPath _path;
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

        public CrowdInput CurrentInput;
        public bool HasCurrentInput;
        public bool DangerThisTick;
        public float ExposureAddition;
        public CrowdActor RememberedTarget;
        public Vector3 Goal;
        public int WaitUntilTick;
        public int LastProgressTick;
        public float LastPathDistance;
        public bool WaitingAtGoal;

        public bool Alive => Object != null && Object.IsValid && Health > 0;
        public Vector3 Center => transform.position + Vector3.up * 0.9f;

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
            _lastDamageSequence = DamageSequence;
            _lastShotSequence = ShotSequence;
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
        }

        public void ResetRound(Vector3 position, Quaternion rotation)
        {
            Health = CrowdRules.MAX_HP;
            Exposure = 0f;
            Exposed = false;
            Action = ACTION_STATE.NONE;
            HitPending = false;
            Aiming = false;
            Magazine = CrowdRules.MAGAZINE;
            Reserve = CrowdRules.RESERVE;
            DeathTick = 0;
            DeathCause = DEATH_CAUSE.NONE;
            Ready = false;
            LoadedRoundId = -1;
            LoadFailed = false;
            RememberedTarget = null;
            AiState = AI_STATE.IDLE;
            WaitingAtGoal = false;
            WaitUntilTick = 0;
            _corners = null;
            _controller.enabled = false;
            _networkTransform.Teleport(position, rotation);
            transform.SetPositionAndRotation(position, rotation);
            _controller.enabled = true;
        }

        public void Move(Vector3 direction, float speed, float turnSpeed, Vector3 facing, float deltaTime)
        {
            if (!Alive) return;
            _controller.enabled = true;
            if (facing.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(facing), turnSpeed * deltaTime);
            _controller.Move((Vector3.ClampMagnitude(direction, 1f) * speed + Vector3.down * 2f) * deltaTime);
        }

        public Vector3 PathDirection(Vector3 goal, int tick)
        {
            if (_corners == null || tick >= _pathTick || (goal - _pathGoal).sqrMagnitude > 1f)
            {
                _pathTick = tick + CrowdRules.Ticks(0.25f, Runner.DeltaTime);
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
            LoadFailed = !success || version != CrowdRules.BUILD_VERSION;
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
            bool visible = alive || Runner.Tick.Raw - DeathTick < CrowdRules.Ticks(20f, Runner.DeltaTime);
            _bodyTrans.gameObject.SetActive(visible);
            _bodyTrans.localRotation = alive ? Quaternion.identity : Quaternion.Euler(0f, 0f, 90f);
            _bodyTrans.localPosition = alive ? Vector3.up * 0.9f : Vector3.up * 0.3f;
            _weaponObj.SetActive(alive && (Aiming || Action != ACTION_STATE.NONE));
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
                    _shotLine.SetPosition(0, Center + transform.forward * 0.4f);
                    _shotLine.SetPosition(1, ShotEnd);
                }
            }
        }
    }
}
