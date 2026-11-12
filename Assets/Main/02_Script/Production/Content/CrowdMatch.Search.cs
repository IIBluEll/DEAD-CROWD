using System.Globalization;
using System.Text;
using DeadCrowd.UI;
using Fusion;
using TMPro;
using UnityEngine;

namespace DeadCrowd.Prototype
{
    public struct CrowdSearchSiteState : INetworkStruct
    {
        public int SiteId;
        public int Charges;
        public int RefillTick;
    }

    public sealed partial class CrowdMatch
    {
        public const float SEARCH_SECONDS = 3f;
        private const int SEARCH_SITE_COUNT = 3;
        private const int SEARCH_SITE_CHARGES = 2;
        private const float SEARCH_REFILL_SECONDS = 12f;
        private const float SEARCH_RANGE = 1.6f;
        private const float SITE_SEPARATION = 18f;
        private const float SPAWN_CLEARANCE = 0.9f;
        private const float SEARCH_MARKER_RANGE = 48f;

        [SerializeField] private GameObject[] _searchSiteObjs;
        [SerializeField] private TMP_FontAsset _searchMarkerFont;
        [Networked, Capacity(SEARCH_SITE_COUNT)] private NetworkArray<CrowdSearchSiteState> SearchSites => default;
        private TextMeshPro[] _searchMarkerTxts;

        public bool SearchEnabled => _searchSiteObjs != null && _searchSiteObjs.Length > SEARCH_SITE_COUNT;

