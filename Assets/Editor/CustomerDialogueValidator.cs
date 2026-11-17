using System;
using System.Collections.Generic;
using System.Linq;
using DeadLetter.Mail;
using UnityEditor;
using UnityEngine;

namespace DeadLetter.Editor
{
    public static class CustomerDialogueValidator
    {
        [MenuItem("Tools/DEAD LETTER/Validate/DAY 1-3 Customer Dialogues")]
        public static void ValidateFromMenu()
        {
            ValidateAll();
            Debug.Log("DAY 1-3 customer dialogue validation passed.");
        }

        public static void ValidateBatch()
        {
            try
            {
                ValidateAll();
                Debug.Log("DAY1_3_DIALOGUE_VALIDATION_SUCCESS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void ValidateAll()
        {
            IReadOnlyList<CustomerDialogueScenario> scenarios = CustomerDialogueCatalog.All;
            Require(scenarios.Count == 15, "Exactly 15 dialogue scenarios are required.");
            Require(scenarios.Select(item => (item.Day, item.CustomerId)).Distinct().Count() == 15,
                "Dialogue day/customer keys must be unique.");

            string[] expectedCustomers = { "mascot", "drunk_driver", "model_student", "bad_construction", "troublemaker" };
            foreach (int day in new[] { 1, 2, 3 })
            {
                CustomerDialogueScenario[] daily = scenarios.Where(item => item.Day == day).ToArray();
                Require(daily.Length == 5, $"DAY {day} must contain five dialogue scenarios.");
                Require(expectedCustomers.All(id => daily.Any(item => item.CustomerId == id)),
                    $"DAY {day} is missing one or more required customers.");
            }

            ValidateDayCounts(scenarios, 1, expectedLines: 33, expectedChoices: 6);
            ValidateDayCounts(scenarios, 2, expectedLines: 31, expectedChoices: 5);
            ValidateDayCounts(scenarios, 3, expectedLines: 34, expectedChoices: 3);

            Require(scenarios.Sum(CountLines) == 98, "The dialogue catalog must contain 98 customer response lines.");
            Require(scenarios.Sum(CountChoices) == 14, "The dialogue catalog must contain 14 choice points.");
            Require(scenarios.Sum(item => CountChoices(item) * 2) == 28, "Every choice point must provide exactly two options.");

            foreach (CustomerDialogueScenario scenario in scenarios)
            {
                Require(!string.IsNullOrWhiteSpace(scenario.SpeakerName), $"Missing speaker name: DAY {scenario.Day}/{scenario.CustomerId}");
                ValidateSection(scenario.Opening, $"DAY {scenario.Day}/{scenario.CustomerId}/opening");
                ValidateSection(scenario.Completion, $"DAY {scenario.Day}/{scenario.CustomerId}/completion");
            }

            CustomerDialogueScenario dayOneDrunk = Get(1, "drunk_driver");
            Require(dayOneDrunk.Opening.Choice != null && dayOneDrunk.Completion.Choice != null,
                "DAY 1 drunk-driver dialogue requires choices both before and after work.");
            Require(Get(2, "bad_construction").Opening.Choice == null && Get(2, "bad_construction").Completion.Choice != null,
                "DAY 2 bad-construction choice must occur after delivery.");
            Require(Get(2, "troublemaker").Opening.Choice == null && Get(2, "troublemaker").Completion.Choice != null,
                "DAY 2 drowned-customer choice must occur after delivery.");
            Require(Get(3, "mascot").Opening.Choice == null && Get(3, "mascot").Completion.Choice != null,
                "DAY 3 mascot choice must occur after delivery.");
            Require(Get(3, "mascot").Completion.EventId == CustomerDialogueCatalog.DayThreeRedBalloonEvent,
                "DAY 3 mascot must expose the deferred red-balloon event hook.");
            Require(Get(3, "mascot").Completion.EventAtStart,
                "DAY 3 red-balloon hook must fire before the completion dialogue begins.");

            foreach (CustomerDialogueScenario dayOne in scenarios.Where(item => item.Day == 1))
            {
                Require(dayOne.Opening.EventId == CustomerDialogueCatalog.DayOneIdentityCheckEvent,
                    $"DAY 1 identity-check hook missing: {dayOne.CustomerId}");
                Require(!dayOne.Opening.EventAtStart,
                    $"DAY 1 identity-check hook must fire after the opening dialogue: {dayOne.CustomerId}");
            }

            Require(scenarios.Where(item => item.CustomerId == "troublemaker").All(item => item.SpeakerName == "익사자"),
                "The existing troublemaker ID must display the planning-document speaker name '익사자'.");
        }

        private static void ValidateDayCounts(IEnumerable<CustomerDialogueScenario> scenarios, int day,
            int expectedLines, int expectedChoices)
        {
            CustomerDialogueScenario[] daily = scenarios.Where(item => item.Day == day).ToArray();
            Require(daily.Sum(CountLines) == expectedLines,
                $"DAY {day} line count mismatch. Expected {expectedLines}.");
            Require(daily.Sum(CountChoices) == expectedChoices,
                $"DAY {day} choice count mismatch. Expected {expectedChoices}.");
        }

        private static void ValidateSection(CustomerDialogueSection section, string label)
        {
            Require(section != null, $"Missing dialogue section: {label}");
            Require(section.BeforeChoice.All(line => !string.IsNullOrWhiteSpace(line)), $"Blank line: {label}/before");
            Require(section.AfterChoice.All(line => !string.IsNullOrWhiteSpace(line)), $"Blank line: {label}/after");
            if (section.Choice == null)
                return;

            Require(section.Choice.Options.Count == 2 && section.Choice.Responses.Count == 2,
                $"Choice must have two options and response branches: {label}");
            Require(section.Choice.Options.All(option => !string.IsNullOrWhiteSpace(option)), $"Blank option: {label}");
            Require(section.Choice.Responses.All(branch => branch.All(line => !string.IsNullOrWhiteSpace(line))),
                $"Blank branch line: {label}");
        }

        private static int CountChoices(CustomerDialogueScenario scenario)
        {
            return (scenario.Opening.Choice == null ? 0 : 1) +
                   (scenario.Completion.Choice == null ? 0 : 1);
        }

        private static int CountLines(CustomerDialogueScenario scenario)
        {
            return CountLines(scenario.Opening) + CountLines(scenario.Completion);
        }

        private static int CountLines(CustomerDialogueSection section)
        {
            int count = section.BeforeChoice.Count + section.AfterChoice.Count;
            if (section.Choice != null)
                count += section.Choice.Responses.Sum(branch => branch.Count);
            return count;
        }

        private static CustomerDialogueScenario Get(int day, string customerId)
        {
            Require(CustomerDialogueCatalog.TryGet(day, customerId, out CustomerDialogueScenario scenario),
                $"Missing dialogue scenario: DAY {day}/{customerId}");
            return scenario;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
