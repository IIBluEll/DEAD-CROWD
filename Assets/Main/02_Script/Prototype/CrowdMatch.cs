using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

namespace DeadCrowd.Prototype
{
    public sealed class CrowdMatch : NetworkBehaviour
    {
        [SerializeField] private NetworkObject _actorPrefab;
        [SerializeField] private Transform[] _spawnTrans;

        [Networked] public ROUND_STATE State { get; set; }
        [Networked] public int RequiredPlayers { get; set; }
        [Networked] public int RoundId { get; set; }
        [Networked] public int CountdownEndTick { get; set; }
        [Networked] public int EndTick { get; set; }
        [Networked] public int WinnerSlot { get; set; }
        [Networked] public int AlivePlayers { get; set; }
        [Networked] public int Seed { get; set; }
        [Networked] public NetworkBool Practice { get; set; }
        [Networked] public int LoadDeadlineTick { get; set; }
        [Networked] public START_FAILURE LastStartFailure { get; set; }
        [Networked] public NetworkBool HostLoadFailed { get; set; }

        private readonly List<CrowdActor> ACTORS = new List<CrowdActor>();
        private readonly List<Damage> DAMAGE = new List<Damage>();
        private readonly List<Noise> NOISES = new List<Noise>();
        private readonly HashSet<PlayerRef> ACTIVE_PLAYERS = new HashSet<PlayerRef>();
        private System.Random _random;
        private int _nextId;
        private int _noiseId;

        private readonly struct Damage
        {
            public readonly CrowdActor TARGET;
            public readonly int AMOUNT;
            public readonly DEATH_CAUSE CAUSE;
            public Damage(CrowdActor target, int amount, DEATH_CAUSE cause) { TARGET = target; AMOUNT = amount; CAUSE = cause; }
        }

        private readonly struct Noise
        {
            public readonly Vector3 POSITION;
            public readonly float RADIUS;
            public readonly int ID;
            public Noise(Vector3 position, float radius, int id) { POSITION = position; RADIUS = radius; ID = id; }
        }

        public IReadOnlyList<CrowdActor> Actors => ACTORS;
        public bool Valid => Object != null && Object.IsValid;
        public int CurrentTick => Runner.Tick.Raw;
        public float RemainingSeconds => Mathf.Max(0f, (EndTick - CurrentTick) * Runner.DeltaTime);
        public int ExpectedNpcCount => RequiredPlayers == 4 ? 30 : 20;

        public int LoadedCount()
        {
            int count = 0;
            foreach (CrowdActor actor in ACTORS) if (!actor.IsNpc && actor.Connected && actor.LoadedRoundId == RoundId) count++;
            return count;
        }

        public void Configure(NetworkObject actorPrefab, Transform[] spawnTrans) { _actorPrefab = actorPrefab; _spawnTrans = spawnTrans; }
        public void Register(CrowdActor actor) { if (!ACTORS.Contains(actor)) ACTORS.Add(actor); }
        public void Unregister(CrowdActor actor) { ACTORS.Remove(actor); }

        public override void Spawned()
        {
            Debug.Log($"[Crowd] Match registered. authority={HasStateAuthority}, mode={Runner.GameMode}");
            if (!HasStateAuthority) return;
            Practice = Runner.GameMode == GameMode.Single;
            RequiredPlayers = Practice ? 1 : Runner.SessionInfo.MaxPlayers;
            State = ROUND_STATE.LOBBY;
            WinnerSlot = -3;
        }

        public CrowdActor LocalActor()
        {
            foreach (CrowdActor actor in ACTORS)
                if (actor != null && actor.Object != null && actor.Object.IsValid && !actor.IsNpc && actor.Object.InputAuthority == Runner.LocalPlayer) return actor;
            return null;
        }

        public int ConnectedCount()
        {
            int count = 0;
            foreach (CrowdActor actor in ACTORS) if (actor != null && actor.Object.IsValid && !actor.IsNpc && actor.Connected) count++;
            return count;
        }

