using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Core.Battle;
using Core.Table;

namespace Core.Flow.ChainAction
{
    /// <summary>
    /// 피버 모드 진입, 유지, 스택 누적 및 종료 대미지 정산을 전담하는 체인 액션 전략
    /// </summary>
    public class FeverChainAction : ChainActionBase
    {
        public override string ActionName => "Fever";

        public override void OnRpsEnd(ChainActionContext ctx, int winner, Action onComplete)
        {
            if (ctx?.battleModel == null)
            {
                onComplete?.Invoke();
                return;
            }

            // 1. 플레이어 피버 처리
            ProcessCharacterFever(ctx, ctx.battleModel.player, winner == 1, () =>
            {
                // 2. 적 피버 처리
                ProcessCharacterFever(ctx, ctx.battleModel.enemy, winner == 2, onComplete);
            });
        }

        public void ProcessCharacterFever(ChainActionContext ctx, BattleCharacterModel charModel, bool isWinner, Action onNext)
        {
            if (charModel == null)
            {
                onNext?.Invoke();
                return;
            }

            int limitRound = GetLimitFeverRound(charModel);

            // [상황 1: 이미 피버 모드 진행 중인 경우]
            if (charModel.isFeverModeActive.Value)
            {
                if (isWinner)
                {
                    charModel.feverStack.Value++;
                    DrawMissingElementCardForFever(charModel);

                    // 3연승(최대 스택) 달성 시 -> 성공 피니셔 대미지 부여 후 종료
                    if (charModel.feverStack.Value >= limitRound)
                    {
                        TerminateFeverMode(ctx, charModel, isSuccess: true, onComplete: onNext);
                        return;
                    }

                    onNext?.Invoke();
                }
                else
                {
                    // 패배 또는 무승부 시 -> 추가 대미지 없이 강제 종료
                    TerminateFeverMode(ctx, charModel, isSuccess: false, onComplete: onNext);
                }
                return;
            }

            // [상황 2: 피버 모드가 아닌 상태에서 게이지 100에 도달한 경우 -> 신규 피버 진입]
            int maxGauge = GetMaxFeverGauge(charModel);
            if (charModel.chainGauge.Value >= maxGauge)
            {
                charModel.ActivateFeverMode(limitRound);

                // [피버 진입 특권] 손패에 없는 묵/찌/빠 속성 보충
                ReplenishHandForFever(charModel);

                if (ctx?.battleView != null) ctx.battleView.RefreshHandVisuals();
                Debug.Log($"[Chain: Fever] {charModel.tableData.id} activated FeverMode on RpsEnd! Hand replenished.");
            }

            onNext?.Invoke();
        }

        public void TerminateFeverMode(ChainActionContext ctx, BattleCharacterModel charModel, bool isSuccess, Action onComplete = null)
        {
            if (charModel == null || !charModel.isFeverModeActive.Value || ctx?.battleModel == null)
            {
                onComplete?.Invoke();
                return;
            }

            int stack = charModel.feverStack.Value;
            var opponent = (charModel == ctx.battleModel.player) ? ctx.battleModel.enemy : ctx.battleModel.player;

            // [성공 종료]: 스택에 비례한 보너스 대미지 부여
            if (isSuccess && stack > 0)
            {
                float bonusDmg = GetFeverDamageForStack(charModel, stack);

                if (ctx.battleView != null)
                {
                    int winnerIndex = (charModel == ctx.battleModel.player) ? 1 : 2;
                    ctx.battleView.PlayDamageAnimation(winnerIndex, ctx.battleModel, () =>
                    {
                        opponent.TakeDamage(bonusDmg);
                        charModel.EndFeverMode(true);
                    }, () =>
                    {
                        ctx.onBattleEndCheck?.Invoke();
                        onComplete?.Invoke();
                    }, bonusDmg);
                    return;
                }
                else
                {
                    opponent.TakeDamage(bonusDmg);
                }
            }

            // [실패 또는 스택 0 종료]
            charModel.EndFeverMode(isSuccess);
            ctx.onBattleEndCheck?.Invoke();
            onComplete?.Invoke();
        }

        public void ReplenishHandForFever(BattleCharacterModel model)
        {
            if (model == null) return;

            var gameSet = TableManager.Instance.GetTable<GameSetTable>().Get(0);
            int maxCards = gameSet?.maxCards ?? 8;

            SkillElement[] requiredElements = new[] { SkillElement.Rock, SkillElement.Scissors, SkillElement.Paper };

            foreach (var element in requiredElements)
            {
                if (model.hand.Count >= maxCards) break;

                bool hasElement = model.hand.Any(c => c.EffectiveElement == element);
                if (!hasElement)
                {
                    DrawSpecificElementCard(model, element);
                }
            }
        }