        internal string SearchLayoutSignature()
        {
            if (!SearchEnabled) return string.Empty;
            var layout = new StringBuilder();
            foreach (GameObject siteObj in _searchSiteObjs)
                layout.Append(siteObj.name).Append(siteObj.transform.localToWorldMatrix.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            return layout.ToString();
        }

        public bool TryGetSearchTarget(CrowdActor actor, out int charges)
        {
            charges = 0;
            if (!SearchEnabled || State != ROUND_STATE.PLAYING || !actor.Alive) return false;
            int slot = FindSearchSlot(actor);
            if (slot < 0) return false;
            charges = SearchSites[slot].Charges;
            return true;
        }

        public bool TryGetNearestSearchOffset(Vector3 position, out Vector3 offset)
        {
            offset = Vector3.zero;
            if (!SearchEnabled || State != ROUND_STATE.PLAYING) return false;
            float bestDistance = float.PositiveInfinity;
            for (int slot = 0; slot < SEARCH_SITE_COUNT; slot++)
            {
                CrowdSearchSiteState site = SearchSites[slot];
                if (site.Charges == 0) continue;
                Vector3 candidate = CrowdRules.Flat(_searchSiteObjs[site.SiteId - 1].transform.position - position);
                if (candidate.sqrMagnitude >= bestDistance) continue;
                bestDistance = candidate.sqrMagnitude;
                offset = candidate;
            }
            return float.IsFinite(bestDistance);
        }

        private int FindSearchSlot(CrowdActor actor)
        {
            float bestDistance = SEARCH_RANGE * SEARCH_RANGE;
            int bestSlot = -1;
            for (int slot = 0; slot < SEARCH_SITE_COUNT; slot++)
            {
                CrowdSearchSiteState site = SearchSites[slot];
                if (site.Charges == 0) continue;
                Transform siteTrans = _searchSiteObjs[site.SiteId - 1].transform;
                Vector3 delta = actor.transform.position - siteTrans.position;
                Vector3 flat = CrowdRules.Flat(delta);
                float distance = flat.sqrMagnitude;
                if (distance > bestDistance || distance < 0.04f || Mathf.Abs(delta.y) > 0.65f) continue;
                if (Vector3.Dot(siteTrans.forward, flat.normalized) < 0.3f) continue;
                if (Physics.Linecast(actor.Center, siteTrans.position + Vector3.up * 0.7f, out RaycastHit hit, CrowdRules.WORLD_MASK, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(siteTrans)) continue;
                bestSlot = slot;
                bestDistance = distance;
            }
            return bestSlot;
        }

        private void UpdateSearchIntent(CrowdActor actor, bool wantsSearch, bool keyHeld)
        {
            if (!SearchEnabled) return;
            if (!keyHeld) actor.SearchInterrupted = false;
            int slot = wantsSearch && !actor.SearchInterrupted ? FindSearchSlot(actor) : -1;
            if (slot < 0)
            {
                if (actor.SearchSiteId != 0) actor.CancelSearch();
                return;
            }
            int siteId = SearchSites[slot].SiteId;
            if (actor.SearchSiteId == siteId) return;
            actor.SearchSiteId = siteId;
            actor.SearchEndTick = CurrentTick + CrowdRules.Ticks(SEARCH_SECONDS, Runner.DeltaTime);
        }

        private void SettleSearches()
        {
            if (!SearchEnabled) return;
            bool depleted = false;
            foreach (CrowdActor actor in ACTORS)
            {
                if (actor.IsNpc || !actor.Alive || actor.SearchSiteId == 0 || CurrentTick < actor.SearchEndTick) continue;
                int slot = FindSearchSlot(actor);
                if (slot < 0 || SearchSites[slot].SiteId != actor.SearchSiteId)
                {
                    actor.CancelSearch();
                    continue;
                }
                CrowdSearchSiteState site = SearchSites[slot];
                actor.SearchScore++;
                site.Charges--;
                if (site.Charges == 0)
                {
                    site.RefillTick = CurrentTick + CrowdRules.Ticks(SEARCH_REFILL_SECONDS, Runner.DeltaTime);
                    depleted = true;
                    foreach (CrowdActor other in ACTORS) if (other.SearchSiteId == site.SiteId) other.CancelSearch();
                }
                else actor.SearchEndTick = CurrentTick + CrowdRules.Ticks(SEARCH_SECONDS, Runner.DeltaTime);
                SearchSites.Set(slot, site);
            }
            if (depleted) ApplySearchSiteObjects();
        }

        private void RefillSearchSites()
        {
            if (!SearchEnabled) return;
            bool changed = false;
            for (int slot = 0; slot < SEARCH_SITE_COUNT; slot++)
            {
                CrowdSearchSiteState previous = SearchSites[slot];
                if (previous.Charges > 0 || CurrentTick < previous.RefillTick) continue;
                int siteId = SelectSearchSite(previous.SiteId, SITE_SEPARATION);
                if (siteId == 0) siteId = SelectSearchSite(previous.SiteId, 0f);
                if (siteId == 0) continue;
                SearchSites.Set(slot, new CrowdSearchSiteState { SiteId = siteId, Charges = SEARCH_SITE_CHARGES });
                changed = true;
            }
            if (changed)
            {
                ApplySearchSiteObjects();
                Physics.SyncTransforms();
            }
        }

        private int SelectSearchSite(int previousId, float separation)
        {
            int selected = 0;
            int eligible = 0;
            for (int id = 1; id <= _searchSiteObjs.Length; id++)
            {
                Vector3 position = _searchSiteObjs[id - 1].transform.position;
                if (previousId > 0 && (position - _searchSiteObjs[previousId - 1].transform.position).sqrMagnitude < SITE_SEPARATION * SITE_SEPARATION) continue;
                bool available = true;
                for (int slot = 0; slot < SEARCH_SITE_COUNT; slot++)
                {
                    CrowdSearchSiteState active = SearchSites[slot];
                    if (active.Charges == 0) continue;
                    Vector3 offset = _searchSiteObjs[active.SiteId - 1].transform.position - position;
                    if (id == active.SiteId || offset.sqrMagnitude < separation * separation) { available = false; break; }
                }
                if (!available) continue;
                foreach (CrowdActor actor in ACTORS)
                    if (actor.Alive && (actor.transform.position - position).sqrMagnitude < SPAWN_CLEARANCE * SPAWN_CLEARANCE) { available = false; break; }
                if (available && _random.Next(++eligible) == 0) selected = id;
            }
            return selected;
        }

        private void ResetSearchSites()
        {
            if (!SearchEnabled) return;
            for (int slot = 0; slot < SEARCH_SITE_COUNT; slot++) SearchSites.Set(slot, default);
            ApplySearchSiteObjects();
        }

        private void ApplySearchSiteObjects()
        {
            for (int i = 0; i < _searchSiteObjs.Length; i++)
            {
                bool active = false;
                for (int slot = 0; slot < SEARCH_SITE_COUNT; slot++)
                {
                    CrowdSearchSiteState site = SearchSites[slot];
                    if (site.SiteId == i + 1 && site.Charges > 0) { active = true; break; }
                }
                if (_searchSiteObjs[i].activeSelf != active) _searchSiteObjs[i].SetActive(active);
            }
        }

        public override void Render()
        {
            if (!SearchEnabled) return;
            ApplySearchSiteObjects();
            Camera camera = Camera.main;
            if (camera == null) return;
            _searchMarkerTxts ??= new TextMeshPro[_searchSiteObjs.Length];
            for (int slot = 0; slot < SEARCH_SITE_COUNT; slot++)
            {
                CrowdSearchSiteState site = SearchSites[slot];
                if (site.Charges == 0) continue;
                int index = site.SiteId - 1;
                Transform siteTrans = _searchSiteObjs[index].transform;
                TextMeshPro markerTxt = _searchMarkerTxts[index];
                if (markerTxt == null)
                {
                    markerTxt = new GameObject("Search Marker").AddComponent<TextMeshPro>();
                    if (_searchMarkerFont != null)
                    {
                        markerTxt.font = _searchMarkerFont;
                        markerTxt.fontSharedMaterial = _searchMarkerFont.material;
                    }
                    markerTxt.transform.SetParent(siteTrans, false);
                    markerTxt.transform.localPosition = Vector3.up * 2.8f;
                    markerTxt.rectTransform.sizeDelta = new Vector2(4.2f, 1f);
                    markerTxt.fontSize = 4.4f;
                    markerTxt.fontStyle = FontStyles.Bold;
                    markerTxt.alignment = TextAlignmentOptions.Center;
                    markerTxt.color = new Color(1f, 0.86f, 0.25f);
                    markerTxt.outlineColor = new Color32(18, 13, 7, 255);
                    markerTxt.outlineWidth = 0.24f;
                    _searchMarkerTxts[index] = markerTxt;
                }
                float distance = Vector3.Distance(camera.transform.position, siteTrans.position);
                markerTxt.enabled = distance <= SEARCH_MARKER_RANGE;
                if (!markerTxt.enabled) continue;
                markerTxt.transform.localScale = Vector3.one * Mathf.Clamp(distance / 16f, 1f, 2.5f);
                markerTxt.transform.rotation = Quaternion.LookRotation(camera.transform.forward, camera.transform.up);
                markerTxt.SetText(_searchMarkerFont != null && SessionLanguage.Korean ? "수색 지점  {0}회" : "SUPPLIES  {0}", site.Charges);
            }
        }
    }
}
