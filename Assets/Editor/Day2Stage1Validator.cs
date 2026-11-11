using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DeadLetter.Mail;
using UnityEditor;
using UnityEngine;

namespace DeadLetter.Editor
{
    public static class Day2Stage1Validator
    {
        [MenuItem("Tools/DEAD LETTER/Validate DAY 2 Stage 1")]
        public static void ValidateFromMenu()
        {
            ValidateAll();
            EditorUtility.DisplayDialog("DEAD LETTER", "DAY 2 1단계 데이터 검증을 통과했습니다.", "확인");
        }

        public static void ValidateBatch()
        {
            try
            {
                ValidateAll();
                Debug.Log("DAY2_STAGE1_VALIDATION_SUCCESS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void ValidateAll()
        {
            IReadOnlyList<Day2RequestDefinition> definitions = Day2RequestCatalog.CreateDefinitions();
            Require(definitions.Count == 5, "DAY 2 must define exactly five requests.");
            Require(definitions.Select(item => item.RequestId).Distinct().Count() == 5, "DAY 2 request IDs must be unique.");
            Require(definitions.Select(item => item.CustomerId).Distinct().Count() == 5, "DAY 2 customers must be unique.");
            Require(definitions.All(item => item.Day == 2), "Every DAY 2 definition must store day 2.");

            Dictionary<string, Day2RequestDefinition> byId = definitions.ToDictionary(item => item.RequestId);
            ValidateDefinition(byId[Day2RequestCatalog.MascotRequestId], MailRequestType.Dispatch,
                new[] { Day2ItemType.Cigarettes, Day2ItemType.Lighter }, LetterAppearanceType.LightlyScorched,
                LetterStamp.Fire | LetterStamp.Shock);
            ValidateDefinition(byId[Day2RequestCatalog.DrunkDriverRequestId], MailRequestType.Receive,
                new[] { Day2ItemType.HangoverRemedy }, LetterAppearanceType.DesignatedDriverCard,
                LetterStamp.Shock | LetterStamp.Alcohol);
            ValidateDefinition(byId[Day2RequestCatalog.ModelStudentRequestId], MailRequestType.Dispatch,
                new[] { Day2ItemType.BulliedUniform }, LetterAppearanceType.MilkAndShoeMarks,
                LetterStamp.Medicine);
            ValidateDefinition(byId[Day2RequestCatalog.BadConstructionRequestId], MailRequestType.Receive,
                new[] { Day2ItemType.SafetyHelmet, Day2ItemType.InsulatedGloves }, LetterAppearanceType.CementDust,
                LetterStamp.Electricity | LetterStamp.Shock);
            ValidateDefinition(byId[Day2RequestCatalog.TroublemakerRequestId], MailRequestType.Receive,
                new[] { Day2ItemType.BulliedUniform }, LetterAppearanceType.WaterSoaked,
                LetterStamp.Water);

            Day2RequestDefinition modelStudent = byId[Day2RequestCatalog.ModelStudentRequestId];
            Day2RequestDefinition troublemaker = byId[Day2RequestCatalog.TroublemakerRequestId];
            Require(modelStudent.RelatedRequestId == troublemaker.RequestId, "Model student relation is invalid.");
            Require(troublemaker.RelatedRequestId == modelStudent.RequestId, "Troublemaker relation is invalid.");
            Require(modelStudent.TransferLinkId == Day2RequestCatalog.StudentUniformTransferLinkId &&
                    troublemaker.TransferLinkId == Day2RequestCatalog.StudentUniformTransferLinkId,
                "Student uniform transfer link must be shared.");

            for (int seed = 0; seed < 1000; seed++)
            {
                IReadOnlyList<Day2RequestDefinition> order = Day2RequestCatalog.CreateRandomOrder(seed);
                Require(order.Count == 5, $"Seed {seed}: order count is invalid.");
                Require(order.Select(item => item.RequestId).Distinct().Count() == 5, $"Seed {seed}: duplicate request.");
                int modelIndex = IndexOf(order, Day2RequestCatalog.ModelStudentRequestId);
                int troubleIndex = IndexOf(order, Day2RequestCatalog.TroublemakerRequestId);
                Require(modelIndex >= 0 && troubleIndex >= 0 && modelIndex < troubleIndex,
                    $"Seed {seed}: model student must appear before troublemaker.");
            }

            ValidateRuntimeDefaults(definitions);
            ValidateDispatchState(byId[Day2RequestCatalog.MascotRequestId]);
            ValidateReceiveState(troublemaker);
            ValidateManagerIntegration();
            Debug.Log("DAY 2 Stage 1 validation passed for 5 definitions and 1000 generated orders.");
        }

        private static void ValidateDefinition(
            Day2RequestDefinition definition,
            MailRequestType type,
            IReadOnlyCollection<Day2ItemType> items,
            LetterAppearanceType appearance,
            LetterStamp pictograms)
        {
            Require(definition.RequestType == type, $"{definition.RequestId}: request type mismatch.");
            Require(definition.RequestedItems.Count == items.Count && items.All(definition.RequestedItems.Contains),
                $"{definition.RequestId}: item list mismatch.");
            Require(definition.LetterAppearance == appearance, $"{definition.RequestId}: appearance mismatch.");
            Require(definition.RequiredPictograms == pictograms, $"{definition.RequestId}: pictogram mismatch.");
        }

        private static void ValidateRuntimeDefaults(IEnumerable<Day2RequestDefinition> definitions)
        {
            foreach (Day2RequestDefinition definition in definitions)
            {
                Day2RequestRuntimeState state = new(definition);
                Require(!state.RequestAccepted && !state.ItemsSecured && !state.EmptyBoxSecured && !state.Packed,
                    $"{definition.RequestId}: physical work must start incomplete.");
                Require(state.AttachedLetterStamps == LetterStamp.None && !state.LetterPostmarkApplied,
                    $"{definition.RequestId}: letter work must start incomplete.");
                Require(state.AppliedBoxPictograms == LetterStamp.None && !state.ConveyorDispatched &&
                        !state.DeliveredToCustomer && !state.CompletionNoticeGiven && !state.Completed,
                    $"{definition.RequestId}: completion flags must start false.");
            }
        }

        private static void ValidateDispatchState(Day2RequestDefinition definition)
        {
            Day2RequestRuntimeState state = PreparePackedState(definition);
            state.SetLetterProcessingState(definition.RequiredPictograms, true);
            Require(state.AppliedBoxPictograms == LetterStamp.None, "Letter stamps must not change box marks.");
            ApplyRequiredBoxMarks(state, definition.RequiredPictograms);
            Require(state.AttachedLetterStamps == definition.RequiredPictograms && state.LetterPostmarkApplied,
                "Box marks must not change letter processing.");
            Require(state.TryRecordConveyorDispatch(), "Valid dispatch must record conveyor dispatch.");
            Require(!state.Completed, "Conveyor dispatch alone must not complete the request.");
            Require(state.TryRecordCompletionNotice(), "Dispatch completion notice must be recordable after dispatch.");
            Require(state.Completed, "Dispatch must complete only after dispatch and completion notice.");
        }

        private static void ValidateReceiveState(Day2RequestDefinition definition)
        {
            Day2RequestRuntimeState state = PreparePackedState(definition);
            ApplyRequiredBoxMarks(state, definition.RequiredPictograms);
            Require(!state.TryDeliverTo("model_student"), "Delivery to a different customer must fail.");
            Require(!state.Completed, "Wrong-customer delivery must not complete the request.");
            Require(state.TryDeliverTo(definition.CustomerId), "Delivery to the owning customer must succeed.");
            Require(state.Completed, "Correct receive delivery must complete the request state.");
            Require(!state.TryDeliverTo(definition.CustomerId), "Repeated delivery must not complete twice.");
        }

        private static Day2RequestRuntimeState PreparePackedState(Day2RequestDefinition definition)
        {
            Day2RequestRuntimeState state = new(definition);
            Require(!state.TryPack(), "Packing before acceptance/items/box must fail.");
            state.AcceptRequest();
            foreach (Day2ItemType item in definition.RequestedItems)
                Require(state.SecureItem(item), $"{definition.RequestId}: requested item should be securable.");
            Require(state.SecureEmptyBox($"box_{definition.RequestId}"), "Empty box should be securable once.");
            Require(state.TryPack(), "Packing must succeed only with all requested items and a box.");
            return state;
        }

        private static void ApplyRequiredBoxMarks(Day2RequestRuntimeState state, LetterStamp required)
        {
            foreach (LetterStamp value in Enum.GetValues(typeof(LetterStamp)))
            {
                if (value != LetterStamp.None && (required & value) == value)
                    Require(state.ApplyBoxPictogram(value), $"Box pictogram {value} should be applied.");
            }
            Require(state.HasOnlyRequiredBoxPictograms, "Required box pictograms must be order-independent and exact.");
        }

        private static void ValidateManagerIntegration()
        {
            GameObject root = new("DAY 2 Validation Root");
            GameObject customerObject = new("DAY 2 Validation Customer");
            try
            {
                LetterTaskState taskState = root.AddComponent<LetterTaskState>();
                TestCustomerNPC customer = customerObject.AddComponent<TestCustomerNPC>();
                DayCycleManager manager = root.AddComponent<DayCycleManager>();
                SerializedObject serializedManager = new(manager);
                serializedManager.FindProperty("taskState").objectReferenceValue = taskState;
                serializedManager.FindProperty("customer").objectReferenceValue = customer;
                serializedManager.ApplyModifiedPropertiesWithoutUndo();

                MethodInfo prepareDay = typeof(DayCycleManager).GetMethod("PrepareDay", BindingFlags.Instance | BindingFlags.NonPublic);
                Require(prepareDay != null, "DayCycleManager.PrepareDay was not found.");
                prepareDay.Invoke(manager, null);
                FieldInfo dayOneOrderField = typeof(DayCycleManager).GetField("dayOneOrder", BindingFlags.Instance | BindingFlags.NonPublic);
                Require(dayOneOrderField?.GetValue(manager) is ICollection dayOneOrder && dayOneOrder.Count == 5,
                    "DAY 1 must retain its existing five-customer order.");
                Require(manager.DayTwoRequests.Count == 0, "DAY 1 must not create DAY 2 request states.");

                SerializedObject serializedTask = new(taskState);
                serializedTask.FindProperty("completedRequests").intValue = taskState.RequiredRequestsPerDay;
                serializedTask.ApplyModifiedPropertiesWithoutUndo();
                Require(manager.TryAdvanceDay(), "Completed DAY 1 must transition through the existing day advance path.");
                Require(taskState.CurrentDay == 2 && taskState.CompletedRequests == 0,
                    "DAY 2 must start with a reset completion count.");
                Require(manager.DayTwoRequests.Count == 5, "DayCycleManager must create five DAY 2 request states.");
                Require(manager.DayTwoOrder.Count == 5, "DayCycleManager must create a five-entry DAY 2 order.");
                Require(manager.CurrentDayTwoRequest != null, "DayCycleManager must prepare the first DAY 2 customer.");
                Require(manager.DayTwoRequests.All(request => !request.Completed),
                    "DAY 2 manager must not fake-complete unimplemented work.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(customerObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static int IndexOf(IReadOnlyList<Day2RequestDefinition> list, string requestId)
        {
            for (int index = 0; index < list.Count; index++)
            {
                if (list[index].RequestId == requestId)
                    return index;
            }
            return -1;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
