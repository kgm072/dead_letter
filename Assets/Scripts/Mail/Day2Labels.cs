namespace DeadLetter.Mail
{
    public static class Day2Labels
    {
        public static string GetItemList(System.Collections.Generic.IEnumerable<Day2ItemType> items)
        {
            return items == null ? "없음" : string.Join(" · ", System.Linq.Enumerable.Select(items, GetItemLabel));
        }

        public static string GetStampList(LetterStamp stamps)
        {
            if (stamps == LetterStamp.None)
                return "없음";

            System.Collections.Generic.List<string> labels = new();
            foreach (LetterStamp value in System.Enum.GetValues(typeof(LetterStamp)))
            {
                if (value != LetterStamp.None && (stamps & value) == value)
                    labels.Add(GetStampLabel(value));
            }
            return string.Join(" · ", labels);
        }

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
                Day2ItemType.DriverLicense => "운전면허증",
                Day2ItemType.Soap => "비누",
                Day2ItemType.Bandage => "붕대",
                Day2ItemType.Soju => "소주",
                Day2ItemType.DriedSnack => "마른안주",
                Day2ItemType.UnknownCargo => "미상의 화물",
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
