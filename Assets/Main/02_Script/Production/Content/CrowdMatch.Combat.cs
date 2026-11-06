using UnityEngine;

namespace DeadCrowd.Prototype
{
    public sealed partial class CrowdMatch
    {
        private readonly Collider[] BLOCK_OVERLAPS = new Collider[8];
        private readonly RaycastHit[] BLOCK_HITS = new RaycastHit[16];

        private void CaptureBackstabOpening(CrowdActor actor)
        {
            CrowdActor target = SelectMeleeTarget(actor);
            if (target != null && CrowdRules.IsBackstab(target.transform.forward, actor.transform.position - target.transform.position)) actor.BackstabCandidate = target;
        }

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
            Collider[] overlaps = BLOCK_OVERLAPS;
            int overlapCount = Physics.OverlapSphereNonAlloc(start, 0.015f, overlaps, ~0, QueryTriggerInteraction.Ignore);
            if (overlapCount == overlaps.Length)
            {
                overlaps = Physics.OverlapSphere(start, 0.015f, ~0, QueryTriggerInteraction.Ignore);
                overlapCount = overlaps.Length;
            }
            for (int i = 0; i < overlapCount; i++)
            {
                CrowdActor overlap = overlaps[i].GetComponentInParent<CrowdActor>();
                if (overlap == self || overlap != null && !overlap.Alive) continue;
                actor = overlap;
                point = start;
                return true;
            }
            Vector3 direction = end - start;
            RaycastHit[] hits = BLOCK_HITS;
            int hitCount = Physics.RaycastNonAlloc(start, direction.normalized, hits, direction.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (hitCount == hits.Length)
            {
                hits = Physics.RaycastAll(start, direction.normalized, direction.magnitude, ~0, QueryTriggerInteraction.Ignore);
                hitCount = hits.Length;
            }
            float distance = float.MaxValue;
            bool blocked = false;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = hits[i];
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
            Vector3 point = TracePistolShot(shooter, out Vector3 start, out CrowdActor target);
            if (target != null) DAMAGE.Add(new Damage(target, CrowdRules.MAX_HP, DEATH_CAUSE.PISTOL));
            shooter.ShotStart = start;
            shooter.ShotEnd = point;
            shooter.ShotSequence++;
        }

        public Vector3 GetPistolImpact(CrowdActor shooter) => TracePistolShot(shooter, out _, out _);

        private Vector3 TracePistolShot(CrowdActor shooter, out Vector3 start, out CrowdActor target)
        {
            Quaternion rotation = shooter.PistolRotation;
            Vector3 muzzle = shooter.GetPistolMuzzle(rotation);
            if (FirstBlock(shooter.Center, muzzle, shooter, out target, out Vector3 point))
            {
                start = shooter.Center;
                return point;
            }

            start = muzzle;
            FirstBlock(muzzle, muzzle + rotation * Vector3.forward * CrowdRules.GUN_RANGE, shooter, out target, out point);
            return point;
        }

    }
}
