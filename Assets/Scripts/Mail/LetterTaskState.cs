using System;
using UnityEngine;

namespace DeadLetter.Mail
{
    [Flags]
    public enum LetterStamp
    {
        None = 0,
        Fire = 1 << 0,
        Water = 1 << 1,
        Electricity = 1 << 2,
        Shock = 1 << 3,
        Medicine = 1 << 4,
        Alcohol = 1 << 5
    }

    public sealed class LetterTaskState : MonoBehaviour
    {
        [SerializeField, Min(1)] private int currentDay = 1;
        [SerializeField, Min(1)] private int requiredRequestsPerDay = 5;
        [SerializeField] private int completedRequests;
        [SerializeField] private string letterId;
        [SerializeField] private string customerId;
        [SerializeField] private string customerDisplayName;
        [SerializeField] private LetterStamp requiredStamps;
        [SerializeField] private LetterStamp attachedStamps;
        [SerializeField] private bool postmarkApplied;
        [SerializeField] private bool hasLetter;
        [SerializeField] private bool letterMailed;
        [SerializeField] private bool workstationOpened;

        private bool workstationViewOpen;

        public int CurrentDay => currentDay;
        public int RequiredRequestsPerDay => requiredRequestsPerDay;
        public int CompletedRequests => completedRequests;
        public string LetterId => letterId;
        public string CustomerId => customerId;
        public string CustomerDisplayName => customerDisplayName;
        public LetterStamp RequiredStamps => requiredStamps;
        public LetterStamp AttachedStamps => attachedStamps;
        public bool PostmarkApplied => postmarkApplied;
        public bool HasLetter => hasLetter;
        public bool LetterMailed => letterMailed;
        public bool WorkstationOpened => workstationOpened;
        public bool WorkstationViewOpen => workstationViewOpen;
        public bool HasRequiredStamps => hasLetter && ContainsRequiredStamps(attachedStamps, requiredStamps);
        public bool HasOnlyRequiredStamps => hasLetter && attachedStamps == requiredStamps;
        public bool ReadyToMail => hasLetter && !letterMailed && HasOnlyRequiredStamps && postmarkApplied;
        public bool RequiredDayComplete => completedRequests >= requiredRequestsPerDay;

        public event Action Changed;

        public static bool ContainsRequiredStamps(LetterStamp attached, LetterStamp required)
        {
            return (attached & required) == required;
        }

        public bool TryReceiveLetter(string newLetterId, string newCustomerId, string newCustomerDisplayName, LetterStamp newRequiredStamps)
        {
            if (hasLetter || letterMailed || string.IsNullOrWhiteSpace(newLetterId) ||
                string.IsNullOrWhiteSpace(newCustomerId) || newRequiredStamps == LetterStamp.None)
                return false;

            letterId = newLetterId;
            customerId = newCustomerId;
            customerDisplayName = newCustomerDisplayName;
            requiredStamps = newRequiredStamps;
            attachedStamps = LetterStamp.None;
            postmarkApplied = false;
            workstationOpened = false;
            hasLetter = true;
            Changed?.Invoke();
            return true;
        }

        public bool TryReceiveLetter(string newLetterId, string newCustomerId, LetterStamp newRequiredStamps)
        {
            return TryReceiveLetter(newLetterId, newCustomerId, newCustomerId, newRequiredStamps);
        }

        public bool ToggleStamp(LetterStamp stamp)
        {
            if (!hasLetter || letterMailed || stamp == LetterStamp.None)
                return false;

            if ((attachedStamps & stamp) == stamp)
                attachedStamps &= ~stamp;
            else
                attachedStamps |= stamp;

            Changed?.Invoke();
            return true;
        }

        public bool AttachStamp(LetterStamp stamp)
        {
            if (!hasLetter || letterMailed || stamp == LetterStamp.None || (attachedStamps & stamp) == stamp)
                return false;

            attachedStamps |= stamp;
            Changed?.Invoke();
            return true;
        }

        public bool ApplyPostmark()
        {
            if (!hasLetter || letterMailed || postmarkApplied)
                return false;

            postmarkApplied = true;
            Changed?.Invoke();
            return true;
        }

        public bool RemovePostmark()
        {
            if (!hasLetter || letterMailed || !postmarkApplied)
                return false;

            postmarkApplied = false;
            Changed?.Invoke();
            return true;
        }

        public bool TryMailLetter()
        {
            if (!ReadyToMail)
                return false;

            letterMailed = true;
            workstationViewOpen = false;
            Changed?.Invoke();
            return true;
        }

        public bool CompleteCurrentRequest()
        {
            if (!letterMailed)
                return false;

            completedRequests = Mathf.Min(requiredRequestsPerDay, completedRequests + 1);
            ClearCurrentRequest();
            Changed?.Invoke();
            return true;
        }

        public void BeginNextDay()
        {
            currentDay++;
            completedRequests = 0;
            ClearCurrentRequest();
            Changed?.Invoke();
        }

        public void SetWorkstationViewOpen(bool open)
        {
            workstationViewOpen = open;
            if (open)
                workstationOpened = true;
            Changed?.Invoke();
        }

        private void ClearCurrentRequest()
        {
            letterId = string.Empty;
            customerId = string.Empty;
            customerDisplayName = string.Empty;
            requiredStamps = LetterStamp.None;
            attachedStamps = LetterStamp.None;
            postmarkApplied = false;
            hasLetter = false;
            letterMailed = false;
            workstationOpened = false;
            workstationViewOpen = false;
        }
    }
}
