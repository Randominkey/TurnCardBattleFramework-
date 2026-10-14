using UnityEngine;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System;
using Core.Table;
using TMPro;

namespace Core.View
{
    /// <summary>
    /// 하이라이트할 UI 대상을 구분하기 위한 플래그
    /// </summary>
    [Flags]
    public enum HighlightMask
    {
        None = 0,
        PlayerStatus = 1 << 0,
        EnemyStatus = 1 << 1,
        PlayerFigure = 1 << 2,
        EnemyFigure = 1 << 3,
        PlayerHand = 1 << 4,
        EnemyHand = 1 << 5,
        PlayerTray = 1 << 6,
        ClashContainer = 1 << 7,
        All = PlayerStatus | EnemyStatus | PlayerFigure | EnemyFigure | PlayerHand | EnemyHand | PlayerTray | ClashContainer
    }

    /// <summary>
    /// 배틀 연출의 종류
    /// </summary>
    public enum ProductionType
    {
        BattleStart,
        RoundStart,
        ClashResult, // 하위 호환성 유지
        ClashWin,
        ClashLose,
        ClashDraw,
        SpecialSkill,
        SpecialSkillCast,
        SpecialSkillSuccess,
        Victory,
        Defeat,
        Fever
    }

    /// <summary>
    /// 배틀 연출(딤드, 하이라이트, 프리팹 스폰)을 총괄하는 매니저
    /// </summary>
    public class BattleProductionManager : MonoBehaviour
    {
        [Header("Data Settings")]
        [SerializeField] private BattleProductionData productionData;

        [Header("Dimmed Settings (One-time)")]
        [SerializeField] private CanvasGroup dimmedOverlay;
        [SerializeField] private float dimFadeDuration = 0.3f;
        [SerializeField] private float dimmedAlpha = 0.6f;

        [Header("Dimmed Settings (Persistent)")]
        [SerializeField] private CanvasGroup persistentDimmedOverlay; // 피버 등 지속 연출용 별도 딤드
        [SerializeField] private float persistentDimmedAlpha = 0.4f;

        [Header("Spawn Settings")]
        [SerializeField] private Transform productionRoot; 

        [Header("UI Mappings (Granular)")]
        [SerializeField] private HighlightableUI playerStatusUI;
        [SerializeField] private HighlightableUI enemyStatusUI;
        [SerializeField] private HighlightableUI playerFigureUI;
        [SerializeField] private HighlightableUI enemyFigureUI;
        [SerializeField] private HighlightableUI playerHandUI;
        [SerializeField] private HighlightableUI enemyHandUI;
        [SerializeField] private HighlightableUI playerTrayUI;
        [SerializeField] private HighlightableUI clashContainerUI;

        private Dictionary<ProductionType, Stack<GameObject>> pool = new Dictionary<ProductionType, Stack<GameObject>>();
        private Dictionary<ProductionType, GameObject> activePersistentInstances = new Dictionary<ProductionType, GameObject>();

        public BattleProductionData Data => productionData;

        /// <summary>
        /// 일회성 연출을 시작하고 완료될 때까지 대기하는 코루틴
        /// </summary>
        public IEnumerator PlayProduction(ProductionType type, CharacterKeyName subKey = CharacterKeyName.None, float? customDuration = null, object data = null)
        {
            var entry = productionData != null ? productionData.GetEntry(type, subKey) : null;
            GameObject effectObj = GetEffectObject(type, entry);

            // [Fallback] 프리팹이 없거나 결손된 경우(데모 환경/IP 보호): TextMeshPro 기반 뉴트럴 텍스트 연출 실행
            if (effectObj == null)
            {
                yield return PlayFallbackTextProduction(type, data);
                yield break;
            }

            float duration = customDuration ?? (entry?.duration ?? 2.0f);
            float dimDelay = entry?.dimDelay ?? 0f;
            float highlightDelay = entry?.highlightDelay ?? 0f;
            float spawnDelay = entry?.spawnDelay ?? 0.2f; // 딤드 시작 기준 상대 딜레이
            float postWait = entry?.postWait ?? 0.5f;
            HighlightMask mask = entry?.highlightMask ?? HighlightMask.None;

            // 1. 딤드 및 하이라이트 시작 제어
            StartCoroutine(WaitAndDim(dimDelay, true));
            StartCoroutine(WaitAndHighlight(highlightDelay, mask, true));

            // 프리팹 스폰 대기 (딤드 시작 후 spawnDelay 만큼 더 대기)
            yield return new WaitForSeconds(dimDelay + spawnDelay);

            // 2. 프리팹 스폰 및 재생
            effectObj.transform.SetParent(productionRoot, false);
            effectObj.SetActive(true);

            effectObj.transform.DOKill(true); 
            effectObj.transform.DORewind(); 

            var effectComponent = effectObj.GetComponent<IBattleProductionEffect>();
            if (effectComponent != null)
            {
                effectComponent.Setup(data);
            }

            effectObj.transform.DOPlay();

            // 3. 연출 지속 시간 대기
            yield return new WaitForSeconds(duration);

            // 4. 프리팹 정리
            if (entry != null && entry.usePooling)
            {
                effectObj.SetActive(false);
                if (!pool.ContainsKey(type)) pool[type] = new Stack<GameObject>();
                pool[type].Push(effectObj);
            }
            else
            {
                Destroy(effectObj);
            }

            // 5. 정리 (여운 대기 후 해제)
            if (postWait > 0) yield return new WaitForSeconds(postWait);

            ApplyHighlight(mask, false);
            if (dimmedOverlay != null)
            {
                yield return dimmedOverlay.DOFade(0f, dimFadeDuration).WaitForCompletion();
                dimmedOverlay.gameObject.SetActive(false);
            }
        }

