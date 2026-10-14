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
            // 최대 피버 게이지 획득 (ChainTable 우선 참조, fallback GameSetTable)
            float maxFever = 100f;
            var chainRow = TableManager.Instance.GetTable<ChainTable>().Get(model.tableData.chainId);
            if (chainRow != null && chainRow.ChainSetValues != null && chainRow.ChainSetValues.Length > 0 && chainRow.ChainSetValues[0] > 0)
            {
                maxFever = chainRow.ChainSetValues[0];
            }
            else
            {
                var gameSet = TableManager.Instance.GetTable<GameSetTable>().Get(0);
                maxFever = gameSet?.maxFeverGauge ?? 100f;
            }

            if (feverSlider != null)
            {
                feverSlider.minValue = 0f;
                feverSlider.maxValue = 1f;
            }

            // 1. 게이지 바인딩
            UpdateGauge(model.chainGauge.Value, maxFever, true);
            model.chainGauge.Skip(1).Subscribe(current =>
            {
                // 피버 모드 진행 중이 아닐 때만 일반 게이지 UI 갱신
                if (!model.isFeverModeActive.Value)
                {
                    UpdateGauge(current, maxFever, false);
                }
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
                        stackText.transform.DOKill();
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

                if (!isActive)
                {
                    // 피버 모드 종료 시 현재 게이지 상태로 즉시 복구
                    UpdateGauge(model.chainGauge.Value, maxFever, true);
                }

            }).AddTo(this);
        }

        private void UpdateGauge(float current, float max, bool instant = false)
        {
            float ratio = Mathf.Clamp01(current / max);

            if (feverSlider != null)
            {
                feverSlider.DOKill();
                if (instant) feverSlider.value = ratio;
                else feverSlider.DOValue(ratio, animDuration).SetEase(Ease.OutQuad);
            }

            if (feverText != null)
            {
                // 100 완충 시에도 정상적으로 100/100 표기
                feverText.text = current >= max ? $"{max:0}/{max:0}" : $"{current:0}/{max:0}";

                if (!instant)
                {
                    feverText.transform.DOKill();
                    feverText.transform.DOPunchScale(Vector3.one * 0.1f, 0.2f);
                }
            }
        }
    }
}
