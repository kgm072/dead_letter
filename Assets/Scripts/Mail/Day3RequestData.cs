using System;
using System.Collections.Generic;
using System.Linq;

namespace DeadLetter.Mail
{
    public static class Day3RequestCatalog
    {
        public const string MascotRequestId = "day_03_mascot_receive";
        public const string DrunkDriverRequestId = "day_03_drunk_driver_dispatch";
        public const string ModelStudentRequestId = "day_03_model_student_receive";
        public const string BadConstructionRequestId = "day_03_bad_construction_receive";
        public const string TroublemakerRequestId = "day_03_troublemaker_dispatch";
        public const string StudentCareTransferLinkId = "day_03_student_care_transfer";

        public static IReadOnlyList<Day2RequestDefinition> CreateDefinitions()
        {
            return new[]
            {
                // The latest direction fixes this as a receive request, but the planning document
                // still leaves the received item undecided. Keeping the requirement unconfirmed
                // prevents an empty item list from being treated as completed work.
                new Day2RequestDefinition(
                    MascotRequestId, "mascot", "인형탈", MailRequestType.Receive,
                    Array.Empty<Day2ItemType>(),
                    LetterAppearanceType.NearlyBurnedBlack, LetterStamp.Fire | LetterStamp.Shock,
                    day: 3, itemRequirementsConfirmed: false),
                new Day2RequestDefinition(
                    DrunkDriverRequestId, "drunk_driver", "음주운전자", MailRequestType.Dispatch,
                    new[] { Day2ItemType.DriverLicense },
                    LetterAppearanceType.DesignatedDriverCardAndBlood, LetterStamp.Shock | LetterStamp.Alcohol,
                    day: 3),
                new Day2RequestDefinition(
                    ModelStudentRequestId, "model_student", "모범생", MailRequestType.Receive,
                    new[] { Day2ItemType.Soap, Day2ItemType.Bandage },
                    LetterAppearanceType.RepeatedPrescription, LetterStamp.Medicine,
                    TroublemakerRequestId, StudentCareTransferLinkId,
                    day: 3, requiresRelatedRequestCompletionForItems: true),
                new Day2RequestDefinition(
                    BadConstructionRequestId, "bad_construction", "부실공사", MailRequestType.Receive,
                    new[] { Day2ItemType.Soju, Day2ItemType.DriedSnack },
                    LetterAppearanceType.ElectricRootScorch, LetterStamp.Electricity | LetterStamp.Shock,
                    day: 3),
                new Day2RequestDefinition(
                    TroublemakerRequestId, "troublemaker", "불량이", MailRequestType.Dispatch,
                    new[] { Day2ItemType.Soap, Day2ItemType.Bandage },
                    LetterAppearanceType.WetBloodAndFishHooks, LetterStamp.Water,
                    ModelStudentRequestId, StudentCareTransferLinkId,
                    day: 3)
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

            Day2RequestCatalog.EnforcePrecedence(order, TroublemakerRequestId, ModelStudentRequestId);
            return order;
        }
    }
}
