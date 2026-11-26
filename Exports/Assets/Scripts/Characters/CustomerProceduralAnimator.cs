using UnityEngine;

namespace DeadLetter.Characters
{
    /// <summary>
    /// 애니메이션 클립 없이 뼈를 직접 돌려서 대기(숨쉬기) / 걷기 / 대화 동작을 만든다.
    /// - 휴머노이드(Tripo: Hips, Left_UpperArm ...)와 3ds Max Biped(Bip001 ...) 뼈대 이름을 모두 지원한다.
    /// - 모델의 Animator에 컨트롤러가 연결되어 있으면 이 스크립트는 아무것도 하지 않고
    ///   Animator 파라미터(Speed, Talking)만 넘겨준다. 나중에 Mixamo 클립으로 바꿀 때 그대로 쓸 수 있다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CustomerProceduralAnimator : MonoBehaviour
    {
        [Header("움직임 감지")]
        [Tooltip("이동 속도를 잴 기준 Transform. 비워 두면 부모를 쓴다.")]
        [SerializeField] private Transform motionSource;
        [Tooltip("이 속도(m/s) 이상이면 걷기 동작을 100% 재생한다.")]
        [SerializeField, Min(0.05f)] private float fullWalkSpeed = 1.0f;

        [Header("걷기")]
        [Tooltip("한 걸음 주기(왼발+오른발)에 이동하는 거리 = 키 × 이 값")]
        [SerializeField, Range(0.4f, 1.5f)] private float strideByHeight = 0.75f;
        [SerializeField, Range(0f, 45f)] private float legSwing = 26f;
        [SerializeField, Range(0f, 80f)] private float kneeBend = 50f;
        [SerializeField, Range(0f, 45f)] private float armSwing = 20f;

        [Header("대기")]
        [Tooltip("T포즈에서 팔을 수평선 아래로 내리는 각도")]
        [SerializeField, Range(0f, 88f)] private float armDownAngle = 74f;
        [SerializeField, Range(0f, 40f)] private float idleElbowBend = 12f;
        [SerializeField, Range(0f, 6f)] private float breathAngle = 1.6f;

        [Header("대화")]
        [SerializeField, Range(0f, 15f)] private float talkNodAngle = 4f;
        [SerializeField, Range(0f, 60f)] private float talkGesture = 28f;

        private enum B
        {
            Hips, Spine, Chest, UpperChest, Neck, Head,
            LUpperArm, LLowerArm, LHand, RUpperArm, RLowerArm, RHand,
            LUpperLeg, LLowerLeg, LFoot, LToes, RUpperLeg, RLowerLeg, RFoot, RToes,
            Count
        }

        private static readonly HumanBodyBones[] HumanMap =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest,
            HumanBodyBones.Neck, HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes
        };

        // 같은 순서. 여러 후보 이름은 '|'로 구분.
        private static readonly string[] NameMap =
        {
            "Hips|Bip001 Pelvis|mixamorig:Hips",
            "Spine|Bip001 Spine|mixamorig:Spine",
            "Chest|Bip001 Spine1|mixamorig:Spine1",
            "UpperChest|Bip001 Spine2|mixamorig:Spine2",
            "Neck|Bip001 Neck|mixamorig:Neck",
            "Head|Bip001 Head|mixamorig:Head",
            "Left_UpperArm|Bip001 L UpperArm|mixamorig:LeftArm",
            "Left_LowerArm|Bip001 L Forearm|mixamorig:LeftForeArm",
            "Left_Hand|Bip001 L Hand|mixamorig:LeftHand",
            "Right_UpperArm|Bip001 R UpperArm|mixamorig:RightArm",
            "Right_LowerArm|Bip001 R Forearm|mixamorig:RightForeArm",
            "Right_Hand|Bip001 R Hand|mixamorig:RightHand",
            "Left_UpperLeg|Bip001 L Thigh|mixamorig:LeftUpLeg",
            "Left_LowerLeg|Bip001 L Calf|mixamorig:LeftLeg",
            "Left_Foot|Bip001 L Foot|mixamorig:LeftFoot",
            "Left_Toes|Bip001 L Toe0|mixamorig:LeftToeBase",
            "Right_UpperLeg|Bip001 R Thigh|mixamorig:RightUpLeg",
            "Right_LowerLeg|Bip001 R Calf|mixamorig:RightLeg",
            "Right_Foot|Bip001 R Foot|mixamorig:RightFoot",
            "Right_Toes|Bip001 R Toe0|mixamorig:RightToeBase"
        };

        private readonly Transform[] bones = new Transform[(int)B.Count];
        private readonly Quaternion[] bindRotations = new Quaternion[(int)B.Count];
        private Vector3 hipsBindLocalPosition;

