using System;
using System.Threading.Tasks;
using DeadCrowd.Prototype;
using DeadCrowd.Session;
using DeadCrowd.UI;
using Fusion;
using UnityEngine;
using UnityEngine.UI;

namespace DeadCrowd.Tests
{
    // Only attached with an explicit development CLI flag. Never added to a saved scene.
    public sealed class SessionLifecycleSmokeTest : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private CrowdBootstrap _runtime;
        private string _role;
        private string _scenario;
        private string _code;
        private bool _finished;
        private float _started;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "--session-smoke") < 0) return;
            CrowdBootstrap runtime = FindFirstObjectByType<CrowdBootstrap>();
            if (runtime != null && runtime.GetComponent<SessionLifecycleSmokeTest>() == null) runtime.gameObject.AddComponent<SessionLifecycleSmokeTest>();
        }

        private async void Start()
        {
            _runtime = GetComponent<CrowdBootstrap>();
            _started = Time.realtimeSinceStartup;
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "--session-smoke") _role = args[i + 1];
                if (args[i] == "--session-case") _scenario = args[i + 1];
                if (args[i] == "--session-code") _code = args[i + 1];
            }
            try
            {
                if (_role == "lifecycle") await Lifecycle_async();
                else await Loading_async();
                Finish(true, _scenario ?? "invalid code, missing room, late success cancellation, retry, 12s live session, duplicate leave, offline loading");
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(false, exception.Message); }
        }

        private void Update()
        {
            if (!_finished && Time.realtimeSinceStartup - _started > 110f) Finish(false, "watchdog timeout");
        }

        private async Task Lifecycle_async()
        {
            CrowdConnectionService connection = _runtime.Connection;
            await _runtime.Connect_async(GameMode.Client, "IO01");
            Require(connection.State == CONNECTION_STATE.IDLE && _runtime.Runner == null && _runtime.Status.StartsWith("Enter 6"), "invalid code reached networking");
            await _runtime.Connect_async(GameMode.Client, "ZZZ999");
            Require(connection.State == CONNECTION_STATE.IDLE && _runtime.Runner == null && _runtime.Status.StartsWith("Room not found"), "missing room cleanup: " + _runtime.Status);
            connection.TestStartupCompletionDelaySeconds = 3f;
            SessionPresenter_presenter presenter = GetComponent<SessionPresenter_presenter>();
            await WaitUntil_async(() => presenter.Model.Screen == SESSION_SCREEN.TITLE, 5f, "title after failed join");
            Click("Create Room");
            Click("Confirm Create");
            await WaitUntil_async(() => connection.TestSuccessfulStartReceived, 15f, "successful StartGame response held before delivery");
            Click("Cancel");
            _runtime.CancelConnection();
            await WaitUntil_async(() => !connection.Busy, 15f, "cancel cleanup");
            Require(connection.State == CONNECTION_STATE.IDLE && _runtime.Runner == null && _runtime.Status == "Connection cancelled.", "late success revived cancelled session");
            await WaitUntil_async(() => presenter.Model.Screen == SESSION_SCREEN.CREATE, 5f, "cancel returns to create screen");
            connection.TestStartupCompletionDelaySeconds = 0f;
            await _runtime.Connect_async(GameMode.Host);
            await WaitUntil_async(() => _runtime.Match != null && _runtime.Match.LocalActor() != null, 15f, "host retry");
            await Task.Delay(12000);
            Require(_runtime.Runner != null && _runtime.Runner.IsRunning && !connection.Busy, "startup timeout killed live session");
            Task first = _runtime.Leave_async();
            Task second = _runtime.Leave_async();
            Require(ReferenceEquals(first, second), "duplicate leave did not share cleanup");
            await first;
            Require(connection.State == CONNECTION_STATE.IDLE && _runtime.Match == null && _runtime.Runner == null, "leave left stale state");
            await _runtime.Connect_async(GameMode.Single);
            await WaitUntil_async(() => _runtime.Match != null && _runtime.Match.LocalActor() != null, 15f, "offline retry");
            CrowdMatch match = _runtime.Match;
            match.LocalActor().RPC_SetReady(true, match.RoundId);
            await WaitUntil_async(() => match.CanStart(), 5f, "offline ready");
            match.RPC_RequestStart(match.RoundId);
            await WaitUntil_async(() => match.State == ROUND_STATE.PLAYING, 10f, "offline loading and countdown");
            Require(match.LoadedCount() == 1 && match.NpcCount() == 20, "offline loading snapshot");
            await _runtime.Leave_async();
        }

        private async Task Loading_async()
        {
            bool host = _role == "host";
            bool block = host ? _scenario == "host-timeout" : _scenario == "delay" || _scenario == "timeout" || _scenario == "load-failure" || _scenario == "loading-leave";
            _runtime.Connection.TestBlockLoading = block;
            await _runtime.Connect_async(host ? GameMode.Host : GameMode.Client, _code);
            await WaitUntil_async(() => _runtime.Match != null && _runtime.Match.ConnectedCount() == 2, 25f, "two players joined");
            CrowdMatch match = _runtime.Match;
            CrowdActor local = match.LocalActor();
            await WaitUntil_async(() => { if (match.State == ROUND_STATE.LOBBY) local.RPC_SetReady(true, match.RoundId); return local.Ready || match.State != ROUND_STATE.LOBBY; }, 5f, "ready");
            if (host)
            {
                await WaitUntil_async(() => match.CanStart(), 10f, "all ready");
                match.RPC_RequestStart(match.RoundId);
            }
            await WaitUntil_async(() => match.State != ROUND_STATE.LOBBY, 10f, "loading began");
            int round = match.RoundId;
            if (_scenario == "delay")
            {
                await Task.Delay(host ? 2500 : 4000);
                Require(match.State == ROUND_STATE.LOADING && match.CountdownEndTick == 0 && match.EndTick == 0, "started before all load responses");
                Require(local.Health == 100 && local.Exposure == 0 && local.Magazine == 6, "combat changed during loading");
                if (!host)
                {
                    local.RPC_ReportLoaded(round - 1, CrowdRules.BUILD_VERSION, true);
                    await Task.Delay(500);
                    Require(match.State == ROUND_STATE.LOADING && local.LoadedRoundId != round, "stale load response accepted");
                    _runtime.Connection.TestBlockLoading = false;
                }
                await WaitUntil_async(() => match.State == ROUND_STATE.PLAYING, 12f, "all load responses to play");
                Require(match.LoadedCount() == 2 && match.NpcCount() == 20, "incomplete load at play");
                await Task.Delay(1000);
            }
            else if (_scenario == "timeout" || _scenario == "load-failure")
            {
                if (!host && _scenario == "load-failure") local.RPC_ReportLoaded(round, CrowdRules.BUILD_VERSION, false);
                if (host)
                {
                    await WaitUntil_async(() => match.State == ROUND_STATE.LOBBY, 40f, "failed loading returns host to lobby");
                    Require(match.LastStartFailure == (_scenario == "timeout" ? START_FAILURE.LOAD_TIMEOUT : START_FAILURE.LOAD_FAILED), "wrong start failure");
                    Require(match.ConnectedCount() == 1 && !local.Ready && match.NpcCount() == 0 && match.EndTick == 0, "failed start not reset");
                    Require(_runtime.Runner.IsRunning, "healthy host disconnected");
                    await Task.Delay(3000);
                }
                else
                {
                    await WaitUntil_async(() => _runtime.Connection.State == CONNECTION_STATE.IDLE, 40f, "failed client returns to title");
                    Require(_runtime.Runner == null && _runtime.Match == null, "failed client retained runner");
                }
            }
            else if (_scenario == "countdown-leave" || _scenario == "loading-leave")
            {
                ROUND_STATE expected = _scenario == "countdown-leave" ? ROUND_STATE.COUNTDOWN : ROUND_STATE.LOADING;
                await WaitUntil_async(() => match.State == expected, 10f, "state before participant leave");
                if (!host) { await Task.Delay(500); await _runtime.Leave_async(); }
                else
                {
                    await WaitUntil_async(() => match.State == ROUND_STATE.LOBBY, 10f, "participant leave cancels start");
                    Require(match.LastStartFailure == START_FAILURE.PARTICIPANT_LEFT && !local.Ready && match.NpcCount() == 0 && match.EndTick == 0, "participant leave reset");
                    await Task.Delay(1000);
                }
            }
            else if (_scenario == "host-timeout")
            {
                await WaitUntil_async(() => _runtime.Connection.State == CONNECTION_STATE.IDLE, 40f, "host loading failure closes session");
                Require(_runtime.Runner == null && _runtime.Match == null, "closed session retained runner");
            }
            else if (_scenario == "host-close" || _scenario == "result-close")
            {
                await WaitUntil_async(() => match.State == ROUND_STATE.PLAYING, 12f, "start before host close");
                if (_scenario == "result-close")
                {
                    if (host) match.EndTick = match.CurrentTick + 60;
                    await WaitUntil_async(() => match.State == ROUND_STATE.RESULT, 8f, "confirmed result before host close");
                }
                if (host) { await Task.Delay(2000); await _runtime.Leave_async(); }
                else await WaitUntil_async(() => _runtime.Connection.State == CONNECTION_STATE.IDLE, 20f, "host close returns client to title");
                Require(_runtime.Match == null && _runtime.Runner == null, "host close cleanup");
                if (!host && _scenario == "result-close") Require(_runtime.Status.Contains("Confirmed result: Draw - Time Limit."), "confirmed result lost on host close");
            }
            else throw new InvalidOperationException("Unknown smoke scenario: " + _scenario);
        }

        private static async Task WaitUntil_async(Func<bool> condition, float seconds, string stage)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup >= deadline) throw new InvalidOperationException("Timed out: " + stage);
                await Task.Delay(100);
            }
        }

        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private void Click(string name)
        {
            foreach (Button button in GetComponentsInChildren<Button>(true))
            {
                if (button.name != name || !button.isActiveAndEnabled || !button.interactable) continue;
                button.onClick.Invoke();
                return;
            }
            throw new InvalidOperationException("Button unavailable: " + name);
        }
        private void Finish(bool success, string message)
        {
            if (_finished) return;
            _finished = true;
            Debug.Log($"[SessionSmoke] {(success ? "PASS" : "FAIL")} role={_role}: {message}");
            Application.Quit(success ? 0 : 1);
        }
#endif
    }
}
