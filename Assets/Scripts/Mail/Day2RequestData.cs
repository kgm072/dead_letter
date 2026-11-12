using System;
using System.Collections.Generic;
using System.Linq;

namespace DeadLetter.Mail
{
    public enum MailRequestType
    {
        Dispatch,
        Receive
    }

    public enum Day2ItemType
    {
        Cigarettes,
        Lighter,
        HangoverRemedy,
        BulliedUniform,
        SafetyHelmet,
        InsulatedGloves
    }

    public enum Day2ItemSource
    {
        Counter,
        Storage
    }

    public enum LetterAppearanceType
    {
        LightlyScorched,
        DesignatedDriverCard,
        MilkAndShoeMarks,
        CementDust,
        WaterSoaked
    }

    public enum MailRequestProgressStage
    {
        WaitingForAcceptance,
        Accepted,
        ItemsSecured,
        EmptyBoxSecured,
        Packed,
        LetterProcessed,
        BoxMarked,
        Dispatched,
        Delivered,
        CompletionNoticeGiven,
        Completed
    }

    public sealed class Day2RequestDefinition
    {
        public Day2RequestDefinition(
            string requestId,
            string customerId,
            string customerDisplayName,
            MailRequestType requestType,
            IEnumerable<Day2ItemType> requestedItems,
            LetterAppearanceType letterAppearance,
            LetterStamp requiredPictograms,
            string relatedRequestId = "",
            string transferLinkId = "")
        {
            RequestId = requestId;
            CustomerId = customerId;
            CustomerDisplayName = customerDisplayName;
            Day = 2;
            RequestType = requestType;
            RequestedItems = requestedItems?.Distinct().ToArray() ?? Array.Empty<Day2ItemType>();
            LetterAppearance = letterAppearance;
            RequiredPictograms = requiredPictograms;
            RelatedRequestId = relatedRequestId ?? string.Empty;
            TransferLinkId = transferLinkId ?? string.Empty;
        }

        public string RequestId { get; }
        public string CustomerId { get; }
        public string CustomerDisplayName { get; }
        public int Day { get; }
        public MailRequestType RequestType { get; }
        public IReadOnlyList<Day2ItemType> RequestedItems { get; }
        public LetterAppearanceType LetterAppearance { get; }
        public LetterStamp RequiredPictograms { get; }
        public string RelatedRequestId { get; }
        public string TransferLinkId { get; }
    }

    public sealed class Day2RequestRuntimeState
    {
        private readonly HashSet<Day2ItemType> securedItems = new();
        private readonly Dictionary<Day2ItemType, string> securedItemInstanceIds = new();
        private readonly List<Day2ItemType> packedItems = new();

