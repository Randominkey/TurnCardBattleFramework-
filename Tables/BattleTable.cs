using System;
using System.Xml.Serialization;
using UnityEngine;

namespace Core.Table
{
    [Serializable]
    public class BattleTableRow : TableRow
    {
        [XmlElement("StoryPlayerCharacter")] public int storyPlayerCharacterId;
        [XmlElement("EnemyCharacter")] public int enemyCharacterId;
        [XmlElement("PlayerAxis")] public string playerAxisRaw;
        [XmlElement("EnemyAxis")] public string enemyAxisRaw;
        [XmlElement("BgmName")] public string bgmName;
        [XmlElement("BgImgName")] public string bgImgName;
        [XmlElement("BgImgAxis")] public string bgImgAxisRaw;
        [XmlElement("LimitRound")] public int limitRound;
        [XmlElement("UpgradeSettings")] public string upgradeSettingsRaw;
        [XmlElement("WinIllust")] public string winIllustName;
        [XmlElement("PONValues")] public string ponValuesRaw;
        [XmlElement("EliteValues")] public string eliteValuesRaw;

        public Vector2Int PlayerAxis => TableUtil.ParseVector2Int(playerAxisRaw);
        public Vector2Int EnemyAxis => TableUtil.ParseVector2Int(enemyAxisRaw);
        public Vector2Int BgImgAxis => TableUtil.ParseVector2Int(bgImgAxisRaw);
        
        private UpgradeType[] cachedUpgrades;
        public UpgradeType[] UpgradeSettings => cachedUpgrades ??= TableUtil.ParseEnumArray<UpgradeType>(upgradeSettingsRaw);

        private float[] cachedPonValues;
        public float[] PONValues => cachedPonValues ??= TableUtil.ParseFloatArray(ponValuesRaw);

        private float[] cachedEliteValues;
        public float[] EliteValues => cachedEliteValues ??= TableUtil.ParseFloatArray(eliteValuesRaw);
    }

    [Serializable]
    [XmlRoot("BattleTable")]
    public class BattleTable : TableBase<BattleTable, BattleTableRow>
    {
        protected override string fileName => "BattleTable";
    }
}
