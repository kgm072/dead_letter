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
        private readonly List<Day2RequestRuntimeState> dayThreeRequests = new();
        private readonly List<Day2RequestRuntimeState> dayThreeOrder = new();
        private readonly Dictionary<string, string> claimedWorldItemOwners = new();
        private readonly Dictionary<string, string> claimedBoxOwners = new();
        private int nextCustomerIndex;
        private bool transitionInProgress;
        private bool customerActive;
        private Day2RequestRuntimeState currentPackageRequest;
        private string currentLetterId = string.Empty;
        private string currentCustomerId = string.Empty;
        private string currentCustomerDisplayName = string.Empty;

        public IReadOnlyList<Day2RequestRuntimeState> DayTwoRequests => dayTwoRequests;
        public IReadOnlyList<Day2RequestRuntimeState> DayTwoOrder => dayTwoOrder;
        public IReadOnlyList<Day2RequestRuntimeState> DayThreeRequests => dayThreeRequests;
        public IReadOnlyList<Day2RequestRuntimeState> DayThreeOrder => dayThreeOrder;
        public Day2RequestRuntimeState CurrentPackageRequest => currentPackageRequest;
        public Day2RequestRuntimeState CurrentDayTwoRequest => IsDayTwoActive ? currentPackageRequest : null;
        public Day2RequestRuntimeState CurrentDayThreeRequest => IsDayThreeActive ? currentPackageRequest : null;
        public string CurrentLetterId => currentLetterId;
        public string CurrentCustomerId => currentCustomerId;
        public string CurrentCustomerDisplayName => currentCustomerDisplayName;
        public bool IsDayTwoActive => taskState != null && taskState.CurrentDay == 2;
        public bool IsDayThreeActive => taskState != null && taskState.CurrentDay == 3;
        public bool IsPackageDayActive => IsDayTwoActive || IsDayThreeActive;
        public bool IsDayThreeComplete => IsDayThreeActive && taskState.RequiredDayComplete;
        public bool AllCustomersDeparted => !customerActive && string.IsNullOrEmpty(currentCustomerId);
        public bool WaitingForNextCustomer => IsPackageDayActive && !customerActive &&
                                              taskState != null && !taskState.RequiredDayComplete;

        public event System.Action DayTwoStateChanged;
        public event System.Action DayThreeStateChanged;
        public event System.Action PackageStateChanged;
        public event System.Action<string> ActionFeedback;

        public string GetCurrentOrderSummary()
        {
            if (taskState == null)
                return string.Empty;
            return taskState.CurrentDay switch
            {
                2 => string.Join(" → ", dayTwoOrder.Select(request => request.Definition.CustomerDisplayName)),
                3 => string.Join(" → ", dayThreeOrder.Select(request => request.Definition.CustomerDisplayName)),
                _ => string.Join(" → ", dayOneOrder.Select(request => request.DisplayName))
            };
        }

        public bool IsRequestPartOfCurrentDay(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId))
                return false;
            return GetCurrentPackageRequests().Any(request =>
                string.Equals(request.Definition.RequestId, requestId, System.StringComparison.Ordinal));
        }

        public bool ShouldPresentWorldItem(string requestId, Day2ItemSource source)
        {
            if (!IsPackageDayActive || string.IsNullOrWhiteSpace(requestId))
                return false;

            Day2RequestRuntimeState request = GetCurrentPackageRequests().FirstOrDefault(candidate =>
                string.Equals(candidate.Definition.RequestId, requestId, System.StringComparison.Ordinal));
            if (request == null)
                return false;

            if (source == Day2ItemSource.Counter)
                return ReferenceEquals(request, currentPackageRequest);

            return !request.Definition.RequiresRelatedRequestCompletionForItems ||
                   IsRelatedRequestComplete(request.Definition.RelatedRequestId);
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

            if (IsPackageDayActive && (currentPackageRequest == null || !currentPackageRequest.Completed))
                return;

            customerActive = false;
            if (!taskState.CompleteCurrentRequest())
                return;
            currentPackageRequest = null;
            currentLetterId = string.Empty;
            currentCustomerId = string.Empty;
            currentCustomerDisplayName = string.Empty;
            if (!taskState.RequiredDayComplete)
                StartCoroutine(BeginNextCustomerAfterDelay());
        }

        public void NotifyRequestAccepted(string letterId)
        {
            if (taskState == null || !IsPackageDayActive || currentPackageRequest == null ||
                !string.Equals(currentLetterId, letterId, System.StringComparison.Ordinal))
                return;
            currentPackageRequest.AcceptRequest();
            HandleTaskStateChanged();
            NotifyPackageStateChanged();
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
            if (!request.Definition.ItemRequirementsConfirmed)
            {
                reason = "현재 민원의 물품 기획이 아직 확정되지 않았습니다.";
                return false;
            }
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
            if (request.Definition.RequiresRelatedRequestCompletionForItems &&
                !IsRelatedRequestComplete(request.Definition.RelatedRequestId))
            {
                reason = "연계된 선행 민원이 아직 완료되지 않았습니다.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public bool TryAcquireCurrentItem(string instanceId, string requestId, Day2ItemType item, Day2ItemSource source)
        {
            if (!CanAcquireCurrentItem(instanceId, requestId, item, source, out string reason))
                return Fail(reason);

            if (!currentPackageRequest.SecureItem(item, instanceId))
                return Fail("물품 상태를 기록하지 못했습니다.");

            claimedWorldItemOwners.Add(instanceId, currentPackageRequest.Definition.RequestId);
            NotifyPackageStateChanged();
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
            if (!currentPackageRequest.SecureEmptyBox(boxId))
                return Fail("빈 박스 상태를 기록하지 못했습니다.");

            claimedBoxOwners.Add(boxId, currentPackageRequest.Definition.RequestId);
            NotifyPackageStateChanged();
            return Succeed("빈 박스 확보 완료");
        }

        public bool CanPackCurrentRequest(out string reason)
        {
            if (!TryGetAcceptedCurrentRequest(string.Empty, out Day2RequestRuntimeState request, out reason, false))
                return false;
            if (!request.Definition.ItemRequirementsConfirmed)
            {
                reason = "현재 민원의 수령 물품이 아직 확정되지 않았습니다.";
                return false;
            }
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
            if (!currentPackageRequest.TryPack())
                return Fail("포장 상태를 기록하지 못했습니다.");

            NotifyPackageStateChanged();
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
            if (!currentPackageRequest.ApplyBoxPictogram(pictogram))
                return Fail("박스 도장 상태를 기록하지 못했습니다.");

            NotifyPackageStateChanged();
            return Succeed($"박스에 {Day2Labels.GetStampLabel(pictogram)} 도장을 찍었습니다.");
        }

        public bool CanToggleBoxPictogram(LetterStamp pictogram, out string reason)
        {
            int value = (int)pictogram;
            if (value <= 0 || (value & (value - 1)) != 0)
            {
                reason = "박스 도장은 한 종류씩 선택해야 합니다.";
                return false;
            }

            Day2RequestRuntimeState request = currentPackageRequest;
            if (request != null && (request.AppliedBoxPictograms & pictogram) == pictogram)
            {
                if (request.ConveyorDispatched || request.DeliveredToCustomer || request.Completed)
                {
                    reason = "이미 인계된 박스의 도장은 수정할 수 없습니다.";
                    return false;
                }
                reason = string.Empty;
                return request.Packed;
            }
            return CanApplyBoxPictogram(pictogram, out reason);
        }

        public bool TryToggleBoxPictogram(LetterStamp pictogram)
        {
            if (!CanToggleBoxPictogram(pictogram, out string reason))
                return Fail(reason);

            bool alreadyApplied = (currentPackageRequest.AppliedBoxPictograms & pictogram) == pictogram;
            bool changed = alreadyApplied
                ? currentPackageRequest.RemoveBoxPictogram(pictogram)
                : currentPackageRequest.ApplyBoxPictogram(pictogram);
            if (!changed)
                return Fail("박스 도장 상태를 변경하지 못했습니다.");

            NotifyPackageStateChanged();
            string label = Day2Labels.GetStampLabel(pictogram);
            return Succeed(alreadyApplied
                ? $"박스에서 {label} 도장을 제거했습니다."
                : $"박스에 {label} 도장을 찍었습니다.");
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
            if (!currentPackageRequest.TryRecordConveyorDispatch())
                return Fail("컨베이어 발송 상태를 기록하지 못했습니다.");

            NotifyPackageStateChanged();
            return Succeed("컨베이어 발송 완료 — 창구에서 민원인에게 알려 주세요.");
        }

        public bool CanFinishCurrentCustomer(string customerId, string letterId)
        {
            if (taskState == null || !taskState.LetterMailed)
                return false;
            if (!IsPackageDayActive)
                return true;
            if (!MatchesCurrentPackageRequest(customerId, letterId))
                return false;

            return currentPackageRequest.Definition.RequestType == MailRequestType.Dispatch
                ? currentPackageRequest.ConveyorDispatched && !currentPackageRequest.CompletionNoticeGiven
                : currentPackageRequest.Packed && currentPackageRequest.LetterProcessingComplete &&
                  currentPackageRequest.HasOnlyRequiredBoxPictograms && !currentPackageRequest.DeliveredToCustomer;
        }

        public bool TryFinishCurrentCustomer(string customerId, string letterId)
        {
            if (!CanFinishCurrentCustomer(customerId, letterId))
                return false;
            if (!IsPackageDayActive)
                return true;

            bool completed = currentPackageRequest.Definition.RequestType == MailRequestType.Dispatch
                ? currentPackageRequest.TryRecordCompletionNotice()
                : currentPackageRequest.TryDeliverTo(customerId);
            if (completed)
            {
                NotifyPackageStateChanged();
                Succeed(currentPackageRequest.Definition.RequestType == MailRequestType.Dispatch
                    ? "발송 완료 안내를 전달했습니다."
                    : "해당 민원인에게 포장 물품을 전달했습니다.");
            }
            return completed;
        }

        public bool TryAdvanceDay()
        {
            if (transitionInProgress || taskState == null || !taskState.RequiredDayComplete)
                return false;

            // DAY 3 completion is an explicit ending-entry point; this stage does not auto-run an ending.
            if (taskState.CurrentDay >= 3)
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
            dayThreeRequests.Clear();
            dayThreeOrder.Clear();
            claimedWorldItemOwners.Clear();
            claimedBoxOwners.Clear();
            currentPackageRequest = null;
            currentLetterId = string.Empty;
            currentCustomerId = string.Empty;
            currentCustomerDisplayName = string.Empty;

            if (taskState != null)
            {
                if (taskState.CurrentDay == 3)
                    PrepareDayThree();
                else if (taskState.CurrentDay == 2)
                    PrepareDayTwo();
                else
                    PrepareDayOne();
            }

            nextCustomerIndex = 0;
            customerActive = false;
            BeginNextCustomer();
            NotifyPackageStateChanged();
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

        private void PrepareDayThree()
        {
            IReadOnlyList<Day2RequestDefinition> definitions = Day3RequestCatalog.CreateDefinitions();
            dayThreeRequests.AddRange(definitions.Select(definition => new Day2RequestRuntimeState(definition)));
            Dictionary<string, Day2RequestRuntimeState> requestsById = dayThreeRequests.ToDictionary(
                request => request.Definition.RequestId,
                request => request);
            int seed = Random.Range(0, int.MaxValue);
            foreach (Day2RequestDefinition definition in Day3RequestCatalog.CreateRandomOrder(seed))
                dayThreeOrder.Add(requestsById[definition.RequestId]);
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

            if (IsPackageDayActive)
            {
                IReadOnlyList<Day2RequestRuntimeState> order = GetCurrentPackageOrder();
                if (nextCustomerIndex >= order.Count)
                    return;

                currentPackageRequest = order[nextCustomerIndex++];
                BeginPackageCustomer(currentPackageRequest);
                NotifyPackageStateChanged();
                return;
            }

            if (nextCustomerIndex >= dayOneOrder.Count)
                return;

            CustomerDefinition dayOneDefinition = dayOneOrder[nextCustomerIndex++];
            BeginDayOneCustomer(dayOneDefinition);
        }

        private void BeginDayOneCustomer(CustomerDefinition dayOneDefinition)
        {
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

        private bool MatchesCurrentPackageRequest(string customerId, string letterId)
        {
            return currentPackageRequest != null &&
                   string.Equals(currentPackageRequest.Definition.CustomerId, customerId, System.StringComparison.Ordinal) &&
                   string.Equals(currentLetterId, letterId, System.StringComparison.Ordinal);
        }

        private void HandleTaskStateChanged()
        {
            if (taskState == null || !IsPackageDayActive || currentPackageRequest == null)
                return;
            currentPackageRequest.SetLetterProcessingState(taskState.AttachedStamps, taskState.PostmarkApplied);
            NotifyPackageStateChanged();
        }

        private bool TryGetAcceptedCurrentRequest(
            string requestId,
            out Day2RequestRuntimeState request,
            out string reason,
            bool requireRequestId = true)
        {
            request = currentPackageRequest;
            if (!IsPackageDayActive || request == null)
            {
                reason = "택배 민원이 진행 중일 때만 사용할 수 있습니다.";
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
            return GetCurrentPackageRequests().Any(request =>
                request.Definition.RequestId == requestId && request.Completed);
        }

        private IReadOnlyList<Day2RequestRuntimeState> GetCurrentPackageRequests()
        {
            if (IsDayThreeActive)
                return dayThreeRequests;
            if (IsDayTwoActive)
                return dayTwoRequests;
            return System.Array.Empty<Day2RequestRuntimeState>();
        }

        private IReadOnlyList<Day2RequestRuntimeState> GetCurrentPackageOrder()
        {
            if (IsDayThreeActive)
                return dayThreeOrder;
            if (IsDayTwoActive)
                return dayTwoOrder;
            return System.Array.Empty<Day2RequestRuntimeState>();
        }

        private void NotifyPackageStateChanged()
        {
            PackageStateChanged?.Invoke();
            if (IsDayTwoActive)
                DayTwoStateChanged?.Invoke();
            if (IsDayThreeActive)
                DayThreeStateChanged?.Invoke();
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
            if (taskState == null || customer == null || day < 1 || day > 3)
                return false;

            StopAllCoroutines();
            customer.HideUntilNextCustomer();
            taskState.DebugResetForDay(day);
            PrepareDay();
            return true;
        }

        public bool DebugRegenerateCurrentDay()
        {
            return taskState != null && DebugStartDay(Mathf.Clamp(taskState.CurrentDay, 1, 3));
        }

        public bool DebugResetCurrentDay()
        {
            return taskState != null && DebugStartDay(Mathf.Clamp(taskState.CurrentDay, 1, 3));
        }

        public bool DebugSelectDayTwoRequest(string requestId)
        {
            return DebugSelectPackageRequest(requestId, 2);
        }

        public bool DebugSelectDayThreeRequest(string requestId)
        {
            return DebugSelectPackageRequest(requestId, 3);
        }

        public bool DebugSelectDayOneCustomer(string customerId)
        {
            if (taskState == null || customer == null || string.IsNullOrWhiteSpace(customerId))
                return false;
            if (taskState.CurrentDay != 1 && !DebugStartDay(1))
                return false;

            int selectedIndex = dayOneOrder.FindIndex(definition => definition.Id == customerId);
            if (selectedIndex < 0)
                return false;

            StopAllCoroutines();
            customer.HideUntilNextCustomer();
            taskState.DebugClearCurrentRequest();
            currentPackageRequest = null;
            nextCustomerIndex = selectedIndex + 1;
            customerActive = true;
            BeginDayOneCustomer(dayOneOrder[selectedIndex]);
            NotifyPackageStateChanged();
            return true;
        }

        public bool DebugSelectPackageRequest(string requestId, int day)
        {
            if (taskState == null || customer == null)
                return false;
            if ((day != 2 && day != 3) || (taskState.CurrentDay != day && !DebugStartDay(day)))
                return false;

            IReadOnlyList<Day2RequestRuntimeState> requests = GetCurrentPackageRequests();
            IReadOnlyList<Day2RequestRuntimeState> order = GetCurrentPackageOrder();
            Day2RequestRuntimeState selected = requests.FirstOrDefault(
                request => request.Definition.RequestId == requestId);
            if (selected == null)
                return false;

            StopAllCoroutines();
            customer.HideUntilNextCustomer();
            taskState.DebugClearCurrentRequest();
            selected.Reset();
            ReleaseClaimsForRequest(selected.Definition.RequestId);
            currentPackageRequest = selected;
            int orderIndex = IndexOfRequest(order, selected);
            nextCustomerIndex = orderIndex >= 0 ? orderIndex + 1 : order.Count;
            BeginPackageCustomer(selected);
            NotifyPackageStateChanged();
            return true;
        }

        public bool DebugRestartCurrentRequest()
        {
            if (taskState != null && taskState.CurrentDay == 1)
                return DebugSelectDayOneCustomer(currentCustomerId);
            return currentPackageRequest != null &&
                   DebugSelectPackageRequest(currentPackageRequest.Definition.RequestId, currentPackageRequest.Definition.Day);
        }

        public bool DebugAcceptCurrentRequest()
        {
            if (taskState == null || currentPackageRequest == null || taskState.HasLetter)
                return false;
            Day2RequestDefinition definition = currentPackageRequest.Definition;
            if (!taskState.TryReceiveLetter(
                    currentLetterId,
                    definition.CustomerId,
                    definition.CustomerDisplayName,
                    definition.RequiredPictograms))
                return false;
            NotifyRequestAccepted(currentLetterId);
            return currentPackageRequest.RequestAccepted;
        }

        public bool DebugSecureAllCurrentItems()
        {
            if (currentPackageRequest == null || !currentPackageRequest.Definition.ItemRequirementsConfirmed)
                return false;
            if (currentPackageRequest.Definition.RequiresRelatedRequestCompletionForItems &&
                !IsRelatedRequestComplete(currentPackageRequest.Definition.RelatedRequestId))
                return false;
            foreach (Day2ItemType item in currentPackageRequest.Definition.RequestedItems)
                currentPackageRequest.SecureItem(item);
            NotifyPackageStateChanged();
            return currentPackageRequest.ItemsSecured;
        }

        public bool DebugSecureCurrentBox()
        {
            bool result = currentPackageRequest != null &&
                          currentPackageRequest.SecureEmptyBox($"debug_box_{currentPackageRequest.Definition.RequestId}");
            if (result) NotifyPackageStateChanged();
            return result;
        }

        public bool DebugPackCurrentRequest()
        {
            bool result = currentPackageRequest != null && currentPackageRequest.TryPack();
            if (result) NotifyPackageStateChanged();
            return result;
        }

        public bool DebugProcessCurrentLetter()
        {
            if (taskState == null || currentPackageRequest == null || !taskState.HasLetter)
                return false;

            foreach (LetterStamp stamp in GetIndividualStamps(currentPackageRequest.Definition.RequiredPictograms))
                taskState.AttachStamp(stamp);
            if (!taskState.PostmarkApplied)
                taskState.ApplyPostmark();
            if (!taskState.LetterMailed)
                taskState.TryMailLetter();
            currentPackageRequest.SetLetterProcessingState(taskState.AttachedStamps, taskState.PostmarkApplied);
            NotifyPackageStateChanged();
            return taskState.LetterMailed;
        }

        public bool DebugApplyCurrentBoxPictograms()
        {
            if (currentPackageRequest == null)
                return false;
            foreach (LetterStamp stamp in GetIndividualStamps(currentPackageRequest.Definition.RequiredPictograms))
                currentPackageRequest.ApplyBoxPictogram(stamp);
            NotifyPackageStateChanged();
            return currentPackageRequest.HasOnlyRequiredBoxPictograms;
        }

        public bool DebugDispatchCurrentRequest()
        {
            bool result = currentPackageRequest != null && currentPackageRequest.TryRecordConveyorDispatch();
            if (result) NotifyPackageStateChanged();
            return result;
        }

        public bool DebugDeliverCurrentRequest(string customerId)
        {
            bool result = currentPackageRequest != null && currentPackageRequest.TryDeliverTo(customerId);
            if (result) NotifyPackageStateChanged();
            return result;
        }

        public bool DebugRecordCurrentCompletionNotice()
        {
            bool result = currentPackageRequest != null && currentPackageRequest.TryRecordCompletionNotice();
            if (result) NotifyPackageStateChanged();
            return result;
        }

        private static int IndexOfRequest(IReadOnlyList<Day2RequestRuntimeState> order, Day2RequestRuntimeState request)
        {
            for (int index = 0; index < order.Count; index++)
            {
                if (ReferenceEquals(order[index], request))
                    return index;
            }
            return -1;
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

        private void BeginPackageCustomer(Day2RequestRuntimeState request)
        {
            Day2RequestDefinition definition = request.Definition;
            request.MarkAppeared();
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
