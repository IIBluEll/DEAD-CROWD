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
    }

    // Initial values from design v1.0, section 17. This is the prototype's single data source.
    public static class CrowdRules
    {
        public const string BUILD_VERSION = "dead-crowd-development-2";
        public const int LOAD_TIMEOUT_SECONDS = 30;
        public const string CODE_ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        public const int MAX_HP = 100;
        public const int ROUND_SECONDS = 360;
        public const float WALK_SPEED = 2.5f;
        public const float SPRINT_SPEED = 5f;
        public const float AIM_SPEED = 1.5f;
        public const float MELEE_SPEED = 1.25f;
        public const float CHASE_SPEED = 3.8f;
        public const float TURN_SPEED = 360f;
        public const float MELEE_TURN_SPEED = 180f;
        public const float MELEE_RANGE = 1.3f;
        public const float MELEE_HALF_ANGLE = 45f;
        public const float BACK_HALF_ANGLE = 50f;
        public const float MELEE_WINDUP = 0.3f;
        public const float MELEE_CYCLE = 0.85f;
        public const int MELEE_DAMAGE = 60;
        public const int GUN_DAMAGE = 55;
        public const float GUN_RANGE = 25f;
        public const float GUN_INTERVAL = 0.35f;
        public const float RELOAD_SECONDS = 1.8f;
        public const int MAGAZINE = 6;
        public const int RESERVE = 12;
        public const float AIM_TOLERANCE = 5f;
        public const float EXP_MELEE = 35f;
        public const float EXP_SHOT = 60f;
        public const float EXP_SPRINT = 20f;
        public const float EXP_DELAY = 2f;
        public const float EXP_DECAY = 15f;
        public const float SIGHT_RANGE = 8f;
        public const float SIGHT_HALF_ANGLE = 60f;
        public const float AI_RANGE = 1.2f;
        public const float AI_HALF_ANGLE = 60f;
        public const float AI_WINDUP = 0.4f;
        public const float AI_CYCLE = 1.2f;
        public const int AI_DAMAGE = 20;
        public const int WORLD_MASK = Physics.DefaultRaycastLayers;

        public static int Ticks(float seconds, float deltaTime) => Mathf.CeilToInt(seconds / deltaTime);
        public static Vector3 Flat(Vector3 value) => new Vector3(value.x, 0f, value.z);

        public static bool WithinCone(Vector3 forward, Vector3 direction, float halfAngle)
        {
            direction = Flat(direction);
            return direction.sqrMagnitude > 0.000001f && Vector3.Dot(Flat(forward).normalized, direction.normalized) >= Mathf.Cos(halfAngle * Mathf.Deg2Rad) - 0.000001f;
        }

        public static bool IsBackstab(Vector3 targetForward, Vector3 targetToAttacker) => WithinCone(-targetForward, targetToAttacker, BACK_HALF_ANGLE);

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
