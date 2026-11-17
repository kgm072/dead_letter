using UnityEngine;

namespace DeadLetter.Mail
{
    public sealed class LetterTutorialHUD : MonoBehaviour
    {
        [SerializeField] private LetterTaskState taskState;
        [SerializeField] private DayCycleManager dayCycle;

        public LetterTaskState TaskState => taskState;

        public string GetCurrentWorkLabel()
        {
            if (taskState == null)
                return "업무 정보 없음";

            ResolveDayCycle();
            if ((taskState.CurrentDay == 2 || taskState.CurrentDay == 3) && dayCycle?.CurrentPackageRequest != null)
            {
                Day2RequestDefinition definition = dayCycle.CurrentPackageRequest.Definition;
                return definition.RequestType == MailRequestType.Dispatch
                    ? $"{definition.CustomerDisplayName} 발송 업무"
                    : $"{definition.CustomerDisplayName} 수령 업무";
            }

            return taskState.RequiredDayComplete ? "오늘 업무 완료" : "편지 처리";
        }

        public string GetNextActionLabel()
        {
            if (taskState == null)
                return "업무 상태를 확인할 수 없음";
            if (taskState.RequiredDayComplete)
                return taskState.CurrentDay >= 3
                    ? "DAY 3 업무 완료 — 엔딩 연결 대기"
                    : "탕비실 소파에서 다음 DAY 시작";
            ResolveDayCycle();
            if ((taskState.CurrentDay == 2 || taskState.CurrentDay == 3) && dayCycle?.CurrentPackageRequest != null)
                return GetPackageNextAction(dayCycle.CurrentPackageRequest);
            if (!taskState.HasLetter)
                return "창구 민원인에게 편지 받기";
            if (!taskState.WorkstationOpened)
                return "작업실 작업대 사용";
            if (!taskState.HasOnlyRequiredStamps)
                return "필수 우표 구성 맞추기";
            if (!taskState.PostmarkApplied)
                return "편지 오른쪽 아래에 도장 찍기";
            if (!taskState.LetterMailed)
                return "처리실 우편함에 편지 넣기";
            return "민원인에게 발송 결과 알리기";
        }

        private string GetPackageNextAction(Day2RequestRuntimeState state)
        {
            if (!state.RequestAccepted || !taskState.HasLetter)
                return "창구 민원인에게 편지와 민원 받기";
            if (!state.Definition.ItemRequirementsConfirmed)
                return $"{state.Definition.CustomerDisplayName} 수령 물품 기획 확정 필요";
            if (!state.ItemsSecured)
                return state.Definition.RequestType == MailRequestType.Dispatch
                    ? "창구에서 요청 물품 받기"
                    : "창고에서 요청 물품 찾기";
            if (!state.EmptyBoxSecured)
                return "창고에서 빈 박스 확보";
            if (!state.Packed)
                return "작업실 포장대에서 물품 포장";
            if (!state.HasOnlyRequiredBoxPictograms)
            {
                LetterStamp incorrect = state.AppliedBoxPictograms & ~state.Definition.RequiredPictograms;
                if (incorrect != LetterStamp.None)
                    return $"박스의 {Day2Labels.GetStampList(incorrect)} 도장을 다시 찍어 제거";
                LetterStamp missing = state.Definition.RequiredPictograms & ~state.AppliedBoxPictograms;
                return $"분류 전표 확인 후 박스에 {Day2Labels.GetStampList(missing)} 도장 날인";
            }
            if (!taskState.HasOnlyRequiredStamps)
                return "편지에 필수 우표 부착";
            if (!taskState.PostmarkApplied)
                return "편지 오른쪽 아래에 도장 날인";
            if (!taskState.LetterMailed)
                return "처리실 우편함에 편지 넣기";
            if (state.Definition.RequestType == MailRequestType.Dispatch && !state.ConveyorDispatched)
                return "컨베이어로 포장 박스 발송";
            return state.Definition.RequestType == MailRequestType.Dispatch
                ? "민원인에게 발송 완료 알리기"
                : "해당 민원인에게 포장 물품 전달";
        }

        private void ResolveDayCycle()
        {
            dayCycle ??= FindAnyObjectByType<DayCycleManager>();
        }
    }
}
