using UnityEngine;
using TMPro;
using DG.Tweening;

namespace Core.View
{
    /// <summary>
    /// 배틀 중 발생하는 각종 이벤트(스킬, 체인 등)를 텍스트로 나타내는 더미 연출용 컴포넌트
    /// </summary>
    public class BattleEffectItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI effectText;
        [SerializeField] private CanvasGroup canvasGroup;
        
        [Header("Animation Settings")]
        [SerializeField] private float moveDistance = 50f;
        [SerializeField] private float duration = 1.2f;
        [SerializeField] private float fadeDelay = 0.7f;

        /// <summary>
        /// 연출을 설정하고 애니메이션을 시작합니다.
        /// </summary>
        /// <param name="text">표시할 텍스트</param>
        /// <param name="color">텍스트 색상</param>
        public void Setup(string text, Color color)
        {
            if (effectText != null)
            {
                effectText.text = text;
                effectText.color = color;
            }

            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            
            // 1. 초기 상태 설정
            transform.localScale = Vector3.zero;
            if (canvasGroup != null) canvasGroup.alpha = 1f;

            // 2. 애니메이션 시퀀스 구성
            Sequence seq = DOTween.Sequence();
            
            // 나타나기 (커짐)
            seq.Append(transform.DOScale(1.2f, 0.2f).SetEase(Ease.OutBack));
            
            // 위로 떠오르기
            seq.Join(transform.DOMoveY(transform.position.y + moveDistance, duration).SetEase(Ease.OutCubic));
            
            // 사라지기
            if (canvasGroup != null)
            {
                seq.Insert(fadeDelay, canvasGroup.DOFade(0f, duration - fadeDelay).SetEase(Ease.InQuad));
            }

            // 완료 후 자동 파괴
            seq.OnComplete(() => Destroy(gameObject));
        }
    }
}
