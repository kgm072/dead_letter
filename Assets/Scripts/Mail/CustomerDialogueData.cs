using System;
using System.Collections.Generic;

namespace DeadLetter.Mail
{
    public sealed class CustomerDialogueChoice
    {
        public CustomerDialogueChoice(string firstOption, IEnumerable<string> firstResponses,
            string secondOption, IEnumerable<string> secondResponses)
        {
            Options = new[] { firstOption, secondOption };
            Responses = new[]
            {
                ToArray(firstResponses),
                ToArray(secondResponses)
            };
        }

        public IReadOnlyList<string> Options { get; }
        public IReadOnlyList<IReadOnlyList<string>> Responses { get; }

        private static string[] ToArray(IEnumerable<string> values)
        {
            return values == null ? Array.Empty<string>() : new List<string>(values).ToArray();
        }
    }

    public sealed class CustomerDialogueSection
    {
        public CustomerDialogueSection(
            IEnumerable<string> beforeChoice,
            CustomerDialogueChoice choice = null,
            IEnumerable<string> afterChoice = null,
            string eventId = "",
            bool eventAtStart = false)
        {
            BeforeChoice = ToArray(beforeChoice);
            Choice = choice;
            AfterChoice = ToArray(afterChoice);
            EventId = eventId ?? string.Empty;
            EventAtStart = eventAtStart;
        }

        public IReadOnlyList<string> BeforeChoice { get; }
        public CustomerDialogueChoice Choice { get; }
        public IReadOnlyList<string> AfterChoice { get; }
        public string EventId { get; }
        public bool EventAtStart { get; }

        private static string[] ToArray(IEnumerable<string> values)
        {
            return values == null ? Array.Empty<string>() : new List<string>(values).ToArray();
        }
    }

    public sealed class CustomerDialogueScenario
    {
        public CustomerDialogueScenario(int day, string customerId, string speakerName,
            CustomerDialogueSection opening, CustomerDialogueSection completion)
        {
            Day = day;
            CustomerId = customerId;
            SpeakerName = speakerName;
            Opening = opening;
            Completion = completion;
        }

        public int Day { get; }
        public string CustomerId { get; }
        public string SpeakerName { get; }
        public CustomerDialogueSection Opening { get; }
        public CustomerDialogueSection Completion { get; }
    }

    public static class CustomerDialogueCatalog
    {
        public const string DayOneIdentityCheckEvent = "day_01_identity_check";
        public const string DayThreeRedBalloonEvent = "day_03_red_balloon";

        private static readonly IReadOnlyList<CustomerDialogueScenario> Scenarios = CreateScenarios();

        public static IReadOnlyList<CustomerDialogueScenario> All => Scenarios;

        public static bool TryGet(int day, string customerId, out CustomerDialogueScenario scenario)
        {
            foreach (CustomerDialogueScenario candidate in Scenarios)
            {
                if (candidate.Day == day &&
                    string.Equals(candidate.CustomerId, customerId, StringComparison.Ordinal))
                {
                    scenario = candidate;
                    return true;
                }
            }

            scenario = null;
            return false;
        }

        private static IReadOnlyList<CustomerDialogueScenario> CreateScenarios()
        {
            return new[]
            {
                Day1Mascot(), Day1DrunkDriver(), Day1ModelStudent(), Day1BadConstruction(), Day1Drowned(),
                Day2Mascot(), Day2DrunkDriver(), Day2ModelStudent(), Day2BadConstruction(), Day2Drowned(),
                Day3Mascot(), Day3DrunkDriver(), Day3ModelStudent(), Day3BadConstruction(), Day3Drowned()
            };
        }

        private static CustomerDialogueScenario Day1Mascot() => Scenario(1, "mascot", "인형탈",
            Section(Lines(
                    "여기가 그 우체국 맞죠?",
                    "아, 맞네. 다행이다. 이런 데는 처음이라서.",
                    "편지 하나 보내려고 왔어요. 별거 아니니까 빨리 처리해 줘요. 조명 아래 오래 서 있으니까 땀이 차네."),
                Choice(
                    "(모니터를 켜며) \"신원 확인 좀 할게요.\"", Lines(
                        "……뭐야, 신원 확인도 해야 해요? 사람이 참 의심이 많네. 안 그래도 힘든데…. 나쁜 짓 하러 온 것도 아니고 편지 한 장 보내는 건데.",
                        "뭐, 규정이라니까 따라야지. 물론 말해줄 수 있죠."),
                    "…인형탈을 벗어주시겠어요?", Lines(
                        "아하하, 탈은 좀 봐줘요. … 내부 사정이 있거든. 혹시라도 동심을 깨면 안 되니까요.",
                        "그리고 탈 벗으면 냄새나요. ……. 별건 아니지만, 조금 부끄럽기도 하니까 말이죠.")),
                eventId: DayOneIdentityCheckEvent),
            Section(Lines(
                "하하, 일하시느라 고생 많으시네.",
                "수고하세요. 뭐든 일이라는 게 쉬운 건 아니잖아요?")));

