namespace DeadLetter.Mail
{
    public static class Day2Labels
    {
        public static string GetItemLabel(Day2ItemType item)
        {
            return item switch
            {
                Day2ItemType.Cigarettes => "담배",
                Day2ItemType.Lighter => "라이터",
                Day2ItemType.HangoverRemedy => "숙취해소제",
                Day2ItemType.BulliedUniform => "괴롭힘 흔적 교복",
                Day2ItemType.SafetyHelmet => "안전모",
                Day2ItemType.InsulatedGloves => "절연장갑",
                _ => item.ToString()
            };
        }

        public static string GetStampLabel(LetterStamp stamp)
        {
            return stamp switch
            {
                LetterStamp.Fire => "불",
                LetterStamp.Water => "물",
                LetterStamp.Electricity => "전기",
                LetterStamp.Shock => "충격",
                LetterStamp.Medicine => "약",
                LetterStamp.Alcohol => "술",
                _ => "미선택"
            };
        }
    }
}
