using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Table;

namespace Core.View
{
    /// <summary>
    /// 배틀의 모든 연출 프리팹과 타이밍 데이터를 관리하는 ScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "BattleProductionData", menuName = "ProjectH/Battle/ProductionData")]
    public class BattleProductionData : ScriptableObject
    {
        [Header("Modular Production Entry SO List")]
        public List<ProductionEntrySO> entries = new List<ProductionEntrySO>();

        [Header("General Clash Timings (ActionSequencer용)")]
        public float clashMoveDuration = 0.4f;
        public float clashShakeDuration = 0.4f;
        public float clashExitDuration = 0.2f;

        private Dictionary<(ProductionType, CharacterKeyName), ProductionEntrySO> cachedSoDict;

        private void InitializeCacheIfNeeded()
        {
            if (cachedSoDict != null && cachedSoDict.Count > 0) return;
            cachedSoDict = new Dictionary<(ProductionType, CharacterKeyName), ProductionEntrySO>();

            // 1. 인스펙터 entries 리스트에 등록된 유효한 SO 할당
            if (entries != null)
            {
                foreach (var item in entries)
                {
                    if (item == null) continue;
                    var key = (item.type, item.subKey);
                    if (!cachedSoDict.ContainsKey(key))
                    {
                        cachedSoDict.Add(key, item);
                    }
                }
            }

            // 2. Resources/Prefabs/ProductionEntries 폴더의 모든 ProductionEntrySO 에셋 런타임 자동 스캔 및 바인딩
            var loadedEntries = Resources.LoadAll<ProductionEntrySO>("Prefabs/ProductionEntries");
            if (loadedEntries != null)
            {
                foreach (var item in loadedEntries)
                {
                    if (item == null) continue;
                    var key = (item.type, item.subKey);
                    if (!cachedSoDict.ContainsKey(key))
                    {
                        cachedSoDict.Add(key, item);
                    }
                }
            }
        }

        public ProductionEntrySO GetEntry(ProductionType type, CharacterKeyName subKey = CharacterKeyName.None)
        {
            InitializeCacheIfNeeded();

            // 1. subKey가 명시되어 있는 경우 정확히 일치하는 SO 에셋 탐색
            if (subKey != CharacterKeyName.None)
            {
                if (cachedSoDict != null && cachedSoDict.TryGetValue((type, subKey), out var match))
                {
                    return match;
                }
                var listMatch = entries?.FirstOrDefault(e => e != null && e.type == type && e.subKey == subKey);
                if (listMatch != null) return listMatch;
            }

            // 2. subKey가 없거나 일치하는 항목이 없으면 기본(subKey == None) SO 탐색
            if (cachedSoDict != null && cachedSoDict.TryGetValue((type, CharacterKeyName.None), out var defaultMatch))
            {
                return defaultMatch;
            }
            
            var fallbackMatch = entries?.FirstOrDefault(e => e != null && e.type == type && (e.subKey == CharacterKeyName.None || e.subKey == subKey));
            if (fallbackMatch != null) return fallbackMatch;

            return null;
        }

        public ProductionEntrySO GetEntrySO(ProductionType type, CharacterKeyName subKey = CharacterKeyName.None)
        {
            return GetEntry(type, subKey);
        }

        private void OnValidate()
        {
            cachedSoDict = null; // 인스펙터 변경 시 캐시 리셋
        }
    }
}
