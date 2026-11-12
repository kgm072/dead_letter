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
            if (taskState.CurrentDay == 2 && dayCycle?.CurrentDayTwoRequest != null)
            {
                Day2RequestDefinition definition = dayCycle.CurrentDayTwoRequest.Definition;
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
                return "탕비실 소파에서 다음 DAY 시작";
            ResolveDayCycle();
            if (taskState.CurrentDay == 2 && dayCycle?.CurrentDayTwoRequest != null)
                return GetDayTwoNextAction(dayCycle.CurrentDayTwoRequest);
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

        private string GetDayTwoNextAction(Day2RequestRuntimeState state)
        {
            if (!state.RequestAccepted || !taskState.HasLetter)
                return "창구 민원인에게 편지와 민원 받기";
            if (!state.ItemsSecured)
                return state.Definition.RequestType == MailRequestType.Dispatch
                    ? "창구에서 요청 물품 받기"
                    : "창고에서 요청 물품 찾기";
            if (!state.EmptyBoxSecured)
                return "창고에서 빈 박스 확보";
            if (!state.Packed)
                return "작업실 포장대에서 물품 포장";
            if (!taskState.HasOnlyRequiredStamps)
                return "편지에 필수 우표 부착";
            if (!taskState.PostmarkApplied)
                return "편지 오른쪽 아래에 도장 날인";
            if (!taskState.LetterMailed)
                return "처리실 우편함에 편지 넣기";
            if (!state.HasOnlyRequiredBoxPictograms)
                return "포장 박스에 필수 픽토그램 날인";
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
