using Fusion;
using UnityEngine;
using UnityEngine.AI;

namespace DeadCrowd.Prototype
{
    public sealed partial class CrowdMatch
    {
        private bool CanSee(CrowdActor npc, CrowdActor target)
        {
            if (target == null || !target.Alive || !target.Connected) return false;
            Vector3 delta = CrowdRules.Flat(target.transform.position - npc.transform.position);
            return delta.sqrMagnitude <= CrowdRules.SIGHT_RANGE * CrowdRules.SIGHT_RANGE && CrowdRules.WithinCone(npc.transform.forward, delta, CrowdRules.SIGHT_HALF_ANGLE) && !Physics.Linecast(npc.transform.position + Vector3.up * 1.55f, target.Center, CrowdRules.WORLD_MASK);
        }

        private void SetGoal(CrowdActor npc, Vector3 goal, AI_STATE state)
        {
            bool continuingChase = state == AI_STATE.CHASE && (npc.AiState == AI_STATE.CHASE || npc.AiState == AI_STATE.ATTACK);
            npc.Goal = goal;
            npc.AiState = state;
            npc.WaitingAtGoal = false;
            if (!continuingChase)
            {
                npc.LastProgressTick = CurrentTick;
                npc.ProgressPosition = npc.transform.position;
                npc.LastPathDistance = float.MaxValue;
            }
        }

        private void SimulateNpc(CrowdActor npc, bool boundary)
        {
            CompleteAction(npc);
            if (boundary) return;
            if (npc.RememberedTarget != null && (!npc.RememberedTarget.Alive || !npc.RememberedTarget.Connected)) { npc.RememberedTarget = null; npc.AiState = AI_STATE.IDLE; npc.RecoveryUntilTick = 0; }
            bool seesTarget = CurrentTick >= npc.RecoveryUntilTick && CanSee(npc, npc.RememberedTarget);
            if (npc.Action == ACTION_STATE.MELEE)
            {
                if (seesTarget) npc.Goal = npc.RememberedTarget.transform.position;
                npc.LastProgressTick = CurrentTick;
                npc.ProgressPosition = npc.transform.position;
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
                else if (NpcMovementFailed(npc, true))
                {
                    SetGoal(npc, npc.transform.position, AI_STATE.SEARCH);
                    npc.WaitingAtGoal = true;
                    npc.WaitUntilTick = CurrentTick + CrowdRules.Ticks(CrowdRules.AI_SEARCH_SECONDS, Runner.DeltaTime);
                    npc.RecoveryUntilTick = CurrentTick + CrowdRules.Ticks(CrowdRules.AI_WAIT_MIN, Runner.DeltaTime);
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
                if (TrySpawnPoint(npc.transform.position, CrowdRules.AI_WANDER_RADIUS, out goal) && Vector3.Distance(goal, npc.transform.position) >= CrowdRules.AI_WANDER_MIN) SetGoal(npc, goal, AI_STATE.WANDER);
                else npc.WaitUntilTick = CurrentTick + CrowdRules.Ticks(CrowdRules.AI_WAIT_MIN, Runner.DeltaTime);
                return;
            }
            if (npc.WaitingAtGoal)
            {
                npc.Move(Vector3.zero, 0f, CrowdRules.TURN_SPEED, Quaternion.Euler(0f, Runner.DeltaTime * CrowdRules.AI_SCAN_TURN, 0f) * npc.transform.forward, Runner.DeltaTime);
                if (CurrentTick >= npc.WaitUntilTick) { npc.AiState = AI_STATE.IDLE; npc.RememberedTarget = null; npc.WaitUntilTick = CurrentTick + CrowdRules.Ticks(CrowdRules.AI_WAIT_MIN, Runner.DeltaTime); }
                return;
            }
            Vector3 movement = npc.PathDirection(npc.Goal, CurrentTick);
            npc.Move(movement, CrowdRules.WALK_SPEED, CrowdRules.TURN_SPEED, movement, Runner.DeltaTime);
            bool failed = NpcMovementFailed(npc, false);
            if (CrowdRules.Flat(npc.Goal - npc.transform.position).sqrMagnitude > 0.16f && !failed) return;
            if (npc.AiState == AI_STATE.WANDER)
            {
                npc.AiState = AI_STATE.IDLE;
                npc.WaitUntilTick = CurrentTick + CrowdRules.Ticks(CrowdRules.AI_WAIT_MIN + (float)_random.NextDouble() * (CrowdRules.AI_WAIT_MAX - CrowdRules.AI_WAIT_MIN), Runner.DeltaTime);
            }
            else
            {
                npc.WaitingAtGoal = true;
                npc.WaitUntilTick = CurrentTick + CrowdRules.Ticks(npc.AiState == AI_STATE.SEARCH ? CrowdRules.AI_SEARCH_SECONDS : CrowdRules.AI_INVESTIGATE_SECONDS, Runner.DeltaTime);
            }
        }
        private bool NpcMovementFailed(CrowdActor npc, bool movingTarget)
        {
            float remaining = npc.RemainingPathDistance();
            bool progressed = movingTarget ? CrowdRules.Flat(npc.transform.position - npc.ProgressPosition).sqrMagnitude >= CrowdRules.AI_PROGRESS_DELTA * CrowdRules.AI_PROGRESS_DELTA : remaining <= npc.LastPathDistance - CrowdRules.AI_PROGRESS_DELTA;
            if (progressed)
            {
                npc.LastProgressTick = CurrentTick;
                npc.ProgressPosition = npc.transform.position;
                npc.LastPathDistance = remaining;
            }
            return CurrentTick - npc.LastProgressTick >= CrowdRules.Ticks(CrowdRules.AI_MOVE_FAIL_SECONDS, Runner.DeltaTime);
        }
    }
}
