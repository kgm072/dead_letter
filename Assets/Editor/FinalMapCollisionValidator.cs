using System;
using System.Collections.Generic;
using System.Linq;
using DeadLetter.DebugTools;
using DeadLetter.Interaction;
using DeadLetter.Mail;
using DeadLetter.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadLetter.Editor
{
    /// <summary>
    /// Read-only geometry and gameplay-reference validation for the final-map visual layer.
    /// Temporary MeshColliders are created with HideAndDontSave and are always destroyed.
    /// </summary>
    public static class FinalMapCollisionValidator
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string FinalRootName = "Final Map Visuals (Visual Only)";
        private const float WallTolerance = 0.35f;

        private static readonly string[] StructuralNames =
        {
            "복도", "비밀의방", "작업실", "창고", "창구", "탕비실"
        };

        private readonly struct AreaProbe
        {
            public AreaProbe(string name, Vector3 position)
            {
                Name = name;
                Position = position;
            }

            public string Name { get; }
            public Vector3 Position { get; }
        }

        [MenuItem("Tools/DEAD LETTER/Art/Validate Final Map Collisions")]
        public static void ValidateFromMenu()
        {
            CollisionReport report = Validate();
            EditorUtility.DisplayDialog(
                "DEAD LETTER",
                $"충돌 검증 완료\n통과: {report.PassedChecks}\n경고: {report.Warnings.Count}\n실패: {report.Failures.Count}\n\n자세한 내용은 Console을 확인하세요.",
                "확인");
        }

        public static void ValidateBatch()
        {
            try
            {
                CollisionReport report = Validate();
                Debug.Log(
                    $"FINAL_MAP_COLLISION_VALIDATION_COMPLETE passed={report.PassedChecks} " +
                    $"warnings={report.Warnings.Count} failures={report.Failures.Count}");
                if (report.Failures.Count > 0)
                    EditorApplication.Exit(1);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static CollisionReport Validate()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var report = new CollisionReport();
            GameObject finalRoot = FindSceneObject(FinalRootName);
            GameObject graybox = FindSceneObject("DEAD LETTER - Graybox");
            if (finalRoot == null)
                throw new InvalidOperationException($"{FinalRootName}을 찾을 수 없습니다.");
            if (graybox == null)
                throw new InvalidOperationException("DEAD LETTER - Graybox를 찾을 수 없습니다.");

            Collider[] legacyColliders = graybox.GetComponentsInChildren<Collider>(true)
                .Where(value => !value.transform.IsChildOf(finalRoot.transform))
                .ToArray();
            Collider[] structuralColliders = legacyColliders
                .Where(value => value.enabled && value.gameObject.activeInHierarchy && IsLegacyStructure(value.name))
                .ToArray();

            CharacterController controller = UnityEngine.Object.FindAnyObjectByType<CharacterController>(FindObjectsInactive.Include);
            float playerRadius = controller != null ? controller.radius : 0.3f;
            float playerHeight = controller != null ? controller.height : 1.8f;
            Debug.Log(
                $"[CollisionSetup] legacyColliders={legacyColliders.Length} structuralColliders={structuralColliders.Length} " +
                $"playerRadius={playerRadius:F3} playerHeight={playerHeight:F3} " +
                $"playerPosition={Format(controller != null ? controller.transform.position : Vector3.zero)}");

            EvaluateOrientationCandidates(finalRoot);
            List<GameObject> temporaryObjects = CreateTemporaryVisualColliders(finalRoot);
            try
            {
                Collider[] visualColliders = temporaryObjects
                    .Select(value => value.GetComponent<Collider>())
                    .Where(value => value != null)
                    .ToArray();
                Physics.SyncTransforms();

                LogAreaBounds(finalRoot);
                CompareStructureSurfaces(report, structuralColliders, visualColliders);
                // These root-level query helpers must not participate in the movement test.
                foreach (Collider helper in visualColliders) helper.enabled = false;
                Physics.SyncTransforms();
                ValidateMovementAreas(report, legacyColliders, finalRoot, controller, playerRadius, playerHeight);
                ValidateDoors(report, legacyColliders, finalRoot, playerRadius, playerHeight);
                ValidateStorage(report, finalRoot, controller, playerRadius, playerHeight);
                ValidateGameplayReferences(report);
            }
            finally
            {
                foreach (GameObject temporaryObject in temporaryObjects)
                    if (temporaryObject != null)
                        UnityEngine.Object.DestroyImmediate(temporaryObject);
                Physics.SyncTransforms();
            }

            foreach (string warning in report.Warnings)
                Debug.LogWarning("[CollisionWarning] " + warning);
            foreach (string failure in report.Failures)
                Debug.LogError("[CollisionFailure] " + failure);

            Debug.Log(
                $"[CollisionSummary] passed={report.PassedChecks} warnings={report.Warnings.Count} " +
                $"failures={report.Failures.Count}");
            return report;
        }

        private static List<GameObject> CreateTemporaryVisualColliders(GameObject finalRoot)
        {
            var created = new List<GameObject>();
            foreach (MeshFilter filter in finalRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || !StructuralNames.Contains(filter.gameObject.name, StringComparer.Ordinal))
                    continue;

                var temporary = new GameObject($"__FINAL_MAP_COLLISION_QA__{filter.name}")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                temporary.transform.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
                temporary.transform.localScale = filter.transform.lossyScale;
                MeshCollider collider = temporary.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = false;
                created.Add(temporary);
            }

            if (created.Count != StructuralNames.Length)
                throw new InvalidOperationException($"임시 구조 MeshCollider가 {created.Count}개 생성되었습니다.");
            return created;
        }

        private static void EvaluateOrientationCandidates(GameObject finalRoot)
        {
            Transform root = finalRoot.transform;
            Vector3 originalPosition = root.position;
            Quaternion originalRotation = root.rotation;
            Bounds originalBounds = CalculateRendererBounds(finalRoot.GetComponentsInChildren<Renderer>(true));
            Vector3 targetCenter = originalBounds.center;

            Vector3[] mainFloorCenters =
            {
                ProbeAtFloorCenter("Counter", "Counter Floor").Position,
                ProbeAtFloorCenter("Workshop", "Workshop Floor").Position,
                ProbeAtFloorCenter("Storage", "Storage Floor").Position,
                ProbeAtFloorCenter("Pantry", "Pantry Floor").Position
            };
            Vector3 hiddenCenter = ProbeAtFloorCenter("Hidden", "Hidden Room Floor").Position;

            try
            {
                foreach (float angle in new[] { 0f, 90f, 180f, 270f })
                {
                    root.SetPositionAndRotation(originalPosition, originalRotation * Quaternion.Euler(0f, angle, 0f));
                    Physics.SyncTransforms();
                    Bounds rotatedBounds = CalculateRendererBounds(finalRoot.GetComponentsInChildren<Renderer>(true));
                    root.position += targetCenter - rotatedBounds.center;
                    Physics.SyncTransforms();

                    float score = 0f;
                    foreach (string roomName in new[] { "창구", "작업실", "창고", "탕비실" })
                    {
                        Renderer room = finalRoot.GetComponentsInChildren<Renderer>(true)
                            .First(value => value.name == roomName);
                        Vector3 center = room.bounds.center;
                        center.y = 0f;
                        score += mainFloorCenters.Min(floor => (center - floor).sqrMagnitude);
                    }

                    Renderer hidden = finalRoot.GetComponentsInChildren<Renderer>(true)
                        .First(value => value.name == "비밀의방");
                    Vector3 hiddenVisualCenter = hidden.bounds.center;
                    hiddenVisualCenter.y = 0f;
                    score += 2f * (hiddenVisualCenter - hiddenCenter).sqrMagnitude;

                    Renderer hall = finalRoot.GetComponentsInChildren<Renderer>(true)
                        .First(value => value.name == "복도");
                    Vector3 hallCenter = hall.bounds.center;
                    hallCenter.y = 0f;
                    score += 2f * hallCenter.sqrMagnitude;
                    Debug.Log(
                        $"[OrientationCandidate] angle={angle:F0} score={score:F2} " +
                        $"rootPosition={Format(root.position)} boundsCenter={Format(CalculateRendererBounds(finalRoot.GetComponentsInChildren<Renderer>(true)).center)}");
                }
            }
            finally
            {
                root.SetPositionAndRotation(originalPosition, originalRotation);
                Physics.SyncTransforms();
            }
        }

        private static Bounds CalculateRendererBounds(Renderer[] renderers)
        {
            Renderer first = renderers.First(value => value != null && value.enabled && value.gameObject.activeInHierarchy);
            Bounds bounds = first.bounds;
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                    bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }

        private static void CompareStructureSurfaces(
            CollisionReport report,
            Collider[] legacyStructure,
            Collider[] visualStructure)
        {
            AreaProbe[] probes =
            {
                ProbeFromFloor("창구", "Counter Floor"),
                // Handoff layout intentionally swaps the gameplay use of the old north
                // Storage room and east Workshop room without renaming stable objects.
                ProbeFromFloor("작업실", "Storage Floor"),
                ProbeFromFloor("창고", "Workshop Floor"),
                ProbeFromFloor("탕비실", "Pantry Floor"),
                ProbeFromFloor("히든방", "Hidden Room Floor"),
                new("복도 중앙", new Vector3(0f, 1f, 0f)),
                new("복도 북측", new Vector3(0f, 1f, 5f)),
                new("복도 남측", new Vector3(0f, 1f, -5f)),
                new("복도 동측", new Vector3(5f, 1f, 0f)),
                new("복도 서측", new Vector3(-5f, 1f, 0f))
            };
            Vector3[] horizontalDirections = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            int compared = 0;
            int mismatches = 0;

            foreach (AreaProbe probe in probes)
            {
                foreach (Vector3 direction in horizontalDirections)
                {
                    float visualDistance = RaycastGroup(visualStructure, new Ray(probe.Position, direction), 18f);
                    float legacyDistance = RaycastGroup(legacyStructure, new Ray(probe.Position, direction), 18f);
                    bool visualHit = float.IsFinite(visualDistance);
                    bool legacyHit = float.IsFinite(legacyDistance);
                    compared++;

                    if (visualHit != legacyHit)
                    {
                        mismatches++;
                        report.Warnings.Add(
                            $"{probe.Name} {DirectionLabel(direction)}: 시각={DistanceLabel(visualDistance)}, " +
                            $"Collider={DistanceLabel(legacyDistance)}");
                        continue;
                    }

                    if (visualHit && Mathf.Abs(visualDistance - legacyDistance) > WallTolerance)
                    {
                        mismatches++;
                        report.Warnings.Add(
                            $"{probe.Name} {DirectionLabel(direction)} 벽 차이 " +
                            $"{Mathf.Abs(visualDistance - legacyDistance):F2}m " +
                            $"(시각 {visualDistance:F2}m / Collider {legacyDistance:F2}m)");
                    }
                }

                float visualFloor = RaycastGroup(visualStructure, new Ray(probe.Position, Vector3.down), 4f);
                float legacyFloor = RaycastGroup(legacyStructure, new Ray(probe.Position, Vector3.down), 4f);
                float visualCeiling = RaycastGroup(visualStructure, new Ray(probe.Position, Vector3.up), 4f);
                float legacyCeiling = RaycastGroup(legacyStructure, new Ray(probe.Position, Vector3.up), 4f);
                CompareVertical(report, probe.Name + " 바닥", visualFloor, legacyFloor, ref mismatches);
                CompareVertical(report, probe.Name + " 천장", visualCeiling, legacyCeiling, ref mismatches);
                compared += 2;
            }

            Debug.Log($"[SurfaceComparison] probes={compared} mismatches={mismatches} tolerance={WallTolerance:F2}m");
            if (mismatches == 0)
                report.Pass("구조 Mesh와 기존 벽·바닥·천장 Collider 거리 일치");
        }

        private static void CompareVertical(
            CollisionReport report,
            string label,
            float visualDistance,
            float legacyDistance,
            ref int mismatches)
        {
            if (!float.IsFinite(visualDistance) || !float.IsFinite(legacyDistance))
            {
                mismatches++;
                report.Warnings.Add(
                    $"{label}: 시각={DistanceLabel(visualDistance)}, Collider={DistanceLabel(legacyDistance)}");
                return;
            }

            if (Mathf.Abs(visualDistance - legacyDistance) <= WallTolerance)
                return;
            mismatches++;
            report.Warnings.Add(
                $"{label} 차이 {Mathf.Abs(visualDistance - legacyDistance):F2}m " +
                $"(시각 {visualDistance:F2}m / Collider {legacyDistance:F2}m)");
        }

        private static void ValidateMovementAreas(
            CollisionReport report,
            Collider[] legacyColliders,
            GameObject finalRoot,
            CharacterController controller,
            float radius,
            float height)
        {
            var ignored = new HashSet<Collider>();
            foreach (HingedDoor door in FindSceneComponents<HingedDoor>())
                foreach (Collider collider in door.GetComponentsInChildren<Collider>(true))
                    ignored.Add(collider);
            if (controller != null)
                foreach (Collider collider in controller.GetComponentsInChildren<Collider>(true))
                    ignored.Add(collider);

            const float step = 0.5f;
            const float minX = -16f;
            const float maxX = 16f;
            const float minZ = -19f;
            const float maxZ = 16f;
            int width = Mathf.RoundToInt((maxX - minX) / step) + 1;
            int depth = Mathf.RoundToInt((maxZ - minZ) / step) + 1;
            var free = new bool[width, depth];
            for (int x = 0; x < width; x++)
            for (int z = 0; z < depth; z++)
            {
                Vector3 position = new(minX + x * step, 0f, minZ + z * step);
                free[x, z] = IsCapsulePositionFree(position, radius, height, ignored, finalRoot);
            }

            Vector3 startPosition = controller != null ? controller.transform.position : Vector3.zero;
            startPosition.y = 0f;
            Vector2Int start = FindNearestFree(ToCell(startPosition, minX, minZ, step), free);
            HashSet<Vector2Int> visited = FloodFill(start, free);
            AreaProbe[] targets =
            {
                new("복도", Vector3.zero),
                ProbeAtFloorCenter("창구", "Counter Floor"),
                ProbeAtFloorCenter("작업실", "Storage Floor"),
                ProbeAtFloorCenter("창고", "Workshop Floor"),
                ProbeAtFloorCenter("탕비실", "Pantry Floor"),
                ProbeAtFloorCenter("히든방", "Hidden Room Floor")
            };

            foreach (AreaProbe target in targets)
            {
                Vector2Int targetCell = FindNearestFree(ToCell(target.Position, minX, minZ, step), free);
                bool reachable = visited.Contains(targetCell);
                Debug.Log(
                    $"[MovementArea] {target.Name} target={Format(target.Position)} " +
                    $"cell={targetCell} reachable={reachable}");
                if (reachable)
                    report.Pass($"{target.Name} 이동 영역 연결");
                else
                    report.Failures.Add($"{target.Name} 이동 영역이 복도와 연결되지 않습니다.");
            }

            Debug.Log($"[MovementGrid] freeVisited={visited.Count} grid={width}x{depth} step={step:F2}");
        }

        private static void ValidateDoors(
            CollisionReport report,
            Collider[] legacyColliders,
            GameObject finalRoot,
            float radius,
            float height)
        {
            HingedDoor[] allDoors = FindSceneComponents<HingedDoor>()
                .OrderBy(value => value.name, StringComparer.Ordinal)
                .ToArray();
            HingedDoor[] doors = allDoors.Where(value => value.gameObject.activeInHierarchy).ToArray();
            if (doors.Length != 6)
                report.Failures.Add($"활성 HingedDoor 수가 6개가 아닙니다: {doors.Length}");
            HingedDoor storageExterior = allDoors.FirstOrDefault(value => value.name == "Storage Exterior Door Pivot");
            if (storageExterior == null || storageExterior.gameObject.activeInHierarchy)
                report.Failures.Add("폐쇄 대상 Storage Exterior Door가 비활성 상태가 아닙니다.");
            else
                report.Pass("Storage Exterior Door 폐쇄 유지");

            foreach (HingedDoor door in doors)
            {
                Collider[] ownColliders = door.GetComponentsInChildren<Collider>(true);
                BoxCollider leaf = ownColliders.OfType<BoxCollider>()
                    .OrderByDescending(value => value.bounds.size.y)
                    .FirstOrDefault();
                if (leaf == null)
                {
                    report.Failures.Add($"{door.name}: 문짝 BoxCollider가 없습니다.");
                    continue;
                }

                var ignored = new HashSet<Collider>(ownColliders);
                bool passageClear = DoorCenterPassageClear(leaf, radius, height, ignored, finalRoot);
                if (passageClear)
                    report.Pass($"{door.name} 중앙 통과 폭");
                else
                    report.Failures.Add($"{door.name}: 문짝을 제외해도 중앙 통로가 막힙니다.");

                Transform hinge = ResolveHinge(door);
                Quaternion closed = hinge.localRotation;
                float positivePenetration;
                float negativePenetration;
                try
                {
                    positivePenetration = MeasureDoorOpenPenetration(
                        hinge, closed * Quaternion.Euler(0f, 90f, 0f), leaf, legacyColliders, ignored, finalRoot);
                    negativePenetration = MeasureDoorOpenPenetration(
                        hinge, closed * Quaternion.Euler(0f, -90f, 0f), leaf, legacyColliders, ignored, finalRoot);
                }
                finally
                {
                    hinge.localRotation = closed;
                    Physics.SyncTransforms();
                }

                Debug.Log(
                    $"[DoorCheck] {door.name} passage={passageClear} " +
                    $"open+90Penetration={positivePenetration:F3} open-90Penetration={negativePenetration:F3}");
                if (positivePenetration <= 0.04f || negativePenetration <= 0.04f)
                    report.Pass($"{door.name} 90도 개방 공간");
                else
                    report.Warnings.Add(
                        $"{door.name}: 양쪽 90도 개방 시 주변 Collider와 겹침 " +
                        $"(+{positivePenetration:F2}m / -{negativePenetration:F2}m)");
            }
        }

        private static void ValidateStorage(
            CollisionReport report,
            GameObject finalRoot,
            CharacterController controller,
            float playerRadius,
            float playerHeight)
        {
            GameObject shelf = FindSceneObject("Storage Production Art");
            if (shelf == null || !shelf.activeInHierarchy)
            {
                report.Failures.Add("활성 창고 선반을 찾을 수 없습니다.");
                return;
            }

            Collider[] shelfColliders = shelf.GetComponentsInChildren<Collider>(true);
            if (shelfColliders.Length == 0)
                report.Failures.Add("창고 선반 Collider가 없습니다.");
            else
                report.Pass("창고 선반 Collider 유지");

            ArtColliderFollower follower = shelf.GetComponentInChildren<ArtColliderFollower>(true);
            if (follower != null && follower.enabled)
                report.Failures.Add("창고 선반 ArtColliderFollower가 다시 활성화되어 방 전체를 막을 수 있습니다.");
            else
                report.Pass("창고 선반 방 전체 차단 Collider 비활성");

            Renderer storageRoom = finalRoot.GetComponentsInChildren<Renderer>(true)
                .FirstOrDefault(value => value.name == "창고");
            if (storageRoom == null)
            {
                report.Failures.Add("최종 맵 창고 Renderer를 찾을 수 없습니다.");
                return;
            }

            Day2WorldItem[] storageItems = FindSceneComponents<Day2WorldItem>()
                .Where(value => value.Source == Day2ItemSource.Storage)
                .ToArray();
            Day2EmptyBox[] boxes = FindSceneComponents<Day2EmptyBox>();
            if (storageItems.Length != 4)
                report.Failures.Add($"창고 물품 수가 4개가 아닙니다: {storageItems.Length}");
            if (boxes.Length != 5)
                report.Failures.Add($"빈 박스 수가 5개가 아닙니다: {boxes.Length}");

            var instanceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (Day2WorldItem item in storageItems)
            {
                Collider[] colliders = item.GetComponentsInChildren<Collider>(true);
                if (!instanceIds.Add(item.InstanceId))
                    report.Failures.Add($"중복 창고 물품 ID: {item.InstanceId}");
                if (colliders.Length == 0)
                    report.Failures.Add($"{item.name}: 획득 Collider가 없습니다.");
                if (!ContainsXZ(storageRoom.bounds, item.transform.position))
                    report.Failures.Add($"{item.name}: 최종 맵 창고 영역 밖에 있습니다.");
                else if (HasReachableInteractionPoint(item.GetComponentsInChildren<Collider>(true), storageRoom.bounds,
                             finalRoot, controller, playerRadius, playerHeight))
                    report.Pass($"{item.name} 3m 이내 접근 가능 배치");
                else
                    report.Failures.Add($"{item.name}: 자유 이동 지점에서 3m 이내 접근할 수 없습니다.");
            }

            var boxIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (Day2EmptyBox box in boxes)
            {
                Collider[] colliders = box.GetComponentsInChildren<Collider>(true);
                if (!boxIds.Add(box.BoxId))
                    report.Failures.Add($"중복 빈 박스 ID: {box.BoxId}");
                if (colliders.Length == 0)
                    report.Failures.Add($"{box.name}: 획득 Collider가 없습니다.");
                if (!ContainsXZ(storageRoom.bounds, box.transform.position))
                    report.Failures.Add($"{box.name}: 최종 맵 창고 영역 밖에 있습니다.");
                else if (HasReachableInteractionPoint(box.GetComponentsInChildren<Collider>(true), storageRoom.bounds,
                             finalRoot, controller, playerRadius, playerHeight))
                    report.Pass($"{box.name} 3m 이내 접근 가능 배치");
                else
                    report.Failures.Add($"{box.name}: 자유 이동 지점에서 3m 이내 접근할 수 없습니다.");
            }
        }

        private static bool HasReachableInteractionPoint(
            Collider[] targetColliders,
            Bounds roomBounds,
            GameObject finalRoot,
            CharacterController controller,
            float radius,
            float height)
        {
            if (targetColliders.Length == 0)
                return false;

            var ignored = new HashSet<Collider>(targetColliders);
            if (controller != null)
                foreach (Collider collider in controller.GetComponentsInChildren<Collider>(true))
                    ignored.Add(collider);

            const float step = 0.4f;
            for (float x = roomBounds.min.x + radius; x <= roomBounds.max.x - radius; x += step)
            for (float z = roomBounds.min.z + radius; z <= roomBounds.max.z - radius; z += step)
            {
                Vector3 ground = new(x, 0f, z);
                if (!IsCapsulePositionFree(ground, radius, height, ignored, finalRoot))
                    continue;

                Vector3 eye = ground + Vector3.up * 1.6f;
                float nearest = targetColliders
                    .Where(value => value != null && value.enabled && value.gameObject.activeInHierarchy)
                    .Select(value => Vector3.Distance(eye, value.ClosestPoint(eye)))
                    .DefaultIfEmpty(float.PositiveInfinity)
                    .Min();
                if (nearest <= 3f)
                    return true;
            }
            return false;
        }

        private static void ValidateGameplayReferences(CollisionReport report)
        {
            RequireCount<FirstPersonInteractor>(report, 1, "FirstPersonInteractor");
            RequireCount<DeveloperTestMode>(report, 1, "DeveloperTestMode");
            RequireCount<LetterWorkstation>(report, 1, "LetterWorkstation");
            RequireCount<Day2PackingStation>(report, 1, "Day2PackingStation");
            RequireCount<Day2ConveyorTerminal>(report, 1, "Day2ConveyorTerminal");
            // Day2PackingStation creates the slip in Awake; absence in edit mode is expected.
            RequireCount<TestCustomerNPC>(report, 1, "TestCustomerNPC");

            int missingScripts = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()
                .Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount);
            if (missingScripts == 0)
                report.Pass("Missing Script 없음");
            else
                report.Failures.Add($"Missing Script {missingScripts}개");
        }

        private static void RequireCount<T>(CollisionReport report, int minimum, string label) where T : Component
        {
            int count = FindSceneComponents<T>().Length;
            if (count >= minimum)
                report.Pass($"{label} 참조 유지 ({count})");
            else
                report.Failures.Add($"{label}이 없습니다.");
        }

        private static bool DoorCenterPassageClear(
            BoxCollider leaf,
            float radius,
            float height,
            HashSet<Collider> ignored,
            GameObject finalRoot)
        {
            Bounds bounds = leaf.bounds;
            Vector3 axis = bounds.size.x < bounds.size.z ? Vector3.right : Vector3.forward;
            Vector3 center = bounds.center;
            center.y = 0f;
            // Only sample through the wall/leaf thickness. Sampling far into both
            // rooms incorrectly reports nearby furniture as a blocked doorway.
            for (int index = -2; index <= 2; index++)
            {
                Vector3 position = center + axis * (index * 0.16f);
                if (!IsCapsulePositionFree(position, radius, height, ignored, finalRoot))
                    return false;
            }
            return true;
        }

        private static float MeasureDoorOpenPenetration(
            Transform hinge,
            Quaternion rotation,
            BoxCollider leaf,
            Collider[] candidates,
            HashSet<Collider> ignored,
            GameObject finalRoot)
        {
            hinge.localRotation = rotation;
            Physics.SyncTransforms();
            float maximum = 0f;
            foreach (Collider candidate in candidates)
            {
                if (candidate == null || !candidate.enabled || candidate.isTrigger || ignored.Contains(candidate))
                    continue;
                if (candidate.transform.IsChildOf(finalRoot.transform) || IsFloorOrCeilingHierarchy(candidate.transform))
                    continue;
                if (!leaf.bounds.Intersects(candidate.bounds))
                    continue;
                if (Physics.ComputePenetration(
                        leaf, leaf.transform.position, leaf.transform.rotation,
                        candidate, candidate.transform.position, candidate.transform.rotation,
                        out _, out float distance))
                {
                    maximum = Mathf.Max(maximum, distance);
                }
            }
            return maximum;
        }

        private static Transform ResolveHinge(HingedDoor door)
        {
            SerializedObject serialized = new(door);
            SerializedProperty property = serialized.FindProperty("hinge");
            return property?.objectReferenceValue as Transform ?? door.transform;
        }

        private static bool IsCapsulePositionFree(
            Vector3 groundPosition,
            float radius,
            float height,
            HashSet<Collider> ignored,
            GameObject finalRoot)
        {
            Vector3 bottom = groundPosition + Vector3.up * (radius + 0.06f);
            Vector3 top = groundPosition + Vector3.up * Mathf.Max(radius + 0.07f, height - radius + 0.02f);
            Collider[] overlaps = Physics.OverlapCapsule(bottom, top, radius * 0.92f, ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider overlap in overlaps)
            {
                if (overlap == null || ignored.Contains(overlap))
                    continue;
                if (overlap.transform.IsChildOf(finalRoot.transform) || IsFloorOrCeilingHierarchy(overlap.transform))
                    continue;
                return false;
            }
            return true;
        }

        private static HashSet<Vector2Int> FloodFill(Vector2Int start, bool[,] free)
        {
            var visited = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            if (!InGrid(start, free) || !free[start.x, start.y])
                return visited;
            visited.Add(start);
            queue.Enqueue(start);
            Vector2Int[] directions =
            {
                Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down
            };
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                foreach (Vector2Int direction in directions)
                {
                    Vector2Int next = current + direction;
                    if (!InGrid(next, free) || !free[next.x, next.y] || !visited.Add(next))
                        continue;
                    queue.Enqueue(next);
                }
            }
            return visited;
        }

        private static Vector2Int FindNearestFree(Vector2Int desired, bool[,] free)
        {
            if (InGrid(desired, free) && free[desired.x, desired.y])
                return desired;
            for (int radius = 1; radius <= 6; radius++)
            {
                for (int x = desired.x - radius; x <= desired.x + radius; x++)
                for (int y = desired.y - radius; y <= desired.y + radius; y++)
                {
                    var candidate = new Vector2Int(x, y);
                    if (InGrid(candidate, free) && free[x, y])
                        return candidate;
                }
            }
            return desired;
        }

        private static bool InGrid(Vector2Int cell, bool[,] free)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < free.GetLength(0) && cell.y < free.GetLength(1);
        }

        private static Vector2Int ToCell(Vector3 position, float minX, float minZ, float step)
        {
            return new Vector2Int(
                Mathf.RoundToInt((position.x - minX) / step),
                Mathf.RoundToInt((position.z - minZ) / step));
        }

        private static float RaycastGroup(IEnumerable<Collider> colliders, Ray ray, float maximumDistance)
        {
            float nearest = float.PositiveInfinity;
            foreach (Collider collider in colliders)
            {
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                    continue;
                if (collider.Raycast(ray, out RaycastHit hit, maximumDistance))
                    nearest = Mathf.Min(nearest, hit.distance);
            }
            return nearest;
        }

        private static AreaProbe ProbeFromFloor(string label, string floorName)
        {
            GameObject floor = FindSceneObject(floorName);
            if (floor == null)
                throw new InvalidOperationException($"{floorName}을 찾을 수 없습니다.");
            Vector3 position = floor.transform.position;
            position.y = 1f;
            return new AreaProbe(label, position);
        }

        private static AreaProbe ProbeAtFloorCenter(string label, string floorName)
        {
            AreaProbe probe = ProbeFromFloor(label, floorName);
            Vector3 position = probe.Position;
            position.y = 0f;
            return new AreaProbe(label, position);
        }

        private static void LogAreaBounds(GameObject finalRoot)
        {
            foreach (string structuralName in StructuralNames)
            {
                Renderer renderer = finalRoot.GetComponentsInChildren<Renderer>(true)
                    .FirstOrDefault(value => value.name == structuralName);
                Debug.Log(renderer == null
                    ? $"[VisualBounds] {structuralName} <missing>"
                    : $"[VisualBounds] {structuralName} center={Format(renderer.bounds.center)} " +
                      $"size={Format(renderer.bounds.size)} min={Format(renderer.bounds.min)} max={Format(renderer.bounds.max)}");
            }

            string[] floorNames =
            {
                "Counter Floor", "Workshop Floor", "Storage Floor", "Pantry Floor", "Hidden Room Floor"
            };
            foreach (string floorName in floorNames)
            {
                GameObject floor = FindSceneObject(floorName);
                Collider collider = floor != null ? floor.GetComponentInChildren<Collider>(true) : null;
                Debug.Log(collider == null
                    ? $"[LegacyFloorBounds] {floorName} <missing>"
                    : $"[LegacyFloorBounds] {floorName} center={Format(collider.bounds.center)} " +
                      $"size={Format(collider.bounds.size)} min={Format(collider.bounds.min)} max={Format(collider.bounds.max)}");
            }
        }

        private static bool IsLegacyStructure(string objectName)
        {
            return objectName.Contains("Wall", StringComparison.OrdinalIgnoreCase) ||
                   objectName.EndsWith("Floor", StringComparison.OrdinalIgnoreCase) ||
                   objectName.EndsWith("Ceiling", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsFloorOrCeiling(string objectName)
        {
            return objectName.Contains("Floor", StringComparison.OrdinalIgnoreCase) ||
                   objectName.Contains("Ceiling", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsXZ(Bounds bounds, Vector3 point)
        {
            return point.x >= bounds.min.x && point.x <= bounds.max.x &&
                   point.z >= bounds.min.z && point.z <= bounds.max.z;
        }

        private static bool IsFloorOrCeilingHierarchy(Transform transform)
        {
            for (Transform current = transform; current != null; current = current.parent)
            {
                if (IsFloorOrCeiling(current.name))
                    return true;
            }
            return false;
        }

        private static T[] FindSceneComponents<T>() where T : Component
        {
            return Resources.FindObjectsOfTypeAll<T>()
                .Where(value => value.gameObject.scene.IsValid())
                .ToArray();
        }

        private static GameObject FindSceneObject(string objectName)
        {
            return Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(value => value.scene.IsValid() && value.name == objectName);
        }

        private static string DirectionLabel(Vector3 direction)
        {
            if (direction == Vector3.right) return "+X";
            if (direction == Vector3.left) return "-X";
            if (direction == Vector3.forward) return "+Z";
            if (direction == Vector3.back) return "-Z";
            return direction.ToString();
        }

        private static string DistanceLabel(float distance)
        {
            return float.IsFinite(distance) ? $"{distance:F2}m" : "미검출";
        }

        private static string Format(Vector3 value)
        {
            return $"({value.x:F2},{value.y:F2},{value.z:F2})";
        }

        private sealed class CollisionReport
        {
            public int PassedChecks { get; private set; }
            public List<string> Warnings { get; } = new();
            public List<string> Failures { get; } = new();

            public void Pass(string label)
            {
                PassedChecks++;
                Debug.Log("[CollisionPass] " + label);
            }
        }
    }
}
