using Fusion;
using UnityEngine;

namespace DeadCrowd.Prototype
{
    public enum ROUND_STATE { LOBBY, COUNTDOWN, PLAYING, RESULT, LOADING }
    public enum START_FAILURE { NONE, PARTICIPANT_LEFT, LOAD_TIMEOUT, LOAD_FAILED }
    public enum ACTION_STATE { NONE, MELEE, SHOT, RELOAD }
    public enum AI_STATE { IDLE, WANDER, INVESTIGATE, CHASE, ATTACK, SEARCH }
    public enum DEATH_CAUSE { NONE, MELEE, BACKSTAB, PISTOL, ZOMBIE, DISCONNECTED }

    public struct CrowdInput : INetworkInput
    {
        public Vector2 Move;
        public float Yaw;
        public float Pitch;
        public int AttackSequence;
        public int ReloadSequence;
        public int RoundId;
        public NetworkBool Aim;
        public NetworkBool Sprint;
        public NetworkBool Interact;
    }

    // Initial values from design v1.0, section 17. This is the prototype's single data source.
    public static class CrowdRules
    {
        public const string BUILD_VERSION = "dead-crowd-development-13";
        public static int LOAD_TIMEOUT_SECONDS => DeadCrowd.Content.CrowdContent.Settings.LOAD_TIMEOUT_SECONDS;
        public const string CODE_ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        public static int MAX_HP => DeadCrowd.Content.CrowdContent.Settings.MAX_HP;
        public static int ROUND_SECONDS => DeadCrowd.Content.CrowdContent.Settings.ROUND_SECONDS;
        public static float WALK_SPEED => DeadCrowd.Content.CrowdContent.Settings.WALK_SPEED;
        public static float SPRINT_SPEED => DeadCrowd.Content.CrowdContent.Settings.SPRINT_SPEED;
        public static float AIM_SPEED => DeadCrowd.Content.CrowdContent.Settings.AIM_SPEED;
        public static float MELEE_SPEED => DeadCrowd.Content.CrowdContent.Settings.MELEE_SPEED;
        public static float CHASE_SPEED => DeadCrowd.Content.CrowdContent.Settings.CHASE_SPEED;
        public static float TURN_SPEED => DeadCrowd.Content.CrowdContent.Settings.TURN_SPEED;
        public static float MELEE_TURN_SPEED => DeadCrowd.Content.CrowdContent.Settings.MELEE_TURN_SPEED;
        public static float MELEE_RANGE => DeadCrowd.Content.CrowdContent.Settings.MELEE_RANGE;
        public static float MELEE_HALF_ANGLE => DeadCrowd.Content.CrowdContent.Settings.MELEE_HALF_ANGLE;
        public static float BACK_HALF_ANGLE => DeadCrowd.Content.CrowdContent.Settings.BACK_HALF_ANGLE;
        public static bool BACKSTAB_WINDUP_GRACE => DeadCrowd.Content.CrowdContent.Settings.BACKSTAB_WINDUP_GRACE;
        public static float MELEE_WINDUP => DeadCrowd.Content.CrowdContent.Settings.MELEE_WINDUP;
        public static float MELEE_CYCLE => DeadCrowd.Content.CrowdContent.Settings.MELEE_CYCLE;
        public static int MELEE_DAMAGE => DeadCrowd.Content.CrowdContent.Settings.MELEE_DAMAGE;
        public static float GUN_RANGE => DeadCrowd.Content.CrowdContent.Settings.GUN_RANGE;
        public static float GUN_INTERVAL => DeadCrowd.Content.CrowdContent.Settings.GUN_INTERVAL;
        public static float RELOAD_SECONDS => DeadCrowd.Content.CrowdContent.Settings.RELOAD_SECONDS;
        public static int MAGAZINE => DeadCrowd.Content.CrowdContent.Settings.MAGAZINE;
        public static int RESERVE => DeadCrowd.Content.CrowdContent.Settings.RESERVE;
        public static float EXP_MELEE => DeadCrowd.Content.CrowdContent.Settings.EXP_MELEE;
        public static float EXP_SHOT => DeadCrowd.Content.CrowdContent.Settings.EXP_SHOT;
        public static float EXP_SPRINT => DeadCrowd.Content.CrowdContent.Settings.EXP_SPRINT;
        public static bool EXPOSED_SPRINT_ALLOWS_RECOVERY => DeadCrowd.Content.CrowdContent.Settings.EXPOSED_SPRINT_ALLOWS_RECOVERY;
        public static float EXP_HIDDEN_DELAY => DeadCrowd.Content.CrowdContent.Settings.EXP_HIDDEN_DELAY;
        public static float EXP_DELAY => DeadCrowd.Content.CrowdContent.Settings.EXP_DELAY;
        public static float EXP_DECAY => DeadCrowd.Content.CrowdContent.Settings.EXP_DECAY;
        public static float SIGHT_RANGE => DeadCrowd.Content.CrowdContent.Settings.SIGHT_RANGE;
        public static float SIGHT_HALF_ANGLE => DeadCrowd.Content.CrowdContent.Settings.SIGHT_HALF_ANGLE;
        public static float AI_RANGE => DeadCrowd.Content.CrowdContent.Settings.AI_RANGE;
        public static float AI_HALF_ANGLE => DeadCrowd.Content.CrowdContent.Settings.AI_HALF_ANGLE;
        public static float AI_WINDUP => DeadCrowd.Content.CrowdContent.Settings.AI_WINDUP;
        public static float AI_CYCLE => DeadCrowd.Content.CrowdContent.Settings.AI_CYCLE;
        public static int AI_DAMAGE => DeadCrowd.Content.CrowdContent.Settings.AI_DAMAGE;
        public static int DUEL_NPCS => DeadCrowd.Content.CrowdContent.Settings.DUEL_NPCS;
        public static int FFA_NPCS => DeadCrowd.Content.CrowdContent.Settings.FFA_NPCS;
        public static float COUNTDOWN_SECONDS => DeadCrowd.Content.CrowdContent.Settings.COUNTDOWN_SECONDS;
        public static float CORPSE_SECONDS => DeadCrowd.Content.CrowdContent.Settings.CORPSE_SECONDS;
        public static float AI_WANDER_RADIUS => DeadCrowd.Content.CrowdContent.Settings.AI_WANDER_RADIUS;
        public static float AI_WANDER_MIN => DeadCrowd.Content.CrowdContent.Settings.AI_WANDER_MIN;
        public static float AI_WAIT_MIN => DeadCrowd.Content.CrowdContent.Settings.AI_WAIT_MIN;
        public static float AI_WAIT_MAX => DeadCrowd.Content.CrowdContent.Settings.AI_WAIT_MAX;
        public static float AI_SEARCH_SECONDS => DeadCrowd.Content.CrowdContent.Settings.AI_SEARCH_SECONDS;
        public static float AI_INVESTIGATE_SECONDS => DeadCrowd.Content.CrowdContent.Settings.AI_INVESTIGATE_SECONDS;
        public static float AI_MOVE_FAIL_SECONDS => DeadCrowd.Content.CrowdContent.Settings.AI_MOVE_FAIL_SECONDS;
        public static float AI_PROGRESS_DELTA => DeadCrowd.Content.CrowdContent.Settings.AI_PROGRESS_DELTA;
        public static float AI_SCAN_TURN => DeadCrowd.Content.CrowdContent.Settings.AI_SCAN_TURN;
        public static float PATH_REFRESH_SECONDS => DeadCrowd.Content.CrowdContent.Settings.PATH_REFRESH_SECONDS;
        public static float NOISE_MELEE => DeadCrowd.Content.CrowdContent.Settings.NOISE_MELEE;
        public static float NOISE_HIT => DeadCrowd.Content.CrowdContent.Settings.NOISE_HIT;
        public static float NOISE_SHOT => DeadCrowd.Content.CrowdContent.Settings.NOISE_SHOT;
        public static float CAMERA_HEIGHT => DeadCrowd.Content.CrowdContent.Settings.CAMERA_HEIGHT;
        public static float CAMERA_SHOULDER => DeadCrowd.Content.CrowdContent.Settings.CAMERA_SHOULDER;
        public static float CAMERA_DISTANCE => DeadCrowd.Content.CrowdContent.Settings.CAMERA_DISTANCE;
        public static float CAMERA_AIM_DISTANCE => DeadCrowd.Content.CrowdContent.Settings.CAMERA_AIM_DISTANCE;
        public static float CAMERA_RADIUS => DeadCrowd.Content.CrowdContent.Settings.CAMERA_RADIUS;
        public static float CAMERA_FOV => DeadCrowd.Content.CrowdContent.Settings.CAMERA_FOV;
        public static float CAMERA_AIM_FOV => DeadCrowd.Content.CrowdContent.Settings.CAMERA_AIM_FOV;
        public const int WORLD_MASK = Physics.DefaultRaycastLayers;

