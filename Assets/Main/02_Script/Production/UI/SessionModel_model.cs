namespace DeadCrowd.UI
{
    public enum SESSION_SCREEN { TITLE, CREATE, JOIN, CONNECTING, LOBBY, COUNTDOWN, PLAYING, RESULT, LOADING }
    public enum SESSION_ACTION { SHOW_CREATE, SHOW_JOIN, BACK, DUEL, FFA, CREATE, JOIN, CANCEL, READY, START, COPY_CODE, LEAVE, RETURN_LOBBY, RESUME, SETTINGS, SAVE_SETTINGS, QUIT }

    // Presentation data only. World identity and other players' health never enter this model.
    public sealed class SessionModel_model
    {
        public SESSION_SCREEN Screen;
        public bool Busy;
        public bool Host;
        public bool Ready;
        public bool CanReady;
        public bool CanStart;
        public bool MenuOpen;
        public bool SettingsOpen;
        public bool ShowCrosshair;
        public float HealthRatio;
        public float ExposureRatio;
        public bool Exposed;
        public int Capacity = 2;
        public string Status = "Create a room or join with an invite code.";
        public string Code = "";
        public string Slots = "";
        public string Countdown = "";
        public string Health = "";
        public string Exposure = "";
        public string Ammo = "";
        public string Time = "";
        public string Crosshair = "";
        public string Spectator = "";
        public string Result = "";
    }
}
