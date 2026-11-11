using UnityEngine;

namespace DeadLetter.Mail
{
    public sealed class LetterTutorialHUD : MonoBehaviour
    {
        [SerializeField] private LetterTaskState taskState;

        public LetterTaskState TaskState => taskState;

        public string GetCurrentWorkLabel()
        {
            if (taskState == null)
                return "업무 정보 없음";

            return taskState.RequiredDayComplete ? "오늘 업무 완료" : "편지 처리";
        }

        public string GetNextActionLabel()
        {
            if (taskState == null)
                return "업무 상태를 확인할 수 없음";
            if (taskState.RequiredDayComplete)
                return "탕비실 소파에서 다음 DAY 시작";
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
    }
}
