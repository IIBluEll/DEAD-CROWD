using Fusion;
using UnityEngine;
using UnityEngine.AI;

namespace DeadCrowd.Prototype
{
    public sealed partial class CrowdMatch
    {
        private void CollectAttack(CrowdActor actor)
        {
            if (!actor.HitPending || CurrentTick < actor.HitTick) return;
            actor.HitPending = false;
            CrowdActor openingTarget = actor.BackstabCandidate;
            actor.BackstabCandidate = null;
            if (actor.AttackId == actor.SettledAttackId) return;
            actor.SettledAttackId = actor.AttackId;
            if (actor.IsNpc)
            {
                CrowdActor target = actor.RememberedTarget;
                if (target != null && target.Alive && InAttackRange(actor, target, CrowdRules.AI_RANGE, CrowdRules.AI_HALF_ANGLE)) DAMAGE.Add(new Damage(target, CrowdRules.AI_DAMAGE, DEATH_CAUSE.ZOMBIE));
            }
            else if (actor.Action == ACTION_STATE.MELEE)
            {
                CrowdActor target = SelectMeleeTarget(actor);
                if (target == null) return;
                bool opening = openingTarget == target;
                bool back = CrowdRules.IsMeleeBackstab(target.transform.forward, actor.transform.position - target.transform.position, opening);
                DAMAGE.Add(new Damage(target, back ? CrowdRules.MAX_HP : CrowdRules.MELEE_DAMAGE, back ? DEATH_CAUSE.BACKSTAB : DEATH_CAUSE.MELEE));
                EmitNoise(target.transform.position, CrowdRules.NOISE_HIT);
                Debug.Log($"[Crowd] Melee round={RoundId}, tick={CurrentTick}, attacker={actor.StableId}, target={target.StableId}, back={back}, opening={opening}, targetAngle={Vector3.Angle(CrowdRules.Flat(target.transform.forward), CrowdRules.Flat(actor.transform.position - target.transform.position)):0.0}");
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
            Vector3 pivot = shooter.transform.position + Vector3.up * CrowdRules.CAMERA_HEIGHT;
            Vector3 cameraPosition = pivot - aim * Vector3.forward * CrowdRules.CAMERA_AIM_DISTANCE + aim * Vector3.right * CrowdRules.CAMERA_SHOULDER;
            Vector3 cameraDelta = cameraPosition - pivot;
            if (Physics.SphereCast(pivot, CrowdRules.CAMERA_RADIUS, cameraDelta.normalized, out RaycastHit cameraHit, cameraDelta.magnitude, CrowdRules.WORLD_MASK)) cameraPosition = pivot + cameraDelta.normalized * Mathf.Max(0.05f, cameraHit.distance - 0.05f);
            Vector3 aimPoint = cameraPosition + aim * Vector3.forward * 100f;
            if (FirstBlock(cameraPosition, aimPoint, shooter, out _, out Vector3 hitPoint)) aimPoint = hitPoint;
            Vector3 muzzle = shooter.transform.position + Vector3.up * 1.2f + shooter.transform.forward * 0.4f + shooter.transform.right * 0.2f;
            bool blocked = FirstBlock(shooter.Center, muzzle, shooter, out CrowdActor target, out Vector3 point);
            if (!blocked) FirstBlock(muzzle, muzzle + (aimPoint - muzzle).normalized * CrowdRules.GUN_RANGE, shooter, out target, out point);
            if (target != null) DAMAGE.Add(new Damage(target, CrowdRules.GUN_DAMAGE, DEATH_CAUSE.PISTOL));
            shooter.ShotEnd = point;
            shooter.ShotSequence++;
        }

    }
}
