using System;
using System.Collections.Generic;
using System.Linq;
using DeadLetter.Mail;
using UnityEditor;
using UnityEngine;

namespace DeadLetter.Editor
{
    public static class Day3Stage1Validator
    {
        [MenuItem("Tools/DEAD LETTER/Validate DAY 3 Stage 1")]
        public static void ValidateFromMenu()
        {
            ValidateAll();
            EditorUtility.DisplayDialog("DEAD LETTER", "DAY 3 1단계 데이터와 진행 구조 검증을 통과했습니다.", "확인");
        }

        public static void ValidateBatch()
        {
            try
            {
                ValidateAll();
                Debug.Log("DAY3_STAGE1_VALIDATION_SUCCESS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void ValidateAll()
        {
            IReadOnlyList<Day2RequestDefinition> definitions = Day3RequestCatalog.CreateDefinitions();
            Require(definitions.Count == 5, "DAY 3 must define exactly five requests.");
            Require(definitions.All(item => item.Day == 3), "Every DAY 3 definition must store day 3.");
            Require(definitions.Select(item => item.RequestId).Distinct().Count() == 5,
                "DAY 3 request IDs must be unique.");
            Require(definitions.Select(item => item.CustomerId).Distinct().Count() == 5,
                "DAY 3 customers must be unique.");

            Dictionary<string, Day2RequestDefinition> byId = definitions.ToDictionary(item => item.RequestId);
            ValidateDefinition(byId[Day3RequestCatalog.MascotRequestId], MailRequestType.Receive,
                Array.Empty<Day2ItemType>(), LetterAppearanceType.NearlyBurnedBlack,
                LetterStamp.Fire | LetterStamp.Shock, false);
            ValidateDefinition(byId[Day3RequestCatalog.DrunkDriverRequestId], MailRequestType.Dispatch,
                new[] { Day2ItemType.DriverLicense }, LetterAppearanceType.DesignatedDriverCardAndBlood,
                LetterStamp.Shock | LetterStamp.Alcohol);
            ValidateDefinition(byId[Day3RequestCatalog.ModelStudentRequestId], MailRequestType.Receive,
                new[] { Day2ItemType.Soap, Day2ItemType.Bandage }, LetterAppearanceType.RepeatedPrescription,
                LetterStamp.Medicine);
            ValidateDefinition(byId[Day3RequestCatalog.BadConstructionRequestId], MailRequestType.Receive,
                new[] { Day2ItemType.Soju, Day2ItemType.DriedSnack }, LetterAppearanceType.ElectricRootScorch,
                LetterStamp.Electricity | LetterStamp.Shock);
            ValidateDefinition(byId[Day3RequestCatalog.TroublemakerRequestId], MailRequestType.Dispatch,
                new[] { Day2ItemType.Soap, Day2ItemType.Bandage }, LetterAppearanceType.WetBloodAndFishHooks,
                LetterStamp.Water);

            Day2RequestDefinition modelStudent = byId[Day3RequestCatalog.ModelStudentRequestId];
            Day2RequestDefinition troublemaker = byId[Day3RequestCatalog.TroublemakerRequestId];
            Require(modelStudent.RelatedRequestId == troublemaker.RequestId &&
                    troublemaker.RelatedRequestId == modelStudent.RequestId,
                "DAY 3 student requests must reference each other.");
            Require(modelStudent.TransferLinkId == Day3RequestCatalog.StudentCareTransferLinkId &&
                    troublemaker.TransferLinkId == Day3RequestCatalog.StudentCareTransferLinkId,
                "DAY 3 student items must share one transfer link.");
            Require(modelStudent.RequiresRelatedRequestCompletionForItems,
                "Model student receive items must wait for the troublemaker dispatch.");

            for (int seed = 0; seed < 1000; seed++)
            {
                IReadOnlyList<Day2RequestDefinition> order = Day3RequestCatalog.CreateRandomOrder(seed);
                Require(order.Count == 5 && order.Select(item => item.RequestId).Distinct().Count() == 5,
                    $"Seed {seed}: DAY 3 order must contain five unique requests.");
                int troubleIndex = IndexOf(order, Day3RequestCatalog.TroublemakerRequestId);
                int modelIndex = IndexOf(order, Day3RequestCatalog.ModelStudentRequestId);
                Require(troubleIndex >= 0 && troubleIndex < modelIndex,
                    $"Seed {seed}: troublemaker must appear before model student.");
            }

            ValidateUnconfirmedMascot(byId[Day3RequestCatalog.MascotRequestId]);
            ValidateManagerIntegration();
            Debug.Log("DAY 3 Stage 1 validation passed for five definitions and 1000 generated orders.");
        }

        private static void ValidateDefinition(
            Day2RequestDefinition definition,
            MailRequestType type,
            IReadOnlyCollection<Day2ItemType> items,
            LetterAppearanceType appearance,
            LetterStamp pictograms,
            bool itemRequirementsConfirmed = true)
        {
            Require(definition.RequestType == type, $"{definition.RequestId}: request type mismatch.");
            Require(definition.RequestedItems.Count == items.Count && items.All(definition.RequestedItems.Contains),
                $"{definition.RequestId}: item list mismatch.");
            Require(definition.LetterAppearance == appearance, $"{definition.RequestId}: appearance mismatch.");
            Require(definition.RequiredPictograms == pictograms, $"{definition.RequestId}: pictogram mismatch.");
            Require(definition.ItemRequirementsConfirmed == itemRequirementsConfirmed,
                $"{definition.RequestId}: item confirmation flag mismatch.");
        }

        private static void ValidateUnconfirmedMascot(Day2RequestDefinition definition)
        {
            Day2RequestRuntimeState state = new(definition);
            state.MarkAppeared();
            state.AcceptRequest();
            Require(!state.ItemsSecured, "Unconfirmed mascot items must not pass as secured.");
            Require(state.SecureEmptyBox("mascot_test_box"), "The box state itself should remain reusable.");
            Require(!state.TryPack(), "Mascot packing must stay blocked until its receive item is confirmed.");
            Require(!state.Completed, "Unconfirmed mascot work must never be fake-completed.");
        }

        private static void ValidateManagerIntegration()
        {
            GameObject root = new("DAY 3 Validation Root");
            GameObject customerObject = new("DAY 3 Validation Customer");
            try
            {
                LetterTaskState taskState = root.AddComponent<LetterTaskState>();
                TestCustomerNPC customer = customerObject.AddComponent<TestCustomerNPC>();
                DayCycleManager manager = root.AddComponent<DayCycleManager>();
                SerializedObject serializedManager = new(manager);
                serializedManager.FindProperty("taskState").objectReferenceValue = taskState;
                serializedManager.FindProperty("customer").objectReferenceValue = customer;
                serializedManager.ApplyModifiedPropertiesWithoutUndo();

                Require(manager.DebugStartDay(3), "Developer DAY 3 start failed.");
                Require(taskState.CurrentDay == 3 && manager.DayThreeRequests.Count == 5 &&
                        manager.DayThreeOrder.Count == 5 && manager.CurrentDayThreeRequest != null,
                    "DAY 3 manager initialization failed.");
                int troubleIndex = IndexOf(manager.DayThreeOrder, Day3RequestCatalog.TroublemakerRequestId);
                int modelIndex = IndexOf(manager.DayThreeOrder, Day3RequestCatalog.ModelStudentRequestId);
                Require(troubleIndex >= 0 && troubleIndex < modelIndex,
                    "DAY 3 manager order broke troublemaker precedence.");

                Require(manager.DebugSelectDayThreeRequest(Day3RequestCatalog.ModelStudentRequestId),
                    "Model student selection failed.");
                Require(manager.DebugAcceptCurrentRequest(), "Model student acceptance failed.");
                Require(!manager.DebugSecureAllCurrentItems(),
                    "Model student items must be blocked before the related troublemaker dispatch completes.");

                CompleteDispatch(manager, Day3RequestCatalog.TroublemakerRequestId);
                CompleteReceive(manager, Day3RequestCatalog.ModelStudentRequestId, "model_student");

                Require(manager.DebugSelectDayThreeRequest(Day3RequestCatalog.MascotRequestId),
                    "Mascot selection failed.");
                Require(manager.DebugAcceptCurrentRequest(), "Mascot acceptance failed.");
                Require(!manager.DebugSecureAllCurrentItems(),
                    "Unconfirmed mascot items must be blocked in developer mode.");
                Require(manager.DebugSecureCurrentBox(), "Mascot box acquisition should still be recordable.");
                Require(!manager.DebugPackCurrentRequest(),
                    "Unconfirmed mascot request must not be packable or completable.");
                Require(!manager.CurrentDayThreeRequest.Completed, "Mascot must remain incomplete.");

                Require(manager.DebugResetCurrentDay(), "DAY 3 reset failed.");
                Require(taskState.CurrentDay == 3 && taskState.CompletedRequests == 0 &&
                        manager.DayThreeRequests.All(request => !request.RequestAccepted && !request.Completed),
                    "DAY 3 reset retained request progress.");

                SerializedObject serializedTask = new(taskState);
                serializedTask.FindProperty("completedRequests").intValue = taskState.RequiredRequestsPerDay;
                serializedTask.ApplyModifiedPropertiesWithoutUndo();
                Require(manager.IsDayThreeComplete, "DAY 3 complete entry point must follow the daily completion state.");
                Require(!manager.TryAdvanceDay(), "DAY 3 completion must not auto-advance into an unimplemented day or ending.");

                Require(manager.DebugStartDay(2), "DAY 2 regression start failed.");
                Require(manager.DayTwoRequests.Count == 5 && manager.DayThreeRequests.Count == 0,
                    "DAY 2 must not retain DAY 3 request states.");
                int dayTwoModel = IndexOf(manager.DayTwoOrder, Day2RequestCatalog.ModelStudentRequestId);
                int dayTwoTrouble = IndexOf(manager.DayTwoOrder, Day2RequestCatalog.TroublemakerRequestId);
                Require(dayTwoModel >= 0 && dayTwoModel < dayTwoTrouble,
                    "DAY 2 precedence must remain model student before troublemaker.");
                Require(manager.DebugStartDay(1) && manager.DayTwoRequests.Count == 0 && manager.DayThreeRequests.Count == 0,
                    "DAY 1 must remain independent from package request states.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(customerObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CompleteDispatch(DayCycleManager manager, string requestId)
        {
            Require(manager.DebugSelectDayThreeRequest(requestId), $"Could not select dispatch {requestId}.");
            Require(manager.DebugAcceptCurrentRequest(), "Dispatch acceptance failed.");
            Require(manager.DebugSecureAllCurrentItems(), "Dispatch item acquisition failed.");
            Require(manager.DebugSecureCurrentBox(), "Dispatch box acquisition failed.");
            Require(manager.DebugPackCurrentRequest(), "Dispatch packing failed.");
            Require(manager.DebugApplyCurrentBoxPictograms(), "Dispatch box marks failed.");
            Require(!manager.DebugDispatchCurrentRequest(), "Dispatch must wait for letter processing.");
            Require(manager.DebugProcessCurrentLetter(), "Dispatch letter processing failed.");
            Require(manager.DebugDispatchCurrentRequest(), "Conveyor dispatch failed.");
            Require(!manager.CurrentDayThreeRequest.Completed,
                "Conveyor dispatch alone must not complete a request.");
            Require(manager.DebugRecordCurrentCompletionNotice(), "Dispatch completion notice failed.");
            Require(manager.CurrentDayThreeRequest.Completed, "Dispatch must complete after its notice.");
        }

        private static void CompleteReceive(DayCycleManager manager, string requestId, string customerId)
        {
            Require(manager.DebugSelectDayThreeRequest(requestId), $"Could not select receive {requestId}.");
            Require(manager.DebugAcceptCurrentRequest(), "Receive acceptance failed.");
            Require(manager.DebugSecureAllCurrentItems(), "Receive item acquisition failed.");
            Require(manager.DebugSecureCurrentBox(), "Receive box acquisition failed.");
            Require(manager.DebugPackCurrentRequest(), "Receive packing failed.");
            Require(manager.DebugApplyCurrentBoxPictograms(), "Receive box marks failed.");
            Require(manager.DebugProcessCurrentLetter(), "Receive letter processing failed.");
            Require(!manager.DebugDeliverCurrentRequest("wrong_customer"),
                "Wrong-customer delivery must fail.");
            Require(!manager.CurrentDayThreeRequest.Completed,
                "Wrong-customer delivery must not complete a request.");
            Require(manager.DebugDeliverCurrentRequest(customerId), "Correct receive delivery failed.");
            Require(manager.CurrentDayThreeRequest.Completed, "Correct receive delivery must complete the request.");
        }

        private static int IndexOf(IReadOnlyList<Day2RequestDefinition> list, string requestId)
        {
            for (int index = 0; index < list.Count; index++)
                if (list[index].RequestId == requestId)
                    return index;
            return -1;
        }

        private static int IndexOf(IReadOnlyList<Day2RequestRuntimeState> list, string requestId)
        {
            for (int index = 0; index < list.Count; index++)
                if (list[index].Definition.RequestId == requestId)
                    return index;
            return -1;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
