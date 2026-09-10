using System;
using System.Xml.Serialization;

namespace Core.Table
{
    [Serializable]
    public class ChainTableRow : TableRow
    {
        [XmlElement("Name")] public int nameStringId;
        [XmlElement("Desc")] public int descStringId;
        [XmlElement("Icon")] public string iconName;
        [XmlElement("Action")] public string action;
        [XmlElement("ChainSetValues")] public string chainSetValuesRaw;
        [XmlElement("Chain2Values")] public string chain2ValuesRaw;
        [XmlElement("Chain3Values")] public string chain3ValuesRaw;
        [XmlElement("Chain4Values")] public string chain4ValuesRaw;
        [XmlElement("Chain5Values")] public string chain5ValuesRaw;

        private float[] cachedChainSetValues;
        public float[] ChainSetValues => cachedChainSetValues ??= TableUtil.ParseFloatArray(chainSetValuesRaw);

        private float[] cachedChain2Values;
        public float[] Chain2Values => cachedChain2Values ??= TableUtil.ParseFloatArray(chain2ValuesRaw);

        private float[] cachedChain3Values;
        public float[] Chain3Values => cachedChain3Values ??= TableUtil.ParseFloatArray(chain3ValuesRaw);

        private float[] cachedChain4Values;
        public float[] Chain4Values => cachedChain4Values ??= TableUtil.ParseFloatArray(chain4ValuesRaw);

        private float[] cachedChain5Values;
        public float[] Chain5Values => cachedChain5Values ??= TableUtil.ParseFloatArray(chain5ValuesRaw);
    }

    [Serializable]
    [XmlRoot("ChainTable")]
    public class ChainTable : TableBase<ChainTable, ChainTableRow>
    {
        protected override string fileName => "ChainTable";
    }
}
