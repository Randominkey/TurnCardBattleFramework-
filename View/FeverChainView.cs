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
    /// 마린 등 피버(Fever) 타입을 사용하는 캐릭터의 체인 UI
    /// </summary>
    public class FeverChainView : BaseChainView
    {
        [SerializeField] private Slider feverSlider;
        [SerializeField] private TextMeshProUGUI feverText;
        [SerializeField] private TextMeshProUGUI stackText;
        [SerializeField] private float animDuration = 0.5f;

        [Header("Mode Toggle Objects")]
        [SerializeField] private GameObject[] normalModeObjects;
        [SerializeField] private GameObject[] feverModeObjects;

        public override void Setup(BattleCharacterModel model)
        {
            var gameSet = TableManager.Instance.GetTable<GameSetTable>().Get(0);
            float maxFever = gameSet?.maxFeverGauge ?? 100f;

            // 1. 게이지 바인딩
            UpdateGauge(model.chainGauge.Value, maxFever, true);
            model.chainGauge.Skip(1).Subscribe(current =>
            {
                UpdateGauge(current, maxFever);
            }).AddTo(this);

            if (tooltipTrigger != null)
            {
                tooltipTrigger.SetData(model.tableData.chainId, TooltipTrigger.TooltipType.Chain);
            }

            // 2. 피버 스택 바인딩
            model.feverStack.Subscribe(stack =>
            {
                if (stackText != null)
                {
                    stackText.text = stack > 0 ? $"STACK {stack}" : "";
                    stackText.gameObject.SetActive(model.isFeverModeActive.Value && stack > 0);

                    if (stack > 0)
                    {
                        // 스택 획득 시 연출 (움찔거림)
                        stackText.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f);
                    }
                }
            }).AddTo(this);

            // 3. 피버 모드 상태 바인딩 (오브젝트 토글 및 상태 표시)
            model.isFeverModeActive.Subscribe(isActive =>
            {


                // 인스펙터에 등록된 오브젝트들 토글
                if (normalModeObjects != null)
                {
                    foreach (var obj in normalModeObjects) if (obj != null) obj.SetActive(!isActive);
                }
                if (feverModeObjects != null)
                {
                    foreach (var obj in feverModeObjects) if (obj != null) obj.SetActive(isActive);
                }

                if (stackText != null) stackText.gameObject.SetActive(isActive && model.feverStack.Value > 0);

                if (!isActive) UpdateGauge(model.chainGauge.Value, maxFever, false);

            }).AddTo(this);
        }

        private void UpdateGauge(float current, float max, bool instant = false)
        {
            if (feverSlider != null)
            {
                if (instant) feverSlider.value = current / max;
                else feverSlider.DOValue(current / max, animDuration).SetEase(Ease.OutQuad);
            }

            if (feverText != null && !feverSlider.value.Equals(1.0f) && current < max)
            {
                feverText.text = $"{current:0}/{max:0}";
            }
        }
    }
}
