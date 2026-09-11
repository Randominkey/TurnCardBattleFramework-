using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

namespace Core.View
{
    /// <summary>
    /// 격돌 시 카드들의 레이아웃을 관리하는 컴포넌트.
    /// 디자이너가 인스펙터에서 오프셋 및 정렬 방식을 조절할 수 있습니다.
    /// </summary>
    public class ClashLayoutGroup : MonoBehaviour
    {
        public enum Alignment { Left, Center, Right }

        [Header("Layout Configuration")]
        [SerializeField] private Alignment alignment = Alignment.Center;
        [SerializeField] private float defaultSpacing = 120f;
        
        [Tooltip("차일드 개수별 오프셋 수치 (인덱스 0: 1장, 인덱스 1: 2장...) - 지정하지 않으면 defaultSpacing 사용")]
        [SerializeField] private List<float> spacingByCount = new List<float>();
        
        [Header("Animation Settings")]
        [SerializeField] private float moveDuration = 0.4f;
        [SerializeField] private Ease moveEase = Ease.OutCubic;
        [SerializeField] private float scaleMultiplier = 1.2f;

        public float MoveDuration => moveDuration;

        /// <summary>
        /// 카드들을 설정된 레이아웃에 맞춰 정렬하는 명령을 시퀀서에 추가합니다.
        /// </summary>
        public void ArrangeCards(List<CardView> cards, ActionSequencer sequencer)
        {
            if (cards == null || cards.Count == 0) return;

            int count = cards.Count;
            float spacing = GetSpacingForCount(count);
            float totalWidth = (count - 1) * spacing;
            
            // 이 컴포넌트가 부착된 RectTransform의 위치를 기준으로 계산
            RectTransform myRt = GetComponent<RectTransform>();
            Vector2 basePos = myRt.anchoredPosition;

            float startX = 0f;
            switch (alignment)
            {
                case Alignment.Left: startX = basePos.x; break;
                case Alignment.Center: startX = basePos.x - (totalWidth / 2f); break;
                case Alignment.Right: startX = basePos.x - totalWidth; break;
            }

            for (int i = 0; i < count; i++)
            {
                var card = cards[i];
                var rt = card.GetComponent<RectTransform>();
                Vector2 targetPos = new Vector2(startX + (i * spacing), basePos.y);

                sequencer.AddCommand(new ActionCommand(() => {
                    rt.DOAnchorPos(targetPos, moveDuration).SetEase(moveEase);
                    rt.DOScale(scaleMultiplier, moveDuration).SetEase(Ease.OutBack);
                }));
            }
        }

        private float GetSpacingForCount(int count)
        {
            if (count > 0 && count <= spacingByCount.Count)
            {
                float s = spacingByCount[count - 1];
                if (s > 0) return s;
            }
            return defaultSpacing;
        }
    }
}
