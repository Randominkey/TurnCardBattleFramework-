using System;
using System.Linq;
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
        public SceneType[] ExecuteTypes => cachedExecuteTypes ??= ParseExecuteTypes(executeTypesRaw);

        private int[] cachedExecuteIds;
        public int[] ExecuteIds => cachedExecuteIds ??= ParseExecuteIds(executeIdsRaw);

        private static SceneType[] ParseExecuteTypes(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw == "-1") return Array.Empty<SceneType>();
            return raw.Split(',')
                      .Select(s => s.Trim())
                      .Where(s => !string.IsNullOrEmpty(s))
                      .Select(s => {
                          if (s.Equals("PreSpeak", StringComparison.OrdinalIgnoreCase) ||
                              s.Equals("AfterSpeak", StringComparison.OrdinalIgnoreCase) ||
                              s.Equals("Speak", StringComparison.OrdinalIgnoreCase))
                          {
                              return SceneType.Speak;
                          }
                          if (s.Equals("Battle", StringComparison.OrdinalIgnoreCase))
                          {
                              return SceneType.Battle;
                          }
                          if (Enum.TryParse<SceneType>(s, true, out var result))
                          {
                              return result;
                          }
                          return SceneType.Title;
                      })
                      .ToArray();
        }

        private static int[] ParseExecuteIds(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw == "-1") return Array.Empty<int>();
            return raw.Split(',')
                      .Select(s => s.Trim())
                      .Where(s => !string.IsNullOrEmpty(s))
                      .Select(s => int.TryParse(s, out int result) ? result : 0)
                      .ToArray();
        }
    }

    [Serializable]
    [XmlRoot("ModeFlowTable")]
    public class ModeFlowTable : TableBase<ModeFlowTable, ModeFlowTableRow>
    {
        protected override string fileName => "ModeFlowTable";
    }
}
