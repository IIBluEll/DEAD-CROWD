using System;
using DeadCrowd.Prototype;
using DeadCrowd.Session;
using DeadCrowd.UI;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DeadCrowd.Tests
{
    // Explicit developer flag only. Exercises the saved Canvas button events, not OS pointer input.
    [DefaultExecutionOrder(300)]
    public sealed class SessionUiSmokeTest : MonoBehaviour
    {
        private string _role;
        private string _code;
        private int _stage;
        private float _started;
        private float _phaseTime;
        private float _nextAction;
        private CrowdSessionController _session;
        private SessionPresenter_presenter _presenter;
        private bool _finished;

        private void Start()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "--ui-smoke") _role = args[i + 1];
                if (args[i] == "--ui-code") _code = args[i + 1];
            }
            if (_role == null) { enabled = false; return; }
            _session = GetComponent<CrowdSessionController>();
            _presenter = GetComponent<SessionPresenter_presenter>();
            _started = Time.realtimeSinceStartup;
#else
            enabled = false;
#endif
        }

        private void Update()
        {
            if (_role == null || _finished) return;
            if (Time.realtimeSinceStartup - _started > 100f) { Finish(false, "timeout at stage " + _stage); return; }
            if (Time.realtimeSinceStartup < _nextAction) return;
            _nextAction = Time.realtimeSinceStartup + 0.25f;
            SessionModel_model model = _presenter.Model;
            CrowdMatch match = _session.Runtime.Match;
            if (_stage == 0 && model.Screen == SESSION_SCREEN.TITLE)
            {
                if (Click(_role == "host" ? "Create Room" : "Join Room")) _stage++;
            }
            else if (_stage == 1 && model.Screen == (_role == "host" ? SESSION_SCREEN.CREATE : SESSION_SCREEN.JOIN))
            {
                if (_role == "host")
                {
                    if (!Click("FFA") || model.Capacity != 4 || !Click("Duel") || model.Capacity != 2) { Finish(false, "mode selection binding"); return; }
                    if (Click("Confirm Create")) _stage++;
                }
                else
                {
                    GetComponentInChildren<TMP_InputField>(true).text = _code;
                    if (Click("Confirm Join")) _stage++;
                }
            }
            else if (_stage == 2 && match != null && model.Screen == SESSION_SCREEN.LOBBY)
            {
                CrowdActor local = match.LocalActor();
                if (local == null) return;
                if (!local.Ready) { Click("Ready"); return; }
                if (_role == "host" && model.CanStart) Click("Host Start");
                if (match.State != ROUND_STATE.LOBBY) _stage++;
            }
            if (_stage >= 2 && match != null && match.State == ROUND_STATE.PLAYING)
            {
                if (_stage < 4)
                {
                    if (model.Screen != SESSION_SCREEN.PLAYING || string.IsNullOrEmpty(model.Health) || string.IsNullOrEmpty(model.Ammo)) return;
                    _stage = 4; _phaseTime = Time.realtimeSinceStartup;
                    Debug.Log("[UiSmoke] HUD bound to the running match: " + _role);
                }
                if (_role == "host" && Time.realtimeSinceStartup - _phaseTime > 3f) match.EndTick = match.CurrentTick + 1;
            }
            if (match != null && match.State == ROUND_STATE.RESULT)
            {
                if (model.Screen != SESSION_SCREEN.RESULT || string.IsNullOrEmpty(model.Result)) return;
                if (_stage < 5) { _stage = 5; _phaseTime = Time.realtimeSinceStartup; Debug.Log("[UiSmoke] Result received: " + _role + " " + model.Result); }
                if (_role == "host" && Time.realtimeSinceStartup - _phaseTime > 2f) Click("Return Lobby");
            }
            if (_stage >= 5 && match != null && match.State == ROUND_STATE.LOBBY)
            {
                if (_stage == 5) { _stage = 6; _phaseTime = Time.realtimeSinceStartup; }
                if (model.Screen != SESSION_SCREEN.LOBBY || _role == "host" && Time.realtimeSinceStartup - _phaseTime < 2f) return;
                CrowdActor local = match.LocalActor();
                Finish(local != null && local.Health == 100 && !local.Ready && match.NpcCount() == 0, "Canvas title, mode/code, ready, start, HUD, result, lobby reset");
            }
        }

        private bool Click(string name)
        {
            foreach (Button button in GetComponentsInChildren<Button>(true))
            {
                if (button.name != name || !button.isActiveAndEnabled || !button.interactable) continue;
                button.onClick.Invoke(); return true;
            }
            return false;
        }

        private void Finish(bool success, string reason)
        {
            _finished = true;
            Debug.Log($"[UiSmoke] {(success ? "PASS" : "FAIL")} role={_role}: {reason}");
            Application.Quit(success ? 0 : 1);
        }
    }
}
