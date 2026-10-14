using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UniRx;
using Core.Battle;
using Core.Table;
using DG.Tweening;

namespace Core.View
{
    /// <summary>
    /// 코보 등 Rain 체인 타입을 사용하는 캐릭터의 체인 상태 표시 UI
    /// </summary>
    public class RainChainView : BaseChainView
    {
        [Header("UI Components")]
        [SerializeField] private Slider rainStackSlider;
        [SerializeField] private TextMeshProUGUI rainStackText;
        [SerializeField] private float animDuration = 0.3f;

        [Header("Optional Visuals")]
        [SerializeField] private GameObject[] stackPipObjects; // 스택별 불 들어오는 핍 오브젝트 (인덱스 0~4)
        [SerializeField] private GameObject rainTimeModeObject; // RainTime 발동 중일 때 표시할 이펙트/오브젝트

        public override void Setup(BattleCharacterModel model)
        {
            if (model == null) return;

            // 1. MaxRainStack 값 획득 (ChainSetValues[0] 또는 기본값 5)
            var chainRow = TableManager.Instance.GetTable<ChainTable>().Get(model.tableData.chainId);
            int maxStack = (chainRow != null && chainRow.ChainSetValues != null && chainRow.ChainSetValues.Length > 0 && chainRow.ChainSetValues[0] > 0)
                ? (int)chainRow.ChainSetValues[0]
                : 5;

            if (rainStackSlider != null)
            {
                rainStackSlider.minValue = 0;
                rainStackSlider.maxValue = maxStack;
            }

            // 툴팁 설정
            if (tooltipTrigger != null)
            {
                tooltipTrigger.SetData(model.tableData.chainId, TooltipTrigger.TooltipType.Chain);
            }

            // 2. Rain 스택 구독 및 갱신
            model.rainStack.Subscribe(current =>
            {
                UpdateStackDisplay(current, maxStack);
            }).AddTo(this);

            // 3. RainTime 활성화 여부 구독
            if (rainTimeModeObject != null)
            {
                model.isRainTimeActive.Subscribe(isActive =>
                {
                    rainTimeModeObject.SetActive(isActive);
                }).AddTo(this);
            }
        }

        private void UpdateStackDisplay(int current, int maxStack)
        {
            // 텍스트 표시 (예: "RAIN 3/5")
            if (rainStackText != null)
            {
                rainStackText.text = $"{current}/{maxStack}";
                rainStackText.transform.DOKill();
                rainStackText.transform.DOPunchScale(Vector3.one * 0.15f, 0.25f);
            }

            // 슬라이더 표시
            if (rainStackSlider != null)
            {
                rainStackSlider.DOKill();
                rainStackSlider.DOValue(current, animDuration).SetEase(Ease.OutQuad);
            }

            // 핍 오브젝트 표시 (배열 크기만큼)
            if (stackPipObjects != null && stackPipObjects.Length > 0)
            {
                for (int i = 0; i < stackPipObjects.Length; i++)
                {
                    if (stackPipObjects[i] != null)
                    {
                        stackPipObjects[i].SetActive(i < current);
                    }
                }
            }
        }
    }
}
