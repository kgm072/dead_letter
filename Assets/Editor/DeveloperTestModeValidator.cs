using System;
using System.Linq;
using DeadLetter.DebugTools;
using DeadLetter.Mail;
using UnityEditor;
using UnityEngine;

namespace DeadLetter.Editor
{
    public static class DeveloperTestModeValidator
    {
        [MenuItem("Tools/DEAD LETTER/Validate Developer Test Mode")]
        public static void ValidateFromMenu()
        {
            Validate();
            EditorUtility.DisplayDialog("DEAD LETTER", "개발자 테스트 모드 검증을 통과했습니다.", "확인");
        }

        public static void ValidateBatch()
        {
            try
            {
                Validate();
                Debug.Log("DEVELOPER_TEST_MODE_VALIDATION_SUCCESS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Validate()
        {
            Require(DeveloperTestMode.IsAvailableInCurrentBuild, "Developer mode must be available in the editor.");

            GameObject root = new("Developer Mode Validation Root");
            GameObject customerObject = new("Developer Mode Validation Customer");
            try
            {
                LetterTaskState taskState = root.AddComponent<LetterTaskState>();
                TestCustomerNPC customer = customerObject.AddComponent<TestCustomerNPC>();
                DayCycleManager manager = root.AddComponent<DayCycleManager>();
                SerializedObject serialized = new(manager);
                serialized.FindProperty("taskState").objectReferenceValue = taskState;
                serialized.FindProperty("customer").objectReferenceValue = customer;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Require(manager.DebugStartDay(2), "Developer DAY 2 jump failed.");
                Require(taskState.CurrentDay == 2 && manager.DayTwoOrder.Count == 5, "DAY 2 developer initialization failed.");
                int modelIndex = IndexOf(manager, Day2RequestCatalog.ModelStudentRequestId);
                int troubleIndex = IndexOf(manager, Day2RequestCatalog.TroublemakerRequestId);
                Require(modelIndex >= 0 && modelIndex < troubleIndex, "Developer regenerated order broke student precedence.");

                foreach (Day2RequestDefinition definition in Day2RequestCatalog.CreateDefinitions())
                {
                    Require(manager.DebugSelectDayTwoRequest(definition.RequestId), $"Could not select {definition.RequestId}.");
                    Require(manager.CurrentDayTwoRequest?.Definition.RequestId == definition.RequestId,
                        $"Selected request mismatch: {definition.RequestId}.");
                }

                Require(manager.DebugSelectDayTwoRequest(Day2RequestCatalog.MascotRequestId), "Mascot selection failed.");
                Require(manager.DebugAcceptCurrentRequest(), "Developer acceptance failed.");
                Require(manager.DebugSecureAllCurrentItems(), "Developer item acquisition failed.");
                Require(manager.DebugSecureCurrentBox(), "Developer box acquisition failed.");
                Require(manager.DebugPackCurrentRequest(), "Developer packing failed.");
                Require(manager.DebugApplyCurrentBoxPictograms(), "Developer early box marking failed.");
                Require(!manager.DebugDispatchCurrentRequest(),
                    "Dispatch must fail until the letter has been processed.");
                Require(manager.DebugProcessCurrentLetter(), "Developer letter processing failed.");
                Require(manager.DebugDispatchCurrentRequest(), "Developer conveyor dispatch failed.");
                Require(!manager.CurrentDayTwoRequest.Completed,
                    "Conveyor dispatch alone must not complete a dispatch request in developer mode.");
                Require(manager.DebugRecordCurrentCompletionNotice(), "Developer completion notice failed.");
                Require(manager.CurrentDayTwoRequest.Completed, "Dispatch request should complete after notice.");

                Require(manager.DebugSelectDayTwoRequest(Day2RequestCatalog.TroublemakerRequestId), "Troublemaker selection failed.");
                Require(manager.DebugAcceptCurrentRequest(), "Receive acceptance failed.");
                Require(manager.DebugSecureAllCurrentItems(), "Receive item acquisition failed.");
                Require(manager.DebugSecureCurrentBox(), "Receive box acquisition failed.");
                Require(manager.DebugPackCurrentRequest(), "Receive packing failed.");
                Require(manager.DebugApplyCurrentBoxPictograms(), "Receive box marking failed.");
                Require(!manager.DebugDeliverCurrentRequest("troublemaker"),
                    "Delivery must fail until the letter has been processed.");
                Require(manager.DebugProcessCurrentLetter(), "Receive letter processing failed.");
                Require(!manager.DebugDeliverCurrentRequest("model_student"), "Wrong-customer developer delivery must fail.");
                Require(!manager.CurrentDayTwoRequest.Completed, "Wrong-customer delivery must not complete the request.");
                Require(manager.DebugDeliverCurrentRequest("troublemaker"), "Correct developer delivery failed.");

                Require(manager.DebugRestartCurrentRequest(), "Developer current-request reset failed.");
                Day2RequestRuntimeState reset = manager.CurrentDayTwoRequest;
                Require(!reset.RequestAccepted && !reset.ItemsSecured && !reset.EmptyBoxSecured && !reset.Packed &&
                        !reset.Completed && taskState.AttachedStamps == LetterStamp.None,
                    "Current request reset left progress behind.");

                Require(manager.DebugRegenerateCurrentDay(), "Developer order regeneration failed.");
                modelIndex = IndexOf(manager, Day2RequestCatalog.ModelStudentRequestId);
                troubleIndex = IndexOf(manager, Day2RequestCatalog.TroublemakerRequestId);
                Require(modelIndex >= 0 && modelIndex < troubleIndex,
                    "Regenerated order broke student precedence.");
                Require(manager.DebugAcceptCurrentRequest(), "Pre-reset acceptance failed.");
                Require(manager.DebugResetCurrentDay(), "Developer current-day reset failed.");
                Require(taskState.CurrentDay == 2 && taskState.CompletedRequests == 0 &&
                        manager.DayTwoRequests.All(request => !request.RequestAccepted && !request.Completed),
                    "Current-day reset retained DAY 2 request progress.");

                Require(manager.DebugStartDay(1), "Developer DAY 1 jump failed.");
                Require(taskState.CurrentDay == 1 && manager.DayTwoRequests.Count == 0,
                    "DAY 1 developer reset must not retain DAY 2 runtime requests.");

                Debug.Log("Developer mode validation passed: day jumps, five selections, guarded state progression, and reset.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(customerObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static int IndexOf(DayCycleManager manager, string requestId)
        {
            for (int index = 0; index < manager.DayTwoOrder.Count; index++)
            {
                if (manager.DayTwoOrder[index].Definition.RequestId == requestId)
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