        public Day2RequestRuntimeState(Day2RequestDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public Day2RequestDefinition Definition { get; }
        public IReadOnlyCollection<Day2ItemType> SecuredItems => securedItems;
        public IReadOnlyDictionary<Day2ItemType, string> SecuredItemInstanceIds => securedItemInstanceIds;
        public IReadOnlyList<Day2ItemType> PackedItems => packedItems;
        public bool RequestAccepted { get; private set; }
        public bool ItemsSecured => Definition.RequestedItems.All(securedItems.Contains);
        public bool EmptyBoxSecured { get; private set; }
        public string BoxId { get; private set; } = string.Empty;
        public bool Packed { get; private set; }
        public LetterStamp AttachedLetterStamps { get; private set; }
        public bool LetterPostmarkApplied { get; private set; }
        public LetterStamp AppliedBoxPictograms { get; private set; }
        public bool ConveyorDispatched { get; private set; }
        public bool DeliveredToCustomer { get; private set; }
        public bool CompletionNoticeGiven { get; private set; }
        public bool Completed { get; private set; }
        public bool HasOnlyRequiredLetterStamps => AttachedLetterStamps == Definition.RequiredPictograms;
        public bool HasOnlyRequiredBoxPictograms => AppliedBoxPictograms == Definition.RequiredPictograms;
        public bool LetterProcessingComplete => HasOnlyRequiredLetterStamps && LetterPostmarkApplied;

        public MailRequestProgressStage ProgressStage
        {
            get
            {
                if (Completed) return MailRequestProgressStage.Completed;
                if (CompletionNoticeGiven) return MailRequestProgressStage.CompletionNoticeGiven;
                if (DeliveredToCustomer) return MailRequestProgressStage.Delivered;
                if (ConveyorDispatched) return MailRequestProgressStage.Dispatched;
                if (HasOnlyRequiredBoxPictograms) return MailRequestProgressStage.BoxMarked;
                if (HasOnlyRequiredLetterStamps && LetterPostmarkApplied) return MailRequestProgressStage.LetterProcessed;
                if (Packed) return MailRequestProgressStage.Packed;
                if (EmptyBoxSecured) return MailRequestProgressStage.EmptyBoxSecured;
                if (ItemsSecured) return MailRequestProgressStage.ItemsSecured;
                return RequestAccepted ? MailRequestProgressStage.Accepted : MailRequestProgressStage.WaitingForAcceptance;
            }
        }

        public void AcceptRequest()
        {
            RequestAccepted = true;
        }

        public bool SecureItem(Day2ItemType item)
        {
            return SecureItem(item, $"state_{Definition.RequestId}_{item}");
        }

        public bool SecureItem(Day2ItemType item, string instanceId)
        {
            if (!RequestAccepted || !Definition.RequestedItems.Contains(item) ||
                string.IsNullOrWhiteSpace(instanceId) || securedItems.Contains(item))
                return false;

            securedItems.Add(item);
            securedItemInstanceIds.Add(item, instanceId);
            return true;
        }

        public bool SecureEmptyBox(string boxId)
        {
            if (!RequestAccepted || EmptyBoxSecured || string.IsNullOrWhiteSpace(boxId))
                return false;
            EmptyBoxSecured = true;
            BoxId = boxId;
            return true;
        }

        public bool TryPack()
        {
            if (!RequestAccepted || !ItemsSecured || !EmptyBoxSecured || Packed)
                return false;
            packedItems.Clear();
            packedItems.AddRange(Definition.RequestedItems);
            Packed = true;
            return true;
        }

        public void SetLetterProcessingState(LetterStamp attachedStamps, bool postmarkApplied)
        {
            AttachedLetterStamps = attachedStamps;
            LetterPostmarkApplied = postmarkApplied;
        }

        public bool ApplyBoxPictogram(LetterStamp pictogram)
        {
            if (!Packed || !IsSinglePictogram(pictogram) || (AppliedBoxPictograms & pictogram) == pictogram)
                return false;
            AppliedBoxPictograms |= pictogram;
            return true;
        }

        private static bool IsSinglePictogram(LetterStamp pictogram)
        {
            int value = (int)pictogram;
            return value > 0 && (value & (value - 1)) == 0;
        }

        public bool TryRecordConveyorDispatch()
        {
            if (Definition.RequestType != MailRequestType.Dispatch || !Packed || !LetterProcessingComplete ||
                !HasOnlyRequiredBoxPictograms || ConveyorDispatched)
                return false;
            ConveyorDispatched = true;
            return true;
        }

        public bool TryRecordCompletionNotice()
        {
            if (Definition.RequestType != MailRequestType.Dispatch || !ConveyorDispatched || CompletionNoticeGiven)
                return false;
            CompletionNoticeGiven = true;
            Completed = true;
            return true;
        }

        public bool TryDeliverTo(string customerId)
        {
            if (Definition.RequestType != MailRequestType.Receive || !Packed || !LetterProcessingComplete ||
                !HasOnlyRequiredBoxPictograms || DeliveredToCustomer ||
                !string.Equals(Definition.CustomerId, customerId, StringComparison.Ordinal))
                return false;
            DeliveredToCustomer = true;
            Completed = true;
            return true;
        }

        public void Reset()
        {
            securedItems.Clear();
            securedItemInstanceIds.Clear();
            packedItems.Clear();
            RequestAccepted = false;
            EmptyBoxSecured = false;
            BoxId = string.Empty;
            Packed = false;
            AttachedLetterStamps = LetterStamp.None;
            LetterPostmarkApplied = false;
            AppliedBoxPictograms = LetterStamp.None;
            ConveyorDispatched = false;
            DeliveredToCustomer = false;
            CompletionNoticeGiven = false;
            Completed = false;
        }
    }

