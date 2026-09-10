using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.IO;
using UnityEngine;

namespace Core.Table
{
    [Serializable]
    public abstract class TableRow
    {
        [XmlElement("Id")]
        public int id;
    }

    [Serializable]
    public abstract class TableBase<T, TRow> : ITable 
        where T : class
        where TRow : TableRow
    {
        [XmlElement("Row")]
        public List<TRow> rows = new List<TRow>();

        protected abstract string fileName { get; }

        private Dictionary<int, TRow> rowCache;

        public void Load()
        {
            TextAsset textAsset = Core.Utils.ResourceUtil.AssetProvider.LoadXmlData(fileName);
            if (textAsset == null)
            {
                Debug.LogError($"[Table] Failed to load table: {fileName}");
                return;
            }

            XmlSerializer serializer = new XmlSerializer(typeof(T));
            using (StringReader reader = new StringReader(textAsset.text))
            {
                T data = serializer.Deserialize(reader) as T;
                if (data != null)
                {
                    this.rows = (data as TableBase<T, TRow>).rows;
                    BuildCache();
                }
            }
        }

        private void BuildCache()
        {
            rowCache = new Dictionary<int, TRow>();
            foreach (var row in rows)
            {
                if (!rowCache.ContainsKey(row.id))
                    rowCache.Add(row.id, row);
            }
        }

        public TRow Get(int id)
        {
            if (rowCache == null) BuildCache();
            rowCache.TryGetValue(id, out TRow row);
            return row;
        }
    }
}
