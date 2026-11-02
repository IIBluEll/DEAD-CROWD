using System;
using UnityEngine;

namespace DeadCrowd.Content
{
    [Serializable]
    public sealed class CrowdStartSet
    {
        public string Name;
        public Vector3[] Positions;
    }

    [CreateAssetMenu(menuName = "DEAD CROWD/Map Profile")]
    public sealed class CrowdMapProfile : ScriptableObject
    {
        public string MapVersion = "city-1";
        public Vector2 BoundsMin = new Vector2(-18.5f, -18.5f);
        public Vector2 BoundsMax = new Vector2(18.5f, 18.5f);
        [Min(1)] public int SpawnAttempts = 100;
        [Min(0.1f)] public float NpcStartRadius = 7f;
        [Min(0)] public int NearbyNpcsPerPlayer = 3;
        [Min(0.6f)] public float BodySeparation = 0.65f;
        public CrowdStartSet[] StartSets;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(MapVersion) || BoundsMax.x <= BoundsMin.x || BoundsMax.y <= BoundsMin.y || SpawnAttempts < 1 || BodySeparation < 0.6f || NpcStartRadius <= 0f || NearbyNpcsPerPlayer < 0) throw new InvalidOperationException("Invalid map bounds or spawn settings.");
            if (!float.IsFinite(BoundsMin.x) || !float.IsFinite(BoundsMin.y) || !float.IsFinite(BoundsMax.x) || !float.IsFinite(BoundsMax.y) || !float.IsFinite(BodySeparation) || !float.IsFinite(NpcStartRadius)) throw new InvalidOperationException("Map values must be finite.");
            bool duel = false, ffa = false;
            if (StartSets == null) throw new InvalidOperationException("Missing approved start sets.");
            foreach (CrowdStartSet set in StartSets)
            {
                if (set == null || set.Positions == null || set.Positions.Length != 2 && set.Positions.Length != 4) throw new InvalidOperationException("Start sets require 2 or 4 positions.");
                duel |= set.Positions.Length == 2; ffa |= set.Positions.Length == 4;
                for (int i = 0; i < set.Positions.Length; i++)
                    for (int j = i + 1; j < set.Positions.Length; j++)
                        if (Vector3.Distance(set.Positions[i], set.Positions[j]) < BodySeparation) throw new InvalidOperationException("Overlapping starts in " + set.Name);
                foreach (Vector3 p in set.Positions) if (!float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z) || p.x < BoundsMin.x || p.x > BoundsMax.x || p.z < BoundsMin.y || p.z > BoundsMax.y) throw new InvalidOperationException("Start outside map bounds.");
            }
            if (!duel || !ffa) throw new InvalidOperationException("Provide both duel and FFA start sets.");
        }
    }
}
