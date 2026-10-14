using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

namespace Core.View
{
    /// <summary>
    /// 카드 드로우/소멸 시 빈번한 Instantiate/Destroy로 인한 GC 스파이크를 방지하는 카드 전용 오브젝트 풀
    /// </summary>
    public static class CardViewPool
    {
        private static readonly Dictionary<CardView, Queue<CardView>> pool = new Dictionary<CardView, Queue<CardView>>();
        private static Transform poolRoot;

        private static void EnsurePoolRoot()
        {
            if (poolRoot == null)
            {
                var go = new GameObject("[CardViewPool]");
                Object.DontDestroyOnLoad(go);
                poolRoot = go.transform;
            }
        }

        /// <summary>
        /// 풀에서 카드를 가져오거나 새로 생성합니다.
        /// </summary>
        public static CardView Get(CardView prefab, Transform parent)
        {
            if (prefab == null) return null;

            if (pool.TryGetValue(prefab, out var queue) && queue.Count > 0)
            {
                var card = queue.Dequeue();
                if (card != null)
                {
                    card.transform.SetParent(parent, false);
                    card.transform.localScale = Vector3.one;
                    card.transform.localRotation = Quaternion.identity;
                    card.ResetVisualState();
                    card.gameObject.SetActive(true);
                    return card;
                }
            }

            // 풀에 없으면 신규 인스턴스화
            var newCard = Object.Instantiate(prefab, parent);
            newCard.transform.localScale = Vector3.one;
            return newCard;
        }

        /// <summary>
        /// 카드를 풀로 반환하고 비활성화합니다.
        /// </summary>
        public static void Release(CardView card, CardView prefabKey = null)
        {
            if (card == null) return;

            // 진행 중인 모든 애니메이션 안전하게 중단
            card.transform.DOKill();
            var rt = card.GetComponent<RectTransform>();
            if (rt != null) rt.DOKill();

            card.ResetVisualState();
            card.SetSelected(false);
            card.gameObject.SetActive(false);

            EnsurePoolRoot();
            card.transform.SetParent(poolRoot, false);

            // 프리팹 키가 제공되지 않은 경우 기본 큐를 찾거나 생성
            CardView key = prefabKey != null ? prefabKey : card;
            if (!pool.TryGetValue(key, out var queue))
            {
                queue = new Queue<CardView>();
                pool[key] = queue;
            }

            queue.Enqueue(card);
        }

        /// <summary>
        /// 씬 언로드 시 풀에 보관된 카드 오브젝트들을 정리합니다.
        /// </summary>
        public static void Clear()
        {
            foreach (var kvp in pool)
            {
                while (kvp.Value.Count > 0)
                {
                    var card = kvp.Value.Dequeue();
                    if (card != null) Object.Destroy(card.gameObject);
                }
            }
            pool.Clear();

            if (poolRoot != null)
            {
                Object.Destroy(poolRoot.gameObject);
                poolRoot = null;
            }
        }
    }
}
