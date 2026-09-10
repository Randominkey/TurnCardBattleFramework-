using System;
using System.Linq;
using System.Xml.Serialization;

namespace Core.Table
{
    [Serializable]
    public class BattleCharacterTableRow : TableRow
    {
        [XmlElement("Name")] public int nameStringId;
        [XmlElement("KeyName")] public string keyNameRaw; // XML에 명시적 태그가 없을 경우를 대비
        [XmlElement("MaxHp")] public float maxHp;
        [XmlElement("CharacterFaceIconName")] public string characterFaceIconName;
        [XmlElement("DefaultSpriteNames")] public string defaultSpriteNamesRaw;
        [XmlElement("AttackSpriteName")] public string attackSpriteNameRaw; // XML 명칭에 맞춰 단수형으로 수정
        [XmlElement("HitSpriteNames")] public string hitSpriteNamesRaw;
        [XmlElement("SpecialSkillSpriteName")] public string specialSkillSpriteName; // 누락된 필드 추가
        [XmlElement("SetSkillElements")] public string setSkillElementsRaw;
        [XmlElement("GetSkills")] public string getSkillsRaw;
        [XmlElement("SpecialSkillId")] public int specialSkillId;
        [XmlElement("SpecialSkillGauge")] public float specialSkillGauge;
        [XmlElement("ChainId")] public int chainId; // 누락된 필드 추가

        private CharacterKeyName? cachedKeyName;
        public CharacterKeyName KeyName => cachedKeyName ??= ParseKeyName();

        private CharacterKeyName ParseKeyName()
        {
            // 1. XML에 명시적으로 정의되어 있다면 우선 사용
            if (!string.IsNullOrEmpty(keyNameRaw) && Enum.TryParse(keyNameRaw, true, out CharacterKeyName result))
            {
                return result;
            }

            // 2. Heuristic: CharacterFaceIconName 파일명에서 캐릭터 이름 추출
            if (!string.IsNullOrEmpty(characterFaceIconName))
            {
                foreach (CharacterKeyName val in Enum.GetValues(typeof(CharacterKeyName)))
                {
                    if (val == CharacterKeyName.None || val == CharacterKeyName.Unknown) continue;
                    
                    // 파일명에 Enum 명칭(예: Marine, Pekora)이 포함되어 있는지 체크
                    if (characterFaceIconName.IndexOf(val.ToString(), StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return val;
                    }
                }
            }

            return CharacterKeyName.None;
        }

        private string[] cachedDefaultSpriteNames;
        public string[] DefaultSpriteNames => cachedDefaultSpriteNames ??= (string.IsNullOrEmpty(defaultSpriteNamesRaw) 
            ? new string[0] 
            : defaultSpriteNamesRaw.Split(',').Select(s => s.Trim()).ToArray());

        private string[] cachedAttackSpriteNames;
        public string[] AttackSpriteNames => cachedAttackSpriteNames ??= (string.IsNullOrEmpty(attackSpriteNameRaw) 
            ? new string[0] 
            : attackSpriteNameRaw.Split(',').Select(s => s.Trim()).ToArray());

        private string[] cachedHitSpriteNames;
        public string[] HitSpriteNames => cachedHitSpriteNames ??= (string.IsNullOrEmpty(hitSpriteNamesRaw) 
            ? new string[0] 
            : hitSpriteNamesRaw.Split(',').Select(s => s.Trim()).ToArray());

        private int[] cachedSkillIds;
        public int[] SkillIds => cachedSkillIds ??= TableUtil.ParseIntArray(setSkillElementsRaw);

        private int[] cachedInitialSkills;
        public int[] InitialSkills => cachedInitialSkills ??= TableUtil.ParseIntArray(getSkillsRaw);
    }

    [Serializable]
    [XmlRoot("BattleCharacterTable")]
    public class BattleCharacterTable : TableBase<BattleCharacterTable, BattleCharacterTableRow>
    {
        protected override string fileName => "BattleCharacterTable";
    }

    [Serializable]
    public class BattleHpTableRow : TableRow
    {
        [XmlElement("MaxHp")] public float maxHp;
        [XmlElement("SpriteDownHp")] public string spriteDownHpRaw;

        private float[] cachedHpThresholds;
        public float[] HpThresholds => cachedHpThresholds ??= TableUtil.ParseFloatArray(spriteDownHpRaw);
    }

    [Serializable]
    [XmlRoot("BattleHpTable")]
    public class BattleHpTable : TableBase<BattleHpTable, BattleHpTableRow>
    {
        protected override string fileName => "BattleHpTable";
    }
}
