using UnityEngine;
using TMPro;
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Battle;
using Core.Table;
using DG.Tweening;

namespace Core.View
{
    /// <summary>
    /// BattleClashPresenter 생성에 필요한 Context 묶음
    /// </summary>
    public class BattleClashContext
    {
        public ClashLayoutGroup playerClashLayout;
        public ClashLayoutGroup enemyClashLayout;
        public RectTransform cardClashPoint;
        public RectTransform clashHighOrderRoot;
        public TextMeshProUGUI clashResultText;
        public BattleProductionManager productionManager;
        public BattleProductionData productionData;
        public Transform viewTransform;
    }

    /// <summary>
    /// 가위바위보 카드 격돌(Clash) 배치, 연출 및 격돌 카드 수명주기를 전담하는 프리젠터
    /// </summary>
    public class BattleClashPresenter
    {
        private readonly BattleClashContext ctx;

        public List<CardView> PlayerClashViews { get; } = new List<CardView>();
        public List<CardView> EnemyClashViews { get; } = new List<CardView>();

        public BattleClashPresenter(BattleClashContext context)
        {
            this.ctx = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void AddPlayerClashCard(CardView card)
        {
            if (card != null && !PlayerClashViews.Contains(card))
            {
                PlayerClashViews.Add(card);
            }
        }

        public void AddEnemyClashCard(CardView card)
        {
            if (card != null && !EnemyClashViews.Contains(card))
            {
                EnemyClashViews.Add(card);
            }
        }

        public void ClearPlayerClashCards()
        {
            PlayerClashViews.Clear();
        }

        public bool HasCapturedPlayerCard(CardInstance instance)
        {
            return PlayerClashViews.Any(c => c.CardInstance == instance);
        }

        public CardView GetCapturedPlayerCard(CardInstance instance, List<CardView> viewList)
        {
            return PlayerClashViews.FirstOrDefault(c => c.CardInstance == instance && viewList.Contains(c));
        }

        public bool HasAnyClashCards => PlayerClashViews.Count > 0 || EnemyClashViews.Count > 0;

        public void PlayCardClashEffect(ActionSequencer sequencer, BattleModel model, int winnerIndex, float customDamage = -1f)
        {
            if (sequencer == null) return;

            float moveDur = ctx.playerClashLayout != null ? ctx.playerClashLayout.MoveDuration : 0.4f;

            // 1. 모든 카드 활성화 및 고오더 루트 이동
            foreach (var card in PlayerClashViews)
            {
                if (card == null) continue;
                if (ctx.clashHighOrderRoot != null) card.transform.SetParent(ctx.clashHighOrderRoot, true);
                else if (ctx.viewTransform != null) card.transform.SetParent(ctx.viewTransform, true);

                card.gameObject.SetActive(true);
                card.ResetVisualState();
                card.SetSelected(false);
            }
            if (ctx.playerClashLayout != null) ctx.playerClashLayout.ArrangeCards(PlayerClashViews, sequencer);

            foreach (var card in EnemyClashViews)
            {
                if (card == null) continue;
                if (ctx.clashHighOrderRoot != null) card.transform.SetParent(ctx.clashHighOrderRoot, true);
                else if (ctx.viewTransform != null) card.transform.SetParent(ctx.viewTransform, true);

                card.gameObject.SetActive(true);
                card.ResetVisualState();

#if UNITY_EDITOR
                if (card.IsCovered)
                {
                    card.SetCovered(false);
                }
#endif
            }
            if (ctx.enemyClashLayout != null) ctx.enemyClashLayout.ArrangeCards(EnemyClashViews, sequencer);
            
            sequencer.AddCommand(new DelayCommand(moveDur + 0.1f));

            // 2. 결과 연출 (SO 데이터 기반)
            if (ctx.productionManager != null)
            {
                float totalDmg = (customDamage >= 0) ? customDamage : 0;
                if (customDamage < 0)
                {
                    totalDmg = BattleLogic.CalculateDamage(model, winnerIndex);
                }

                bool isPlayerWinner = (winnerIndex == 1);
                int chainCount = isPlayerWinner ? model.playerSelectedCards.Count : model.enemySelectedCards.Count;

                var data = new { winnerIndex, totalDmg, chainCount };
                
                ProductionType resultType = ProductionType.ClashDraw;
                if (winnerIndex == 1) resultType = ProductionType.ClashWin;
                else if (winnerIndex == 2) resultType = ProductionType.ClashLose;

                sequencer.AddCommand(new ProductionCommand(ctx.productionManager, resultType, CharacterKeyName.None, null, data));
            }
            else
            {
                sequencer.AddCommand(new ActionCommand(() => {
                    if (ctx.clashResultText != null)
                    {
                        if (winnerIndex == 0)
                        {
                            ctx.clashResultText.text = "DRAW";
                        }
                        else
                        {
                            bool isPlayerWinner = (winnerIndex == 1);
                            var winnerCards = isPlayerWinner ? model.playerSelectedCards : model.enemySelectedCards;
                            
                            float totalDmg = (customDamage >= 0) ? customDamage : 0;
                            if (customDamage < 0)
                            {
                                totalDmg = BattleLogic.CalculateDamage(model, winnerIndex);
                            }

                            if (winnerCards.Count == 0 && totalDmg > 0)
                            {
                                ctx.clashResultText.text = $"FEVER BONUS\nTOTAL DMG: {totalDmg}";
                            }
                            else
                            {
                                ctx.clashResultText.text = $"{winnerCards.Count} CHAIN\nTOTAL DMG: {totalDmg}";
                            }
                        }
                        
                        ctx.clashResultText.gameObject.SetActive(true);
                        ctx.clashResultText.transform.localScale = Vector3.zero;
                        ctx.clashResultText.transform.DOScale(1.2f, 0.2f).SetEase(Ease.OutBack);
                    }
                }));
                sequencer.AddCommand(new DelayCommand(0.8f));
            }

            // 3. 격돌 효과 (흔들기)
            float shakeDur = ctx.productionData != null ? ctx.productionData.clashShakeDuration : 0.4f;
            sequencer.AddCommand(new ActionCommand(() => {
                foreach (var c in PlayerClashViews) if (c != null) c.transform.DOShakePosition(shakeDur, 30f);
                foreach (var c in EnemyClashViews) if (c != null) c.transform.DOShakePosition(shakeDur, 30f);
            }));
            sequencer.AddCommand(new DelayCommand(shakeDur + 0.1f));
            
            // 4. 퇴장 연출
            float exitDur = ctx.productionData != null ? ctx.productionData.clashExitDuration : 0.2f;
            sequencer.AddCommand(new ActionCommand(() => {
                foreach (var c in PlayerClashViews) if (c != null) c.transform.DOScale(0, exitDur);
                foreach (var c in EnemyClashViews) if (c != null) c.transform.DOScale(0, exitDur);
                if (ctx.clashResultText != null) ctx.clashResultText.gameObject.SetActive(false);
            }));
            
            sequencer.AddCommand(new DelayCommand(exitDur));
        }

        public void CleanupClashCards()
        {
            foreach (var card in PlayerClashViews) if (card != null) CardViewPool.Release(card);
            foreach (var card in EnemyClashViews) if (card != null) CardViewPool.Release(card);
            PlayerClashViews.Clear();
            EnemyClashViews.Clear();
        }
    }
}

