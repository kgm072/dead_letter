using UnityEngine;

namespace DeadLetter.Interaction
{
    public enum ArtInteractionRole
    {
        None,
        LetterWorkstation,
        Stamp,
        Tape,
        VoiceRecorder,
        DaySofa,
        Note
    }

    public sealed class ArtInteractionMarker : MonoBehaviour
    {
        [SerializeField] private ArtInteractionRole role;

        public ArtInteractionRole Role
        {
            get => role;
            set => role = value;
        }
    }
}
