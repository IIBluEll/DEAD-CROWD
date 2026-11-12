using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeadCrowd.UI
{
    public sealed class InGameHud_view : MonoBehaviour
    {
        [SerializeField] private TMP_Text _clockTxt;
        [SerializeField] private TMP_Text _aliveTxt;
        [SerializeField] private GameObject _scoreRowObj;
        [SerializeField] private TMP_Text _scoreTxt;
        [SerializeField] private GameObject _leadObj;
        [SerializeField] private TMP_Text _leadTxt;
        [SerializeField] private GameObject _vitalsObj;
        [SerializeField] private TMP_Text _exposurePctTxt;
        [SerializeField] private TMP_Text _exposureStatusTxt;
        [SerializeField] private Image _exposureFillImg;
        [SerializeField] private TMP_Text _healthTxt;
        [SerializeField] private Image _healthFillImg;
        [SerializeField] private CanvasGroup _ammoGroup;
        [SerializeField] private TMP_Text _ammoTitleTxt;
        [SerializeField] private TMP_Text _ammoTxt;
        [SerializeField] private GameObject _objectiveObj;
        [SerializeField] private TMP_Text _objectiveTitleTxt;
        [SerializeField] private TMP_Text _objectiveTxt;
        [SerializeField] private GameObject _progressObj;
        [SerializeField] private Image _progressFillImg;

        private static readonly Color INK = new Color32(245, 242, 230, 255);
        private static readonly Color MUTED = new Color32(185, 183, 174, 255);
        private static readonly Color AMBER = new Color32(249, 193, 79, 255);
        private static readonly Color DANGER = new Color32(255, 103, 86, 255);
        private static readonly Color HEALTH = new Color32(156, 187, 132, 255);

        public void Show(SessionModel_model model)
        {
            _clockTxt.text = $"{model.RoundSeconds / 60:00}:{model.RoundSeconds % 60:00}";
            _clockTxt.color = model.Screen == SESSION_SCREEN.PLAYING && model.RoundSeconds <= 30 ? DANGER : INK;
            _aliveTxt.SetText("{0}", model.AliveCount);
            _scoreRowObj.SetActive(model.SearchEnabled);
            _leadObj.SetActive(!model.Practice);
            if (model.SearchEnabled)
            {
                _scoreTxt.SetText("{0}", model.SearchScore);
                _leadTxt.SetText("{0}", model.LeadSearchScore);
            }

            bool alive = model.HasLocalPlayer && model.LocalAlive;
            _vitalsObj.SetActive(alive);
            _ammoGroup.gameObject.SetActive(alive);
            if (alive)
            {
                Color exposureColor = model.Exposed ? DANGER : AMBER;
                _exposurePctTxt.SetText("{0}%", model.ExposurePercent);
                _exposurePctTxt.color = exposureColor;
                _exposureStatusTxt.text = model.Exposed ? model.Text("EXPOSED", "노출 중") : model.Text("HIDDEN", "비노출");
                _exposureStatusTxt.color = model.Exposed ? DANGER : MUTED;
                SetFill(_exposureFillImg, model.ExposureRatio);
                _exposureFillImg.color = exposureColor;
                _healthTxt.SetText("{0} / {1}", model.HealthValue, model.MaxHealth);
                SetFill(_healthFillImg, model.HealthRatio);
                _healthFillImg.color = model.HealthRatio <= .25f ? DANGER : HEALTH;
                _ammoGroup.alpha = model.ShowCrosshair || model.Reloading || model.Magazine == 0 ? 1f : .82f;
                _ammoTitleTxt.text = model.Reloading ? model.Text("RELOADING", "재장전") : model.Text("PISTOL", "권총");
                _ammoTitleTxt.color = model.Reloading ? AMBER : MUTED;
                _ammoTxt.color = model.Magazine == 0 && !model.Reloading ? DANGER : INK;
                if (model.Reloading) _ammoTxt.text = model.Korean ? $"{model.ReloadSeconds:0.0}초" : $"{model.ReloadSeconds:0.0} s";
                else _ammoTxt.SetText("{0} / {1}", model.Magazine, model.Reserve);
            }

            bool showObjective = model.Screen == SESSION_SCREEN.PLAYING && !model.MenuOpen && !string.IsNullOrEmpty(model.Spectator);
            _objectiveObj.SetActive(showObjective);
            if (!showObjective) return;
            _objectiveTitleTxt.text = alive ? model.Text("SUPPLIES", "수색 지점") : model.Text("SPECTATING", "관전");
            _objectiveTitleTxt.color = alive ? AMBER : MUTED;
            _objectiveTxt.text = model.Spectator;
            _progressObj.SetActive(alive && model.SearchProgress >= 0f);
            SetFill(_progressFillImg, model.SearchProgress);
        }

        private static void SetFill(Image fillImg, float amount) => fillImg.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(amount), 1f);
    }
}
