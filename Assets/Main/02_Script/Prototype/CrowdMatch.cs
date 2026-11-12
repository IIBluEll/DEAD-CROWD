using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

namespace DeadCrowd.Prototype
{
    public sealed partial class CrowdMatch : NetworkBehaviour
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
        private readonly List<CrowdActor> BACKSTAB_OPENINGS = new List<CrowdActor>();
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

        public int PredictedSteps { get; private set; }
        public int ResimulatedSteps { get; private set; }
        public IReadOnlyList<CrowdActor> Actors => ACTORS;
        public bool Valid => Object != null && Object.IsValid;
        public int CurrentTick => Runner.Tick.Raw;
        public float RemainingSeconds => Mathf.Max(0f, (EndTick - CurrentTick) * Runner.DeltaTime);
        public int ExpectedNpcCount => RequiredPlayers == 4 ? CrowdRules.FFA_NPCS : CrowdRules.DUEL_NPCS;

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
            if (!HasStateAuthority) { Runner.SetIsSimulated(Object, true); return; }
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
            foreach (CrowdActor actor in ACTORS) if (!Practice && !actor.IsNpc && actor.Connected && !actor.Ready) return false;
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
            ResetSearchSites();
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
            if (!HasStateAuthority)
            {
                CrowdActor local = LocalActor();
                if (State == ROUND_STATE.PLAYING && local != null && local.Alive)
                {
                    SimulatePlayer(local, CurrentTick >= EndTick, false);
                    if (Runner.IsResimulation) ResimulatedSteps++; else PredictedSteps++;
                }
                return;
            }
            SynchronizePlayers();
            if (State == ROUND_STATE.LOBBY || State == ROUND_STATE.RESULT) return;
            if (State == ROUND_STATE.LOADING) { UpdateLoading(); return; }
            if (State == ROUND_STATE.COUNTDOWN)
            {
                if (ConnectedCount() != RequiredPlayers) { LastStartFailure = START_FAILURE.PARTICIPANT_LEFT; ReturnToLobby(); return; }
                if (CurrentTick < CountdownEndTick) return;
                State = ROUND_STATE.PLAYING;
                EndTick = CurrentTick + CrowdRules.Ticks(CrowdRules.ROUND_SECONDS, Runner.DeltaTime);
                ResetSearchSites();
                Debug.Log($"[Crowd] Round {RoundId} playing. players={RequiredPlayers}, npc={NpcCount()}, seed={Seed}");
            }

            DAMAGE.Clear();
            bool boundary = CurrentTick >= EndTick;
            if (!boundary) RefillSearchSites();
            foreach (CrowdActor actor in ACTORS)
            {
                if (!actor.Alive) continue;
                actor.DangerThisTick = false;
                actor.ExposureAddition = 0f;
                if (!actor.IsNpc) SimulatePlayer(actor, boundary);
            }
            Physics.SyncTransforms();
            // Use this tick's player poses before NPCs react to attack noise.
            foreach (CrowdActor actor in BACKSTAB_OPENINGS) CaptureBackstabOpening(actor);
            BACKSTAB_OPENINGS.Clear();
            foreach (CrowdActor actor in ACTORS)
            {
                if (!actor.Alive || actor.IsNpc) continue;
                float exposure = actor.Exposure;
                bool exposed = actor.Exposed;
                if (actor.DangerThisTick) actor.LastDangerTick = CurrentTick;
                bool canDecay = CrowdRules.CanDecayExposure(exposed, CurrentTick - actor.LastDangerTick, Runner.DeltaTime);
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
            if (!boundary) SettleSearches();
            AlivePlayers = 0;
            int lastSlot = -1;
            int highScore = -1;
            int scoreWinner = 0;
            foreach (CrowdActor actor in ACTORS)
            {
                if (actor.IsNpc || !actor.Connected || !actor.Alive) continue;
                AlivePlayers++;
                lastSlot = actor.Slot;
                if (actor.SearchScore > highScore) { highScore = actor.SearchScore; scoreWinner = actor.Slot; }
                else if (actor.SearchScore == highScore) scoreWinner = 0;
            }
            int result = Practice ? (AlivePlayers == 0 ? -1 : boundary ? -2 : -3) : CrowdRules.ResolveResult(AlivePlayers, lastSlot, boundary);
            if (result == -2 && !Practice && SearchEnabled && scoreWinner > 0) result = scoreWinner;
            if (result != -3)
            {
                WinnerSlot = result;
                State = ROUND_STATE.RESULT;
                ResetSearchSites();
                foreach (CrowdActor actor in ACTORS) if (!actor.IsNpc) actor.CancelSearch();
                foreach (CrowdActor actor in ACTORS) { actor.HitPending = false; actor.Action = ACTION_STATE.NONE; actor.Aiming = false; }
                Debug.Log($"[Crowd] Result round={RoundId}, winner={result}, alive={AlivePlayers}");
            }
            foreach (CrowdActor actor in ACTORS) if (!actor.IsNpc) actor.ConfirmPresentation();
            for (int i = ACTORS.Count - 1; i >= 0; i--)
                if (ACTORS[i].IsNpc && !ACTORS[i].Alive && CurrentTick - ACTORS[i].DeathTick >= CrowdRules.Ticks(CrowdRules.CORPSE_SECONDS, Runner.DeltaTime)) Runner.Despawn(ACTORS[i].Object);
        }