        private static CustomerDialogueScenario Day1DrunkDriver() => Scenario(1, "drunk_driver", "음주운전",
            Section(Lines(
                    "저기……. 여기서 편지를 보낼 수 있다고 해서요.",
                    "네, 이거요. 늦기 전에 꼭 전달돼야 해서……. 부탁드릴게요."),
                Choice(
                    "신원 확인 좀 하겠습니다.", Lines("아, 네네. … 신원 확인은 중요하죠. 천천히 일 보셔도 괜찮아요."),
                    "얼굴이 많이 찌푸려져 있으신데, 괜찮으신가요?", Lines("아…… 괜찮아요. 그냥 술자리가 좀 길어져서 머리가 지끈거릴 뿐이에요.")),
                eventId: DayOneIdentityCheckEvent),
            Section(Array.Empty<string>(),
                Choice(
                    "저기… 손님? 처리 끝났습니다.", Array.Empty<string>(),
                    "어디 불편하신 곳이라도 있으신가요?", Array.Empty<string>()),
                Lines(
                    "……아뇨. 괜찮아요. 그냥 무언가 잊어버린 것 같아서요.",
                    "감사합니다. 그럼, 안녕히 계세요.")));

        private static CustomerDialogueScenario Day1ModelStudent() => Scenario(1, "model_student", "모범생",
            Section(Lines(
                    "저…… 들어가도 되나요? 아직 영업시간 안 끝났죠…….?",
                    "편지를 보내려고 왔어요. 아주 멀리 가는 편지인데……."),
                Choice(
                    "네, 이쪽으로 내주세요.", Lines("네, 바로 처리해주시네요. … 호, 혹시 따로 필요한 절차가 더 있을까요?"),
                    "(슬쩍 눈치를 살피며) 무슨 일 있어요?", Lines(
                        "아, 아뇨… 아무 일도 없어요. 걱정해주셔서 감사합니다…….",
                        "시간을 너무 오래 끌었죠, 죄송합니다… 화, 확인은 끝났나요……?")),
                eventId: DayOneIdentityCheckEvent),
            Section(Lines(
                "아, 아니에요. 아무것도 아니에요. 죄송합니다.",
                "감사합니다. 죄송합니다. 그럼 가 볼게요.")));

        private static CustomerDialogueScenario Day1BadConstruction() => Scenario(1, "bad_construction", "부실공사",
            Section(Lines(
                    "하하하! 여기가 맞구먼! 날이 어두워서 길을 한참 헤맸어!",
                    "편지 하나 보내려고 왔네. 뭐, 별일은 아니고 뭔가를 전할 사람이 있어서."),
                Choice(
                    "규정상 신원 확인 절차가 필요합니다.", Array.Empty<string>(),
                    "반갑습니다. 서류 작성 도와드릴까요?", Array.Empty<string>()),
                Lines(
                    "요즘은 이런 것도 전부 확인해야 하나? 세상 참 복잡해졌어.",
                    "……뭐, 규정이라면 따라야지. 일은 원래 순서대로 안전하게 해야 하는 거 아니겠나?"),
                DayOneIdentityCheckEvent),
            Section(Lines("하하! 수고하게. 몸도 좀 챙기고!")));

        private static CustomerDialogueScenario Day1Drowned() => Scenario(1, "troublemaker", "익사자",
            Section(Lines(
                    "아, 여기가 맞아요? 길 찾기 더럽게 어렵네 진짜.",
                    "편지 보내러 왔는데. 오래 걸리는 거면 그냥 관두고."),
                Choice(
                    "접수표 확인해 드리겠습니다. 편지 이쪽에 두세요.", Array.Empty<string>(),
                    "(빤히 바라보며) 무슨 불편한 일이라도 있으신가요?", Array.Empty<string>()),
                Lines(
                    "……왜 그렇게 봐요? 내가 뭐 죄라도 졌나? 사람 기분 팍 상하게 쳐다보네.",
                    "됐고, 빨리 처리나 해 줘요. 나 시간 없으니까.",
                    "하, 이런 것까지 귀찮게 하네."),
                DayOneIdentityCheckEvent),
            Section(Lines("이런 거 얼마나 걸린다고, 남의 시간을 이렇게 잡아먹네. 갈게요.")));

