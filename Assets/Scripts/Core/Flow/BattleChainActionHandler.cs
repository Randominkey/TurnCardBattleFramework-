using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Core.Battle;
using Core.Table;
using Core.View;

namespace Core.Flow
{
    /// <summary>
    /// 배틀 체인(Chain: Fever, Rain, DiceRoll, DrawDamage 등) 시스템 전담 핸들러.
    /// 기획 4대 타이밍(드로우, 공격 승리, 가위바위보 종료, 특수 경우)에 맞춰 라이프사이클을 일원화하여 관리합니다.
    /// </summary>
    public class BattleChainActionHandler
    {
        private readonly BattleModel battleModel;
        private readonly BattleView battleView;
        private readonly MonoBehaviour coroutineRunner;
        private readonly Action onBattleEndCheck;

        public BattleChainActionHandler(
            BattleModel battleModel, 
            BattleView battleView, 
            MonoBehaviour coroutineRunner,
            Action onBattleEndCheck = null)
        {
            this.battleModel = battleModel;
            this.battleView = battleView;
            this.coroutineRunner = coroutineRunner;
            this.onBattleEndCheck = onBattleEndCheck;
        }

        #region [1] 라운드 플로우 - 드로우
        /// <summary>
        /// 드로우 행위 시 상대의 DrawDamage 체인 권능을 체크하고 데미지를 부여합니다.
        /// </summary>
        public void OnDraw(BattleCharacterModel drawTarget)
        {
            if (drawTarget == null || battleModel == null) return;

            var opponent = (drawTarget == battleModel.player) ? battleModel.enemy : battleModel.player;
            if (opponent.drawDamageProb.Value > 0)
            {
                if (UnityEngine.Random.value < opponent.drawDamageProb.Value)
                {
                    int damage = opponent.drawDamageValue.Value;
                    drawTarget.TakeDamage(damage);
                    Debug.Log($"[Chain: DrawDamage] {damage} damage dealt to {drawTarget.tableData.id} on Draw!");
                }
            }
        }
        #endregion

        #region [2] Attack 플로우 - 가위바위보 승리
        /// <summary>
        /// 가위바위보 승리 시 체인 효과를 발동합니다.
        /// (Fever 게이지, DrawDamage 누적, DamageUp, Rain 부여, DiceRoll 굴림 등)
        /// </summary>
        public void OnAttackWin(int winner, List<CardInstance> winnerCards)
        {
            if (winner == 0 || winnerCards == null || winnerCards.Count < 2) return;

            BattleLogic.ApplyChainEffects(battleModel, winner, winnerCards);
            if (battleView != null) battleView.RefreshHandVisuals();
        }
        #endregion

        #region [3] 라운드 플로우 - 가위바위보 종료
        /// <summary>
        /// 가위바위보 및 애니메이션 종료 후 호출되는 종합 체인 정산 파이프라인.
        /// 1. 피버 진행 중: 승리 시 스택++ (3스택 시 완주 대미지 후 종료), 패배/무승부 시 강제 종료
        /// 2. 피버 미진행 중: 게이지 100 도달 시 즉시 피버 진입 & 결핍 속성 보충
        /// 3. 코보: Rain 스택 확인 후 RainTime 발동
        /// 4. 후부키: 주사위 결과 정산 및 연출
        /// </summary>
        public void OnRpsEnd(int winner, Action onComplete = null)
        {
            // 1. 피버 상태 정산 (진행 중인 피버의 승패 처리 또는 신규 피버 진입)
            HandleFeverRpsEnd(winner, () =>
            {
                // 2. 코보 Rain 체인 스택 확인 및 RainTime 발동
                HandleRainRpsEnd();

                // 3. 후부키 DiceRoll 체인 정산 (연출 완료 후 최종 콜백 호출)
                HandleDiceRollRpsEnd(onComplete);
            });
        }

        private void HandleFeverRpsEnd(int winner, Action onFeverResolved)
        {
            // --- A. 플레이어 피버 처리 ---
            ProcessCharacterFever(battleModel.player, winner == 1, () =>
            {
                // --- B. 적 피버 처리 ---
                ProcessCharacterFever(battleModel.enemy, winner == 2, onFeverResolved);
            });
        }