        private TextMeshProUGUI fallbackTextComponent;

        /// <summary>
        /// 프리팹이 없는 환경(데모 프로젝트 등)에서 저작권 없이 게임 흐름을 직관적으로 보여주는 TextMeshPro Fallback 연출
        /// </summary>
        private IEnumerator PlayFallbackTextProduction(ProductionType type, object data)
        {
            if (productionRoot == null) yield break;

            if (fallbackTextComponent == null)
            {
                var go = new GameObject("FallbackProductionText", typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(productionRoot, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(800f, 200f);
                rt.anchoredPosition = Vector2.zero;

                fallbackTextComponent = go.GetComponent<TextMeshProUGUI>();
                fallbackTextComponent.alignment = TextAlignmentOptions.Center;
                fallbackTextComponent.fontSize = 72;
                fallbackTextComponent.fontStyle = FontStyles.Bold;
                fallbackTextComponent.outlineWidth = 0.25f;
                fallbackTextComponent.outlineColor = new Color32(0, 0, 0, 255);
                fallbackTextComponent.raycastTarget = false;
            }

            // 타입별 텍스트 문구 및 테마 색상 설정
            string text = "";
            Color color = Color.white;
            float duration = 0.7f;

            switch (type)
            {
                case ProductionType.BattleStart:
                    text = "BATTLE START";
                    color = new Color32(255, 213, 79, 255); // Gold Yellow
                    duration = 0.8f;
                    break;
                case ProductionType.RoundStart:
                    int round = data is int r ? r : 1;
                    text = $"ROUND {round}";
                    color = new Color32(255, 255, 255, 255); // Clean White
                    duration = 0.6f;
                    break;
                case ProductionType.ClashWin:
                    text = "WIN!";
                    color = new Color32(0, 230, 118, 255); // Emerald Green
                    duration = 0.5f;
                    break;
                case ProductionType.ClashLose:
                    text = "LOSE...";
                    color = new Color32(255, 82, 82, 255); // Coral Red
                    duration = 0.5f;
                    break;
                case ProductionType.ClashDraw:
                    text = "DRAW";
                    color = new Color32(255, 179, 0, 255); // Amber Orange
                    duration = 0.5f;
                    break;
                case ProductionType.Victory:
                    text = "VICTORY!";
                    color = new Color32(255, 215, 0, 255); // Pure Gold
                    duration = 1.0f;
                    break;
                case ProductionType.Defeat:
                    text = "DEFEAT";
                    color = new Color32(176, 190, 197, 255); // Slate Gray
                    duration = 1.0f;
                    break;
                case ProductionType.Fever:
                    text = "FEVER TIME!";
                    color = new Color32(255, 64, 129, 255); // Neon Pink
                    duration = 0.8f;
                    break;
                default:
                    text = type.ToString().ToUpper();
                    color = new Color32(179, 136, 255, 255); // Soft Purple
                    duration = 0.5f;
                    break;
            }

            fallbackTextComponent.text = text;
            fallbackTextComponent.color = color;
            fallbackTextComponent.gameObject.SetActive(true);

            // 가벼운 딤드 연출
            if (dimmedOverlay != null)
            {
                dimmedOverlay.gameObject.SetActive(true);
                dimmedOverlay.DOFade(0.4f, 0.15f);
            }

            // 산뜻한 펀치 및 페이드아웃 트윈 (0.6~0.8초)
            Transform textTr = fallbackTextComponent.transform;
            textTr.localScale = Vector3.one * 0.5f;
            Sequence seq = DOTween.Sequence();
            seq.Append(textTr.DOScale(1.15f, 0.2f).SetEase(Ease.OutBack));
            seq.Append(textTr.DOScale(1.0f, 0.1f));
            seq.AppendInterval(duration - 0.45f);
            seq.Append(fallbackTextComponent.DOFade(0f, 0.15f));

            yield return seq.WaitForCompletion();

            fallbackTextComponent.gameObject.SetActive(false);

            if (dimmedOverlay != null)
            {
                yield return dimmedOverlay.DOFade(0f, 0.15f).WaitForCompletion();
                dimmedOverlay.gameObject.SetActive(false);
            }
        }

        private IEnumerator WaitAndDim(float delay, bool active)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            
            if (dimmedOverlay != null)
            {
                if (active)
                {
                    dimmedOverlay.gameObject.SetActive(true);
                    dimmedOverlay.DOFade(dimmedAlpha, dimFadeDuration);
                }
                else
                {
                    dimmedOverlay.DOFade(0f, dimFadeDuration).OnComplete(() => dimmedOverlay.gameObject.SetActive(false));
                }
            }
        }

        private IEnumerator WaitAndHighlight(float delay, HighlightMask mask, bool active)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            ApplyHighlight(mask, active);
        }

