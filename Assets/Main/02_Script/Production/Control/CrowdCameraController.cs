using System.Collections.Generic;
using DeadCrowd.Prototype;
using UnityEngine;

namespace DeadCrowd.Control
{
    public sealed class CrowdCameraController
    {
        private readonly List<CrowdActor> SURVIVORS = new List<CrowdActor>();
        public void Follow(Camera viewCamera, CrowdMatch match, CrowdLocalInput input)
        {
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
            }
            Quaternion rotation = Quaternion.Euler(input.Pitch, input.Yaw, 0f);
            Vector3 pivot = target.transform.position + Vector3.up * CrowdRules.CAMERA_HEIGHT;
            bool aim = input.Aiming && target.Alive;
            float distance = aim ? CrowdRules.CAMERA_AIM_DISTANCE : CrowdRules.CAMERA_DISTANCE;
            Vector3 offset = rotation * new Vector3(CrowdRules.CAMERA_SHOULDER, 0f, -distance);
            Vector3 position = pivot + offset;
            if (Physics.SphereCast(pivot, CrowdRules.CAMERA_RADIUS, offset.normalized, out RaycastHit hit, offset.magnitude, CrowdRules.WORLD_MASK)) position = pivot + offset.normalized * Mathf.Max(0.05f, hit.distance - 0.05f);
            viewCamera.transform.SetPositionAndRotation(position, rotation);
            viewCamera.fieldOfView = Mathf.MoveTowards(viewCamera.fieldOfView, aim ? CrowdRules.CAMERA_AIM_FOV : CrowdRules.CAMERA_FOV, Time.deltaTime * 70f);

        }
    }
}