        public static int Ticks(float seconds, float deltaTime) => Mathf.CeilToInt(seconds / deltaTime);
        public static Vector3 Flat(Vector3 value) => new Vector3(value.x, 0f, value.z);

        public static bool WithinCone(Vector3 forward, Vector3 direction, float halfAngle)
        {
            direction = Flat(direction);
            return direction.sqrMagnitude > 0.000001f && Vector3.Dot(Flat(forward).normalized, direction.normalized) >= Mathf.Cos(halfAngle * Mathf.Deg2Rad) - 0.000001f;
        }

        public static bool IsBackstab(Vector3 targetForward, Vector3 targetToAttacker) => WithinCone(-targetForward, targetToAttacker, BACK_HALF_ANGLE);

        public static bool IsMeleeBackstab(Vector3 targetForward, Vector3 targetToAttacker, bool sameOpeningTarget)
        {
            return IsBackstab(targetForward, targetToAttacker) || BACKSTAB_WINDUP_GRACE && sameOpeningTarget;
        }

        public static bool CanDecayExposure(bool exposed, int ticksSinceDanger, float deltaTime)
        {
            return ticksSinceDanger >= Ticks(exposed ? EXP_DELAY : EXP_HIDDEN_DELAY, deltaTime);
        }

        public static void UpdateExposure(ref float exposure, ref bool exposed, bool danger, bool canDecay, float addition, float deltaTime)
        {
            if (danger) exposure = Mathf.Min(100f, exposure + addition);
            else if (canDecay) exposure = Mathf.Max(0f, exposure - EXP_DECAY * deltaTime);
            if (exposure >= 100f) exposed = true;
            if (exposure <= 0f) exposed = false;
        }

        public static int ExposureDisplay(float exposure) => exposure <= 0f ? 0 : Mathf.Max(1, Mathf.FloorToInt(exposure));

        public static int ReloadAmount(int magazine, int reserve) => Mathf.Min(MAGAZINE - magazine, reserve);

        // Winner: a slot, -1 for mutual elimination, -2 for timeout, -3 while unresolved.
        public static int ResolveResult(int alive, int lastSlot, bool timedOut)
        {
            if (alive == 0) return -1;
            if (alive == 1) return lastSlot;
            return timedOut ? -2 : -3;
        }
    }
}
