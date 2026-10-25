using System;
using UnityEngine;

namespace DeadLetter.Mail
{
    [Flags]
    public enum LetterStamp
    {
        None = 0,
        Fire = 1 << 0,
        Shock = 1 << 1
    }

    public sealed class LetterTaskState : MonoBehaviour
    {
        [SerializeField] private string letterId;
        [SerializeField] private string customerId;
        [SerializeField] private LetterStamp requiredStamps;
        [SerializeField] private LetterStamp attachedStamps;
        [SerializeField] private bool postmarkApplied;
        [SerializeField] private bool hasLetter;

        public string LetterId => letterId;
        public string CustomerId => customerId;
        public LetterStamp RequiredStamps => requiredStamps;
        public LetterStamp AttachedStamps => attachedStamps;
        public bool PostmarkApplied => postmarkApplied;
        public bool HasLetter => hasLetter;
        public bool HasRequiredStamps => hasLetter && ContainsRequiredStamps(attachedStamps, requiredStamps);

        public event Action Changed;

        public static bool ContainsRequiredStamps(LetterStamp attached, LetterStamp required)
        {
            return (attached & required) == required;
        }

        public bool TryReceiveLetter(string newLetterId, string newCustomerId, LetterStamp newRequiredStamps)
        {
            if (hasLetter || string.IsNullOrWhiteSpace(newLetterId) || string.IsNullOrWhiteSpace(newCustomerId))
                return false;

            letterId = newLetterId;
            customerId = newCustomerId;
            requiredStamps = newRequiredStamps;
            attachedStamps = LetterStamp.None;
            postmarkApplied = false;
            hasLetter = true;
            Changed?.Invoke();
            return true;
        }

        public bool AttachStamp(LetterStamp stamp)
        {
            if (!hasLetter || stamp == LetterStamp.None || (attachedStamps & stamp) == stamp)
                return false;

            attachedStamps |= stamp;
            Changed?.Invoke();
            return true;
        }

        public bool ApplyPostmark()
        {
            if (!hasLetter || postmarkApplied)
                return false;

            postmarkApplied = true;
            Changed?.Invoke();
            return true;
        }
    }
}