        // 캐릭터 기준 축 (이 오브젝트의 로컬 공간)
        private Vector3 localRight, localUp, localForward;
        private Vector3 leftToeBindLocal, rightToeBindLocal;

        private Animator animator;
        private bool useAnimatorController;
        private bool initialized;
        private Vector3 baseLocalPosition;
        private float footGroundHeight;   // 바인드 포즈에서 '발목 뼈 높이 - 발바닥 높이' (월드 단위)
        private System.Func<Vector3, float> groundHeightAt; // 해당 위치의 실제 바닥 높이
        private float currentFloorY;
        private bool floorInitialized;
        private float height = 1.7f;

        private Vector3 lastSourcePosition;
        private float walkWeight;
        private float talkWeight;
        private float phase;

        private System.Func<bool> isTalking;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int TalkingParam = Animator.StringToHash("Talking");

        public bool IsReady => initialized;

        /// <summary>
        /// 모델 배치(크기, 방향, 바닥 맞춤)가 끝난 뒤 호출한다.
        /// </summary>
        public void Initialize(Transform source, float characterHeight, System.Func<bool> talkingQuery,
            System.Func<Vector3, float> groundQuery = null)
        {
            groundHeightAt = groundQuery;
            floorInitialized = false;
            motionSource = source != null ? source : transform.parent;
            height = Mathf.Max(0.3f, characterHeight);
            isTalking = talkingQuery;

            animator = GetComponentInChildren<Animator>(true);
            useAnimatorController = animator != null && animator.runtimeAnimatorController != null;
            if (animator != null && !useAnimatorController)
                animator.enabled = false; // 빈 Animator가 포즈를 덮어쓰지 않도록

            FindBones();
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] != null)
                    bindRotations[i] = bones[i].localRotation;
            if (bones[(int)B.Hips] != null)
                hipsBindLocalPosition = bones[(int)B.Hips].localPosition;

            BuildCharacterFrame();
            baseLocalPosition = transform.localPosition;
            // CustomerVisualSet이 발바닥(메시 최저점)을 NPC 위치 높이에 맞춰 둔 상태에서 호출된다.
            footGroundHeight = LowestFootY() - (motionSource != null ? motionSource.position.y : transform.position.y);
            if (float.IsInfinity(footGroundHeight)) footGroundHeight = 0f;
            lastSourcePosition = motionSource != null ? motionSource.position : transform.position;
            initialized = true;
        }

        public void ResetMotion()
        {
            walkWeight = 0f;
            talkWeight = 0f;
            phase = 0f;
            if (motionSource != null)
                lastSourcePosition = motionSource.position;
        }

        private void FindBones()
        {
            Transform[] all = GetComponentsInChildren<Transform>(true);
            bool human = animator != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman;
            for (int i = 0; i < bones.Length; i++)
            {
                Transform found = null;
                if (human)
                    found = animator.GetBoneTransform(HumanMap[i]);
                if (found == null)
                {
                    foreach (string candidate in NameMap[i].Split('|'))
                    {
                        foreach (Transform t in all)
                        {
                            if (t.name == candidate) { found = t; break; }
                        }
                        if (found != null) break;
                    }
                }
                bones[i] = found;
            }
        }

        private void BuildCharacterFrame()
        {
            Vector3 up = Vector3.up;
            Vector3 right;
            Transform lLeg = bones[(int)B.LUpperLeg], rLeg = bones[(int)B.RUpperLeg];
            Transform lArm = bones[(int)B.LUpperArm], rArm = bones[(int)B.RUpperArm];
            if (lLeg != null && rLeg != null)
                right = rLeg.position - lLeg.position;
            else if (lArm != null && rArm != null)
                right = rArm.position - lArm.position;
            else
                right = transform.right;
            right = Vector3.ProjectOnPlane(right, up).normalized;
            if (right.sqrMagnitude < 0.5f) right = transform.right;
            Vector3 forward = Vector3.Cross(right, up).normalized;

            localRight = transform.InverseTransformDirection(right).normalized;
            localUp = transform.InverseTransformDirection(up).normalized;
            localForward = transform.InverseTransformDirection(forward).normalized;

            leftToeBindLocal = BoneDirLocal(B.LFoot, B.LToes, -up * 0.3f + forward);
            rightToeBindLocal = BoneDirLocal(B.RFoot, B.RToes, -up * 0.3f + forward);
        }

        private Vector3 BoneDirLocal(B from, B to, Vector3 fallbackWorld)
        {
            Transform a = bones[(int)from], b = bones[(int)to];
            Vector3 dir = a != null && b != null ? b.position - a.position : fallbackWorld;
            return transform.InverseTransformDirection(dir.normalized);
        }

        /// <summary>바인드 포즈에서 이 모델의 정면(월드 방향). 방향 맞추기에 쓴다.</summary>
        public static Vector3 EstimateForward(Transform modelRoot)
        {
            Transform l = null, r = null;
            foreach (Transform t in modelRoot.GetComponentsInChildren<Transform>(true))
            {
                if (l == null && (t.name == "Left_UpperLeg" || t.name == "Bip001 L Thigh" || t.name == "mixamorig:LeftUpLeg")) l = t;
                if (r == null && (t.name == "Right_UpperLeg" || t.name == "Bip001 R Thigh" || t.name == "mixamorig:RightUpLeg")) r = t;
            }
            if (l == null || r == null)
                return modelRoot.forward;
            Vector3 right = Vector3.ProjectOnPlane(r.position - l.position, Vector3.up).normalized;
            return Vector3.Cross(right, Vector3.up).normalized;
        }

        private void Update()
        {
            if (!initialized)
                return;

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 sourcePos = motionSource != null ? motionSource.position : transform.position;
            Vector3 delta = sourcePos - lastSourcePosition;
            delta.y = 0f;
            lastSourcePosition = sourcePos;
            // 순간이동(손님 등장 위치 세팅)은 걷기로 치지 않는다.
            float speed = delta.magnitude > height ? 0f : delta.magnitude / dt;

            float targetWalk = Mathf.Clamp01(speed / fullWalkSpeed);
            walkWeight = Mathf.MoveTowards(walkWeight, targetWalk, dt * 4f);
            bool talking = isTalking != null && isTalking();
            talkWeight = Mathf.MoveTowards(talkWeight, talking ? 1f : 0f, dt * 3f);

            float cycle = Mathf.Max(0.2f, height * strideByHeight);
            phase += speed / cycle * Mathf.PI * 2f * dt;
            // 멈춰 있을 때는 다리가 모이도록 천천히 위상 진행
            if (speed < 0.05f) phase = Mathf.MoveTowards(phase, Mathf.Round(phase / Mathf.PI) * Mathf.PI, dt * 2f);
            if (phase > 1000f) phase -= Mathf.PI * 2f * 100f;

            if (useAnimatorController && animator != null)
            {
                SetParamIfExists(SpeedParam, speed, AnimatorControllerParameterType.Float);
                SetParamIfExists(TalkingParam, talking ? 1f : 0f, AnimatorControllerParameterType.Bool);
            }
        }

        private void SetParamIfExists(int hash, float value, AnimatorControllerParameterType type)
        {
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.nameHash != hash || p.type != type) continue;
                if (type == AnimatorControllerParameterType.Float) animator.SetFloat(hash, value);
                else animator.SetBool(hash, value > 0.5f);
                return;
            }
        }

        private void LateUpdate()
        {
            if (!initialized)
                return;

            float floorY = SampleFloor();

            if (useAnimatorController)
            {
                // 클립 재생 중에는 몸 전체만 바닥 높이에 맞춘다
                transform.localPosition = baseLocalPosition;
                float sourceY = motionSource != null ? motionSource.position.y : 0f;
                transform.position += Vector3.up * (floorY - sourceY);
                return;
            }

            // 1) 바인드 포즈로 되돌리기
            transform.localPosition = baseLocalPosition;
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] != null)
                    bones[i].localRotation = bindRotations[i];
            if (bones[(int)B.Hips] != null)
                bones[(int)B.Hips].localPosition = hipsBindLocalPosition;

            Vector3 R = transform.TransformDirection(localRight).normalized;
            Vector3 U = transform.TransformDirection(localUp).normalized;
            Vector3 F = transform.TransformDirection(localForward).normalized;
            float t = Time.time;
            float w = walkWeight;
            float s = Mathf.Sin(phase);
            float c = Mathf.Cos(phase);

            // 2) 몸통: 숨쉬기 + 걸을 때 약간 앞으로 + 대화 시 고개 끄덕임
            float breath = Mathf.Sin(t * 1.7f) * breathAngle;
            Tilt(B.Spine, R, breath * 0.5f + w * 3f);
            Tilt(B.Chest, R, breath * 0.5f);
            Tilt(B.Hips, U, w * 6f * s); // 골반 비틀기
            Tilt(B.UpperChest, U, -w * 5f * s);
            float nod = Mathf.Sin(t * 7.5f) * talkNodAngle * talkWeight + Mathf.Sin(t * 0.6f) * 1.5f;
            Tilt(B.Head, R, nod);
            Tilt(B.Head, U, Mathf.Sin(t * 0.37f) * 3f * (1f - w));

            // 3) 팔
            float gesture = talkWeight * (0.6f + 0.4f * Mathf.Sin(t * 2.3f));
            PoseArm(B.LUpperArm, B.LLowerArm, B.LHand, -R, U, F,
                armSwing * w * -s, idleElbowBend + w * 10f * Mathf.Max(0f, -s));
            PoseArm(B.RUpperArm, B.RLowerArm, B.RHand, R, U, F,
                armSwing * w * s + talkGesture * 0.5f * gesture,
                idleElbowBend + w * 10f * Mathf.Max(0f, s) + talkGesture * gesture);

            // 4) 다리 (왼다리 = sin, 오른다리 = 반대 위상)
            PoseLeg(B.LUpperLeg, B.LLowerLeg, B.LFoot, B.LToes, leftToeBindLocal, U, F,
                legSwing * w * s, kneeBend * w * Mathf.Max(0f, c) + 3f);
            PoseLeg(B.RUpperLeg, B.RLowerLeg, B.RFoot, B.RToes, rightToeBindLocal, U, F,
                -legSwing * w * s, kneeBend * w * Mathf.Max(0f, -c) + 3f);

            // 5) 디딘 발이 바닥에 닿게 몸 전체 높이 보정
            float groundY = floorY + footGroundHeight;
            float lowest = LowestFootY();
            if (!float.IsInfinity(lowest))
                transform.position += U * (groundY - lowest);
        }

        private void Tilt(B bone, Vector3 axis, float degrees)
        {
            Transform b = bones[(int)bone];
            if (b == null || Mathf.Abs(degrees) < 0.001f) return;
            b.rotation = Quaternion.AngleAxis(degrees, axis) * b.rotation;
        }

        private static void Aim(Transform bone, Transform child, Vector3 targetDir)
        {
            if (bone == null || child == null) return;
            Vector3 current = child.position - bone.position;
            if (current.sqrMagnitude < 1e-8f || targetDir.sqrMagnitude < 1e-8f) return;
            bone.rotation = Quaternion.FromToRotation(current.normalized, targetDir.normalized) * bone.rotation;
        }

        private static Vector3 BendToward(Vector3 dir, Vector3 toward, float degrees)
        {
            Vector3 perp = Vector3.ProjectOnPlane(toward, dir);
            if (perp.sqrMagnitude < 1e-6f) return dir;
            float r = degrees * Mathf.Deg2Rad;
            return (dir.normalized * Mathf.Cos(r) + perp.normalized * Mathf.Sin(r)).normalized;
        }

        private void PoseArm(B upper, B lower, B hand, Vector3 outward, Vector3 U, Vector3 F,
            float forwardSwingDeg, float elbowDeg)
        {
            Transform up = bones[(int)upper], lo = bones[(int)lower], ha = bones[(int)hand];
            if (up == null || lo == null) return;

            float down = armDownAngle * Mathf.Deg2Rad;
            Vector3 armDir = (outward * Mathf.Cos(down) - U * Mathf.Sin(down) + F * 0.08f).normalized;
            armDir = BendToward(armDir, F, forwardSwingDeg);
            Aim(up, lo, armDir);

            if (ha != null)
                Aim(lo, ha, BendToward(armDir, F, elbowDeg));
        }

        private void PoseLeg(B upper, B lower, B foot, B toes, Vector3 toeBindLocal, Vector3 U, Vector3 F,
            float swingDeg, float kneeDeg)
        {
            Transform th = bones[(int)upper], sh = bones[(int)lower], ft = bones[(int)foot], to = bones[(int)toes];
            if (th == null || sh == null) return;

            Vector3 thighDir = BendToward(-U, F, swingDeg);
            if (swingDeg < 0f) thighDir = BendToward(-U, -F, -swingDeg);
            Aim(th, sh, thighDir);

            if (ft != null)
                Aim(sh, ft, BendToward(thighDir, -F, kneeDeg));

            if (ft != null && to != null)
                Aim(ft, to, transform.TransformDirection(toeBindLocal));
        }

        private float SampleFloor()
        {
            Vector3 p = motionSource != null ? motionSource.position : transform.position;
            float target = groundHeightAt != null ? groundHeightAt(p) : p.y;
            if (!floorInitialized)
            {
                currentFloorY = target;
                floorInitialized = true;
            }
            else
            {
                // 문턱 같은 높이 차는 부드럽게 따라간다
                currentFloorY = Mathf.MoveTowards(currentFloorY, target, Time.deltaTime * 1.5f);
            }
            return currentFloorY;
        }

        private float LowestFootY()
        {
            float y = float.PositiveInfinity;
            Transform l = bones[(int)B.LFoot], r = bones[(int)B.RFoot];
            if (l != null) y = Mathf.Min(y, l.position.y);
            if (r != null) y = Mathf.Min(y, r.position.y);
            return y;
        }
    }
}
