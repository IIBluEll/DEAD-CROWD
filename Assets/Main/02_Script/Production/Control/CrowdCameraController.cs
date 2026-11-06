using System.Collections.Generic;
using DeadCrowd.Prototype;
using UnityEngine;

namespace DeadCrowd.Control
{
    public sealed class CrowdCameraController
    {
        private readonly List<CrowdActor> SURVIVORS = new List<CrowdActor>();
        public int SpectatedSlot { get; private set; }
        public Vector2? PistolImpactViewport { get; private set; }

        public void Follow(Camera viewCamera, CrowdMatch match, CrowdLocalInput input)
        {
            SpectatedSlot = 0;
            PistolImpactViewport = null;
            int spectatorIndex = input.SpectatorIndex;
            if (viewCamera == null) return;
            if (match == null) return;
            CrowdActor target = match.LocalActor();
            if (target == null) return;
            if (!target.Alive)
            {
                var survivors = SURVIVORS;
                survivors.Clear();
                foreach (CrowdActor actor in match.Actors) if (!actor.IsNpc && actor.Connected && actor.Alive) survivors.Add(actor);
                if (survivors.Count == 0) return;
                spectatorIndex = (spectatorIndex % survivors.Count + survivors.Count) % survivors.Count;
                target = survivors[spectatorIndex];
                SpectatedSlot = target.Slot;
            }
            Quaternion rotation = Quaternion.Euler(input.Pitch, input.Yaw, 0f);
            bool aim = input.Aiming && target.Alive;
            viewCamera.transform.SetPositionAndRotation(GetCameraPosition(target.transform.position, rotation, aim), rotation);
            viewCamera.fieldOfView = Mathf.MoveTowards(viewCamera.fieldOfView, aim ? CrowdRules.CAMERA_AIM_FOV : CrowdRules.CAMERA_FOV, Time.deltaTime * 70f);

            if (!aim || input.MenuOpen || SpectatedSlot != 0 || !target.Aiming || match.State != ROUND_STATE.PLAYING) return;
            Physics.SyncTransforms();
            Vector3 viewport = viewCamera.WorldToViewportPoint(match.GetPistolImpact(target));
            if (viewport.z > viewCamera.nearClipPlane && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f)
                PistolImpactViewport = new Vector2(viewport.x, viewport.y);
        }

        private static Vector3 GetCameraPosition(Vector3 targetPosition, Quaternion rotation, bool aim)
        {
            Vector3 pivot = targetPosition + Vector3.up * CrowdRules.CAMERA_HEIGHT;
            float distance = aim ? CrowdRules.CAMERA_AIM_DISTANCE : CrowdRules.CAMERA_DISTANCE;
            Vector3 offset = rotation * new Vector3(CrowdRules.CAMERA_SHOULDER, 0f, -distance);
            if (Physics.SphereCast(pivot, CrowdRules.CAMERA_RADIUS, offset.normalized, out RaycastHit hit, offset.magnitude, CrowdRules.WORLD_MASK)) return pivot + offset.normalized * Mathf.Max(0.05f, hit.distance - 0.05f);
            return pivot + offset;
        }
    }
}