        private static CustomerDialogueScenario Day2Mascot() => Scenario(2, "mascot", "인형탈",
            Section(Lines("아, 오늘도 왔어요. 이 정도면 단골이라고 해도 되려나."),
                Choice(
                    "우체국에서 담배와 라이터 발송은 제한될 수 있습니다.", Lines(
                        "왜요, 갑자기 담배 가져와서 보내는 게 그렇게 이상한가.",
                        "그래도 이게 그쪽 일이니까, 그냥 보내줘요."),
                    "탈에 탄 자국이 있는데… 괜찮으신가요?", Lines(
                        "아, 이거~ 너무 신경쓰지 마세요.",
                        "원래 일하다가 보면 조금 타고, 그러는 거죠."))),
            Section(Lines("할 건 끝났으니까 이만 갈게요. 일 열심히 해요~!")));

        private static CustomerDialogueScenario Day2DrunkDriver() => Scenario(2, "drunk_driver", "음주운전",
            Section(Lines("이거……. 제가 받는 게 맞나요?"),
                Choice(
                    "(숙취해소제를 전달하며) 네, 본인 확인 되셨습니다.", Lines(
                        "숙취해소제…. 별로 안 마셨는데, 받아도 되는 건가요?",
                        "…아니에요, 혼잣말이에요."),
                    "술을 드신 건가요? 차는 가지고 오지 않으셨죠?", Lines(
                        "네, 이 정도는 아무렇지도 않으니까요.",
                        "…그렇게 피곤하지도 않고, 괜찮아요. 신경써줘서 고마워요."))),
            Section(Lines("감사합니다. 먼저 가 볼게요.")));

        private static CustomerDialogueScenario Day2ModelStudent() => Scenario(2, "model_student", "모범생",
            Section(Lines(
                    "저기, …… 이거 보내려고요.",
                    "별건 아니고, 교복이에요. …세탁은 못 했지만, 그래도…."),
                Choice(
                    "어제보다 상태가 더 안 좋아진 것 같은데, 괜찮아요?", Lines(
                        "신경 안쓰셔도 괜찮아요. … 일상이기도 하고…….",
                        "그건 그렇고, …"),
                    "교복을 왜 보내는지 물어봐도 될까요?", Array.Empty<string>()),
                Lines(
                    "그, 그냥 보낼 수 있을까요? ……받을 사람이 이걸 보면, 알아서 알 거니까….",
                    "죄송해요, 설명을 하기엔 조금 어려워서.")),
            Section(Lines("……아, …찌, 죄송해요! 일을 방해했네요. …감사합니다, 수고하세요.")));

        private static CustomerDialogueScenario Day2BadConstruction() => Scenario(2, "bad_construction", "부실공사",
            Section(Lines("이거 받으라며. 그래서 왔는데. 어디에 물건이 있다는 거야?")),
            Section(Lines(
                    "…… 절연장갑까지 있네.",
                    "이런 건 진작 제대로 챙겼어야 했는데. … 뭘 그렇게 쳐다보고 있어?"),
                Choice(
                    "안전을 위해 꼭 착용하셔야 합니다.", Array.Empty<string>(),
                    "……아닙니다. 안녕히 가세요.", Array.Empty<string>()),
                Lines("됐어, 신경 쓰지 말게. 생각해 보면 자네 잘못은 아니니까.")));

        private static CustomerDialogueScenario Day2Drowned() => Scenario(2, "troublemaker", "익사자",
            Section(Lines("안녕, 오라고 해서 왔어요. 그나저나 줄 게 있는 것 같던데, 빨리 주세요.")),
            Section(Lines(
                    "…뭐야, 누가 이런 걸 보냈어?",
                    "그쪽, 이게 뭔지 알… 아니에요."),
                Choice(
                    "보낸 분이 꼭 전해달라고 하셨습니다.", Lines(
                        "젠장, … 대충 누군지는 알 것 같네요.",
                        "괜히 이런 짓을 왜 해서…."),
                    "아시는 옷인가요?", Array.Empty<string>()),
                Lines(
                    "됐어요, 그냥 이걸 누가 아직도 가지고 있었는지 궁금했을 뿐이라서.",
                    "… 젠장, …그래 미안하다고.",
                    "예, 뭐…. 갈게요….")));