        private void DrawMissingElementCardForFever(BattleCharacterModel model)
        {
            if (model == null) return;

            var gameSet = TableManager.Instance.GetTable<GameSetTable>().Get(0);
            int maxCards = gameSet?.maxCards ?? 8;
            if (model.hand.Count >= maxCards) return;

            bool hasRock = model.hand.Any(c => c.EffectiveElement == SkillElement.Rock);
            bool hasScissors = model.hand.Any(c => c.EffectiveElement == SkillElement.Scissors);
            bool hasPaper = model.hand.Any(c => c.EffectiveElement == SkillElement.Paper);

            SkillElement missing = SkillElement.Rock;
            if (!hasRock) missing = SkillElement.Rock;
            else if (!hasScissors) missing = SkillElement.Scissors;
            else if (!hasPaper) missing = SkillElement.Paper;
            else
            {
                DrawRandomCard(model);
                return;
            }

            DrawSpecificElementCard(model, missing);
        }

        private void DrawSpecificElementCard(BattleCharacterModel model, SkillElement element)
        {
            var skillTable = TableManager.Instance.GetTable<SkillTable>();
            if (skillTable == null || model.tableData.SkillIds == null) return;

            SkillTableRow targetSkillRow = null;
            foreach (var skillId in model.tableData.SkillIds)
            {
                var skillRow = skillTable.Get(skillId);
                if (skillRow != null && skillRow.element == element)
                {
                    targetSkillRow = skillRow;
                    break;
                }
            }

            if (targetSkillRow != null)
            {
                var newCard = new CardInstance(targetSkillRow);
                model.hand.Add(newCard);
                Debug.Log($"[Chain: Replenish] {model.tableData.id} drew guaranteed {element} card (SkillId: {targetSkillRow.id})!");
            }
        }

        private void DrawRandomCard(BattleCharacterModel model)
        {
            var skillTable = TableManager.Instance.GetTable<SkillTable>();
            if (skillTable == null || model.tableData.SkillIds == null || model.tableData.SkillIds.Length == 0) return;

            int randIdx = UnityEngine.Random.Range(0, model.tableData.SkillIds.Length);
            int skillId = model.tableData.SkillIds[randIdx];
            var skillRow = skillTable.Get(skillId);
            if (skillRow != null)
            {
                var newCard = new CardInstance(skillRow);
                model.hand.Add(newCard);
            }
        }

        public int GetMaxFeverGauge(BattleCharacterModel charModel)
        {
            if (charModel == null) return 100;
            var chainRow = TableManager.Instance.GetTable<ChainTable>().Get(charModel.tableData.chainId);
            if (chainRow != null && chainRow.ChainSetValues != null && chainRow.ChainSetValues.Length > 0 && chainRow.ChainSetValues[0] > 0)
                return (int)chainRow.ChainSetValues[0];

            var gameSet = TableManager.Instance.GetTable<GameSetTable>().Get(0);
            return gameSet?.maxFeverGauge ?? 100;
        }

        public int GetLimitFeverRound(BattleCharacterModel charModel)
        {
            if (charModel == null) return 3;
            var chainRow = TableManager.Instance.GetTable<ChainTable>().Get(charModel.tableData.chainId);
            if (chainRow != null && chainRow.ChainSetValues != null && chainRow.ChainSetValues.Length > 1 && chainRow.ChainSetValues[1] > 0)
                return (int)chainRow.ChainSetValues[1];

            var gameSet = TableManager.Instance.GetTable<GameSetTable>().Get(0);
            return gameSet?.limitFeverRound ?? 3;
        }

        public float GetFeverDamageForStack(BattleCharacterModel charModel, int stack)
        {
            if (charModel == null) return 0f;
            int targetIdx = 1 + stack;
            var chainRow = TableManager.Instance.GetTable<ChainTable>().Get(charModel.tableData.chainId);
            if (chainRow != null && chainRow.ChainSetValues != null && chainRow.ChainSetValues.Length > 2 && chainRow.ChainSetValues[2] > 0)
            {
                if (targetIdx < chainRow.ChainSetValues.Length && chainRow.ChainSetValues[targetIdx] > 0)
                    return chainRow.ChainSetValues[targetIdx];
                return chainRow.ChainSetValues.Last();
            }

            var gameSet = TableManager.Instance.GetTable<GameSetTable>().Get(0);
            if (gameSet != null && gameSet.FeverStackDamage != null && gameSet.FeverStackDamage.Length > 0)
            {
                int idx = Mathf.Clamp(stack - 1, 0, gameSet.FeverStackDamage.Length - 1);
                return gameSet.FeverStackDamage[idx];
            }

            return stack * 10f;
        }
    }
}

