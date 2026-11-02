using UnityEngine;

namespace DeadCrowd.Content
{
    public sealed class CrowdMapAuthoring : MonoBehaviour
    {
        public CrowdContentCatalog Catalog;
        public CrowdMapProfile Profile => Catalog != null ? Catalog.Map : null;
        private void OnDrawGizmosSelected()
        {
            if (Profile == null) return;
            Gizmos.color = Color.cyan;
            Vector2 center = (Profile.BoundsMin + Profile.BoundsMax) * 0.5f;
            Vector2 size = Profile.BoundsMax - Profile.BoundsMin;
            Gizmos.DrawWireCube(new Vector3(center.x, 0.1f, center.y), new Vector3(size.x, 0.2f, size.y));
            if (Profile.StartSets == null) return;
            foreach (CrowdStartSet set in Profile.StartSets)
            {
                if (set?.Positions == null) continue;
                Gizmos.color = set.Positions.Length == 2 ? Color.yellow : Color.green;
                foreach (Vector3 point in set.Positions)
                {
                    Gizmos.DrawWireSphere(point + Vector3.up * 0.9f, 0.3f);
                    Gizmos.DrawWireSphere(point, Profile.NpcStartRadius);
#if UNITY_EDITOR
                    UnityEditor.Handles.Label(point + Vector3.up * 2f, set.Name);
#endif
                }
            }
        }
    }
}
