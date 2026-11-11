using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DeadLetter.Mail
{
    public sealed class DayCycleManager : MonoBehaviour
    {
        private readonly struct CustomerDefinition
        {
            public CustomerDefinition(string id, string displayName, LetterStamp requiredStamps)
            {
                Id = id;
                DisplayName = displayName;
                RequiredStamps = requiredStamps;
            }

            public string Id { get; }
            public string DisplayName { get; }
            public LetterStamp RequiredStamps { get; }
        }

        [SerializeField] private LetterTaskState taskState;
        [SerializeField] private TestCustomerNPC customer;
        [SerializeField, Min(0f)] private float nextCustomerDelay = 2f;

        private readonly List<CustomerDefinition> dayOneOrder = new();
        private readonly List<Day2RequestRuntimeState> dayTwoRequests = new();
        private readonly List<Day2RequestRuntimeState> dayTwoOrder = new();
        private int nextCustomerIndex;
        private bool transitionInProgress;
        private bool customerActive;
        private Day2RequestRuntimeState currentDayTwoRequest;
        private string currentLetterId = string.Empty;
        private string currentCustomerId = string.Empty;
        private string currentCustomerDisplayName = string.Empty;

        public IReadOnlyList<Day2RequestRuntimeState> DayTwoRequests => dayTwoRequests;
        public IReadOnlyList<Day2RequestRuntimeState> DayTwoOrder => dayTwoOrder;
        public Day2RequestRuntimeState CurrentDayTwoRequest => currentDayTwoRequest;
        public string CurrentCustomerId => currentCustomerId;
        public string CurrentCustomerDisplayName => currentCustomerDisplayName;

        public string GetCurrentOrderSummary()
        {
            return taskState != null && taskState.CurrentDay == 2
                ? string.Join(" → ", dayTwoOrder.Select(request => request.Definition.CustomerDisplayName))
                : string.Join(" → ", dayOneOrder.Select(request => request.DisplayName));
        }

        private void Awake()
        {
            if (taskState != null)
                taskState.Changed += HandleTaskStateChanged;
        }

        private void OnDestroy()
        {
            if (taskState != null)
                taskState.Changed -= HandleTaskStateChanged;
        }

        private void Start()
        {
            PrepareDay();
        }

        public void NotifyCustomerDeparted()
        {
            if (!customerActive || taskState == null || !taskState.LetterMailed)
                return;

            if (taskState.CurrentDay == 2 && (currentDayTwoRequest == null || !currentDayTwoRequest.Completed))
                return;

            customerActive = false;
            if (!taskState.CompleteCurrentRequest())
                return;
            currentDayTwoRequest = null;
            currentLetterId = string.Empty;
            currentCustomerId = string.Empty;
            currentCustomerDisplayName = string.Empty;
            if (!taskState.RequiredDayComplete)
                StartCoroutine(BeginNextCustomerAfterDelay());
        }

        public void NotifyRequestAccepted(string letterId)
        {
            if (taskState == null || taskState.CurrentDay != 2 || currentDayTwoRequest == null ||
                !string.Equals(currentLetterId, letterId, System.StringComparison.Ordinal))
                return;
            currentDayTwoRequest.AcceptRequest();
            HandleTaskStateChanged();
        }

        public bool CanFinishCurrentCustomer(string customerId, string letterId)
        {
            if (taskState == null || !taskState.LetterMailed)
                return false;
            if (taskState.CurrentDay != 2)
                return true;
            if (!MatchesCurrentDayTwoRequest(customerId, letterId))
                return false;

            return currentDayTwoRequest.Definition.RequestType == MailRequestType.Dispatch
                ? currentDayTwoRequest.ConveyorDispatched && !currentDayTwoRequest.CompletionNoticeGiven
                : currentDayTwoRequest.Packed && currentDayTwoRequest.HasOnlyRequiredBoxPictograms &&
                  !currentDayTwoRequest.DeliveredToCustomer;
        }

        public bool TryFinishCurrentCustomer(string customerId, string letterId)
        {
            if (!CanFinishCurrentCustomer(customerId, letterId))
                return false;
            if (taskState.CurrentDay != 2)
                return true;

            return currentDayTwoRequest.Definition.RequestType == MailRequestType.Dispatch
                ? currentDayTwoRequest.TryRecordCompletionNotice()
                : currentDayTwoRequest.TryDeliverTo(customerId);
        }

        public bool TryAdvanceDay()
        {
            if (transitionInProgress || taskState == null || !taskState.RequiredDayComplete)
                return false;

            // DAY 2 -> DAY 3 gameplay is intentionally outside DAY 2 stage 1.
            if (taskState.CurrentDay >= 2)
                return false;

            transitionInProgress = true;
            StopAllCoroutines();
            if (customer != null)
                customer.HideUntilNextCustomer();
            taskState.BeginNextDay();
            PrepareDay();
            transitionInProgress = false;
            return true;
        }

        private void PrepareDay()
        {
            StopAllCoroutines();
            dayOneOrder.Clear();
            dayTwoRequests.Clear();
            dayTwoOrder.Clear();
            currentDayTwoRequest = null;
            currentLetterId = string.Empty;
            currentCustomerId = string.Empty;
            currentCustomerDisplayName = string.Empty;

            if (taskState != null && taskState.CurrentDay == 2)
                PrepareDayTwo();
            else
                PrepareDayOne();

            nextCustomerIndex = 0;
            customerActive = false;
            BeginNextCustomer();
        }

        private void PrepareDayOne()
        {
            dayOneOrder.Add(new CustomerDefinition("mascot", "인형탈", LetterStamp.Fire | LetterStamp.Shock));
            dayOneOrder.Add(new CustomerDefinition("drunk_driver", "음주운전", LetterStamp.Shock | LetterStamp.Alcohol));
            dayOneOrder.Add(new CustomerDefinition("model_student", "모범생", LetterStamp.Medicine));
            dayOneOrder.Add(new CustomerDefinition("bad_construction", "부실공사", LetterStamp.Electricity | LetterStamp.Shock));
            dayOneOrder.Add(new CustomerDefinition("troublemaker", "불량이", LetterStamp.Water));

            for (int index = dayOneOrder.Count - 1; index > 0; index--)
            {
                int swapIndex = Random.Range(0, index + 1);
                (dayOneOrder[index], dayOneOrder[swapIndex]) = (dayOneOrder[swapIndex], dayOneOrder[index]);
            }
        }

        private void PrepareDayTwo()
        {
            IReadOnlyList<Day2RequestDefinition> definitions = Day2RequestCatalog.CreateDefinitions();
            dayTwoRequests.AddRange(definitions.Select(definition => new Day2RequestRuntimeState(definition)));
            Dictionary<string, Day2RequestRuntimeState> requestsById = dayTwoRequests.ToDictionary(
                request => request.Definition.RequestId,
                request => request);
            int seed = Random.Range(0, int.MaxValue);
            foreach (Day2RequestDefinition definition in Day2RequestCatalog.CreateRandomOrder(seed))
                dayTwoOrder.Add(requestsById[definition.RequestId]);
        }

        private IEnumerator BeginNextCustomerAfterDelay()
        {
            yield return new WaitForSeconds(nextCustomerDelay);
            BeginNextCustomer();
        }

        private void BeginNextCustomer()
        {
            if (customer == null || taskState == null || taskState.RequiredDayComplete)
                return;

            if (taskState.CurrentDay == 2)
            {
                if (nextCustomerIndex >= dayTwoOrder.Count)
                    return;

                currentDayTwoRequest = dayTwoOrder[nextCustomerIndex++];
                Day2RequestDefinition definition = currentDayTwoRequest.Definition;
                currentLetterId = $"{definition.RequestId}_letter";
                currentCustomerId = definition.CustomerId;
                currentCustomerDisplayName = definition.CustomerDisplayName;
                customerActive = true;
                customer.BeginCustomer(
                    definition.CustomerId,
                    definition.CustomerDisplayName,
                    currentLetterId,
                    definition.RequiredPictograms,
                    this);
                return;
            }

            if (nextCustomerIndex >= dayOneOrder.Count)
                return;

            CustomerDefinition dayOneDefinition = dayOneOrder[nextCustomerIndex++];
            string letterId = $"day_{taskState.CurrentDay:00}_{dayOneDefinition.Id}";
            currentCustomerId = dayOneDefinition.Id;
            currentCustomerDisplayName = dayOneDefinition.DisplayName;
            customerActive = true;
            customer.BeginCustomer(
                dayOneDefinition.Id,
                dayOneDefinition.DisplayName,
                letterId,
                dayOneDefinition.RequiredStamps,
                this);
        }

        private bool MatchesCurrentDayTwoRequest(string customerId, string letterId)
        {
            return currentDayTwoRequest != null &&
                   string.Equals(currentDayTwoRequest.Definition.CustomerId, customerId, System.StringComparison.Ordinal) &&
                   string.Equals(currentLetterId, letterId, System.StringComparison.Ordinal);
        }

        private void HandleTaskStateChanged()
        {
            if (taskState == null || taskState.CurrentDay != 2 || currentDayTwoRequest == null)
                return;
            currentDayTwoRequest.SetLetterProcessingState(taskState.AttachedStamps, taskState.PostmarkApplied);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool DebugStartDay(int day)
        {
            if (taskState == null || customer == null || (day != 1 && day != 2))
                return false;

            StopAllCoroutines();
            customer.HideUntilNextCustomer();
            taskState.DebugResetForDay(day);
            PrepareDay();
            return true;
        }

        public bool DebugRegenerateCurrentDay()
        {
            return taskState != null && DebugStartDay(taskState.CurrentDay == 2 ? 2 : 1);
        }

        public bool DebugResetCurrentDay()
        {
            return taskState != null && DebugStartDay(taskState.CurrentDay == 2 ? 2 : 1);
        }

        public bool DebugSelectDayTwoRequest(string requestId)
        {
            if (taskState == null || customer == null)
                return false;
            if (taskState.CurrentDay != 2 && !DebugStartDay(2))
                return false;

            Day2RequestRuntimeState selected = dayTwoRequests.FirstOrDefault(
                request => request.Definition.RequestId == requestId);
            if (selected == null)
                return false;

            StopAllCoroutines();
            customer.HideUntilNextCustomer();
            taskState.DebugClearCurrentRequest();
            selected.Reset();
            currentDayTwoRequest = selected;
            int orderIndex = dayTwoOrder.IndexOf(selected);
            nextCustomerIndex = orderIndex >= 0 ? orderIndex + 1 : dayTwoOrder.Count;
            BeginDayTwoCustomer(selected);
            return true;
        }

        public bool DebugRestartCurrentRequest()
        {
            return currentDayTwoRequest != null && DebugSelectDayTwoRequest(currentDayTwoRequest.Definition.RequestId);
        }

        public bool DebugAcceptCurrentRequest()
        {
            if (taskState == null || currentDayTwoRequest == null || taskState.HasLetter)
                return false;
            Day2RequestDefinition definition = currentDayTwoRequest.Definition;
            if (!taskState.TryReceiveLetter(
                    currentLetterId,
                    definition.CustomerId,
                    definition.CustomerDisplayName,
                    definition.RequiredPictograms))
                return false;
            NotifyRequestAccepted(currentLetterId);
            return currentDayTwoRequest.RequestAccepted;
        }

        public bool DebugSecureAllCurrentItems()
        {
            if (currentDayTwoRequest == null)
                return false;
            foreach (Day2ItemType item in currentDayTwoRequest.Definition.RequestedItems)
                currentDayTwoRequest.SecureItem(item);
            return currentDayTwoRequest.ItemsSecured;
        }

        public bool DebugSecureCurrentBox()
        {
            return currentDayTwoRequest != null &&
                   currentDayTwoRequest.SecureEmptyBox($"debug_box_{currentDayTwoRequest.Definition.RequestId}");
        }

        public bool DebugPackCurrentRequest()
        {
            return currentDayTwoRequest != null && currentDayTwoRequest.TryPack();
        }

        public bool DebugProcessCurrentLetter()
        {
            if (taskState == null || currentDayTwoRequest == null || !taskState.HasLetter)
                return false;

            foreach (LetterStamp stamp in GetIndividualStamps(currentDayTwoRequest.Definition.RequiredPictograms))
                taskState.AttachStamp(stamp);
            if (!taskState.PostmarkApplied)
                taskState.ApplyPostmark();
            if (!taskState.LetterMailed)
                taskState.TryMailLetter();
            currentDayTwoRequest.SetLetterProcessingState(taskState.AttachedStamps, taskState.PostmarkApplied);
            return taskState.LetterMailed;
        }

        public bool DebugApplyCurrentBoxPictograms()
        {
            if (currentDayTwoRequest == null)
                return false;
            foreach (LetterStamp stamp in GetIndividualStamps(currentDayTwoRequest.Definition.RequiredPictograms))
                currentDayTwoRequest.ApplyBoxPictogram(stamp);
            return currentDayTwoRequest.HasOnlyRequiredBoxPictograms;
        }

        public bool DebugDispatchCurrentRequest()
        {
            return currentDayTwoRequest != null && currentDayTwoRequest.TryRecordConveyorDispatch();
        }

        public bool DebugDeliverCurrentRequest(string customerId)
        {
            return currentDayTwoRequest != null && currentDayTwoRequest.TryDeliverTo(customerId);
        }

        public bool DebugRecordCurrentCompletionNotice()
        {
            return currentDayTwoRequest != null && currentDayTwoRequest.TryRecordCompletionNotice();
        }

        private static IEnumerable<LetterStamp> GetIndividualStamps(LetterStamp stamps)
        {
            foreach (LetterStamp value in System.Enum.GetValues(typeof(LetterStamp)))
            {
                if (value != LetterStamp.None && (stamps & value) == value)
                    yield return value;
            }
        }
#endif

        private void BeginDayTwoCustomer(Day2RequestRuntimeState request)
        {
            Day2RequestDefinition definition = request.Definition;
            currentLetterId = $"{definition.RequestId}_letter";
            currentCustomerId = definition.CustomerId;
            currentCustomerDisplayName = definition.CustomerDisplayName;
            customerActive = true;
            customer.BeginCustomer(
                definition.CustomerId,
                definition.CustomerDisplayName,
                currentLetterId,
                definition.RequiredPictograms,
                this);
        }
    }
}
