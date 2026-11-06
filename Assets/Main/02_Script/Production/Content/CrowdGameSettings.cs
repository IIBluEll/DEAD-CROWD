using System;
using UnityEngine;

namespace DeadCrowd.Content
{
    [CreateAssetMenu(menuName = "DEAD CROWD/Game Settings")]
    public sealed class CrowdGameSettings : ScriptableObject
    {
        [Header("Session")]
        [Min(0)] public int LOAD_TIMEOUT_SECONDS = 30;
        [Header("Health and round")]
        [Min(0)] public int MAX_HP = 100;
        [Min(0)] public int ROUND_SECONDS = 360;
        [Header("Movement")]
        [Min(0)] public float WALK_SPEED = 2.5f;
        [Min(0)] public float SPRINT_SPEED = 6f;
        [Min(0)] public float AIM_SPEED = 1.5f;
        [Min(0)] public float MELEE_SPEED = 1.25f;
        [Min(0)] public float CHASE_SPEED = 3.8f;
        [Min(0)] public float TURN_SPEED = 360f;
        [Min(0)] public float MELEE_TURN_SPEED = 180f;
        [Header("Melee")]
        [Min(0)] public float MELEE_RANGE = 1.3f;
        [Min(0)] public float MELEE_HALF_ANGLE = 45f;
        [Range(0f, 180f)] public float BACK_HALF_ANGLE = 80f;
        [Tooltip("Keep the backstab opening for the same target during this melee windup. Range, facing and obstruction are still checked at impact.")]
        public bool BACKSTAB_WINDUP_GRACE = true;
        [Min(0)] public float MELEE_WINDUP = 0.3f;
        [Min(0)] public float MELEE_CYCLE = 0.85f;
        [Min(0)] public int MELEE_DAMAGE = 60;
        [Header("Pistol")]
        [Min(0)] public float GUN_RANGE = 25f;
        [Min(0)] public float GUN_INTERVAL = 0.35f;
        [Min(0)] public float RELOAD_SECONDS = 1.8f;
        [Min(0)] public int MAGAZINE = 6;
        [Min(0)] public int RESERVE = 12;
        [Header("Exposure")]
        [Min(0)] public float EXP_MELEE = 35f;
        [Min(0)] public float EXP_SHOT = 60f;
        [Min(0)] public float EXP_SPRINT = 20f;
        [Tooltip("While already exposed, sprinting does not add exposure or postpone recovery. Attacks still do.")]
        public bool EXPOSED_SPRINT_ALLOWS_RECOVERY = true;
        [Tooltip("Seconds without danger before exposure starts falling while still hidden.")]
        [Min(0)] public float EXP_HIDDEN_DELAY = 4f;
        [Tooltip("Seconds without danger before exposure starts falling after being exposed. Applies until exposure reaches zero.")]
        [Min(0)] public float EXP_DELAY = 2f;
        [Min(0)] public float EXP_DECAY = 15f;
        [Header("NPC vision and attack")]
        [Min(0)] public float SIGHT_RANGE = 8f;
        [Min(0)] public float SIGHT_HALF_ANGLE = 60f;
        [Min(0)] public float AI_RANGE = 1.2f;
        [Min(0)] public float AI_HALF_ANGLE = 60f;
        [Min(0)] public float AI_WINDUP = 0.4f;
        [Min(0)] public float AI_CYCLE = 1.2f;
        [Min(0)] public int AI_DAMAGE = 20;
        [Header("Population and lifecycle")]
        [Min(0)] public int DUEL_NPCS = 20;
        [Min(0)] public int FFA_NPCS = 30;
        [Min(0)] public float COUNTDOWN_SECONDS = 3f;
        [Min(0)] public float CORPSE_SECONDS = 20f;
        [Header("NPC navigation and memory")]
        [Min(0)] public float AI_WANDER_RADIUS = 8f;
        [Min(0)] public float AI_WANDER_MIN = 3f;
        [Min(0)] public float AI_WAIT_MIN = 1f;
        [Min(0)] public float AI_WAIT_MAX = 3f;
        [Min(0)] public float AI_SEARCH_SECONDS = 3f;
        [Min(0)] public float AI_INVESTIGATE_SECONDS = 2f;
        [Min(0)] public float AI_MOVE_FAIL_SECONDS = 5f;
        [Min(0)] public float AI_PROGRESS_DELTA = 0.1f;
        [Min(0)] public float AI_SCAN_TURN = 90f;
        [Min(0)] public float PATH_REFRESH_SECONDS = 0.25f;
        [Header("Gameplay noise radii")]
        [Min(0)] public float NOISE_MELEE = 3f;
        [Min(0)] public float NOISE_HIT = 6f;
        [Min(0)] public float NOISE_SHOT = 18f;
        [Header("Camera")]
        [Min(0)] public float CAMERA_HEIGHT = 1.5f;
        [Min(0)] public float CAMERA_SHOULDER = 0.45f;
        [Min(0)] public float CAMERA_DISTANCE = 3.5f;
        [Min(0)] public float CAMERA_AIM_DISTANCE = 2.2f;
        [Min(0)] public float CAMERA_RADIUS = 0.2f;
        [Min(0)] public float CAMERA_FOV = 70f;
        [Min(0)] public float CAMERA_AIM_FOV = 60f;
        public void Validate()
        {
            foreach (var field in typeof(CrowdGameSettings).GetFields())
            {
                if (field.FieldType != typeof(float) && field.FieldType != typeof(int)) continue;
                double value = Convert.ToDouble(field.GetValue(this));
                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0) throw new InvalidOperationException("Invalid setting: " + field.Name);
            }
            if (MAX_HP < 1 || ROUND_SECONDS < 1 || LOAD_TIMEOUT_SECONDS < 1 || MAGAZINE < 1 || AI_WAIT_MAX < AI_WAIT_MIN || AI_WANDER_RADIUS < AI_WANDER_MIN || MELEE_CYCLE <= MELEE_WINDUP || AI_CYCLE <= AI_WINDUP || GUN_INTERVAL <= 0f || RELOAD_SECONDS <= 0f || AI_MOVE_FAIL_SECONDS <= 0f || AI_PROGRESS_DELTA <= 0f || COUNTDOWN_SECONDS <= 0f || CAMERA_RADIUS <= 0f || CAMERA_DISTANCE <= 0f || CAMERA_AIM_DISTANCE <= 0f || PATH_REFRESH_SECONDS <= 0f) throw new InvalidOperationException("Invalid durations, ammunition or AI ranges.");
            if (MELEE_HALF_ANGLE > 180f || BACK_HALF_ANGLE > 180f || AI_HALF_ANGLE > 180f || SIGHT_HALF_ANGLE > 180f || CAMERA_FOV < 1f || CAMERA_FOV > 179f || CAMERA_AIM_FOV < 1f || CAMERA_AIM_FOV > 179f) throw new InvalidOperationException("Invalid angle/FOV.");
        }
    }
}
