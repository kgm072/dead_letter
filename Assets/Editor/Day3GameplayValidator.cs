using System;
using System.Collections.Generic;
using System.Linq;
using DeadLetter.Mail;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadLetter.Editor
{
    public static class Day3GameplayValidator
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Tools/DEAD LETTER/DAY 3/Validate Gameplay Stage 2")]
        public static void ValidateFromMenu()
        {
            ValidateAll();
            EditorUtility.DisplayDialog("DEAD LETTER", "DAY 3 2단계 자동 검증을 통과했습니다.", "확인");
        }

        public static void InstallAndValidateBatch()
        {
            try
            {
                Day3GameplaySceneSetup.Install();
                ValidateAll();
                Debug.Log("DAY3_GAMEPLAY_STAGE2_VALIDATION_SUCCESS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void ValidateAll()
        {
            ValidateSceneObjects();
            ValidatePlayableRequestFlow();
            ValidateDayTwoRegression();
            Debug.Log("DAY 3 Stage 2 validation passed for scene items and four confirmed requests.");
        }

        private static void ValidateSceneObjects()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Require(scene.IsValid() && scene.isLoaded, "SampleScene could not be loaded.");

            GameObject dayThreeRoot = GameObject.Find("DAY 3 Gameplay Objects");
            Require(dayThreeRoot != null, "DAY 3 gameplay root is missing.");
            Day2WorldItem[] items = dayThreeRoot.GetComponentsInChildren<Day2WorldItem>(true);
            Require(items.Length == 7, "DAY 3 must install exactly seven confirmed item pickups.");
            Require(items.Select(item => item.InstanceId).Distinct().Count() == items.Length,
                "DAY 3 item instance IDs must be unique.");

            Dictionary<string, (string requestId, Day2ItemType item, Day2ItemSource source)> expected = new()
            {
                ["day3_drunk_driver_license"] = (Day3RequestCatalog.DrunkDriverRequestId, Day2ItemType.DriverLicense, Day2ItemSource.Counter),
                ["day3_troublemaker_soap"] = (Day3RequestCatalog.TroublemakerRequestId, Day2ItemType.Soap, Day2ItemSource.Counter),
                ["day3_troublemaker_bandage"] = (Day3RequestCatalog.TroublemakerRequestId, Day2ItemType.Bandage, Day2ItemSource.Counter),
                ["day3_model_student_soap"] = (Day3RequestCatalog.ModelStudentRequestId, Day2ItemType.Soap, Day2ItemSource.Storage),
                ["day3_model_student_bandage"] = (Day3RequestCatalog.ModelStudentRequestId, Day2ItemType.Bandage, Day2ItemSource.Storage),
                ["day3_bad_construction_soju"] = (Day3RequestCatalog.BadConstructionRequestId, Day2ItemType.Soju, Day2ItemSource.Storage),
                ["day3_bad_construction_dried_snack"] = (Day3RequestCatalog.BadConstructionRequestId, Day2ItemType.DriedSnack, Day2ItemSource.Storage)
            };

            foreach (Day2WorldItem item in items)
            {
                Require(expected.TryGetValue(item.InstanceId, out var definition),
                    $"Unexpected DAY 3 item: {item.InstanceId}");
                Require(item.RequestId == definition.requestId && item.ItemType == definition.item && item.Source == definition.source,
                    $"DAY 3 item configuration mismatch: {item.InstanceId}");
                Require(item.GetComponentsInChildren<Collider>(true).Length > 0,
                    $"DAY 3 item collider is missing: {item.InstanceId}");
                Require(item.GetComponentsInChildren<Renderer>(true).Length > 0,
                    $"DAY 3 item renderer is missing: {item.InstanceId}");
                Require(!string.IsNullOrWhiteSpace(Day2Labels.GetItemLabel(item.ItemType)),
                    $"DAY 3 item label is missing: {item.InstanceId}");
            }

            GameObject sharedRoot = GameObject.Find("DAY 2 Gameplay Objects");
            Require(sharedRoot != null, "Shared DAY 2 gameplay root is missing.");
            Require(sharedRoot.GetComponentsInChildren<Day2EmptyBox>(true).Length == 5,
                "Five reusable empty boxes must remain in the scene.");
            Require(sharedRoot.GetComponentsInChildren<Day2PackingStation>(true).Length == 1,
                "The shared packing station must remain in the scene.");
            Require(sharedRoot.GetComponentsInChildren<Day2ConveyorTerminal>(true).Length == 1,
                "The shared conveyor terminal must remain in the scene.");
            Require(sharedRoot.GetComponentsInChildren<Day2BoxStampTool>(true).Length == 6,
                "All six shared pictogram stamp tools must remain in the scene.");
        }

        private static void ValidatePlayableRequestFlow()
        {
            GameObject root = new("DAY 3 Stage 2 Validation Root");
            GameObject customerObject = new("DAY 3 Stage 2 Validation Customer");
            try
            {
                LetterTaskState taskState = root.AddComponent<LetterTaskState>();
                TestCustomerNPC customer = customerObject.AddComponent<TestCustomerNPC>();
                DayCycleManager manager = root.AddComponent<DayCycleManager>();
                SerializedObject serializedManager = new(manager);
                serializedManager.FindProperty("taskState").objectReferenceValue = taskState;
                serializedManager.FindProperty("customer").objectReferenceValue = customer;
                serializedManager.ApplyModifiedPropertiesWithoutUndo();

                Require(manager.DebugStartDay(3), "DAY 3 start failed.");
                Require(manager.DayThreeOrder.Count == 5 && manager.DayThreeOrder.Select(item => item.Definition.RequestId).Distinct().Count() == 5,
                    "DAY 3 order must contain five unique requests.");
                int troubleIndex = IndexOf(manager.DayThreeOrder, Day3RequestCatalog.TroublemakerRequestId);
                int modelIndex = IndexOf(manager.DayThreeOrder, Day3RequestCatalog.ModelStudentRequestId);
                Require(troubleIndex >= 0 && troubleIndex < modelIndex,
                    "Troublemaker must appear before model student on DAY 3.");
                Require(!manager.ShouldPresentWorldItem(Day3RequestCatalog.ModelStudentRequestId, Day2ItemSource.Storage),
                    "Linked model-student items must be hidden before troublemaker dispatch completion.");

                CompleteDispatch(manager, taskState, Day3RequestCatalog.TroublemakerRequestId);
                Require(manager.ShouldPresentWorldItem(Day3RequestCatalog.ModelStudentRequestId, Day2ItemSource.Storage),
                    "Linked model-student items must become available after troublemaker dispatch completion.");
                CompleteReceive(manager, taskState, Day3RequestCatalog.ModelStudentRequestId, "model_student");
                CompleteDispatch(manager, taskState, Day3RequestCatalog.DrunkDriverRequestId);
                CompleteReceive(manager, taskState, Day3RequestCatalog.BadConstructionRequestId, "bad_construction");

                Require(taskState.CompletedRequests == 4 && !taskState.RequiredDayComplete,
                    "Four confirmed requests must complete exactly once without faking DAY 3 completion.");
                Require(!taskState.CompleteCurrentRequest(),
                    "A cleared request must not increment the completion count twice.");

                Require(manager.DebugSelectDayThreeRequest(Day3RequestCatalog.MascotRequestId),
                    "Mascot request selection failed.");
                Require(manager.DebugAcceptCurrentRequest(), "Mascot request acceptance failed.");
                Require(!manager.DebugSecureAllCurrentItems() && !manager.DebugPackCurrentRequest(),
                    "Unconfirmed mascot items must keep the fifth request from fake completion.");
                Require(taskState.CompletedRequests == 4 && !manager.CurrentDayThreeRequest.Completed,
                    "Mascot request must remain unresolved until its item is specified.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(customerObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateDayTwoRegression()
        {
            for (int seed = 0; seed < 250; seed++)
            {
                IReadOnlyList<Day2RequestDefinition> order = Day2RequestCatalog.CreateRandomOrder(seed);
                Require(order.Count == 5 && order.Select(item => item.RequestId).Distinct().Count() == 5,
                    $"DAY 2 order regression at seed {seed}.");
                Require(IndexOf(order, Day2RequestCatalog.ModelStudentRequestId) <
                        IndexOf(order, Day2RequestCatalog.TroublemakerRequestId),
                    $"DAY 2 precedence regression at seed {seed}.");
            }
        }

        private static void CompleteDispatch(DayCycleManager manager, LetterTaskState taskState, string requestId)
        {
            Require(manager.DebugSelectDayThreeRequest(requestId), $"Dispatch selection failed: {requestId}");
            Require(manager.DebugAcceptCurrentRequest(), $"Dispatch acceptance failed: {requestId}");
            Require(manager.DebugSecureAllCurrentItems(), $"Dispatch item acquisition failed: {requestId}");
            Require(manager.DebugSecureCurrentBox(), $"Dispatch box acquisition failed: {requestId}");
            Require(manager.DebugPackCurrentRequest(), $"Dispatch packing failed: {requestId}");
            Require(manager.DebugApplyCurrentBoxPictograms(), $"Dispatch box stamping failed: {requestId}");
            Require(manager.DebugProcessCurrentLetter(), $"Dispatch letter processing failed: {requestId}");
            Require(manager.DebugDispatchCurrentRequest(), $"Conveyor dispatch failed: {requestId}");
            Require(!manager.CurrentDayThreeRequest.Completed,
                $"Conveyor dispatch alone completed the request: {requestId}");
            Require(manager.DebugRecordCurrentCompletionNotice(), $"Completion notice failed: {requestId}");
            Require(manager.CurrentDayThreeRequest.Completed, $"Dispatch did not complete after notice: {requestId}");
            Require(taskState.CompleteCurrentRequest(), $"Dispatch daily count failed: {requestId}");
        }

        private static void CompleteReceive(
            DayCycleManager manager,
            LetterTaskState taskState,
            string requestId,
            string customerId)
        {
            Require(manager.DebugSelectDayThreeRequest(requestId), $"Receive selection failed: {requestId}");
            Require(manager.DebugAcceptCurrentRequest(), $"Receive acceptance failed: {requestId}");
            Require(manager.DebugSecureAllCurrentItems(), $"Receive item acquisition failed: {requestId}");
            Require(manager.DebugSecureCurrentBox(), $"Receive box acquisition failed: {requestId}");
            Require(manager.DebugPackCurrentRequest(), $"Receive packing failed: {requestId}");
            Require(manager.DebugApplyCurrentBoxPictograms(), $"Receive box stamping failed: {requestId}");
            Require(manager.DebugProcessCurrentLetter(), $"Receive letter processing failed: {requestId}");
            Require(!manager.DebugDeliverCurrentRequest("wrong_customer"),
                $"Wrong customer accepted a package: {requestId}");
            Require(manager.DebugDeliverCurrentRequest(customerId), $"Correct delivery failed: {requestId}");
            Require(manager.CurrentDayThreeRequest.Completed, $"Receive request did not complete: {requestId}");
            Require(taskState.CompleteCurrentRequest(), $"Receive daily count failed: {requestId}");
        }

        private static int IndexOf(IReadOnlyList<Day2RequestRuntimeState> list, string requestId)
        {
            for (int index = 0; index < list.Count; index++)
                if (list[index].Definition.RequestId == requestId)
                    return index;
            return -1;
        }

        private static int IndexOf(IReadOnlyList<Day2RequestDefinition> list, string requestId)
        {
            for (int index = 0; index < list.Count; index++)
                if (list[index].RequestId == requestId)
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
