using System;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

namespace DeadCrowd.Prototype
{
    // Runs only with an explicit development-build command line flag. Never changes combat rules.
    [DefaultExecutionOrder(100)]
    public sealed class CrowdSmokeTest : MonoBehaviour
    {
        private CrowdBootstrap _bootstrap;
        private string _mode;
        private string _code = "TSTABC";
        private float _started;
        private bool _requestedStart;
        private bool _played;
        private bool _result;
        private bool _finished;
        private int _initialNpcCount;
        private int _attackSequence;
        private int _reloadSequence;
        private float _nextAttack;
        private float _playStarted;
        private float _sprintStarted;
        private float _resultSeenTime;
        private float _lobbyReturnTime;
        private string _capturePath;
        private bool _captured;
        private bool _observedExposure;
        private bool _observedDamage;
        private bool _observedShot;
        private bool _observedReload;
        private NavMeshPath _path;

        private async void Start()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "--crowd-smoke") _mode = args[i + 1];
                if (args[i] == "--crowd-code") _code = args[i + 1];
                if (args[i] == "--crowd-capture") _capturePath = args[i + 1];
            }
            if (_mode == null) { enabled = false; return; }
            _bootstrap = GetComponent<CrowdBootstrap>();
            _bootstrap.SetCaptureMode(_capturePath != null);
            _path = new NavMeshPath();
            _started = Time.realtimeSinceStartup;
            Debug.Log("[CrowdSmoke] Starting " + _mode);
            await _bootstrap.Connect_async(_mode == "solo" ? GameMode.Single : _mode == "host" || _mode == "hold" ? GameMode.Host : GameMode.Client, _code);
#else
            enabled = false;
#endif
        }

        private void Update()
        {
            if (_mode == null || _finished || _bootstrap == null) return;
            if (Time.realtimeSinceStartup - _started > 110f) { Finish(false, "timeout: " + _bootstrap.Status); return; }
            CrowdMatch match = _bootstrap.Match;
            if (match == null) return;
            CrowdActor local = match.LocalActor();
            if (local == null) return;
            if (_mode == "hold")
            {
                if (Time.realtimeSinceStartup - _started >= 35f) Finish(match.State == ROUND_STATE.LOBBY && match.ConnectedCount() == 1 && _bootstrap.Runner.IsRunning, "host lobby survived the original 10-second connection deadline");
                return;
            }
            if (match.State == ROUND_STATE.LOBBY)
            {
                if (_result)
                {
                    if (_lobbyReturnTime == 0f) _lobbyReturnTime = Time.realtimeSinceStartup;
                    if (_bootstrap.Runner.IsServer && Time.realtimeSinceStartup - _lobbyReturnTime < 1f) return;
                    bool reset = local.Health == 100 && local.Exposure == 0f && !local.Exposed && local.Magazine == 6 && local.Reserve == 12 && match.NpcCount() == 0 && !local.Ready;
                    Finish(reset && _played && (_mode != "solo" || _observedExposure && _observedDamage && _observedShot && _observedReload), $"reset={reset}, exposed={_observedExposure}, damage={_observedDamage}, shot={_observedShot}, reload={_observedReload}");
                    return;
                }
                if (!local.Ready) local.RPC_SetReady(true, match.RoundId);
                if (_bootstrap.Runner.IsServer && match.CanStart()) { match.RPC_RequestStart(match.RoundId); _requestedStart = true; }
                return;
            }
            if (match.State == ROUND_STATE.PLAYING)
            {
                if (!_played) { _played = true; _playStarted = Time.realtimeSinceStartup; _initialNpcCount = match.NpcCount(); Debug.Log("[CrowdSmoke] Playing with NPCs=" + _initialNpcCount); }
                if (_capturePath != null && !_captured && Time.realtimeSinceStartup - _playStarted > 0.5f)
                {
                    _captured = true;
                    ScreenCapture.CaptureScreenshot(_capturePath);
                }
                _observedExposure |= local.Exposed;
                _observedDamage |= match.NpcCount() < _initialNpcCount || local.Health < 100;
                foreach (CrowdActor actor in match.Actors) if (actor.IsNpc && actor.Health < CrowdRules.MAX_HP) _observedDamage = true;
                _observedShot |= local.Magazine < 6;
                _observedReload |= _observedShot && local.Magazine == 6 && local.Reserve < 12;
                if (_observedReload && _sprintStarted == 0f) _sprintStarted = Time.realtimeSinceStartup;
                CrowdInput input = new CrowdInput { Yaw = local.transform.eulerAngles.y, AttackSequence = _attackSequence, ReloadSequence = _reloadSequence };
                if (_mode == "solo" && _observedReload && Time.realtimeSinceStartup - _sprintStarted < 6f)
                {
                    input.Move = Vector2.up;
                    input.Sprint = true;
                    input.Yaw = Mathf.Repeat((Time.realtimeSinceStartup - _sprintStarted) * 90f, 360f);
                }
                else
                {
                    CrowdActor target = null;
                    float closest = float.MaxValue;
                    foreach (CrowdActor actor in match.Actors)
                    {
                        if (actor == local || !actor.Alive || (_mode != "solo" && actor.IsNpc)) continue;
                        float distance = Vector3.Distance(local.transform.position, actor.transform.position);
                        if (distance < closest) { closest = distance; target = actor; }
                    }
                    if (target != null && local.Alive)
                    {
                        Vector3 delta = CrowdRules.Flat(target.transform.position - local.transform.position);
                        bool shooting = _mode == "solo" && !_observedReload;
                        input.Yaw = Quaternion.LookRotation(delta).eulerAngles.y;
                        input.Pitch = 0f;
                        input.Aim = shooting;
                        if (!shooting && closest > 1.05f && NavMesh.CalculatePath(local.transform.position, target.transform.position, NavMesh.AllAreas, _path))
                        {
                            Vector3[] corners = _path.corners;
                            Vector3 direction = corners.Length > 1 ? CrowdRules.Flat(corners[1] - local.transform.position).normalized : delta.normalized;
                            input.Yaw = Quaternion.LookRotation(direction).eulerAngles.y;
                            input.Move = Vector2.up;
                        }
                        else if (Time.realtimeSinceStartup >= _nextAttack)
                        {
                            input.AttackSequence = ++_attackSequence;
                            _nextAttack = Time.realtimeSinceStartup + (shooting ? 0.45f : 0.9f);
                        }
                        if (shooting && local.Magazine < 6 && local.Action == ACTION_STATE.NONE) input.ReloadSequence = ++_reloadSequence;
                    }
                }
                _bootstrap.OverrideTestInput(input);
                // Solo exercises real inputs, damage and reset; ending is a timer-boundary test.
                if (_mode == "solo" && _observedExposure && _observedDamage && _observedShot && _observedReload && match.HasStateAuthority) match.EndTick = match.CurrentTick + 1;
            }
            if (match.State == ROUND_STATE.RESULT)
            {
                if (!_result)
                {
                    _result = true;
                    _resultSeenTime = Time.realtimeSinceStartup;
                    Debug.Log($"[CrowdSmoke] Saw result winner={match.WinnerSlot}, alive={match.AlivePlayers}");
                }
                if (_bootstrap.Runner.IsServer && Time.realtimeSinceStartup - _resultSeenTime > 2f) match.RPC_ReturnToLobby(match.RoundId);
            }
        }

        private void Finish(bool success, string details)
        {
            _finished = true;
            Debug.Log($"[CrowdSmoke] {(success ? "PASS" : "FAIL")} mode={_mode}, started={_requestedStart}, result={_result}, {details}");
            Application.Quit(success ? 0 : 1);
        }
    }
}
