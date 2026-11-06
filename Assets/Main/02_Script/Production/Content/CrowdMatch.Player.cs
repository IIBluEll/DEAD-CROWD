using Fusion;
using UnityEngine;
using UnityEngine.AI;

namespace DeadCrowd.Prototype
{
    public sealed partial class CrowdMatch
    {
        private void SimulatePlayer(CrowdActor actor, bool boundary, bool authority = true)
        {
            bool hasCurrentInput = Runner.TryGetInputForPlayer(actor.Object.InputAuthority, out CrowdInput input) && input.RoundId == RoundId;
            if (!hasCurrentInput) input = new CrowdInput { Yaw = actor.transform.eulerAngles.y, AttackSequence = actor.LastAttackSequence, ReloadSequence = actor.LastReloadSequence };
            if (!float.IsFinite(input.Yaw) || !float.IsFinite(input.Pitch) || !float.IsFinite(input.Move.x) || !float.IsFinite(input.Move.y)) input = new CrowdInput { Yaw = actor.transform.eulerAngles.y, AttackSequence = actor.LastAttackSequence, ReloadSequence = actor.LastReloadSequence };
            input.Pitch = Mathf.Clamp(input.Pitch, -60f, 30f);
            input.Move = Vector2.ClampMagnitude(input.Move, 1f);
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
            if (actor.Aiming) actor.PistolPitch = input.Pitch;
            Vector3 direction = Quaternion.Euler(0f, input.Yaw, 0f) * new Vector3(input.Move.x, 0f, input.Move.y);
            bool sprint = unlocked && !reloadValid && !attack && !actor.Aiming && input.Sprint && direction.sqrMagnitude > 0f;
            float speed = actor.Action == ACTION_STATE.MELEE || meleeIntent ? CrowdRules.MELEE_SPEED : actor.Aiming ? CrowdRules.AIM_SPEED : sprint ? CrowdRules.SPRINT_SPEED : CrowdRules.WALK_SPEED;
            Vector3 facing = actor.Aiming ? Quaternion.Euler(0f, input.Yaw, 0f) * Vector3.forward : direction;
            Vector3 previous = actor.transform.position;
            actor.Move(direction, speed, actor.Action == ACTION_STATE.MELEE || meleeIntent ? CrowdRules.MELEE_TURN_SPEED : CrowdRules.TURN_SPEED, facing, Runner.DeltaTime);
            if (authority && sprint && (!actor.Exposed || !CrowdRules.EXPOSED_SPRINT_ALLOWS_RECOVERY) && CrowdRules.Flat(actor.transform.position - previous).sqrMagnitude > 0.000001f) AddExposure(actor, CrowdRules.EXP_SPRINT * Runner.DeltaTime);

            if (reloadValid)
            {
                if (authority) { actor.ActionSequence++; actor.ActionEvent = ACTION_STATE.RELOAD; }
                actor.Action = ACTION_STATE.RELOAD;
                actor.Aiming = false;
                actor.ActionEndTick = CurrentTick + CrowdRules.Ticks(CrowdRules.RELOAD_SECONDS, Runner.DeltaTime);
            }
            else if (meleeIntent)
            {
                actor.HitTick = CurrentTick + CrowdRules.Ticks(CrowdRules.MELEE_WINDUP, Runner.DeltaTime);
                actor.Action = ACTION_STATE.MELEE;
                actor.ActionEndTick = CurrentTick + CrowdRules.Ticks(CrowdRules.MELEE_CYCLE, Runner.DeltaTime);
                if (authority)
                {
                    StartAttack(actor, ACTION_STATE.MELEE, CrowdRules.MELEE_WINDUP, CrowdRules.MELEE_CYCLE);
                    AddExposure(actor, CrowdRules.EXP_MELEE);
                    EmitNoise(actor.transform.position, CrowdRules.NOISE_MELEE);
                }
            }
            else if (unlocked && attack && input.Aim && actor.Magazine > 0)
            {
                actor.Action = ACTION_STATE.SHOT;
                actor.ActionEndTick = CurrentTick + CrowdRules.Ticks(CrowdRules.GUN_INTERVAL, Runner.DeltaTime);
                actor.Magazine--;
                if (authority)
                {
                    StartAttack(actor, ACTION_STATE.SHOT, 0f, CrowdRules.GUN_INTERVAL);
                    AddExposure(actor, CrowdRules.EXP_SHOT);
                    EmitNoise(actor.transform.position, CrowdRules.NOISE_SHOT);
                }
            }
        }

        private void CompleteAction(CrowdActor actor)
        {
            if (actor.Action == ACTION_STATE.NONE || CurrentTick < actor.ActionEndTick) return;
            // Quantization can put hit/end on one Tick even when cycle > windup in seconds.
            if (actor.Action == ACTION_STATE.MELEE && CurrentTick == actor.HitTick) return;
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
            actor.AttackId++;
            actor.ActionSequence++;
            actor.ActionEvent = action;
            actor.Action = action;
            actor.HitPending = true;
            actor.HitTick = CurrentTick + CrowdRules.Ticks(windup, Runner.DeltaTime);
            actor.ActionEndTick = CurrentTick + CrowdRules.Ticks(cycle, Runner.DeltaTime);
            actor.BackstabCandidate = null;
            if (!actor.IsNpc && action == ACTION_STATE.MELEE && CrowdRules.BACKSTAB_WINDUP_GRACE) BACKSTAB_OPENINGS.Add(actor);
        }

        private void AddExposure(CrowdActor actor, float amount) { actor.DangerThisTick = true; actor.ExposureAddition += amount; }
        private void EmitNoise(Vector3 position, float radius) { NOISES.Add(new Noise(position, radius, ++_noiseId)); }

    }
}
