using System;
using System.Xml.Serialization;

namespace Core.Table
{
    [Serializable]
    public class StringTableRow : TableRow
    {
        [XmlElement("KrString")] public string krString;
        [XmlElement("EnString")] public string enString;
        [XmlElement("JpString")] public string jpString;

        public string Get(Language language)
        {
            switch (language)
            {
                case Language.KrString: return krString;
                case Language.EnString: return enString;
                case Language.JpString: return jpString;
                default: return krString;
            }
        }
    }

    [Serializable]
    [XmlRoot("StringTable")]
    public class StringTable : TableBase<StringTable, StringTableRow>
    {
        protected override string fileName => "StringTable";
    }

    [Serializable]
    [XmlRoot("ScenarioTextTable")]
    public class ScenarioTextTable : TableBase<ScenarioTextTable, StringTableRow>
    {
        protected override string fileName => "ScenarioTextTable";
    }
}
