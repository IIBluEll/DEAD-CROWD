using DeadCrowd.Prototype;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadCrowd.Control
{
    public sealed class CrowdLocalInput
    {
        private CrowdInput _input;
        public bool MenuOpen { get; set; }
        public float Yaw { get; private set; }
        public float Pitch { get; private set; } = 12f;
        public float Sensitivity { get; set; } = 0.12f;
        public int SpectatorIndex { get; private set; }
        public bool Aiming => _input.Aim;
        public void Reset(CrowdActor local = null)
        {
            _input = local == null ? default : new CrowdInput { AttackSequence = local.LastAttackSequence, ReloadSequence = local.LastReloadSequence };
            Yaw = local != null ? local.transform.eulerAngles.y : 0f;
            Pitch = 12f;
            MenuOpen = false;
            SpectatorIndex = 0;
        }
        public void Poll(CrowdMatch match, CrowdActor local)
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            bool playing = match != null && match.State == ROUND_STATE.PLAYING;
            bool countdown = match != null && match.State == ROUND_STATE.COUNTDOWN;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && (playing || countdown)) MenuOpen = !MenuOpen;
            bool looking = (playing || countdown) && !MenuOpen && Application.isFocused && !Application.isBatchMode;
            Cursor.lockState = looking ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !looking;
            if (looking && mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue();
                Yaw = Mathf.Repeat(Yaw + delta.x * Sensitivity, 360f);
                Pitch = Mathf.Clamp(Pitch - delta.y * Sensitivity, -60f, 30f);
            }
            _input.Move = Vector2.zero;
            _input.Aim = false;
            _input.Sprint = false;
            _input.Interact = false;
            _input.Yaw = Yaw;
            _input.Pitch = Pitch;
            if (local != null && local.Alive && playing && looking && keyboard != null && mouse != null)
            {
                _input.Move = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0), (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
                _input.Aim = mouse.rightButton.isPressed;
                _input.Sprint = keyboard.leftShiftKey.isPressed;
                _input.Interact = keyboard.eKey.isPressed;
                if (mouse.leftButton.wasPressedThisFrame) _input.AttackSequence++;
                if (keyboard.rKey.wasPressedThisFrame) _input.ReloadSequence++;
            }
            if (local != null && !local.Alive && playing && !MenuOpen && keyboard != null)
            {
                if (keyboard.aKey.wasPressedThisFrame) SpectatorIndex--;
                if (keyboard.dKey.wasPressedThisFrame) SpectatorIndex++;
            }

        }
        public CrowdInput Read(int roundId) { _input.RoundId = roundId; return _input; }
        public void Override(CrowdInput input) { _input = input; Yaw = input.Yaw; Pitch = input.Pitch; }
    }
}