    public static class Day2RequestCatalog
    {
        public const string MascotRequestId = "day_02_mascot_dispatch";
        public const string DrunkDriverRequestId = "day_02_drunk_driver_receive";
        public const string ModelStudentRequestId = "day_02_model_student_dispatch";
        public const string BadConstructionRequestId = "day_02_bad_construction_receive";
        public const string TroublemakerRequestId = "day_02_troublemaker_receive";
        public const string StudentUniformTransferLinkId = "day_02_student_uniform_transfer";

        public static IReadOnlyList<Day2RequestDefinition> CreateDefinitions()
        {
            return new[]
            {
                new Day2RequestDefinition(
                    MascotRequestId, "mascot", "인형탈", MailRequestType.Dispatch,
                    new[] { Day2ItemType.Cigarettes, Day2ItemType.Lighter },
                    LetterAppearanceType.LightlyScorched, LetterStamp.Fire | LetterStamp.Shock),
                new Day2RequestDefinition(
                    DrunkDriverRequestId, "drunk_driver", "음주운전", MailRequestType.Receive,
                    new[] { Day2ItemType.HangoverRemedy },
                    LetterAppearanceType.DesignatedDriverCard, LetterStamp.Shock | LetterStamp.Alcohol),
                new Day2RequestDefinition(
                    ModelStudentRequestId, "model_student", "모범생", MailRequestType.Dispatch,
                    new[] { Day2ItemType.BulliedUniform },
                    LetterAppearanceType.MilkAndShoeMarks, LetterStamp.Medicine,
                    TroublemakerRequestId, StudentUniformTransferLinkId),
                new Day2RequestDefinition(
                    BadConstructionRequestId, "bad_construction", "부실공사", MailRequestType.Receive,
                    new[] { Day2ItemType.SafetyHelmet, Day2ItemType.InsulatedGloves },
                    LetterAppearanceType.CementDust, LetterStamp.Electricity | LetterStamp.Shock),
                new Day2RequestDefinition(
                    TroublemakerRequestId, "troublemaker", "불량이", MailRequestType.Receive,
                    new[] { Day2ItemType.BulliedUniform },
                    LetterAppearanceType.WaterSoaked, LetterStamp.Water,
                    ModelStudentRequestId, StudentUniformTransferLinkId)
            };
        }

        public static IReadOnlyList<Day2RequestDefinition> CreateRandomOrder(int seed)
        {
            List<Day2RequestDefinition> order = CreateDefinitions().ToList();
            Random random = new(seed);
            for (int index = order.Count - 1; index > 0; index--)
            {
                int swapIndex = random.Next(index + 1);
                (order[index], order[swapIndex]) = (order[swapIndex], order[index]);
            }

            EnforcePrecedence(order, ModelStudentRequestId, TroublemakerRequestId);
            return order;
        }

        public static void EnforcePrecedence(List<Day2RequestDefinition> order, string earlierRequestId, string laterRequestId)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));
            int earlierIndex = order.FindIndex(item => item.RequestId == earlierRequestId);
            int laterIndex = order.FindIndex(item => item.RequestId == laterRequestId);
            if (earlierIndex < 0 || laterIndex < 0)
                throw new InvalidOperationException("Both precedence requests must exist in the order.");
            if (earlierIndex > laterIndex)
                (order[earlierIndex], order[laterIndex]) = (order[laterIndex], order[earlierIndex]);
        }
    }
}
