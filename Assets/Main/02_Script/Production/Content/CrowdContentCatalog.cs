using UnityEngine;

namespace DeadCrowd.Content
{
    [CreateAssetMenu(menuName = "DEAD CROWD/Content Catalog")]
    public sealed class CrowdContentCatalog : ScriptableObject
    {
        public CrowdGameSettings GameSettings;
        public CrowdMapProfile Map;
        public CrowdPresentationProfile Presentation;
    }
}
