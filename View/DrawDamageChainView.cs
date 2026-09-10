using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UniRx;
using Core.Battle;
using DG.Tweening;

namespace Core.View
{
    /// <summary>
    /// 페코라 등 드로우 데미지(DrawDamage) 타입을 사용하는 캐릭터의 체인 UI
    /// </summary>
    public class DrawDamageChainView : BaseChainView
    {
        [SerializeField] private Slider probSlider; // 확률 표시용 슬라이더
        [SerializeField] private TextMeshProUGUI probText;
        [SerializeField] private TextMeshProUGUI damageValueText; // 누적 데미지 수치 박스용
        [SerializeField] private float animDuration = 0.5f;

        public override void Setup(BattleCharacterModel model)
        {
            // 1. 드로우 데미지 확률 바인딩
            model.drawDamageProb.Subscribe(prob => {
                UpdateProb(prob);
            }).AddTo(this);

            if (tooltipTrigger != null)
            {
                tooltipTrigger.SetData(model.tableData.chainId, TooltipTrigger.TooltipType.Chain);
            }

            // 2. 누적 데미지 수치 바인딩
            model.drawDamageValue.Subscribe(val => {
                if (damageValueText != null)
                {
                    damageValueText.text = val > 0 ? $"{val}" : "0";
                    damageValueText.gameObject.SetActive(true);
                    
                    if (val > 0)
                    {
                        // 수치 증가 시 연출
                        damageValueText.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f);
                    }
                }
            }).AddTo(this);
        }

        private void UpdateProb(float prob)
        {
            if (probSlider != null)
            {
                probSlider.DOValue(prob, animDuration).SetEase(Ease.OutQuad);
            }

            if (probText != null)
            {
                probText.text = $"{(prob * 100):0}%";
            }
        }
    }
}
