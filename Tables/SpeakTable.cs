using System;
using System.Xml.Serialization;

namespace Core.Table
{
    [Serializable]
    public class SpeakTableRow : TableRow
    {
        [XmlElement("Group")] public int group;
        [XmlElement("BgImgName")] public string bgImgName;
        
        [XmlElement("PositionA")] public string positionARaw;
        [XmlElement("PositionB")] public string positionBRaw;
        [XmlElement("PositionC")] public string positionCRaw;
        [XmlElement("PositionD")] public string positionDRaw;
        [XmlElement("PositionE")] public string positionERaw;
        
        [XmlElement("SpeakerKeyName")] public string speakerKeyNameRaw;
        [XmlElement("SpeakerFaceName")] public string speakerFaceNameRaw;
        
        [XmlElement("ScenarioTextId")] public int scenarioTextId;
        [XmlElement("WaitAction")] public string waitActionRaw;
        [XmlElement("KeepAction")] public string keepActionRaw;
        
        [XmlElement("ActionName")] public string actionNameRaw;
        
        [XmlElement("ActionValue0")] public string actionValue0;
        [XmlElement("ActionValue1")] public string actionValue1;
        [XmlElement("ActionValue2")] public string actionValue2;
        [XmlElement("ActionValue3")] public string actionValue3;
        [XmlElement("ActionValue4")] public string actionValue4;
        
        [XmlElement("BgmName")] public string bgmName;
        [XmlElement("SeName")] public string seName;
        [XmlElement("VoiceName")] public string voiceName;

        // Enum 및 Bool 파싱 프로퍼티
        public CharacterKeyName positionA => TableUtil.ParseEnum<CharacterKeyName>(positionARaw);
        public CharacterKeyName positionB => TableUtil.ParseEnum<CharacterKeyName>(positionBRaw);
        public CharacterKeyName positionC => TableUtil.ParseEnum<CharacterKeyName>(positionCRaw);
        public CharacterKeyName positionD => TableUtil.ParseEnum<CharacterKeyName>(positionDRaw);
        public CharacterKeyName positionE => TableUtil.ParseEnum<CharacterKeyName>(positionERaw);
        
        public CharacterKeyName speakerKeyName => TableUtil.ParseEnum<CharacterKeyName>(speakerKeyNameRaw);
        public CharacterFaceName speakerFaceName => TableUtil.ParseEnum<CharacterFaceName>(speakerFaceNameRaw);
        public SpeakAction actionName => TableUtil.ParseEnum<SpeakAction>(actionNameRaw);
        public bool waitAction => TableUtil.ParseBool(waitActionRaw);
        public bool keepAction => TableUtil.ParseBool(keepActionRaw);

        private string[] cachedActionValues;
        public string[] ActionValues => cachedActionValues ??= new string[] { actionValue0, actionValue1, actionValue2, actionValue3, actionValue4 };

        public CharacterKeyName GetPosition(int index)
        {
            switch (index)
            {
                case 0: return positionA;
                case 1: return positionB;
                case 2: return positionC;
                case 3: return positionD;
                case 4: return positionE;
                default: return CharacterKeyName.None;
            }
        }
    }

    [Serializable]
    [XmlRoot("SpeakTable")]
    public class SpeakTable : TableBase<SpeakTable, SpeakTableRow>
    {
        protected override string fileName => "SpeakTable";
    }
}