        private void SynchronizePlayers()
        {
            ACTIVE_PLAYERS.Clear();
            foreach (PlayerRef player in Runner.ActivePlayers) ACTIVE_PLAYERS.Add(player);
            for (int i = ACTORS.Count - 1; i >= 0; i--)
            {
                CrowdActor actor = ACTORS[i];
                if (actor.IsNpc || !actor.Connected || ACTIVE_PLAYERS.Contains(actor.Object.InputAuthority)) continue;
                actor.Connected = false;
                actor.ApplyDamage(CrowdRules.MAX_HP, DEATH_CAUSE.DISCONNECTED, CurrentTick);
                ResetReady();
                if (State == ROUND_STATE.LOBBY) Runner.Despawn(actor.Object);
            }
            if (State != ROUND_STATE.LOBBY) return;
            foreach (PlayerRef player in Runner.ActivePlayers)
            {
                bool exists = false;
                foreach (CrowdActor actor in ACTORS) if (!actor.IsNpc && actor.Connected && actor.Object.InputAuthority == player) { exists = true; break; }
                if (exists) continue;
                int slot = 1;
                while (slot <= RequiredPlayers && SlotUsed(slot)) slot++;
                if (slot > RequiredPlayers) continue;
                Transform spawn = _spawnTrans[(slot - 1) % _spawnTrans.Length];
                int id = ++_nextId;
                NetworkObject obj = Runner.Spawn(_actorPrefab, spawn.position, spawn.rotation, player, (_, spawned) => spawned.GetComponent<CrowdActor>().Initialize(id, slot, false));
                Runner.SetPlayerObject(player, obj);
                ResetReady();
                Debug.Log($"[Crowd] Player joined slot={slot}");
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
            var map = DeadCrowd.Content.CrowdContent.Map;
            int count = ExpectedNpcCount;
            long nearbyCount = (long)RequiredPlayers * map.NearbyNpcsPerPlayer;
            if (nearbyCount > count)
            {
                HostLoadFailed = true;
                LastStartFailure = START_FAILURE.LOAD_FAILED;
                Debug.LogError($"[Content] This mode requires {nearbyCount} nearby NPCs, but only {count} NPCs are configured.");
                return;
            }
            var approved = new List<DeadCrowd.Content.CrowdStartSet>();
            foreach (var set in map.StartSets)
                if (set.Positions.Length == (Practice ? 2 : RequiredPlayers) && ValidateStartSet(set.Positions)) approved.Add(set);
            if (approved.Count == 0) { HostLoadFailed = true; LastStartFailure = START_FAILURE.LOAD_FAILED; Debug.LogError("[Content] No valid approved start set for this mode."); return; }
            Vector3[] positions = (Vector3[])approved[_random.Next(approved.Count)].Positions.Clone();
            for (int i = positions.Length - 1; i > 0; i--) { int j = _random.Next(i + 1); (positions[i], positions[j]) = (positions[j], positions[i]); }
            foreach (CrowdActor actor in ACTORS)
            {
                actor.ResetRound(positions[actor.Slot - 1], Quaternion.Euler(0f, _random.Next(360), 0f));
            }
            // Give every start a nearby crowd before distributing the remaining NPCs.
            var starts = new List<Vector3>();
            foreach (CrowdActor actor in ACTORS) if (!actor.IsNpc && actor.Connected) starts.Add(actor.transform.position);
            for (int i = 0; i < count; i++)
            {
                bool nearStart = i < nearbyCount;
                Vector3 origin = nearStart ? starts[i / map.NearbyNpcsPerPlayer] : starts[0];
                if (!TrySpawnPoint(origin, nearStart ? map.NpcStartRadius : (float?)null, out Vector3 position))
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
            CountdownEndTick = CurrentTick + CrowdRules.Ticks(CrowdRules.COUNTDOWN_SECONDS, Runner.DeltaTime);
            State = ROUND_STATE.COUNTDOWN;
            Debug.Log($"[Crowd] Countdown round={RoundId}, loaded={LoadedCount()}/{RequiredPlayers}");
        }

        private bool ValidateStartSet(Vector3[] positions)
        {
            foreach (Vector3 p in positions)
            {
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 0.1f, NavMesh.AllAreas) || Physics.CheckCapsule(p + Vector3.up * 0.4f, p + Vector3.up * 1.4f, 0.3f, CrowdRules.WORLD_MASK)) return false;
                foreach (Vector3 q in positions)
                {
                    if (p == q) continue;
                    if (Vector3.Distance(p, q) <= CrowdRules.MELEE_RANGE || !Physics.Linecast(p + Vector3.up * 1.55f, q + Vector3.up * 1.55f, CrowdRules.WORLD_MASK)) return false;
                    var path = new NavMeshPath();
                    if (!NavMesh.CalculatePath(p, q, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return false;
                }
            }
            return true;
        }

        private bool TrySpawnPoint(Vector3 origin, float? radius, out Vector3 position)
        {
            var map = DeadCrowd.Content.CrowdContent.Map;
            for (int attempt = 0; attempt < map.SpawnAttempts; attempt++)
            {
                float x = (float)_random.NextDouble();
                float z = (float)_random.NextDouble();
                Vector3 point = radius.HasValue
                    ? origin + new Vector3(Mathf.Lerp(-radius.Value, radius.Value, x), 0f, Mathf.Lerp(-radius.Value, radius.Value, z))
                    : new Vector3(Mathf.Lerp(map.BoundsMin.x, map.BoundsMax.x, x), origin.y, Mathf.Lerp(map.BoundsMin.y, map.BoundsMax.y, z));
                if (!map.ContainsSpawnPosition(point) || !NavMesh.SamplePosition(point, out NavMeshHit hit, 0.8f, NavMesh.AllAreas)) continue;
                point = hit.position;
                if (!map.ContainsSpawnPosition(point) || radius.HasValue && CrowdRules.Flat(point - origin).sqrMagnitude > radius.Value * radius.Value) continue;
                if (Physics.CheckCapsule(point + Vector3.up * 0.4f, point + Vector3.up * 1.4f, 0.32f, CrowdRules.WORLD_MASK)) continue;
                bool overlaps = false;
                foreach (CrowdActor actor in ACTORS) if (actor.Alive && CrowdRules.Flat(actor.transform.position - point).sqrMagnitude < map.BodySeparation * map.BodySeparation) { overlaps = true; break; }
                if (overlaps) continue;
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(origin, point, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                position = point;
                return true;
            }
            position = default;
            return false;
        }

    }
}