        public bool CanStart()
        {
            if (State != ROUND_STATE.LOBBY || ConnectedCount() != RequiredPlayers) return false;
            foreach (CrowdActor actor in ACTORS) if (!actor.IsNpc && actor.Connected && !actor.Ready) return false;
            return true;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        public void RPC_RequestStart(int roundId, RpcInfo info = default)
        {
            if (RoundId != roundId || !Practice && info.Source != Runner.LocalPlayer || !CanStart()) return;
            BeginRound();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        public void RPC_ReturnToLobby(int roundId, RpcInfo info = default)
        {
            if (RoundId != roundId || !Practice && info.Source != Runner.LocalPlayer || State != ROUND_STATE.RESULT) return;
            ReturnToLobby();
        }

        public void ReturnToLobby()
        {
            State = ROUND_STATE.LOBBY;
            SetRoomOpen(true);
            ClearNpcs();
            for (int i = ACTORS.Count - 1; i >= 0; i--)
            {
                CrowdActor actor = ACTORS[i];
                if (!actor.Connected) { Runner.Despawn(actor.Object); continue; }
                Transform spawn = _spawnTrans[(actor.Slot - 1) % _spawnTrans.Length];
                actor.ResetRound(spawn.position, spawn.rotation);
            }
            WinnerSlot = -3;
            CountdownEndTick = 0;
            EndTick = 0;
            LoadDeadlineTick = 0;
            Debug.Log("[Crowd] Returned to lobby; round state cleared.");
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            SynchronizePlayers();
            if (State == ROUND_STATE.LOBBY || State == ROUND_STATE.RESULT) return;
            if (State == ROUND_STATE.LOADING) { UpdateLoading(); return; }
            if (State == ROUND_STATE.COUNTDOWN)
            {
                if (ConnectedCount() != RequiredPlayers) { LastStartFailure = START_FAILURE.PARTICIPANT_LEFT; ReturnToLobby(); return; }
                if (CurrentTick < CountdownEndTick) return;
                State = ROUND_STATE.PLAYING;
                EndTick = CurrentTick + CrowdRules.Ticks(CrowdRules.ROUND_SECONDS, Runner.DeltaTime);
                Debug.Log($"[Crowd] Round {RoundId} playing. players={RequiredPlayers}, npc={NpcCount()}, seed={Seed}");
            }

            DAMAGE.Clear();
            bool boundary = CurrentTick >= EndTick;
            foreach (CrowdActor actor in ACTORS)
            {
                if (!actor.Alive) continue;
                actor.DangerThisTick = false;
                actor.ExposureAddition = 0f;
                if (!actor.IsNpc) SimulatePlayer(actor, boundary);
            }
            Physics.SyncTransforms();
            foreach (CrowdActor actor in ACTORS)
            {
                if (!actor.Alive || actor.IsNpc) continue;
                float exposure = actor.Exposure;
                bool exposed = actor.Exposed;
                if (actor.DangerThisTick) actor.LastDangerTick = CurrentTick;
                bool canDecay = CurrentTick - actor.LastDangerTick >= CrowdRules.Ticks(CrowdRules.EXP_DELAY, Runner.DeltaTime);
                CrowdRules.UpdateExposure(ref exposure, ref exposed, actor.DangerThisTick, canDecay, actor.ExposureAddition, Runner.DeltaTime);
                actor.Exposure = exposure;
                actor.Exposed = exposed;
            }
            foreach (CrowdActor actor in ACTORS) if (actor.Alive && actor.IsNpc) SimulateNpc(actor, boundary);
            NOISES.Clear();
            Physics.SyncTransforms();
            foreach (CrowdActor actor in ACTORS) if (actor.Alive) CollectAttack(actor);

            // Every attack sees pre-damage health. Commit damage only after all hit queries.
            foreach (Damage damage in DAMAGE) damage.TARGET.ApplyDamage(damage.AMOUNT, damage.CAUSE, CurrentTick);
            AlivePlayers = 0;
            int lastSlot = -1;
            foreach (CrowdActor actor in ACTORS)
                if (!actor.IsNpc && actor.Connected && actor.Alive) { AlivePlayers++; lastSlot = actor.Slot; }
            int result = Practice ? (AlivePlayers == 0 ? -1 : boundary ? -2 : -3) : CrowdRules.ResolveResult(AlivePlayers, lastSlot, boundary);
            if (result != -3)
            {
                WinnerSlot = result;
                State = ROUND_STATE.RESULT;
                foreach (CrowdActor actor in ACTORS) { actor.HitPending = false; actor.Action = ACTION_STATE.NONE; actor.Aiming = false; }
                Debug.Log($"[Crowd] Result round={RoundId}, winner={result}, alive={AlivePlayers}");
            }
            for (int i = ACTORS.Count - 1; i >= 0; i--)
                if (ACTORS[i].IsNpc && !ACTORS[i].Alive && CurrentTick - ACTORS[i].DeathTick >= CrowdRules.Ticks(20f, Runner.DeltaTime)) Runner.Despawn(ACTORS[i].Object);
        }

        private void SynchronizePlayers()
        {
            ACTIVE_PLAYERS.Clear();
            foreach (PlayerRef player in Runner.ActivePlayers)
            {
                ACTIVE_PLAYERS.Add(player);
                bool exists = false;
                foreach (CrowdActor actor in ACTORS) if (!actor.IsNpc && actor.Connected && actor.Object.InputAuthority == player) { exists = true; break; }
                if (exists || State != ROUND_STATE.LOBBY) continue;
                int slot = 1;
                while (SlotUsed(slot)) slot++;
                Transform spawn = _spawnTrans[(slot - 1) % _spawnTrans.Length];
                int id = ++_nextId;
                NetworkObject obj = Runner.Spawn(_actorPrefab, spawn.position, spawn.rotation, player, (_, spawned) => spawned.GetComponent<CrowdActor>().Initialize(id, slot, false));
                Runner.SetPlayerObject(player, obj);
                ResetReady();
                Debug.Log($"[Crowd] Player joined slot={slot}");
            }
            for (int i = ACTORS.Count - 1; i >= 0; i--)
            {
                CrowdActor actor = ACTORS[i];
                if (actor.IsNpc || !actor.Connected || ACTIVE_PLAYERS.Contains(actor.Object.InputAuthority)) continue;
                actor.Connected = false;
                actor.ApplyDamage(CrowdRules.MAX_HP, DEATH_CAUSE.DISCONNECTED, CurrentTick);
                ResetReady();
                if (State == ROUND_STATE.LOBBY) Runner.Despawn(actor.Object);
            }
        }

        private bool SlotUsed(int slot)
        {
            foreach (CrowdActor actor in ACTORS) if (!actor.IsNpc && actor.Connected && actor.Slot == slot) return true;
            return false;
        }

        private void ResetReady() { foreach (CrowdActor actor in ACTORS) if (!actor.IsNpc) actor.Ready = false; }
        private void SetRoomOpen(bool open) { if (!Practice && Runner.SessionInfo.IsValid) Runner.SessionInfo.IsOpen = open; }
        private void ClearNpcs() { for (int i = ACTORS.Count - 1; i >= 0; i--) if (ACTORS[i].IsNpc) Runner.Despawn(ACTORS[i].Object); }
        public int NpcCount() { int count = 0; foreach (CrowdActor actor in ACTORS) if (actor.IsNpc && actor.Alive) count++; return count; }

        private void BeginRound()
        {
            LastStartFailure = START_FAILURE.NONE;
            HostLoadFailed = false;
            State = ROUND_STATE.LOADING;
            SetRoomOpen(false);
            LoadDeadlineTick = CurrentTick + CrowdRules.Ticks(CrowdRules.LOAD_TIMEOUT_SECONDS, Runner.DeltaTime);
            ClearNpcs();
            RoundId++;
            Seed = UnityEngine.Random.Range(1, int.MaxValue);
            _random = new System.Random(Seed);
            int offset = _random.Next(_spawnTrans.Length);
            foreach (CrowdActor actor in ACTORS)
            {
                Transform spawn = _spawnTrans[(actor.Slot - 1 + offset) % _spawnTrans.Length];
                actor.ResetRound(spawn.position, Quaternion.Euler(0f, _random.Next(360), 0f));
            }
            int count = ExpectedNpcCount;
            // Give every start a nearby crowd before distributing the remaining NPCs.
            var starts = new List<Vector3>();
            foreach (CrowdActor actor in ACTORS) if (!actor.IsNpc && actor.Connected) starts.Add(actor.transform.position);
            for (int i = 0; i < count; i++)
            {
                Vector3 position;
                bool nearStart = i < starts.Count * 3;
                if (!TrySpawnPoint(nearStart ? starts[i / 3] : Vector3.zero, nearStart ? 7f : 18f, out position))
                {
                    Debug.LogError("[Crowd] Valid NPC spawn unavailable. Round start cancelled.");
                    HostLoadFailed = true;
                    LastStartFailure = START_FAILURE.LOAD_FAILED;
                    return;
                }
                int id = ++_nextId;
                Runner.Spawn(_actorPrefab, position, Quaternion.Euler(0f, _random.Next(360), 0f), PlayerRef.None, (_, spawned) => spawned.GetComponent<CrowdActor>().Initialize(id, 0, true));
            }
            NOISES.Clear();
            DAMAGE.Clear();
            WinnerSlot = -3;
            AlivePlayers = ConnectedCount();
            CountdownEndTick = 0;
            EndTick = 0;
            Debug.Log($"[Crowd] Loading round={RoundId}, seed={Seed}, deadline={LoadDeadlineTick}");
        }

        private void UpdateLoading()
        {
            if (HostLoadFailed) return;
            if (ConnectedCount() != RequiredPlayers) { LastStartFailure = START_FAILURE.PARTICIPANT_LEFT; ReturnToLobby(); return; }
            bool expired = CurrentTick >= LoadDeadlineTick;
            bool failed = false;
            foreach (CrowdActor actor in ACTORS)
            {
                if (actor.IsNpc || !actor.Connected || !actor.LoadFailed && (!expired || actor.LoadedRoundId == RoundId)) continue;
                failed = true;
                LastStartFailure = actor.LoadFailed ? START_FAILURE.LOAD_FAILED : START_FAILURE.LOAD_TIMEOUT;
                if (actor.Object.InputAuthority == Runner.LocalPlayer) { HostLoadFailed = true; return; }
                actor.Connected = false;
                Runner.Disconnect(actor.Object.InputAuthority);
                Debug.LogWarning($"[Crowd] Loading rejected slot={actor.Slot}, reason={LastStartFailure}");
            }
            if (failed) { ReturnToLobby(); return; }
            if (LoadedCount() != RequiredPlayers) return;
            CountdownEndTick = CurrentTick + CrowdRules.Ticks(3f, Runner.DeltaTime);
            State = ROUND_STATE.COUNTDOWN;
            Debug.Log($"[Crowd] Countdown round={RoundId}, loaded={LoadedCount()}/{RequiredPlayers}");
        }

        private bool TrySpawnPoint(Vector3 origin, float radius, out Vector3 position)
        {
            for (int attempt = 0; attempt < 100; attempt++)
            {
                Vector3 point = origin + new Vector3((float)(_random.NextDouble() * 2 - 1) * radius, 0f, (float)(_random.NextDouble() * 2 - 1) * radius);
                if (Mathf.Abs(point.x) > 18.5f || Mathf.Abs(point.z) > 18.5f || !NavMesh.SamplePosition(point, out NavMeshHit hit, 0.8f, NavMesh.AllAreas)) continue;
                point = hit.position;
                if (Physics.CheckCapsule(point + Vector3.up * 0.4f, point + Vector3.up * 1.4f, 0.32f, CrowdRules.WORLD_MASK)) continue;
                bool overlaps = false;
                foreach (CrowdActor actor in ACTORS) if (actor.Alive && CrowdRules.Flat(actor.transform.position - point).sqrMagnitude < 0.65f * 0.65f) { overlaps = true; break; }
                if (overlaps) continue;
                position = point;
                return true;
            }
            position = default;
            return false;
        }

        private void SimulatePlayer(CrowdActor actor, bool boundary)
        {
            actor.HasCurrentInput = Runner.TryGetInputForPlayer(actor.Object.InputAuthority, out CrowdInput input) && input.RoundId == RoundId;
            if (!actor.HasCurrentInput) input = new CrowdInput { Yaw = actor.transform.eulerAngles.y, AttackSequence = actor.LastAttackSequence, ReloadSequence = actor.LastReloadSequence };
            if (!float.IsFinite(input.Yaw) || !float.IsFinite(input.Pitch) || !float.IsFinite(input.Move.x) || !float.IsFinite(input.Move.y)) input = new CrowdInput { Yaw = actor.transform.eulerAngles.y, AttackSequence = actor.LastAttackSequence, ReloadSequence = actor.LastReloadSequence };
            input.Pitch = Mathf.Clamp(input.Pitch, -60f, 30f);
            input.Move = Vector2.ClampMagnitude(input.Move, 1f);
            actor.CurrentInput = input;
            CompleteAction(actor);
            bool attack = input.AttackSequence != actor.LastAttackSequence;
            bool reload = input.ReloadSequence != actor.LastReloadSequence;
            actor.LastAttackSequence = input.AttackSequence;
            actor.LastReloadSequence = input.ReloadSequence;
            if (boundary) return;

            bool unlocked = actor.Action == ACTION_STATE.NONE;
            bool reloadValid = unlocked && reload && actor.Magazine < CrowdRules.MAGAZINE && actor.Reserve > 0;
            bool meleeIntent = unlocked && attack && !input.Aim && !reloadValid;
            actor.Aiming = !reloadValid && (actor.Action == ACTION_STATE.NONE || actor.Action == ACTION_STATE.SHOT) && input.Aim;
            Vector3 direction = Quaternion.Euler(0f, input.Yaw, 0f) * new Vector3(input.Move.x, 0f, input.Move.y);
            bool sprint = unlocked && !reloadValid && !attack && !actor.Aiming && input.Sprint && direction.sqrMagnitude > 0f;
            float speed = actor.Action == ACTION_STATE.MELEE || meleeIntent ? CrowdRules.MELEE_SPEED : actor.Aiming ? CrowdRules.AIM_SPEED : sprint ? CrowdRules.SPRINT_SPEED : CrowdRules.WALK_SPEED;
            Vector3 facing = actor.Aiming ? Quaternion.Euler(0f, input.Yaw, 0f) * Vector3.forward : direction;
            Vector3 previous = actor.transform.position;
            actor.Move(direction, speed, actor.Action == ACTION_STATE.MELEE || meleeIntent ? CrowdRules.MELEE_TURN_SPEED : CrowdRules.TURN_SPEED, facing, Runner.DeltaTime);
            if (sprint && CrowdRules.Flat(actor.transform.position - previous).sqrMagnitude > 0.000001f) AddExposure(actor, CrowdRules.EXP_SPRINT * Runner.DeltaTime);

            if (reloadValid)
            {
                actor.Action = ACTION_STATE.RELOAD;
                actor.Aiming = false;
                actor.ActionEndTick = CurrentTick + CrowdRules.Ticks(CrowdRules.RELOAD_SECONDS, Runner.DeltaTime);
            }
            else if (meleeIntent)
            {
                StartAttack(actor, ACTION_STATE.MELEE, CrowdRules.MELEE_WINDUP, CrowdRules.MELEE_CYCLE);
                AddExposure(actor, CrowdRules.EXP_MELEE);
                EmitNoise(actor.transform.position, 3f);
            }
            else if (unlocked && attack && input.Aim && actor.Magazine > 0 && Vector3.Angle(actor.transform.forward, Quaternion.Euler(0f, input.Yaw, 0f) * Vector3.forward) <= CrowdRules.AIM_TOLERANCE)
            {
                StartAttack(actor, ACTION_STATE.SHOT, 0f, CrowdRules.GUN_INTERVAL);
                actor.Magazine--;
                AddExposure(actor, CrowdRules.EXP_SHOT);
                EmitNoise(actor.transform.position, 18f);
            }
        }

        private void CompleteAction(CrowdActor actor)
        {
            if (actor.Action == ACTION_STATE.NONE || CurrentTick < actor.ActionEndTick) return;
            if (actor.Action == ACTION_STATE.RELOAD)
            {
                int amount = CrowdRules.ReloadAmount(actor.Magazine, actor.Reserve);
                actor.Magazine += amount;
                actor.Reserve -= amount;
            }
            actor.Action = ACTION_STATE.NONE;
        }

        private void StartAttack(CrowdActor actor, ACTION_STATE action, float windup, float cycle)
        {
            actor.Action = action;
            actor.HitPending = true;
            actor.HitTick = CurrentTick + CrowdRules.Ticks(windup, Runner.DeltaTime);
            actor.ActionEndTick = CurrentTick + CrowdRules.Ticks(cycle, Runner.DeltaTime);
        }

        private void AddExposure(CrowdActor actor, float amount) { actor.DangerThisTick = true; actor.ExposureAddition += amount; }
        private void EmitNoise(Vector3 position, float radius) { NOISES.Add(new Noise(position, radius, ++_noiseId)); }

        private void CollectAttack(CrowdActor actor)
        {
            if (!actor.HitPending || CurrentTick < actor.HitTick) return;
            actor.HitPending = false;
            if (actor.IsNpc)
            {
                CrowdActor target = actor.RememberedTarget;
                if (target != null && target.Alive && InAttackRange(actor, target, CrowdRules.AI_RANGE, CrowdRules.AI_HALF_ANGLE)) DAMAGE.Add(new Damage(target, CrowdRules.AI_DAMAGE, DEATH_CAUSE.ZOMBIE));
            }
            else if (actor.Action == ACTION_STATE.MELEE)
            {
                CrowdActor target = SelectMeleeTarget(actor);
                if (target == null) return;
                bool back = CrowdRules.IsBackstab(target.transform.forward, actor.transform.position - target.transform.position);
                DAMAGE.Add(new Damage(target, back ? CrowdRules.MAX_HP : CrowdRules.MELEE_DAMAGE, back ? DEATH_CAUSE.BACKSTAB : DEATH_CAUSE.MELEE));
                EmitNoise(target.transform.position, 6f);
                Debug.Log($"[Crowd] Melee round={RoundId}, tick={CurrentTick}, attacker={actor.StableId}, target={target.StableId}, back={back}");
            }
            else if (actor.Action == ACTION_STATE.SHOT) CollectShot(actor);
        }

        private CrowdActor SelectMeleeTarget(CrowdActor actor)
        {
            CrowdActor selected = null;
            float distance = float.MaxValue;
            foreach (CrowdActor target in ACTORS)
            {
                if (target == actor || !target.Alive || !InAttackRange(actor, target, CrowdRules.MELEE_RANGE, CrowdRules.MELEE_HALF_ANGLE)) continue;
                float next = CrowdRules.Flat(target.transform.position - actor.transform.position).sqrMagnitude;
                if (next < distance || next == distance && selected != null && target.StableId < selected.StableId) { distance = next; selected = target; }
            }
            return selected;
        }

        private bool InAttackRange(CrowdActor actor, CrowdActor target, float range, float angle)
        {
            Vector3 delta = CrowdRules.Flat(target.transform.position - actor.transform.position);
            if (delta.sqrMagnitude > range * range || !CrowdRules.WithinCone(actor.transform.forward, delta, angle)) return false;
            return !FirstBlock(actor.Center, target.Center, actor, out CrowdActor blocker, out _) || blocker == target;
        }

        private bool FirstBlock(Vector3 start, Vector3 end, CrowdActor self, out CrowdActor actor, out Vector3 point)
        {
            actor = null;
            point = end;
            foreach (Collider collider in Physics.OverlapSphere(start, 0.015f, ~0, QueryTriggerInteraction.Ignore))
            {
                CrowdActor overlap = collider.GetComponentInParent<CrowdActor>();
                if (overlap == self || overlap != null && !overlap.Alive) continue;
                actor = overlap;
                point = start;
                return true;
            }
            Vector3 direction = end - start;
            RaycastHit[] hits = Physics.RaycastAll(start, direction.normalized, direction.magnitude, ~0, QueryTriggerInteraction.Ignore);
            float distance = float.MaxValue;
            bool blocked = false;
            foreach (RaycastHit hit in hits)
            {
                CrowdActor candidate = hit.collider.GetComponentInParent<CrowdActor>();
                if (candidate == self || candidate != null && !candidate.Alive || hit.distance >= distance) continue;
                distance = hit.distance;
                actor = candidate;
                point = hit.point;
                blocked = true;
            }
            return blocked;
        }

        private void CollectShot(CrowdActor shooter)
        {
            Quaternion aim = Quaternion.Euler(shooter.CurrentInput.Pitch, shooter.CurrentInput.Yaw, 0f);
            Vector3 pivot = shooter.transform.position + Vector3.up * 1.5f;
            Vector3 cameraPosition = pivot - aim * Vector3.forward * 2.2f + aim * Vector3.right * 0.45f;
            Vector3 cameraDelta = cameraPosition - pivot;
            if (Physics.SphereCast(pivot, 0.2f, cameraDelta.normalized, out RaycastHit cameraHit, cameraDelta.magnitude, CrowdRules.WORLD_MASK)) cameraPosition = pivot + cameraDelta.normalized * Mathf.Max(0.05f, cameraHit.distance - 0.05f);
            Vector3 aimPoint = cameraPosition + aim * Vector3.forward * 100f;
            if (FirstBlock(cameraPosition, aimPoint, shooter, out _, out Vector3 hitPoint)) aimPoint = hitPoint;
            Vector3 muzzle = shooter.transform.position + Vector3.up * 1.2f + shooter.transform.forward * 0.4f + shooter.transform.right * 0.2f;
            bool blocked = FirstBlock(shooter.Center, muzzle, shooter, out CrowdActor target, out Vector3 point);
            if (!blocked) FirstBlock(muzzle, muzzle + (aimPoint - muzzle).normalized * CrowdRules.GUN_RANGE, shooter, out target, out point);
            if (target != null) DAMAGE.Add(new Damage(target, CrowdRules.GUN_DAMAGE, DEATH_CAUSE.PISTOL));
            shooter.ShotEnd = point;
            shooter.ShotSequence++;
        }

        private bool CanSee(CrowdActor npc, CrowdActor target)
        {
            if (target == null || !target.Alive || !target.Connected) return false;
            Vector3 delta = CrowdRules.Flat(target.transform.position - npc.transform.position);
            return delta.sqrMagnitude <= CrowdRules.SIGHT_RANGE * CrowdRules.SIGHT_RANGE && CrowdRules.WithinCone(npc.transform.forward, delta, CrowdRules.SIGHT_HALF_ANGLE) && !Physics.Linecast(npc.transform.position + Vector3.up * 1.55f, target.Center, CrowdRules.WORLD_MASK);
        }

        private void SetGoal(CrowdActor npc, Vector3 goal, AI_STATE state)
        {
            npc.Goal = goal;
            npc.AiState = state;
            npc.WaitingAtGoal = false;
            npc.LastProgressTick = CurrentTick;
            npc.LastPathDistance = float.MaxValue;
        }

        private void SimulateNpc(CrowdActor npc, bool boundary)
        {
            CompleteAction(npc);
            if (boundary) return;
            if (npc.RememberedTarget != null && (!npc.RememberedTarget.Alive || !npc.RememberedTarget.Connected)) { npc.RememberedTarget = null; npc.AiState = AI_STATE.IDLE; }
            bool seesTarget = CanSee(npc, npc.RememberedTarget);
            if (npc.Action == ACTION_STATE.MELEE)
            {
                npc.Move(Vector3.zero, 0f, CrowdRules.TURN_SPEED, seesTarget ? CrowdRules.Flat(npc.RememberedTarget.transform.position - npc.transform.position) : Vector3.zero, Runner.DeltaTime);
                return;
            }
            if (seesTarget)
            {
                SetGoal(npc, npc.RememberedTarget.transform.position, AI_STATE.CHASE);
            }
            else if (npc.RememberedTarget == null)
            {
                CrowdActor found = null;
                float best = float.MaxValue;
                foreach (CrowdActor player in ACTORS)
                {
                    if (player.IsNpc || !player.Exposed || !CanSee(npc, player)) continue;
                    float distance = (player.transform.position - npc.transform.position).sqrMagnitude;
                    if (distance < best || distance == best && found != null && player.StableId < found.StableId) { best = distance; found = player; }
                }
                if (found != null) { npc.RememberedTarget = found; SetGoal(npc, found.transform.position, AI_STATE.CHASE); }
            }
            else if (npc.AiState == AI_STATE.CHASE || npc.AiState == AI_STATE.ATTACK) SetGoal(npc, npc.Goal, AI_STATE.SEARCH);

            if (npc.AiState == AI_STATE.CHASE)
            {
                Vector3 direction = npc.PathDirection(npc.Goal, CurrentTick);
                npc.Move(direction, CrowdRules.CHASE_SPEED, CrowdRules.TURN_SPEED, CrowdRules.Flat(npc.Goal - npc.transform.position), Runner.DeltaTime);
                if (InAttackRange(npc, npc.RememberedTarget, CrowdRules.AI_RANGE, CrowdRules.AI_HALF_ANGLE))
                {
                    StartAttack(npc, ACTION_STATE.MELEE, CrowdRules.AI_WINDUP, CrowdRules.AI_CYCLE);
                    npc.AiState = AI_STATE.ATTACK;
                }
                return;
            }
            if (npc.AiState != AI_STATE.SEARCH)
            {
                Noise? selected = null;
                float best = float.MaxValue;
                foreach (Noise noise in NOISES)
                {
                    float distance = (npc.transform.position - noise.POSITION).sqrMagnitude;
                    if (distance > noise.RADIUS * noise.RADIUS) continue;
                    if (!selected.HasValue || noise.RADIUS > selected.Value.RADIUS || noise.RADIUS == selected.Value.RADIUS && (distance < best || distance == best && noise.ID < selected.Value.ID)) { selected = noise; best = distance; }
                }
                if (selected.HasValue) SetGoal(npc, selected.Value.POSITION, AI_STATE.INVESTIGATE);
            }
            if (npc.AiState == AI_STATE.IDLE)
            {
                if (CurrentTick < npc.WaitUntilTick) return;
                Vector3 goal;
                if (TrySpawnPoint(npc.transform.position, 8f, out goal) && Vector3.Distance(goal, npc.transform.position) >= 3f) SetGoal(npc, goal, AI_STATE.WANDER);
                else npc.WaitUntilTick = CurrentTick + CrowdRules.Ticks(1f, Runner.DeltaTime);
                return;
            }
            if (npc.WaitingAtGoal)
            {
                npc.Move(Vector3.zero, 0f, CrowdRules.TURN_SPEED, Quaternion.Euler(0f, Runner.DeltaTime * 90f, 0f) * npc.transform.forward, Runner.DeltaTime);
                if (CurrentTick >= npc.WaitUntilTick) { npc.AiState = AI_STATE.IDLE; npc.RememberedTarget = null; npc.WaitUntilTick = CurrentTick + CrowdRules.Ticks(1f, Runner.DeltaTime); }
                return;
            }
            Vector3 movement = npc.PathDirection(npc.Goal, CurrentTick);
            npc.Move(movement, CrowdRules.WALK_SPEED, CrowdRules.TURN_SPEED, movement, Runner.DeltaTime);
            float remaining = npc.RemainingPathDistance();
            if (remaining <= npc.LastPathDistance - 0.1f) { npc.LastPathDistance = remaining; npc.LastProgressTick = CurrentTick; }
            bool failed = CurrentTick - npc.LastProgressTick >= CrowdRules.Ticks(5f, Runner.DeltaTime);
            if (CrowdRules.Flat(npc.Goal - npc.transform.position).sqrMagnitude > 0.16f && !failed) return;
            if (npc.AiState == AI_STATE.WANDER)
            {
                npc.AiState = AI_STATE.IDLE;
                npc.WaitUntilTick = CurrentTick + CrowdRules.Ticks(1f + (float)_random.NextDouble() * 2f, Runner.DeltaTime);
            }
            else
            {
                npc.WaitingAtGoal = true;
                npc.WaitUntilTick = CurrentTick + CrowdRules.Ticks(npc.AiState == AI_STATE.SEARCH ? 3f : 2f, Runner.DeltaTime);
            }
        }
    }
}
