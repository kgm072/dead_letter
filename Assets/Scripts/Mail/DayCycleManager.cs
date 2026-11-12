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
        private readonly Dictionary<string, string> claimedWorldItemOwners = new();
        private readonly Dictionary<string, string> claimedBoxOwners = new();
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
        public string CurrentLetterId => currentLetterId;
        public string CurrentCustomerId => currentCustomerId;
        public string CurrentCustomerDisplayName => currentCustomerDisplayName;
        public bool IsDayTwoActive => taskState != null && taskState.CurrentDay == 2;

        public event System.Action DayTwoStateChanged;
        public event System.Action<string> ActionFeedback;

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
            NotifyDayTwoStateChanged();
        }

        public bool IsWorldItemClaimed(string instanceId)
        {
            return !string.IsNullOrWhiteSpace(instanceId) && claimedWorldItemOwners.ContainsKey(instanceId);
        }

        public bool IsBoxClaimed(string boxId)
        {
            return !string.IsNullOrWhiteSpace(boxId) && claimedBoxOwners.ContainsKey(boxId);
        }

        public bool CanAcquireCurrentItem(
            string instanceId,
            string requestId,
            Day2ItemType item,
            Day2ItemSource source,
            out string reason)
        {
            if (!TryGetAcceptedCurrentRequest(requestId, out Day2RequestRuntimeState request, out reason))
                return false;
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                reason = "물품 식별값이 없습니다.";
                return false;
            }
            if (claimedWorldItemOwners.ContainsKey(instanceId))
            {
                reason = "이미 사용한 물품입니다.";
                return false;
            }
            Day2ItemSource requiredSource = request.Definition.RequestType == MailRequestType.Dispatch
                ? Day2ItemSource.Counter
                : Day2ItemSource.Storage;
            if (source != requiredSource)
            {
                reason = requiredSource == Day2ItemSource.Counter
                    ? "발송 물품은 창구에서 받아야 합니다."
                    : "수령 물품은 창고에서 확보해야 합니다.";
                return false;
            }
            if (!request.Definition.RequestedItems.Contains(item))
            {
                reason = "현재 민원의 요청 물품이 아닙니다.";
                return false;
            }
            if (request.SecuredItems.Contains(item))
            {
                reason = "이미 확보한 물품입니다.";
                return false;
            }
            if (request.Definition.RequestId == Day2RequestCatalog.TroublemakerRequestId &&
                item == Day2ItemType.BulliedUniform && !IsRelatedRequestComplete(request.Definition.RelatedRequestId))
            {
                reason = "모범생의 교복 발송이 아직 완료되지 않았습니다.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public bool TryAcquireCurrentItem(string instanceId, string requestId, Day2ItemType item, Day2ItemSource source)
        {
            if (!CanAcquireCurrentItem(instanceId, requestId, item, source, out string reason))
                return Fail(reason);

            if (!currentDayTwoRequest.SecureItem(item, instanceId))
                return Fail("물품 상태를 기록하지 못했습니다.");

            claimedWorldItemOwners.Add(instanceId, currentDayTwoRequest.Definition.RequestId);
            NotifyDayTwoStateChanged();
            return Succeed($"{Day2Labels.GetItemLabel(item)} 확보 완료");
        }

        public bool CanAcquireEmptyBox(string boxId, out string reason)
        {
            if (!TryGetAcceptedCurrentRequest(string.Empty, out Day2RequestRuntimeState request, out reason, false))
                return false;
            if (string.IsNullOrWhiteSpace(boxId))
            {
                reason = "박스 식별값이 없습니다.";
                return false;
            }
            if (claimedBoxOwners.ContainsKey(boxId))
            {
                reason = "이미 사용한 박스입니다.";
                return false;
            }
            if (request.EmptyBoxSecured)
            {
                reason = "현재 민원에 이미 빈 박스를 연결했습니다.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public bool TryAcquireEmptyBox(string boxId)
        {
            if (!CanAcquireEmptyBox(boxId, out string reason))
                return Fail(reason);
            if (!currentDayTwoRequest.SecureEmptyBox(boxId))
                return Fail("빈 박스 상태를 기록하지 못했습니다.");

            claimedBoxOwners.Add(boxId, currentDayTwoRequest.Definition.RequestId);
            NotifyDayTwoStateChanged();
            return Succeed("빈 박스 확보 완료");
        }

        public bool CanPackCurrentRequest(out string reason)
        {
            if (!TryGetAcceptedCurrentRequest(string.Empty, out Day2RequestRuntimeState request, out reason, false))
                return false;
            if (!request.ItemsSecured)
            {
                reason = "요청 물품을 모두 확보해야 합니다.";
                return false;
            }
            if (!request.EmptyBoxSecured)
            {
                reason = "창고에서 빈 박스를 확보해야 합니다.";
                return false;
            }
            if (request.Packed)
            {
                reason = "이미 포장한 민원입니다.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public bool TryPackCurrentRequest()
        {
            if (!CanPackCurrentRequest(out string reason))
                return Fail(reason);
            if (!currentDayTwoRequest.TryPack())
                return Fail("포장 상태를 기록하지 못했습니다.");

            NotifyDayTwoStateChanged();
            return Succeed("포장 완료 — 편지와 박스 도장을 처리하세요.");
        }

        public bool CanApplyBoxPictogram(LetterStamp pictogram, out string reason)
        {
            if (!TryGetAcceptedCurrentRequest(string.Empty, out Day2RequestRuntimeState request, out reason, false))
                return false;
            if (!request.Packed)
            {
                reason = "먼저 물품을 포장해야 합니다.";
                return false;
            }
            if (request.ConveyorDispatched || request.DeliveredToCustomer || request.Completed)
            {
                reason = "이미 인계된 박스에는 날인할 수 없습니다.";
                return false;
            }
            int value = (int)pictogram;
            if (value <= 0 || (value & (value - 1)) != 0)
            {
                reason = "박스 도장은 한 종류씩 선택해야 합니다.";
                return false;
            }
            if ((request.AppliedBoxPictograms & pictogram) == pictogram)
            {
                reason = "이미 찍은 박스 도장입니다.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public bool TryApplyBoxPictogram(LetterStamp pictogram)
        {
            if (!CanApplyBoxPictogram(pictogram, out string reason))
                return Fail(reason);
            if (!currentDayTwoRequest.ApplyBoxPictogram(pictogram))
                return Fail("박스 도장 상태를 기록하지 못했습니다.");

            NotifyDayTwoStateChanged();
            return Succeed($"박스에 {Day2Labels.GetStampLabel(pictogram)} 도장을 찍었습니다.");
        }

        public bool CanDispatchCurrentPackage(out string reason)
        {
            if (!TryGetAcceptedCurrentRequest(string.Empty, out Day2RequestRuntimeState request, out reason, false))
                return false;
            if (request.Definition.RequestType != MailRequestType.Dispatch)
            {
                reason = "수령 민원은 컨베이어가 아니라 민원인에게 전달해야 합니다.";
                return false;
            }
            if (!request.Packed)
            {
                reason = "먼저 박스를 포장해야 합니다.";
                return false;
            }
            if (!request.HasOnlyRequiredBoxPictograms)
            {
                reason = "박스의 필수 픽토그램 도장을 확인하세요.";
                return false;
            }
            if (taskState == null || !taskState.LetterMailed || !request.LetterProcessingComplete)
            {
                reason = "편지 우표·도장을 마치고 우편함에 넣어야 합니다.";
                return false;
            }
            if (request.ConveyorDispatched)
            {
                reason = "이미 발송한 박스입니다.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public bool TryDispatchCurrentPackage()
        {
            if (!CanDispatchCurrentPackage(out string reason))
                return Fail(reason);
            if (!currentDayTwoRequest.TryRecordConveyorDispatch())
                return Fail("컨베이어 발송 상태를 기록하지 못했습니다.");

            NotifyDayTwoStateChanged();
            return Succeed("컨베이어 발송 완료 — 창구에서 민원인에게 알려 주세요.");
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

            bool completed = currentDayTwoRequest.Definition.RequestType == MailRequestType.Dispatch
                ? currentDayTwoRequest.TryRecordCompletionNotice()
                : currentDayTwoRequest.TryDeliverTo(customerId);
            if (completed)
            {
                NotifyDayTwoStateChanged();
                Succeed(currentDayTwoRequest.Definition.RequestType == MailRequestType.Dispatch
                    ? "발송 완료 안내를 전달했습니다."
                    : "해당 민원인에게 포장 물품을 전달했습니다.");
            }
            return completed;
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
            claimedWorldItemOwners.Clear();
            claimedBoxOwners.Clear();
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
            NotifyDayTwoStateChanged();
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
                NotifyDayTwoStateChanged();
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
            NotifyDayTwoStateChanged();
        }

        private bool TryGetAcceptedCurrentRequest(
            string requestId,
            out Day2RequestRuntimeState request,
            out string reason,
            bool requireRequestId = true)
        {
            request = currentDayTwoRequest;
            if (!IsDayTwoActive || request == null)
            {
                reason = "DAY 2 민원이 진행 중일 때만 사용할 수 있습니다.";
                return false;
            }
            if (requireRequestId && !string.Equals(request.Definition.RequestId, requestId, System.StringComparison.Ordinal))
            {
                reason = "현재 민원에 연결된 물품이 아닙니다.";
                return false;
            }
            if (!request.RequestAccepted)
            {
                reason = "먼저 창구에서 민원을 접수해야 합니다.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private bool IsRelatedRequestComplete(string requestId)
        {
            return dayTwoRequests.Any(request =>
                request.Definition.RequestId == requestId && request.Completed);
        }

        private void NotifyDayTwoStateChanged()
        {
            DayTwoStateChanged?.Invoke();
        }

        private bool Fail(string message)
        {
            ActionFeedback?.Invoke(message);
            return false;
        }

        private bool Succeed(string message)
        {
            ActionFeedback?.Invoke(message);
            return true;
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
            ReleaseClaimsForRequest(selected.Definition.RequestId);
            currentDayTwoRequest = selected;
            int orderIndex = dayTwoOrder.IndexOf(selected);
            nextCustomerIndex = orderIndex >= 0 ? orderIndex + 1 : dayTwoOrder.Count;
            BeginDayTwoCustomer(selected);
            NotifyDayTwoStateChanged();
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
            NotifyDayTwoStateChanged();
            return currentDayTwoRequest.ItemsSecured;
        }

        public bool DebugSecureCurrentBox()
        {
            bool result = currentDayTwoRequest != null &&
                          currentDayTwoRequest.SecureEmptyBox($"debug_box_{currentDayTwoRequest.Definition.RequestId}");
            if (result) NotifyDayTwoStateChanged();
            return result;
        }

        public bool DebugPackCurrentRequest()
        {
            bool result = currentDayTwoRequest != null && currentDayTwoRequest.TryPack();
            if (result) NotifyDayTwoStateChanged();
            return result;
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
            NotifyDayTwoStateChanged();
            return taskState.LetterMailed;
        }

        public bool DebugApplyCurrentBoxPictograms()
        {
            if (currentDayTwoRequest == null)
                return false;
            foreach (LetterStamp stamp in GetIndividualStamps(currentDayTwoRequest.Definition.RequiredPictograms))
                currentDayTwoRequest.ApplyBoxPictogram(stamp);
            NotifyDayTwoStateChanged();
            return currentDayTwoRequest.HasOnlyRequiredBoxPictograms;
        }

        public bool DebugDispatchCurrentRequest()
        {
            bool result = currentDayTwoRequest != null && currentDayTwoRequest.TryRecordConveyorDispatch();
            if (result) NotifyDayTwoStateChanged();
            return result;
        }

        public bool DebugDeliverCurrentRequest(string customerId)
        {
            bool result = currentDayTwoRequest != null && currentDayTwoRequest.TryDeliverTo(customerId);
            if (result) NotifyDayTwoStateChanged();
            return result;
        }

        public bool DebugRecordCurrentCompletionNotice()
        {
            bool result = currentDayTwoRequest != null && currentDayTwoRequest.TryRecordCompletionNotice();
            if (result) NotifyDayTwoStateChanged();
            return result;
        }

        private void ReleaseClaimsForRequest(string requestId)
        {
            foreach (string key in claimedWorldItemOwners.Where(pair => pair.Value == requestId).Select(pair => pair.Key).ToArray())
                claimedWorldItemOwners.Remove(key);
            foreach (string key in claimedBoxOwners.Where(pair => pair.Value == requestId).Select(pair => pair.Key).ToArray())
                claimedBoxOwners.Remove(key);
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