        private static CustomerDialogueScenario Day3Mascot() => Scenario(3, "mascot", "인형탈",
            Section(Lines("오늘은 내가 받는 쪽이에요. 뭘 보냈는지는 모르겠는데, 꽤 가벼워 보이네. 빨리 열어 봐도 되죠?")),
            Section(Lines("이럴 줄 알았는데… 젠장."),
                Choice(
                    "그 풍선은 무슨 의미인가요?", Lines(
                        "대충, 제 과오라고 할까요. … 그렇게 중요하진….",
                        "…… 됐어요."),
                    "……괜찮으신가요?", Lines("네, 네. 멀정해요. 그냥 생각에 잠겼을 뿐이에요.")),
                Lines(
                    "… 뭘 그렇게 봐요? 그렇게 이상해요?",
                    "그래도 전달받아서 그런지 조금 나아진 기분이네요. … 갈게요, 안녕."),
                DayThreeRedBalloonEvent, eventAtStart: true));

        private static CustomerDialogueScenario Day3DrunkDriver() => Scenario(3, "drunk_driver", "음주운전",
            Section(Lines(
                    "이걸 보내면…… 끝나는 거겠죠? 아니, 끝날 리가 없나.",
                    "……죄송해요. 제가 무슨 말을 하는지 모르겠네요. 저한테는 이제 쓰지도 못하는 물건이 되어버렸네요."),
                Choice(
                    "(면허증을 접수하며) 정말 발송하실건가요?", Lines(
                        "네, 이건 제가 가지고 있을 물건이 아니니까요.",
                        "미련없이…."),
                    "돌아가고 싶으신 순간이 있으신가요?", Lines(
                        "그런 순간이야 항상 있죠. 후회는 매순간 만들어지는 거잖아요.",
                        "하지만… 그렇다고 한들, 이미 생긴 과거는 지울 수 없죠.",
                        "이런, 감성에 젖어서 그만.")),
                Lines("…생각은 제대로 해볼 걸 그랬네요. 그냥, … 그냥 보내주세요.")),
            Section(Lines("이제 됐어요.")));

        private static CustomerDialogueScenario Day3ModelStudent() => Scenario(3, "model_student", "모범생",
            Section(Lines("그, 그게 받을 게 있다고 해서… …… 아닌가요?")),
            Section(Lines(
                "…이럴 줄은 몰랐는데. 아, 아니… 그냥 의외라고 생각이 들어서요.",
                "죄송해요, …… 아, 너무 오래 붙잡았죠…?",
                "……이럴 거면 그 때 잘 하지. 하지만, 과거는 바꿀 수 없으니까……",
                "아, … 아! 이만 갈게요. 안녕히 계세요.")));

        private static CustomerDialogueScenario Day3BadConstruction() => Scenario(3, "bad_construction", "부실공사",
            Section(Lines("오, 이건가?")),
            Section(Lines(
                "소주에 안주까지…… 하하, 이런 걸 받게 될 줄은 몰랐네.",
                "……참. 이런 걸 마신다고 뭐가 달라지겠나.",
                "그래도 보내 준 성의가 있으니 받아야지.")));

        private static CustomerDialogueScenario Day3Drowned() => Scenario(3, "troublemaker", "익사자",
            Section(Lines("어제, 많이 생각해봤는데. 아무래도 보내야 할 것 같아서…"),
                Choice(
                    "무엇을 보내시겠어요?", Lines(
                        "보면 안 보여요?",
                        "… 비누랑 반창고예요. 이건 별거 아니니까, 넘어가고."),
                    "(상처를 바라보며) 다리가 많이 아파 보이십니다.", Lines(
                        "… 아파보이는 거 저도 아니까, 이거나 보내주세요.",
                        "저보다 더 아픈 사람도 있을거고.")),
                Lines("… 왜 그렇게 열심히 봐요? 그냥 보내달라면 좀 보내 줘요.")),
            Section(Lines(
                "… 보낸 거 맞죠? 그럼 됐어요.",
                "그래도, 그래도… 주는 게 맞다고 난 생각하거든요.",
                "그럼, 이만 갈게요. … 감사해요.")));

        private static CustomerDialogueScenario Scenario(int day, string id, string speaker,
            CustomerDialogueSection opening, CustomerDialogueSection completion)
        {
            return new CustomerDialogueScenario(day, id, speaker, opening, completion);
        }

        private static CustomerDialogueSection Section(IEnumerable<string> before,
            CustomerDialogueChoice choice = null, IEnumerable<string> after = null, string eventId = "",
            bool eventAtStart = false)
        {
            return new CustomerDialogueSection(before, choice, after, eventId, eventAtStart);
        }

        private static CustomerDialogueChoice Choice(string first, IEnumerable<string> firstResponses,
            string second, IEnumerable<string> secondResponses)
        {
            return new CustomerDialogueChoice(first, firstResponses, second, secondResponses);
        }

        private static string[] Lines(params string[] values) => values;
    }
}
