using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using Core.Table;

namespace Core.View
{
    public class HandContainer : MonoBehaviour
    {
        [Header("Layout Settings")]
        [SerializeField] private float spacing = 160f;
        [SerializeField] private float cardWidth = 100f; // 카드 자체의 너비
        [SerializeField] private float animDuration = 0.4f;
        [SerializeField] private Vector2 drawStartOffset = new Vector2(500f, -500f); // 드로우 시작 지점 (덱 위치 가정)

        private List<CardView> cards = new List<CardView>();

        public void AddCard(CardView card, bool animate = true)
        {
            cards.Add(card);
            card.transform.SetParent(transform, false);
            card.transform.localScale = Vector3.one; //user edited
            card.SetOwnerContainer(this); // [Fix] 소속 컨테이너 정보 갱신 (마우스 이탈 시 정렬 동기화 대응)

            // RPS 속성 및 ID 순으로 정렬 (Rock, Scissors, Paper 순서 유지)
            cards.Sort((a, b) =>
            {
                int elementCompare = a.SkillData.element.CompareTo(b.SkillData.element);
                if (elementCompare != 0) return elementCompare;
                return a.SkillData.id.CompareTo(b.SkillData.id);
            });

            // Hierarchy 순서 동기화
            SyncSiblingIndices();

            if (animate)
            {
                // 정렬된 후 이 카드의 인덱스를 찾아 목표 위치 계산
                int index = cards.IndexOf(card);
                Vector2 targetPos = GetTargetPosition(index);
                RectTransform rt = card.GetComponent<RectTransform>();

                Vector3 targetScale = card.transform.localScale;
                rt.anchoredPosition = targetPos + drawStartOffset;
                card.transform.localScale = Vector3.zero;

                rt.DOAnchorPos(targetPos, animDuration).SetEase(Ease.OutCubic);
                card.transform.DOScale(targetScale, animDuration).SetEase(Ease.OutBack);

                // 나머지 카드들도 새로운 위치로 이동
                UpdateLayout(card);
            }
            else
            {
                UpdateLayout();
            }
        }

        public void RemoveCard(CardView card, bool animate = true)
        {
            if (cards.Contains(card))
            {
                cards.Remove(card);
                // [Fix] 애니메이션 여부와 상관없이 항상 레이아웃을 갱신하여 빈 공간을 메꿉니다.
                UpdateLayout(animate);
                SyncSiblingIndices();
            }
        }

        public void Clear()
        {
            foreach (var card in cards)
            {
                if (card != null) Destroy(card.gameObject);
            }
            cards.Clear();
        }

        /// <summary>
        /// 카드의 리스트 순서와 Hierarchy 순서(Sibling Index)를 동기화합니다.
        /// 이를 통해 왼쪽 카드가 아래에, 오른쪽 카드가 위에 자연스럽게 포개지게 됩니다.
        /// </summary>
        public void SyncSiblingIndices()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null)
                {
                    cards[i].transform.SetSiblingIndex(i);
                }
            }
        }

        public void UpdateLayout(bool animate = true, CardView excludeCard = null)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] == null || cards[i] == excludeCard) continue;

                Vector2 targetPos = GetTargetPosition(i);
                RectTransform rt = cards[i].GetComponent<RectTransform>();

                if (animate)
                {
                    // 부드럽게 이동
                    rt.DOAnchorPos(targetPos, animDuration).SetEase(Ease.OutCubic);
                }
                else
                {
                    // 즉시 정렬 (Gaps 해결 핵심)
                    rt.anchoredPosition = targetPos;
                }
            }
            SyncSiblingIndices();
        }

        private Vector2 GetTargetPosition(int index)
        {
            int total = cards.Count;
            float rightmostCenter = GetComponent<RectTransform>().sizeDelta.x / 2 - cardWidth / 2;
            float startX = rightmostCenter - (total - 1) * spacing;

            return new Vector2(startX + index * spacing, 0);
        }
    }
}