        private void ProcessCharacterFever(BattleCharacterModel charModel, bool isWinner, Action onNext)
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
                        TerminateFeverMode(charModel, isSuccess: true, onComplete: onNext);
                        return;
                    }

                    onNext?.Invoke();
                }
                else
                {
                    // 패배 또는 무승부 시 -> 추가 대미지 없이 강제 종료
                    TerminateFeverMode(charModel, isSuccess: false, onComplete: onNext);
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

                if (battleView != null) battleView.RefreshHandVisuals();
                Debug.Log($"[Chain: Fever] {charModel.tableData.id} activated FeverMode on RpsEnd! Hand replenished.");
            }

            onNext?.Invoke();
        }

        private void HandleRainRpsEnd()
        {
            CheckRainTime(battleModel.player, battleModel.enemy);
            CheckRainTime(battleModel.enemy, battleModel.player);
        }

        private void HandleDiceRollRpsEnd(Action onComplete)
        {
            BattleCharacterModel diceUser = null;
            BattleCharacterModel diceOpponent = null;

            if (battleModel.player.lastDiceRoll.Value > 0)
            {
                diceUser = battleModel.player;
                diceOpponent = battleModel.enemy;
            }
            else if (battleModel.enemy.lastDiceRoll.Value > 0)
            {
                diceUser = battleModel.enemy;
                diceOpponent = battleModel.player;
            }

            if (diceUser != null)
            {
                int dice = diceUser.lastDiceRoll.Value;
                float effectValue = diceUser.lastDiceRollValue.Value;
                diceUser.lastDiceRoll.Value = 0;
                diceUser.lastDiceRollValue.Value = 0f;

                if (battleView != null && coroutineRunner != null)
                {
                    coroutineRunner.StartCoroutine(battleView.PlayDiceRollResolveRoutine(diceUser, diceOpponent, dice, effectValue, onComplete));
                }
                else
                {
                    BattleLogic.ProcessDiceRollResult(diceUser, diceOpponent, dice, effectValue);
                    onComplete?.Invoke();
                }
            }
            else
            {
                onComplete?.Invoke();
            }
        }
        #endregion

        #region [4] 라운드 플로우 - 특수 경우
        /// <summary>
        /// 제출된 Rain 효과 적용 카드의 수만큼 코보의 RainStack을 증가시킵니다.
        /// (양측 필드에 나온 모든 Rain 카드의 총합만큼 누적)
        /// </summary>
        public void OnCardSubmitted()
        {
            int pRain = battleModel.playerSelectedCards != null ? battleModel.playerSelectedCards.Count(c => c.IsRain) : 0;
            int eRain = battleModel.enemySelectedCards != null ? battleModel.enemySelectedCards.Count(c => c.IsRain) : 0;
            int totalRain = pRain + eRain;

            if (totalRain <= 0) return;

            ApplyRainStackIfRainChain(battleModel.player, totalRain, pRain, eRain);
            ApplyRainStackIfRainChain(battleModel.enemy, totalRain, pRain, eRain);
        }

        /// <summary>
        /// 유저가 피버 종료 버튼을 클릭했을 때 자진 종료 및 현재 스택 대미지를 정산합니다.
        /// </summary>
        public void OnManualFeverEnd(Action onComplete = null)
        {
            TerminateFeverMode(battleModel.player, isSuccess: true, onComplete);
        }
        #endregion

        #region Fever Core Engine & Utilities
        /// <summary>
        /// 피버 모드를 종료하고, 성공(완주 또는 자진 종료) 시 누적 스택 비례 대미지를 정산합니다.
        /// </summary>
        public void TerminateFeverMode(BattleCharacterModel charModel, bool isSuccess, Action onComplete = null)
        {
            if (charModel == null || !charModel.isFeverModeActive.Value)
            {
                onComplete?.Invoke();
                return;
            }

            int stack = charModel.feverStack.Value;
            var opponent = (charModel == battleModel.player) ? battleModel.enemy : battleModel.player;

            // [성공 종료]: 스택에 비례한 보너스 대미지 부여
            if (isSuccess && stack > 0)
            {
                float bonusDmg = GetFeverDamageForStack(charModel, stack);

                if (battleView != null)
                {
                    int winnerIndex = (charModel == battleModel.player) ? 1 : 2;
                    battleView.PlayDamageAnimation(winnerIndex, battleModel, () =>
                    {
                        opponent.TakeDamage(bonusDmg);
                        charModel.EndFeverMode(true);
                    }, () =>
                    {
                        onBattleEndCheck?.Invoke();
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
            onBattleEndCheck?.Invoke();
            onComplete?.Invoke();
        }

        /// <summary>
        /// 피버 타임 발동 시 발동자의 손패에 묵, 찌, 빠 중 한 장도 없는 속성이 있다면 최소 1장씩 보유하도록 카드를 보충합니다.
        /// </summary>
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
            int targetIdx = 1 + stack; // index2 = Stack1, index3 = Stack2, index4 = Stack3
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
        #endregion

        #region Rain Core Engine
        private void CheckRainTime(BattleCharacterModel charModel, BattleCharacterModel opponent)
        {
            if (charModel == null || opponent == null) return;
            var chainRow = TableManager.Instance.GetTable<ChainTable>().Get(charModel.tableData.chainId);
            if (chainRow == null || !chainRow.action.Equals("Rain", StringComparison.OrdinalIgnoreCase)) return;

            int maxStack = (chainRow.ChainSetValues != null && chainRow.ChainSetValues.Length > 0 && chainRow.ChainSetValues[0] > 0)
                           ? (int)chainRow.ChainSetValues[0]
                           : charModel.maxRainStack;

            if (charModel.rainStack.Value >= maxStack && !charModel.isRainTimeActive.Value)
            {
                charModel.ActivateRainTime();

                // 1. 코보 상대방의 손패 전체 삭제
                var cardsToRemove = opponent.hand.ToList();
                foreach (var card in cardsToRemove)
                {
                    opponent.hand.Remove(card);
                }

                // 2. 상대방의 특수 스킬 1라운드간 봉쇄
                opponent.BlockSpecialSkill(1);

                // 3. RainStack 초기화
                charModel.rainStack.Value = 0;

                if (battleView != null)
                {
                    battleView.RefreshHandVisuals();
                    battleView.ShowWarningMessage("RAIN TIME!");
                }

                Debug.Log($"[Chain: RainTime] {charModel.tableData.id} triggered RainTime! Cleared {opponent.tableData.id}'s entire hand.");
            }
        }

        private void ApplyRainStackIfRainChain(BattleCharacterModel character, int totalRain, int pRain, int eRain)
        {
            if (character == null) return;
            var chainRow = TableManager.Instance.GetTable<ChainTable>().Get(character.tableData.chainId);
            bool isRainChain = chainRow != null && chainRow.action.Equals("Rain", StringComparison.OrdinalIgnoreCase);
            if (isRainChain)
            {
                character.AddRainStack(totalRain);
                Debug.Log($"[Chain: Rain] {character.tableData.id} gained {totalRain} RainStack (Player: {pRain}, Enemy: {eRain})! Current: {character.rainStack.Value}");
            }
        }

        public SkillTableRow GetFoolSkillRow(BattleCharacterModel charModel)
        {
            if (charModel == null) return null;
            var chainRow = TableManager.Instance.GetTable<ChainTable>().Get(charModel.tableData.chainId);
            int foolCardId = (chainRow != null && chainRow.ChainSetValues != null && chainRow.ChainSetValues.Length > 1 && chainRow.ChainSetValues[1] > 0)
                           ? (int)chainRow.ChainSetValues[1]
                           : -1;

            SkillTableRow foolSkillRow = null;
            if (foolCardId > 0)
            {
                foolSkillRow = TableManager.Instance.GetTable<SkillTable>()?.Get(foolCardId);
            }
            if (foolSkillRow == null)
            {
                foolSkillRow = TableManager.Instance.GetTable<SkillTable>()?.rows?.FirstOrDefault(r => r.element == SkillElement.Fool);
            }
            return foolSkillRow;
        }
        #endregion
    }
}
