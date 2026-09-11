using System;
using System.Linq;
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
    /// 캐릭터의 상태(이름, HP, 게이지, 초상화)를 표시하는 UI 컴포넌트
    /// </summary>
    [RequireComponent(typeof(Canvas), typeof(HighlightableUI))]
    public class BattleStatusView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextIdHolder nameTextIdHolder;
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private Slider hpSlider;
        [SerializeField] private TextMeshProUGUI gaugeText;
        [SerializeField] private Image gaugeFillImage; // 특수 스킬용 (기존 게이지 fillAmount 제어 전용)
        [SerializeField] private Image[] specialSkillIconImages; // 각 캐릭터별 특수 스킬 아이콘 이미지 (Img_Icon)

        private HighlightableUI highlightUI;

        [Header("Chain UI Delegation")]
        [SerializeField] private RectTransform chainUIRoot;
        [SerializeField] private BaseChainView feverChainPrefab;
        [SerializeField] private BaseChainView drawDamageChainPrefab;

        [SerializeField] private Image portraitImage;

        [Header("Feedback Settings")]
        [SerializeField] private TextMeshProUGUI feedbackTextPrefab;
        [SerializeField] private Transform feedbackRoot;

        [Header("Animation Settings")]
        [SerializeField] private float animDuration = 0.5f;
        [SerializeField] private float floatingTextDuration = 0.5f;
        [SerializeField] private Color32 damageFontColor = Color.red;
        [SerializeField] private Color32 recoverFontColor = Color.green;

        private BattleCharacterModel characterModel;
        private float lastHp;

        public void Setup(BattleCharacterModel model)
        {
            characterModel = model;
            lastHp = model.hp.Value;
            highlightUI = GetComponent<HighlightableUI>();

            // 1. 이름 설정
            if (nameTextIdHolder != null)
                nameTextIdHolder.SetId(model.tableData.nameStringId);

            // 2. HP 바인딩 (초기값은 즉시 반영)
            float maxHp = model.tableData.maxHp;
            UpdateHP(model.hp.Value, maxHp, true);
            model.hp.Skip(1).Subscribe(current =>
            {
                UpdateHP(current, maxHp);
            }).AddTo(this);

            // 3. 특수 스킬 게이지 바인딩
            float maxGauge = model.tableData.specialSkillGauge;
            UpdateGauge(model.specialGauge.Value, maxGauge, true);
            model.specialGauge.Skip(1).Subscribe(current =>
            {
                UpdateGauge(current, maxGauge);
            }).AddTo(this);

            if (gaugeFillImage != null)
            {
                var trigger = gaugeFillImage.gameObject.GetComponent<TooltipTrigger>();
                if (trigger == null) trigger = gaugeFillImage.gameObject.AddComponent<TooltipTrigger>();
                trigger.SetData(model.tableData.specialSkillId, TooltipTrigger.TooltipType.Skill);
            }

            // [Fix] 스페셜 스킬 아이콘(Img_Icon) 동적 로드 및 Fallback (에디터 잔재 방지)
            var skillRow = TableManager.Instance.GetTable<SkillTable>().Get(model.tableData.specialSkillId);
            var targetIcons = (specialSkillIconImages != null && specialSkillIconImages.Length > 0)
                ? specialSkillIconImages
                : GetComponentsInChildren<Image>(true).Where(img => img.name.Equals("Img_Icon", System.StringComparison.OrdinalIgnoreCase)).ToArray();

            if (skillRow != null && !string.IsNullOrEmpty(skillRow.iconName) && targetIcons != null)
            {
                foreach (var iconImg in targetIcons)
                {
                    if (iconImg != null)
                    {
                        Core.Utils.ResourceUtil.LoadSpriteWithFallback($"Sprites/SkillUI/{skillRow.iconName}", iconImg, model.tableData.KeyName);
                    }
                }
            }

            // 4. 체인 UI 동적 생성 및 바인딩
            var chainRow = TableManager.Instance.GetTable<ChainTable>().Get(model.tableData.chainId);
            string actionType = chainRow?.action ?? "";

            if (chainUIRoot != null)
            {
                BaseChainView prefab = null;
                if (actionType.Equals("Fever", System.StringComparison.OrdinalIgnoreCase)) prefab = feverChainPrefab;
                else if (actionType.Equals("DrawDamage", System.StringComparison.OrdinalIgnoreCase)) prefab = drawDamageChainPrefab;

                if (prefab != null)
                {
                    var chainView = Instantiate(prefab, chainUIRoot);
                    chainView.Setup(model);
                }
            }

            // 5. 초상화 설정
            LoadPortrait(model.tableData.characterFaceIconName);
        }

        private void UpdateHP(float current, float max, bool instant = false)
        {
            if (hpSlider != null)
            {
                if (instant) hpSlider.value = current / max;
                else hpSlider.DOValue(current / max, animDuration).SetEase(Ease.OutQuad);
            }

            if (hpText != null)
            {
                hpText.text = $"{current:0}/{max:0}";
                if (instant) hpText.gameObject.SetActive(true);
            }

            // HP 변화에 따른 플로팅 텍스트 출력
            float delta = current - lastHp;
            if (!instant && delta != 0)
            {
                ShowFloatingText(delta);
            }
            lastHp = current;
        }

        private void UpdateGauge(float current, float max, bool instant = false)
        {
            if (gaugeFillImage != null)
            {
                if (instant) gaugeFillImage.fillAmount = current / max;
                else gaugeFillImage.DOFillAmount(current / max, animDuration).SetEase(Ease.OutQuad);

                if (instant) gaugeFillImage.gameObject.SetActive(true);
            }

            if (gaugeText != null)
            {
                if (instant) gaugeText.gameObject.SetActive(true);

                if (current >= max)
                {
                    gaugeText.text = "READY";
                    gaugeText.color = Color.red;
                }
                else
                {
                    gaugeText.text = $"{current:0}/{max:0}";
                    gaugeText.color = Color.white;
                }
            }
        }

        private void ShowFloatingText(float amount)
        {
            if (feedbackTextPrefab == null || feedbackRoot == null) return;

            var textObj = Instantiate(feedbackTextPrefab, feedbackRoot);
            bool isDamage = amount < 0;

            textObj.text = isDamage ? $"{(int)amount}" : $"+{(int)amount}";
            textObj.color = isDamage ? damageFontColor : recoverFontColor;

            //// 연출: 위로 솟아오르며 페이드 아웃
            //var rt = textObj.GetComponent<RectTransform>();
            //rt.anchoredPosition = Vector2.zero;

            Sequence seq = DOTween.Sequence();
            seq.AppendInterval(floatingTextDuration);
            seq.OnComplete(() => Destroy(textObj.gameObject));
        }

        private void LoadPortrait(string spriteName)
        {
            if (portraitImage == null) return;

            if (string.IsNullOrEmpty(spriteName))
            {
                portraitImage.gameObject.SetActive(false);
                return;
            }

            var keyName = characterModel?.tableData?.KeyName ?? CharacterKeyName.None;
            Core.Utils.ResourceUtil.LoadSpriteWithFallback($"Sprites/Characters/BattleCharIcon/{spriteName}", portraitImage, keyName);
        }
    }
}
