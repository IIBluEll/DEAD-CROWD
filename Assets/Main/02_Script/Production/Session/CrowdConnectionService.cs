using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DeadCrowd.Prototype;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace DeadCrowd.Session
{
    public enum CONNECTION_STATE { IDLE, CONNECTING, CONNECTED, DISCONNECTING }

    // One owner for the runner, cancellation token and scene cleanup. Input remains in Bootstrap until D3.
    public sealed class CrowdConnectionService : INetworkRunnerCallbacks, IDisposable
    {
        private readonly Func<CrowdInput> READ_INPUT;
        private NetworkRunner _runner;
        private CrowdMatch _match;
        private CancellationTokenSource _cancellation;
        private Task _connectTask = Task.CompletedTask;
        private Task _leaveTask = Task.CompletedTask;
        private int _attempt;
        private int _sceneIndex;
        private bool _disposed;
        private bool _shutdownPending;
        private bool _sceneReady;
        private string _shutdownMessage;
        private float _nextLoadReport;
        private float _initialLoadDeadline;
        private bool _initialLoadComplete;
        private string _confirmedResult = "";

        public NetworkRunner Runner => _runner;
        public CrowdMatch Match => _match != null && _match.Valid ? _match : null;
        public CONNECTION_STATE State { get; private set; }
        public bool Busy => State == CONNECTION_STATE.CONNECTING || State == CONNECTION_STATE.DISCONNECTING || State == CONNECTION_STATE.CONNECTED && !_initialLoadComplete;
        public string Status { get; private set; } = "Create a room or join with an invite code.";
        public string InviteCode { get; private set; } = "";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool TestBlockLoading { get; set; }
        public float TestStartupCompletionDelaySeconds { get; set; }
        public bool TestSuccessfulStartReceived { get; private set; }
#endif

        public CrowdConnectionService(Func<CrowdInput> readInput) { READ_INPUT = readInput; }

        public Task Connect_async(GameMode mode, string code = null, int capacity = 2)
        {
            if (_disposed || State != CONNECTION_STATE.IDLE) return Task.CompletedTask;
            string requestedCode = (code ?? "").Trim().ToUpperInvariant();
            if (mode == GameMode.Client && !ValidCode(requestedCode))
            {
                Status = "Enter 6 letters or digits. I, O, 0 and 1 are excluded.";
                return Task.CompletedTask;
            }
            if (mode != GameMode.Single && capacity != 2 && capacity != 4) { Status = "Choose a 2-player or 4-player room."; return Task.CompletedTask; }
            State = CONNECTION_STATE.CONNECTING;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            TestSuccessfulStartReceived = false;
#endif
            Status = "Connecting to the room...";
            _confirmedResult = "";
            _initialLoadComplete = false;
            _sceneIndex = SceneManager.GetActiveScene().buildIndex;
            _connectTask = ConnectCore_async(mode, code, requestedCode, capacity, ++_attempt);
            return _connectTask;
        }

        private async Task ConnectCore_async(GameMode mode, string codeOverride, string requestedCode, int capacity, int attempt)
        {
            bool connected = false;
            try
            {
                for (int retry = 0; retry < (mode == GameMode.Host ? 5 : 1); retry++)
                {
                    if (_disposed || attempt != _attempt) return;
                    _cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    var runnerObj = new GameObject("Crowd Network Runner");
                    UnityEngine.Object.DontDestroyOnLoad(runnerObj);
                    NetworkRunner runner = runnerObj.AddComponent<NetworkRunner>();
                    _runner = runner;
                    _sceneReady = false;
                    _shutdownPending = false;
                    runner.ProvideInput = true;
                    runner.AddCallbacks(this);
                    var sceneManager = runnerObj.AddComponent<NetworkSceneManagerDefault>();
                    var provider = runnerObj.AddComponent<NetworkObjectProviderDefault>();
                    var settings = Fusion.Photon.Realtime.PhotonAppSettings.Global.AppSettings.GetCopy();
                    settings.AppVersion = CrowdRules.BUILD_VERSION;
                    settings.FixedRegion = "kr";
                    string code = mode == GameMode.Single ? "PRACTICE" : mode == GameMode.Host && codeOverride == null ? GenerateCode() : requestedCode;
                    InviteCode = code;
                    var sceneInfo = new NetworkSceneInfo();
                    sceneInfo.AddSceneRef(SceneRef.FromIndex(_sceneIndex), LoadSceneMode.Single);
                    StartGameResult result = await runner.StartGame(new StartGameArgs
                    {
                        GameMode = mode, SessionName = code, PlayerCount = mode == GameMode.Single ? 1 : capacity,
                        IsVisible = false, IsOpen = true, EnableClientSessionCreation = false,
                        ConnectionToken = Encoding.UTF8.GetBytes(CrowdRules.BUILD_VERSION), CustomPhotonAppSettings = settings,
                        Scene = sceneInfo, SceneManager = sceneManager, ObjectProvider = provider, StartGameCancellationToken = _cancellation.Token
                    });
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    TestSuccessfulStartReceived = result.Ok;
                    if (result.Ok && TestStartupCompletionDelaySeconds > 0f)
                    {
                        Debug.Log("[SessionSmoke] Holding a successful StartGame response for cancellation testing.");
                        await Task.Delay(TimeSpan.FromSeconds(TestStartupCompletionDelaySeconds));
                    }
#endif
                    // A successful response is still stale when the user cancelled while it was in flight.
                    if (_disposed || attempt != _attempt) return;
                    if (_cancellation.IsCancellationRequested) { Status = "Connection timed out. Please try again."; return; }
                    if (result.Ok && runner != null && runner.IsRunning && !_shutdownPending)
                    {
                        // Fusion retains the token's shutdown callback after StartGame completes.
                        _cancellation.CancelAfter(Timeout.Infinite);
                        connected = true;
                        State = CONNECTION_STATE.CONNECTED;
                        _initialLoadDeadline = Time.realtimeSinceStartup + CrowdRules.LOAD_TIMEOUT_SECONDS;
                        Status = "The host can start when everyone is ready.";
                        Debug.Log($"[Crowd] Connected mode={mode}, room={code}, attempt={attempt}");
                        return;
                    }
                    string reason = result.ShutdownReason.ToString();
                    Status = ConnectionMessage(reason);
                    Debug.LogWarning($"[Crowd] Connection failed: {reason}, attempt={attempt}");
                    await StopRunner_async();
                    if (mode != GameMode.Host || codeOverride != null || reason != "GameIdAlreadyExists") return;
                }
                Status = "Could not create a unique invite code. Please try again.";
            }
            catch (OperationCanceledException)
            {
                if (attempt == _attempt) Status = "Connection timed out. Please try again.";
            }
            catch (Exception exception)
            {
                if (attempt == _attempt) Status = "Could not connect. Check your connection and game version, then try again.";
                Debug.LogException(exception);
            }
            finally
            {
                if (!connected)
                {
                    await StopRunner_async();
                    // Explicit leave owns the reload when it has superseded this attempt.
                    if (State != CONNECTION_STATE.DISCONNECTING)
                    {
                        await RestoreScene_async();
                        State = CONNECTION_STATE.IDLE;
                    }
                }
            }
        }

        public void CancelConnection()
        {
            if (State == CONNECTION_STATE.CONNECTING || State == CONNECTION_STATE.CONNECTED && !_initialLoadComplete) _ = Leave_async("Connection cancelled.");
        }

        public Task Leave_async(string message = "You left the room.")
        {
            if (_disposed) return Task.CompletedTask;
            if (State == CONNECTION_STATE.DISCONNECTING) return _leaveTask;
            if (State == CONNECTION_STATE.IDLE) { Status = message; return Task.CompletedTask; }
            State = CONNECTION_STATE.DISCONNECTING;
            Status = message;
            ++_attempt;
            _cancellation?.Cancel();
            _leaveTask = LeaveCore_async();
            return _leaveTask;
        }

        private async Task LeaveCore_async()
        {
            try
            {
                await _connectTask;
                await StopRunner_async();
                await RestoreScene_async();
            }
            finally { State = CONNECTION_STATE.IDLE; }
        }

        private async Task StopRunner_async()
        {
            NetworkRunner runner = _runner;
            _runner = null;
            _match = null;
            _sceneReady = false;
            _shutdownPending = false;
            try
            {
                if (runner != null)
                {
                    runner.RemoveCallbacks(this);
                    await runner.Shutdown();
                    if (runner != null) UnityEngine.Object.Destroy(runner.gameObject);
                }
            }
            catch (Exception exception) { Debug.LogException(exception); }
            finally { _cancellation?.Dispose(); _cancellation = null; }
        }

        private async Task RestoreScene_async()
        {
            if (_disposed) return;
            try
            {
                AsyncOperation load = SceneManager.LoadSceneAsync(_sceneIndex);
                while (!load.isDone && !_disposed) await Task.Yield();
            }
            catch (Exception exception) { Status = "Could not restore the game scene. Restart the game."; Debug.LogException(exception); }
        }

        public void Tick()
        {
            if (_disposed || State != CONNECTION_STATE.CONNECTED) return;
            if (_shutdownPending) { _ = Leave_async(_shutdownMessage); return; }
            if (_runner == null || !_runner.IsRunning) return;
            if (Match == null) _match = UnityEngine.Object.FindFirstObjectByType<CrowdMatch>();
            CrowdMatch match = Match;
            if (!_initialLoadComplete)
            {
                _initialLoadComplete = _sceneReady && match != null && match.Runner == _runner && match.LocalActor() != null;
                if (!_initialLoadComplete && Time.realtimeSinceStartup >= _initialLoadDeadline)
                {
                    _ = Leave_async("The room scene could not load in time. Please try again.");
                    return;
                }
            }
            if (match == null || match.Runner != _runner) return;
            if (match.State == ROUND_STATE.RESULT)
                _confirmedResult = match.WinnerSlot > 0 ? $"Slot {match.WinnerSlot} Wins" : match.WinnerSlot == -1 ? "Draw - All Eliminated" : "Draw - Time Limit";
            else if (match.State == ROUND_STATE.LOBBY) _confirmedResult = "";
            if (match.HostLoadFailed) { _ = Leave_async("The host could not load the round. The room has closed."); return; }
            if (match.State != ROUND_STATE.LOADING) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (TestBlockLoading) return;
#endif
            CrowdActor local = match.LocalActor();
            if (!_sceneReady || local == null || local.LoadedRoundId == match.RoundId || Time.unscaledTime < _nextLoadReport) return;
            if (match.ConnectedCount() != match.RequiredPlayers || match.NpcCount() != match.ExpectedNpcCount) return;
            _nextLoadReport = Time.unscaledTime + 0.5f;
            bool mapReady = NavMesh.SamplePosition(local.transform.position, out _, 1f, NavMesh.AllAreas);
            local.RPC_ReportLoaded(match.RoundId, CrowdRules.BUILD_VERSION, mapReady);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ++_attempt;
            _cancellation?.Cancel();
            if (_runner != null) _runner.RemoveCallbacks(this);
            // Pending startup/leave keeps ownership until its continuation has released the runner.
            if (State == CONNECTION_STATE.CONNECTED) _ = StopRunner_async();
        }

        private static bool ValidCode(string code)
        {
            if (code.Length != 6) return false;
            foreach (char character in code) if (CrowdRules.CODE_ALPHABET.IndexOf(character) < 0) return false;
            return true;
        }

        private static string GenerateCode()
        {
            var code = new char[6];
            for (int i = 0; i < code.Length; i++) code[i] = CrowdRules.CODE_ALPHABET[UnityEngine.Random.Range(0, CrowdRules.CODE_ALPHABET.Length)];
            return new string(code);
        }

        private static string ConnectionMessage(string reason)
        {
            switch (reason)
            {
                case "GameNotFound": return "Room not found. Check the invite code and game version.";
                case "GameIsFull": return "The room is full.";
                case "GameClosed": return "The match is in progress. Join when it returns to the lobby.";
                case "InvalidAuthentication": return "Could not authenticate with the game service.";
                case "IncompatibleConfiguration": return "The host and client must use the same game version.";
                case "OperationCanceled": return "Connection timed out. Please try again.";
                default: return "Could not connect. Check your connection and game version, then try again.";
            }
        }

        private void SessionEnded(NetworkRunner runner, string reason)
        {
            Debug.LogWarning("[Crowd] Session ended: " + reason);
            if (_disposed || runner != _runner) return;
            _shutdownPending = true;
            _shutdownMessage = Match != null && Match.State == ROUND_STATE.LOADING
                ? "The session ended during loading. Loading failed, timed out, or the host left."
                : "The session ended. Create a room or join again.";
            if (!string.IsNullOrEmpty(_confirmedResult)) _shutdownMessage += " Confirmed result: " + _confirmedResult + ".";
        }

        public void OnInput(NetworkRunner runner, NetworkInput input) { if (runner == _runner && State == CONNECTION_STATE.CONNECTED) input.Set(READ_INPUT()); }
        public void OnShutdown(NetworkRunner runner, ShutdownReason reason) { SessionEnded(runner, reason.ToString()); }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { SessionEnded(runner, reason.ToString()); }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            if (runner == _runner && (Match == null || Match.State == ROUND_STATE.LOBBY) && Encoding.UTF8.GetString(token ?? Array.Empty<byte>()) == CrowdRules.BUILD_VERSION) request.Accept();
            else request.Refuse();
        }
        public void OnSceneLoadStart(NetworkRunner runner) { if (runner == _runner) _sceneReady = false; }
        public void OnSceneLoadDone(NetworkRunner runner) { if (runner == _runner) { _sceneReady = true; _match = UnityEngine.Object.FindFirstObjectByType<CrowdMatch>(); } }
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { Debug.LogWarning($"[Crowd] Player left: {player}"); }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress address, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessions) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken token) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    }
}