        /// <summary>
        /// 상태에 따라 유지되는 지속형 연출을 시작합니다.
        /// </summary>
        public void StartPersistentProduction(ProductionType type, CharacterKeyName subKey = CharacterKeyName.None)
        {
            if (activePersistentInstances.ContainsKey(type)) return;

            var entry = productionData != null ? productionData.GetEntry(type, subKey) : null;
            if (entry == null || entry.prefab == null) return;

            // 1. 전용 딤드 및 하이라이트
            if (persistentDimmedOverlay != null)
            {
                persistentDimmedOverlay.gameObject.SetActive(true);
                persistentDimmedOverlay.DOFade(persistentDimmedAlpha, dimFadeDuration);
            }
            ApplyHighlight(entry.highlightMask, true, 501); 

            // 2. 프리팹 스폰
            GameObject instance = Instantiate(entry.prefab, productionRoot);
            activePersistentInstances.Add(type, instance);
        }

        /// <summary>
        /// 지속형 연출을 중단하고 정리합니다.
        /// </summary>
        public void StopPersistentProduction(ProductionType type, CharacterKeyName subKey = CharacterKeyName.None)
        {
            if (activePersistentInstances.TryGetValue(type, out GameObject instance))
            {
                Destroy(instance);
                activePersistentInstances.Remove(type);
            }

            var entry = productionData != null ? productionData.GetEntry(type, subKey) : null;
            HighlightMask mask = entry?.highlightMask ?? HighlightMask.None;

            ApplyHighlight(mask, false);
            if (persistentDimmedOverlay != null && activePersistentInstances.Count == 0)
            {
                persistentDimmedOverlay.DOFade(0f, dimFadeDuration).OnComplete(() => {
                    persistentDimmedOverlay.gameObject.SetActive(false);
                });
            }
        }

        private void ApplyHighlight(HighlightMask mask, bool active, int order = 501)
        {
            if ((mask & HighlightMask.PlayerStatus) != 0 && playerStatusUI != null) playerStatusUI.SetHighlight(active, order);
            if ((mask & HighlightMask.EnemyStatus) != 0 && enemyStatusUI != null) enemyStatusUI.SetHighlight(active, order);
            if ((mask & HighlightMask.PlayerFigure) != 0 && playerFigureUI != null) playerFigureUI.SetHighlight(active, order);
            if ((mask & HighlightMask.EnemyFigure) != 0 && enemyFigureUI != null) enemyFigureUI.SetHighlight(active, order);
            
            if ((mask & HighlightMask.PlayerHand) != 0 && playerHandUI != null) playerHandUI.SetHighlight(active, order);
            if ((mask & HighlightMask.EnemyHand) != 0 && enemyHandUI != null) enemyHandUI.SetHighlight(active, order);
            if ((mask & HighlightMask.PlayerTray) != 0 && playerTrayUI != null) playerTrayUI.SetHighlight(active, order);
            if ((mask & HighlightMask.ClashContainer) != 0 && clashContainerUI != null) clashContainerUI.SetHighlight(active, order);
        }

        private GameObject GetEffectObject(ProductionType type, ProductionEntrySO entry)
        {
            if (entry != null && entry.usePooling)
            {
                if (!pool.ContainsKey(type)) pool[type] = new Stack<GameObject>();
                if (pool[type].Count > 0) return pool[type].Pop();
            }

            if (entry != null && entry.prefab != null)
            {
                return Instantiate(entry.prefab);
            }
            return null;
        }

        private bool IsPoolable(ProductionType type)
        {
            var entry = productionData != null ? productionData.GetEntry(type) : null;
            return entry != null && entry.usePooling;
        }
    }
}
