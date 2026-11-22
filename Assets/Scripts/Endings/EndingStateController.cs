using UnityEngine;

namespace DeadLetter.Endings
{
    public enum EndingKind { Normal, True }

    // Only viewing records are persistent. Discovery and sequence state belong to the scene/run.
    public static class EndingRecords
    {
        public const string NormalKey = "dead_letter.ending.normal_seen";
        public const string TrueKey = "dead_letter.ending.true_seen";
        public static bool HasSeen(EndingKind kind) => PlayerPrefs.GetInt(Key(kind), 0) == 1;
        public static void Record(EndingKind kind) { PlayerPrefs.SetInt(Key(kind), 1); PlayerPrefs.Save(); }
        private static string Key(EndingKind kind) => kind == EndingKind.Normal ? NormalKey : TrueKey;
    }

    public sealed class EndingStateController : MonoBehaviour
    {
        public bool FrameLowered { get; private set; }
        public bool MarksDiscovered { get; private set; }
        public bool LetterOpened { get; private set; }
        public bool Playing { get; private set; }
        public bool CanSkip { get; private set; }
        public EndingKind Kind { get; private set; }
        public bool Discover(bool normalSeen)
        {
            if (!normalSeen || FrameLowered || Playing) return false;
            FrameLowered = MarksDiscovered = true;
            return true;
        }
        public bool Begin(EndingKind kind, bool normalReady, bool alreadySeen)
        {
            if (Playing || (kind == EndingKind.Normal ? !normalReady : !MarksDiscovered || LetterOpened)) return false;
            Kind = kind;
            CanSkip = alreadySeen; // Snapshot: recording this viewing must not enable first-view skipping.
            Playing = true;
            if (kind == EndingKind.True) LetterOpened = true;
            return true;
        }
        public void Finish() { Playing = false; CanSkip = false; }
        public static bool NormalReady(int day, int completed, int required, bool customerActive, bool dialogueActive)
            => day == 3 && required == 5 && completed == required && !customerActive && !dialogueActive;
    }
}
