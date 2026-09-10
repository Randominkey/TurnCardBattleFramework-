using System;
using System.Xml.Serialization;

namespace Core.Table
{
    [Serializable]
    public class GameSetTableRow : TableRow
    {
        [XmlElement("MaxCards")] public int maxCards;
        [XmlElement("RoundDrawCards")] public int roundDrawCards;
        [XmlElement("EndGameDamageValue")] public float endGameDamageValue;
        [XmlElement("MaxFeverGauge")] public int maxFeverGauge;
        [XmlElement("LimitFeverRound")] public int limitFeverRound;
        [XmlElement("FeverStackDamage")] public string feverStackDamageRaw;
        [XmlElement("DisableTextSkipSecond")] public float disableTextSkipSecond;
        [XmlElement("PositionAAxis")] public float positionAAxis;
        [XmlElement("PositionBAxis")] public float positionBAxis;
        [XmlElement("PositionCAxis")] public float positionCAxis;
        [XmlElement("PositionDAxis")] public float positionDAxis;
        [XmlElement("PositionEAxis")] public float positionEAxis;

        private float[] cachedFeverStackDamage;
        public float[] FeverStackDamage => cachedFeverStackDamage ??= TableUtil.ParseFloatArray(feverStackDamageRaw);
    }

    [Serializable]
    [XmlRoot("GameSetTable")]
    public class GameSetTable : TableBase<GameSetTable, GameSetTableRow>
    {
        protected override string fileName => "GameSetTable";
    }

    [Serializable]
    public class ModeFlowTableRow : TableRow
    {
        [XmlElement("ModeType")] public ModeType modeType;
        [XmlElement("ReverseCharacterId")] public int reverseCharacterId;
        [XmlElement("ExecuteType")] public string executeTypesRaw;
        [XmlElement("ExecuteId")] public string executeIdsRaw;

        private SceneType[] cachedExecuteTypes;
        public SceneType[] ExecuteTypes => cachedExecuteTypes ??= TableUtil.ParseEnumArray<SceneType>(executeTypesRaw);

        private int[] cachedExecuteIds;
        public int[] ExecuteIds => cachedExecuteIds ??= TableUtil.ParseIntArray(executeIdsRaw);
    }

    [Serializable]
    [XmlRoot("ModeFlowTable")]
    public class ModeFlowTable : TableBase<ModeFlowTable, ModeFlowTableRow>
    {
        protected override string fileName => "ModeFlowTable";
    }
}
