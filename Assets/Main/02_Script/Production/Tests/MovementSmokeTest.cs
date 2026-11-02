using System;
using System.Threading.Tasks;
using DeadCrowd.Prototype;
using Fusion;
using UnityEngine;

namespace DeadCrowd.Tests
{
    [DefaultExecutionOrder(400)]
    public sealed class MovementSmokeTest : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private CrowdBootstrap _runtime;
        private string _role;
        private bool _drive;
        private float _playTime;
        private int _attack;
        private int _reload;
        private float _started;
        private bool _done;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--motion-smoke") < 0) return;
            CrowdBootstrap runtime = FindFirstObjectByType<CrowdBootstrap>();
            if (runtime != null && runtime.GetComponent<MovementSmokeTest>() == null) runtime.gameObject.AddComponent<MovementSmokeTest>();
        }

        private async void Start()
        {
            _runtime = GetComponent<CrowdBootstrap>();
            _started = Time.realtimeSinceStartup;
            string code = null;
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "--motion-delay") >= 0)
            {
                // Runtime-only adverse conditions in this explicit test process; no project asset is saved.
                NetworkProjectConfig.Global.NetworkConditions.Enabled = true;
                NetworkProjectConfig.Global.NetworkConditions.DelayMin = 0.08f;
                NetworkProjectConfig.Global.NetworkConditions.DelayMax = 0.08f;
                Debug.Log("[MotionSmoke] SDK simulated packet delay enabled: 0.08 seconds.");
            }
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "--motion-smoke") _role = args[i + 1];
                if (args[i] == "--motion-code") code = args[i + 1];
            }
            try
            {
                await _runtime.Connect_async(_role == "host" ? GameMode.Host : GameMode.Client, code);
                await Until_async(() => _runtime.Match != null && _runtime.Match.LocalActor() != null && _runtime.Match.ConnectedCount() == 2, "participants");
                CrowdMatch match = _runtime.Match;
                CrowdActor local = match.LocalActor();
                local.RPC_SetReady(true, match.RoundId);
                if (_role == "host") { await Until_async(() => match.CanStart(), "ready"); match.RPC_RequestStart(match.RoundId); }
                await Until_async(() => match.State == ROUND_STATE.COUNTDOWN, "countdown");
                Vector3 countdownPosition = local.transform.position;
                _drive = true;
                await Task.Delay(500);
                Require(Vector3.Distance(countdownPosition, local.transform.position) < 0.05f && local.Exposure == 0 && local.Magazine == 6, "countdown accepted combat/movement");
                await Until_async(() => match.State == ROUND_STATE.PLAYING, "play");
                _playTime = Time.realtimeSinceStartup;
                Vector3 start = local.transform.position;
                await Task.Delay(1400);
                Require(Vector3.Distance(start, local.transform.position) > 0.2f, "player did not move");
                await Task.Delay(6500);
                Require(local.ConfirmedMagazine == 5 && local.ShotSequence == 1, "predicted shot repeated costs or never fired");
                if (_role == "client") Require(match.PredictedSteps > 0 && match.ResimulatedSteps > 0, "client did not predict/resimulate");
                _reload = 1;
                await Task.Delay(2300);
                Require(local.ConfirmedMagazine == 6 && local.ConfirmedReserve == 11, "reload reconciliation");
                _drive = false;
                if (_role == "host") match.EndTick = match.CurrentTick + 30;
                await Until_async(() => match.State == ROUND_STATE.RESULT, "first result");
                if (_role == "host") { await Task.Delay(1000); match.RPC_ReturnToLobby(match.RoundId); }
                await Until_async(() => match.State == ROUND_STATE.LOBBY, "rematch lobby");
                local.RPC_SetReady(true, match.RoundId);
                if (_role == "host") { await Until_async(() => match.CanStart(), "rematch ready"); match.RPC_RequestStart(match.RoundId); }
                await Until_async(() => match.State == ROUND_STATE.PLAYING, "rematch play");
                await Task.Delay(500);
                Require(local.Exposure == 0f && local.ConfirmedMagazine == 6 && local.ConfirmedReserve == 12, "round reset replayed a stale attack/reload");
                Debug.Log($"[MotionSmoke] PASS role={_role}, predicted={match.PredictedSteps}, resimulated={match.ResimulatedSteps}, shots={local.ShotSequence}, ammo={local.ConfirmedMagazine}/{local.ConfirmedReserve}");
                _done = true;
                await Task.Delay(_role == "host" ? 2000 : 500);
                Application.Quit(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Debug.Log("[MotionSmoke] FAIL " + exception.Message); _done = true; Application.Quit(1); }
        }

        private void Update()
        {
            if (!_done && Time.realtimeSinceStartup - _started > 65f) { Debug.Log("[MotionSmoke] FAIL watchdog"); Application.Quit(1); }
            if (!_drive) return;
            float elapsed = _playTime == 0f ? 0f : Time.realtimeSinceStartup - _playTime;
            if (elapsed > 3.5f) _attack = 1;
            _runtime.OverrideTestInput(new CrowdInput { Yaw = 0f, Move = elapsed < 1.5f ? Vector2.up : Vector2.zero, Aim = elapsed >= 1.5f, AttackSequence = _attack, ReloadSequence = _reload });
        }

        private static async Task Until_async(Func<bool> condition, string stage)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (!condition()) { if (Time.realtimeSinceStartup > deadline) throw new InvalidOperationException(stage); await Task.Delay(50); }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
#endif
    }
}
