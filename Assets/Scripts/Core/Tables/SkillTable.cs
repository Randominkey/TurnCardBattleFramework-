using System;
using System.Xml.Serialization;
using Core.Table;

namespace Core.Table
{
    [Serializable]
    public class SkillTableRow : TableRow
    {
        [XmlElement("DevNote")] public string devNote;
        [XmlElement("Name")] public int nameStringId;
        [XmlElement("Desc")] public int descStringId;
        [XmlElement("Icon")] public string iconName;
        [XmlElement("CastCutScene")] public string castCutScene;
        [XmlElement("SucceseCutScene")] public string successCutScene;
        [XmlElement("Element")] public SkillElement element; // Enum은 자동 매핑됨
        [XmlElement("Action")] public string action;
        [XmlElement("ActionValues")] public string actionValuesRaw;

        private float[] cachedActionValues;
        public float[] ActionValues => cachedActionValues ??= TableUtil.ParseFloatArray(actionValuesRaw);
    }

    [Serializable]
    [XmlRoot("SkillTable")]
    public class SkillTable : TableBase<SkillTable, SkillTableRow>
    {
        protected override string fileName => "SkillTable";
    }
}
