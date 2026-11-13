using DeadCrowd.Prototype;
using UnityEngine;

namespace DeadCrowd.Content
{
    [CreateAssetMenu(menuName = "DEAD CROWD/Music Profile")]
    public sealed class CrowdMusicProfile : ScriptableObject
    {
        [SerializeField] private AudioClip _mainMenuClip;
        [Tooltip("Uses Main Menu Clip when left empty.")]
        [SerializeField] private AudioClip _lobbyClip;
        [Tooltip("Plays from the countdown through the end of the round. Leave empty for silence during play.")]
        [SerializeField] private AudioClip _matchClip;
        [Tooltip("Uses Lobby Clip or Main Menu Clip when left empty.")]
        [SerializeField] private AudioClip _resultClip;
        [SerializeField, Range(0f, 1f)] private float _musicVolume = 0.3f;

        public float MusicVolume => _musicVolume;

        public AudioClip GetClip(ROUND_STATE? state)
        {
            AudioClip lobbyClip = _lobbyClip != null ? _lobbyClip : _mainMenuClip;
            return state switch
            {
                ROUND_STATE.LOBBY or ROUND_STATE.LOADING => lobbyClip,
                ROUND_STATE.COUNTDOWN or ROUND_STATE.PLAYING => _matchClip,
                ROUND_STATE.RESULT => _resultClip != null ? _resultClip : lobbyClip,
                _ => _mainMenuClip
            };
        }
    }
}
