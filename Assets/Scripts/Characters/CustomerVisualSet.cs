using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace DeadLetter.Characters
{
    /// <summary>
    /// 손님 NPC 하나가 (일차, 손님 ID)에 따라 다른 캐릭터 모델을 보여 주게 한다.
    /// TestCustomerNPC.BeginCustomer()에서 Show(day, customerId)를 호출한다.
    /// 등록된 모델이 없으면 기존 그레이박스 인형탈(placeholderParts)을 그대로 보여 준다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CustomerVisualSet : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("1~3. 0이면 모든 일차에 사용")]
            [Range(0, 3)] public int day = 1;
            [Tooltip("DayCycleManager의 손님 ID (mascot, drunk_driver, model_student, bad_construction, troublemaker)")]
            public string customerId;
            [Tooltip("FBX 모델 또는 프리팹")]
            public GameObject model;
            [Tooltip("모델 전체에 입힐 텍스처 (Base Map)")]
            public Texture2D baseMap;
            [Tooltip("눈 감은 텍스처. 비워 두면 깜빡이지 않는다 (인형탈)")]
            public Texture2D eyesClosedMap;
            [Tooltip("게임 안에서의 키 (m)")]
            [Min(0.3f)] public float height = 1.72f;
            [Tooltip("방향이 틀어져 보이면 여기서 돌린다 (도)")]
            public float extraYaw;
        }

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private static readonly string[] VisualFloorMeshNames = { "복도", "비밀의방", "작업실", "창고", "창구", "탕비실" };
        private const string FloorMarkerPrefix = "Floor Standard Y=";

        [SerializeField] private List<Entry> entries = new();
        [Tooltip("실제 모델이 나올 때 렌더러를 끌 그레이박스 인형탈 부품들 (콜라이더는 유지)")]
        [SerializeField] private List<GameObject> placeholderParts = new();
        [Tooltip("NPC 정면이 로컬 -Z인지 여부. TestCustomerNPC의 modelForwardYawOffset이 180이면 켠다.")]
        [SerializeField] private bool npcFacesNegativeZ = true;

        [Header("바닥 맞춤")]
        [Tooltip("최종 맵 바닥(눈에 보이는 바닥) 높이를 자동으로 찾는다. 'Floor Standard Y=...' 오브젝트 이름에서 읽는다.")]
        [SerializeField] private bool autoDetectVisualFloor = true;
        [Tooltip("자동 탐색 실패 시 사용할 눈에 보이는 바닥 높이 (m)")]
        [SerializeField] private float visualFloorHeight = 0.1993f;
        [Tooltip("발바닥을 추가로 올리거나 내린다 (m). 살짝 뜨면 음수, 묻히면 양수.")]
        [SerializeField] private float extraFootOffset = 0.005f;

        private GameObject currentInstance;
        private string currentKey;
        private Func<bool> talkingQuery;
        private MaterialPropertyBlock block;

        private bool floorDataReady;
        private readonly List<Bounds> visualFloorAreas = new();
        private float detectedVisualFloorY;
        private readonly RaycastHit[] hits = new RaycastHit[16];

        public IReadOnlyList<Entry> Entries => entries;

        public void SetTalkingQuery(Func<bool> query) => talkingQuery = query;

        /// <summary>이전 버전 호환용 (1일차로 간주)</summary>
        public void Show(string customerId) => Show(1, customerId);

        public void Show(int day, string customerId)
        {
            Entry entry = FindEntry(day, customerId);
            string key = entry != null ? $"{entry.day}:{customerId}" : null;

            if (currentInstance != null && entry != null && currentKey == key)
            {
                currentInstance.GetComponent<CustomerProceduralAnimator>()?.ResetMotion();
                return;
            }

            ClearInstance();
            SetPlaceholderVisible(entry == null);
            if (entry == null)
                return;

            currentKey = key;
            currentInstance = Instantiate(entry.model, transform);
            currentInstance.name = $"Customer Model (DAY {day} {customerId})";
            currentInstance.transform.localPosition = Vector3.zero;
            currentInstance.transform.localRotation = Quaternion.identity;
            currentInstance.transform.localScale = Vector3.one;
            foreach (Collider col in currentInstance.GetComponentsInChildren<Collider>(true))
                col.enabled = false; // 상호작용 판정은 NPC 루트의 캡슐 콜라이더가 담당

            Renderer[] renderers = currentInstance.GetComponentsInChildren<Renderer>(true);
            ApplyTexture(renderers, entry.baseMap);

            // 1) 크기: 메시 실제 높이를 목표 키에 맞춘다
            Bounds bounds = MeasureBounds(currentInstance);
            if (bounds.size.y > 0.0001f)
                currentInstance.transform.localScale = Vector3.one * (entry.height / bounds.size.y);

            // 2) 방향: 모델 정면을 NPC 진행 방향에 맞춘다
            Vector3 desired = npcFacesNegativeZ ? -transform.forward : transform.forward;
            Vector3 modelForward = CustomerProceduralAnimator.EstimateForward(currentInstance.transform);
            float yaw = Vector3.SignedAngle(Vector3.ProjectOnPlane(modelForward, Vector3.up),
                Vector3.ProjectOnPlane(desired, Vector3.up), Vector3.up);
            currentInstance.transform.rotation =
                Quaternion.AngleAxis(yaw + entry.extraYaw, Vector3.up) * currentInstance.transform.rotation;

            // 3) 발바닥(메시 최저점)을 NPC 위치 높이에 맞추고 가운데 정렬.
            //    실제 바닥 높이 보정은 CustomerProceduralAnimator가 매 프레임 한다.
            bounds = MeasureBounds(currentInstance);
            currentInstance.transform.position += new Vector3(transform.position.x - bounds.center.x,
                transform.position.y - bounds.min.y,
                transform.position.z - bounds.center.z);

            // 4) 애니메이션 + 바닥 맞춤
            CustomerProceduralAnimator anim = currentInstance.GetComponent<CustomerProceduralAnimator>();
            if (anim == null)
                anim = currentInstance.AddComponent<CustomerProceduralAnimator>();
            anim.Initialize(transform, entry.height, talkingQuery, GroundHeightAt);

            // 5) 눈 깜빡임 (눈 감은 텍스처가 있는 캐릭터만)
            if (entry.eyesClosedMap != null && entry.baseMap != null)
            {
                CustomerBlink blink = currentInstance.GetComponent<CustomerBlink>();
                if (blink == null)
                    blink = currentInstance.AddComponent<CustomerBlink>();
                blink.Initialize(renderers, entry.baseMap, entry.eyesClosedMap);
            }
        }

        public void Hide()
        {
            ClearInstance();
            SetPlaceholderVisible(true);
        }

        private Entry FindEntry(int day, string customerId)
        {
            Entry anyDay = null;
            foreach (Entry e in entries)
            {
                if (e == null || e.model == null || !string.Equals(e.customerId, customerId, StringComparison.Ordinal))
                    continue;
                if (e.day == day)
                    return e;
                if (e.day == 0 && anyDay == null)
                    anyDay = e;
            }
            return anyDay;
        }

        private void ApplyTexture(Renderer[] renderers, Texture tex)
        {
            if (tex == null) return;
            block ??= new MaterialPropertyBlock();
            foreach (Renderer r in renderers)
            {
                r.GetPropertyBlock(block);
                block.SetTexture(BaseMapId, tex);
                block.SetTexture(MainTexId, tex);
                block.SetColor(BaseColorId, Color.white);
                block.SetColor(ColorId, Color.white);
                r.SetPropertyBlock(block);
            }
        }

        /// <summary>
        /// 해당 위치에서 발이 디뎌야 할 높이.
        /// 물리 바닥(그레이박스 콜라이더)과 최종 맵의 눈에 보이는 바닥 중 높은 쪽을 쓴다.
        /// </summary>
        public float GroundHeightAt(Vector3 position)
        {
            EnsureFloorData();

            float y = position.y;
            int count = Physics.RaycastNonAlloc(position + Vector3.up * 1.0f, Vector3.down, hits, 3f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider c = hits[i].collider;
                if (c == null || c.transform.IsChildOf(transform)) continue;
                float hy = hits[i].point.y;
                if (hy > position.y + 0.35f) continue; // 책상, 선반 같은 높은 물체는 무시
                if (hy > best) best = hy;
            }
            if (!float.IsNegativeInfinity(best))
                y = best;

            foreach (Bounds area in visualFloorAreas)
            {
                if (position.x >= area.min.x && position.x <= area.max.x &&
                    position.z >= area.min.z && position.z <= area.max.z)
                {
                    y = Mathf.Max(y, detectedVisualFloorY);
                    break;
                }
            }
            return y + extraFootOffset;
        }

        private void EnsureFloorData()
        {
            if (floorDataReady) return;
            floorDataReady = true;
            visualFloorAreas.Clear();
            detectedVisualFloorY = visualFloorHeight;

            if (!autoDetectVisualFloor)
                return;

            bool markerFound = false;
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!t.name.StartsWith(FloorMarkerPrefix, StringComparison.Ordinal)) continue;
                string value = t.name.Substring(FloorMarkerPrefix.Length).TrimEnd('m', ' ');
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                {
                    detectedVisualFloorY = parsed;
                    markerFound = true;
                }
                break;
            }

            foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!r.enabled) continue;
                if (Array.IndexOf(VisualFloorMeshNames, r.gameObject.name) < 0) continue;
                visualFloorAreas.Add(r.bounds);
            }

            if (!markerFound && visualFloorAreas.Count > 0)
                Debug.Log($"[CustomerVisualSet] 바닥 기준 오브젝트를 못 찾아 기본값 {visualFloorHeight:F4}m 사용");
        }

        private void ClearInstance()
        {
            if (currentInstance != null)
            {
                if (Application.isPlaying) Destroy(currentInstance);
                else DestroyImmediate(currentInstance);
            }
            currentInstance = null;
            currentKey = null;
        }

        private void SetPlaceholderVisible(bool visible)
        {
            foreach (GameObject part in placeholderParts)
            {
                if (part == null) continue;
                foreach (Renderer r in part.GetComponentsInChildren<Renderer>(true))
                    r.enabled = visible;
            }
        }

        /// <summary>
        /// 현재 포즈의 실제 정점으로 월드 경계 상자를 잰다. 비활성 상태에서도 동작한다.
        /// </summary>
        private static Bounds MeasureBounds(GameObject root)
        {
            bool has = false;
            Bounds result = default;
            Mesh baked = new Mesh();
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Vector3[] verts;
                Matrix4x4 m;
                if (renderer is SkinnedMeshRenderer smr && smr.sharedMesh != null)
                {
                    smr.BakeMesh(baked, true);
                    verts = baked.vertices;
                    Transform t = smr.transform;
                    m = Matrix4x4.TRS(t.position, t.rotation, Vector3.one);
                }
                else if (renderer is MeshRenderer && renderer.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null && mf.sharedMesh.isReadable)
                {
                    verts = mf.sharedMesh.vertices;
                    m = renderer.transform.localToWorldMatrix;
                }
                else continue;

                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 p = m.MultiplyPoint3x4(verts[i]);
                    if (!has) { result = new Bounds(p, Vector3.zero); has = true; }
                    else result.Encapsulate(p);
                }
            }
            if (Application.isPlaying) Destroy(baked); else DestroyImmediate(baked);
            return result;
        }
    }
}
