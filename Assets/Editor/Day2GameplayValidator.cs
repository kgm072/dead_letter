using System;
using System.Linq;
using DeadLetter.Mail;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadLetter.Editor
{
    public static class Day2GameplayValidator
    {
        [MenuItem("Tools/DEAD LETTER/DAY 2/Validate Full Gameplay")]
        public static void ValidateFromMenu()
        {
            ValidateAll();
            EditorUtility.DisplayDialog("DEAD LETTER", "DAY 2 전체 게임플레이 자동 검증을 통과했습니다.", "확인");
        }

        public static void ValidateBatch()
        {
            try
            {
                ValidateAll();
                Debug.Log("DAY2_FULL_GAMEPLAY_VALIDATION_SUCCESS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void BuildDevelopmentBatch()
        {
            try
            {
                string outputPath = GetCommandLineValue("-day2BuildPath");
                if (string.IsNullOrWhiteSpace(outputPath))
                    throw new InvalidOperationException("-day2BuildPath 인자가 필요합니다.");

                string directory = System.IO.Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    System.IO.Directory.CreateDirectory(directory);

                string[] scenes = EditorBuildSettings.scenes
                    .Where(scene => scene.enabled)
                    .Select(scene => scene.path)
                    .ToArray();
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException($"Development build failed: {report.summary.result}");
                Debug.Log("DAY2_DEVELOPMENT_BUILD_SUCCESS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void ValidateAll()
        {
            ValidateSceneAndPrefabs();
            ValidateRuntimeFlow();
            Debug.Log("DAY 2 full gameplay validation passed: world objects, packaging, independent marks, dispatch, delivery, and student link.");
        }

        private static void ValidateSceneAndPrefabs()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            GameObject root = GameObject.Find("DAY 2 Gameplay Objects");
            Require(root != null, "DAY 2 gameplay root is missing from SampleScene.");
            Require(root.GetComponent<Day2InteractionFeedback>() != null, "DAY 2 feedback component is missing.");

            Require(root.GetComponentsInChildren<Day2WorldItem>(true).Length == 7,
                "Scene must contain seven request-bound temporary item objects.");
            Require(root.GetComponentsInChildren<Day2EmptyBox>(true).Length == 5,
                "Scene must contain five uniquely identified empty boxes.");
            Require(root.GetComponentsInChildren<Day2PackingStation>(true).Length == 1,
                "Scene must contain one DAY 2 packing station.");
            Require(root.GetComponentsInChildren<Day2BoxStampTool>(true).Length == 6,
                "Scene must contain six box pictogram stamp tools.");
            Require(root.GetComponentsInChildren<Day2PackedBox>(true).Length == 1,
                "Scene must contain one packed-box interaction surface.");
            Require(root.GetComponentsInChildren<Day2ConveyorTerminal>(true).Length == 1,
                "Scene must contain one conveyor terminal.");

            string[] prefabPaths =
            {
                "Assets/Day2/Prefabs/Day2_ItemPickup.prefab",
                "Assets/Day2/Prefabs/Day2_EmptyBox.prefab",
                "Assets/Day2/Prefabs/Day2_PackingStation.prefab",
                "Assets/Day2/Prefabs/Day2_BoxStampTool.prefab",
                "Assets/Day2/Prefabs/Day2_PackedBox.prefab",
                "Assets/Day2/Prefabs/Day2_ConveyorTerminal.prefab"
            };
            foreach (string path in prefabPaths)
                Require(AssetDatabase.LoadAssetAtPath<GameObject>(path) != null, $"Missing prefab: {path}");

            Day2WorldItem[] items = root.GetComponentsInChildren<Day2WorldItem>(true);
            Require(items.Select(item => item.InstanceId).Distinct().Count() == items.Length,
                "Every world item must have a unique instance ID.");
            Day2EmptyBox[] boxes = root.GetComponentsInChildren<Day2EmptyBox>(true);
            Require(boxes.Select(box => box.BoxId).Distinct().Count() == boxes.Length,
                "Every empty box must have a unique box ID.");
        }

        private static void ValidateRuntimeFlow()
        {
            GameObject root = new("DAY 2 Full Validation Root");
            GameObject customerObject = new("DAY 2 Full Validation Customer");
            try
            {
                LetterTaskState taskState = root.AddComponent<LetterTaskState>();
                TestCustomerNPC customer = customerObject.AddComponent<TestCustomerNPC>();
                DayCycleManager manager = root.AddComponent<DayCycleManager>();
                SerializedObject serialized = new(manager);
                serialized.FindProperty("taskState").objectReferenceValue = taskState;
                serialized.FindProperty("customer").objectReferenceValue = customer;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Require(manager.DebugStartDay(2), "DAY 2 initialization failed.");
                Require(manager.DayTwoOrder.Count == 5 && manager.DayTwoOrder.Select(request => request.Definition.RequestId).Distinct().Count() == 5,
                    "DAY 2 must contain five unique requests.");
                int modelIndex = IndexOf(manager, Day2RequestCatalog.ModelStudentRequestId);
                int troubleIndex = IndexOf(manager, Day2RequestCatalog.TroublemakerRequestId);
                Require(modelIndex >= 0 && modelIndex < troubleIndex, "Model student must precede troublemaker.");

                ValidateDispatchFlow(manager, taskState);
                ValidateReceiveFlow(manager);
                ValidateStudentTransfer(manager);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(customerObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateDispatchFlow(DayCycleManager manager, LetterTaskState taskState)
        {
            Require(manager.DebugSelectDayTwoRequest(Day2RequestCatalog.MascotRequestId), "Could not select mascot request.");
            Require(manager.DebugAcceptCurrentRequest(), "Mascot request acceptance failed.");
            Require(!manager.TryAcquireCurrentItem("mascot_wrong_source", Day2RequestCatalog.MascotRequestId,
                Day2ItemType.Cigarettes, Day2ItemSource.Storage), "Dispatch item must not be acquired from storage.");
            Require(manager.TryAcquireCurrentItem("mascot_cigarettes", Day2RequestCatalog.MascotRequestId,
                Day2ItemType.Cigarettes, Day2ItemSource.Counter), "Counter cigarettes acquisition failed.");
            Require(!manager.TryAcquireCurrentItem("mascot_cigarettes", Day2RequestCatalog.MascotRequestId,
                Day2ItemType.Cigarettes, Day2ItemSource.Counter), "Same item must not be acquired twice.");
            Require(!manager.TryPackCurrentRequest(), "Packing must fail while an item and box are missing.");
            Require(manager.TryAcquireCurrentItem("mascot_lighter", Day2RequestCatalog.MascotRequestId,
                Day2ItemType.Lighter, Day2ItemSource.Counter), "Counter lighter acquisition failed.");
            Require(!manager.TryPackCurrentRequest(), "Packing must fail while the box is missing.");
            Require(manager.TryAcquireEmptyBox("validation_box_1"), "Empty box acquisition failed.");
            Require(manager.TryPackCurrentRequest(), "Packing with complete items and box failed.");

            Day2RequestRuntimeState state = manager.CurrentDayTwoRequest;
            Require(state.PackedItems.Count == 2 && state.SecuredItemInstanceIds.Count == 2,
                "Packed contents and acquired instance IDs were not recorded.");
            Require(manager.TryApplyBoxPictogram(LetterStamp.Shock), "Shock box mark failed.");
            Require(manager.TryApplyBoxPictogram(LetterStamp.Fire), "Fire box mark failed.");
            Require(!manager.TryApplyBoxPictogram(LetterStamp.Fire), "Duplicate box mark must fail.");
            Require(state.HasOnlyRequiredBoxPictograms, "Box mark order must not affect the result.");
            Require(taskState.AttachedStamps == LetterStamp.None,
                "Applying box marks must not change letter stamps.");
            Require(!manager.TryDispatchCurrentPackage(), "Dispatch must fail before letter processing and mailing.");
            Require(manager.DebugProcessCurrentLetter(), "Letter processing failed.");
            Require(state.AppliedBoxPictograms == (LetterStamp.Fire | LetterStamp.Shock),
                "Letter processing must not change box marks.");
            Require(manager.TryDispatchCurrentPackage(), "Conveyor dispatch failed after all conditions were met.");
            Require(!state.Completed, "Conveyor dispatch alone must not complete the request.");
            Require(manager.TryFinishCurrentCustomer(manager.CurrentCustomerId, manager.CurrentLetterId),
                "Dispatch completion notice failed.");
            Require(state.Completed, "Dispatch request must complete after the notice.");
            Require(!manager.TryFinishCurrentCustomer(manager.CurrentCustomerId, manager.CurrentLetterId),
                "Dispatch request must not complete twice.");
        }

        private static void ValidateReceiveFlow(DayCycleManager manager)
        {
            Require(manager.DebugSelectDayTwoRequest(Day2RequestCatalog.DrunkDriverRequestId),
                "Could not select drunk-driver request.");
            Require(manager.DebugAcceptCurrentRequest(), "Receive request acceptance failed.");
            Require(!manager.TryAcquireCurrentItem("receive_wrong_source", Day2RequestCatalog.DrunkDriverRequestId,
                Day2ItemType.HangoverRemedy, Day2ItemSource.Counter), "Receive item must not be acquired from the counter.");
            Require(manager.TryAcquireCurrentItem("receive_remedy", Day2RequestCatalog.DrunkDriverRequestId,
                Day2ItemType.HangoverRemedy, Day2ItemSource.Storage), "Storage item acquisition failed.");
            Require(!manager.TryAcquireEmptyBox("validation_box_1"), "A used box must not be reusable by another request.");
            Require(manager.TryAcquireEmptyBox("validation_box_2"), "Second unique box acquisition failed.");
            Require(manager.TryPackCurrentRequest(), "Receive package packing failed.");
            Require(manager.TryApplyBoxPictogram(LetterStamp.Alcohol), "Alcohol box mark failed.");
            Require(manager.TryApplyBoxPictogram(LetterStamp.Shock), "Shock box mark failed.");
            Require(manager.DebugProcessCurrentLetter(), "Receive letter processing failed.");
            Require(!manager.TryFinishCurrentCustomer("model_student", manager.CurrentLetterId),
                "Wrong-customer delivery must fail.");
            Require(!manager.CurrentDayTwoRequest.Completed, "Wrong-customer delivery must not complete the request.");
            Require(manager.TryFinishCurrentCustomer(manager.CurrentCustomerId, manager.CurrentLetterId),
                "Correct-customer delivery failed.");
            Require(manager.CurrentDayTwoRequest.DeliveredToCustomer && manager.CurrentDayTwoRequest.Completed,
                "Receive request did not record delivery and completion.");
        }

        private static void ValidateStudentTransfer(DayCycleManager manager)
        {
            Require(manager.DebugSelectDayTwoRequest(Day2RequestCatalog.TroublemakerRequestId),
                "Could not select troublemaker request.");
            Require(manager.DebugAcceptCurrentRequest(), "Troublemaker request acceptance failed.");
            Require(!manager.TryAcquireCurrentItem("linked_uniform_early", Day2RequestCatalog.TroublemakerRequestId,
                Day2ItemType.BulliedUniform, Day2ItemSource.Storage),
                "Linked uniform must remain unavailable before model-student dispatch completion.");

            Require(manager.DebugSelectDayTwoRequest(Day2RequestCatalog.ModelStudentRequestId),
                "Could not select model-student request.");
            Require(manager.DebugAcceptCurrentRequest(), "Model-student request acceptance failed.");
            Require(manager.TryAcquireCurrentItem("model_uniform", Day2RequestCatalog.ModelStudentRequestId,
                Day2ItemType.BulliedUniform, Day2ItemSource.Counter), "Model-student uniform acquisition failed.");
            Require(manager.TryAcquireEmptyBox("validation_box_3"), "Model-student box acquisition failed.");
            Require(manager.TryPackCurrentRequest(), "Model-student packing failed.");
            Require(manager.TryApplyBoxPictogram(LetterStamp.Medicine), "Model-student box mark failed.");
            Require(manager.DebugProcessCurrentLetter(), "Model-student letter processing failed.");
            Require(manager.TryDispatchCurrentPackage(), "Model-student dispatch failed.");
            Require(manager.TryFinishCurrentCustomer(manager.CurrentCustomerId, manager.CurrentLetterId),
                "Model-student completion notice failed.");

            Require(manager.DebugSelectDayTwoRequest(Day2RequestCatalog.TroublemakerRequestId),
                "Could not reselect troublemaker request.");
            Require(manager.DebugAcceptCurrentRequest(), "Troublemaker request re-acceptance failed.");
            Require(manager.TryAcquireCurrentItem("linked_uniform_after_dispatch", Day2RequestCatalog.TroublemakerRequestId,
                Day2ItemType.BulliedUniform, Day2ItemSource.Storage),
                "Linked uniform must become available after model-student completion.");
            Require(manager.CurrentDayTwoRequest.Definition.TransferLinkId == Day2RequestCatalog.StudentUniformTransferLinkId,
                "Student transfer link ID was not retained.");
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

        private static string GetCommandLineValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                    return args[index + 1];
            }
            return string.Empty;
        }
    }
}
