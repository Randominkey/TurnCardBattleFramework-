using System;
using System.Collections.Generic;
using UnityEngine;
using Core.Battle;
using Core.Table;
using Core.View;

namespace Core.Flow
{
    /// <summary>
    /// 라운드 진행 결과(승자, 대미지, 게이지, 결과 적용 콜백 및 연출 명령)를 담는 DTO
    /// </summary>
    public class BattleRoundResult
    {
        public int Winner { get; set; }
        public float PlayerDamage { get; set; }
        public float EnemyDamage { get; set; }
        public float PlayerGauge { get; set; }
        public float EnemyGauge { get; set; }
        public float RoundDamage { get; set; }
        public Action ApplyResults { get; set; }
        public Action ApplyAttackWinChain { get; set; }
        public ProductionCommand SuccessCommand { get; set; }
    }

    /// <summary>
    /// 가위바위보 승패 판정, 대미지/게이지 계산, 환경 배율 및 스킬 효과(LoseHeal, Berserk) 정산을 전담하는 라운드 프로세서
    /// </summary>
    public class BattleRoundProcessor
    {
        private readonly BattleModel battleModel;
        private readonly BattleCardDealer cardDealer;
        private readonly BattleChainActionHandler chainHandler;
        private readonly BattleSpecialSkillHandler specialSkillHandler;

        public BattleRoundProcessor(
            BattleModel battleModel,
            BattleCardDealer cardDealer,
            BattleChainActionHandler chainHandler,
            BattleSpecialSkillHandler specialSkillHandler)
        {
            this.battleModel = battleModel ?? throw new ArgumentNullException(nameof(battleModel));
            this.cardDealer = cardDealer;
            this.chainHandler = chainHandler;
            this.specialSkillHandler = specialSkillHandler;
        }

        /// <summary>
        /// 제출된 카드를 바탕으로 승패 판정, 드로우 정산, 대미지/게이지 계산 및 결과 적용 액션을 산출합니다.
        /// </summary>
        public BattleRoundResult ProcessRound()
        {
            // 1. 실제 카드 사용 시점에 핸드에서 제거 (증발 방지)
            foreach (var card in battleModel.playerSelectedCards) battleModel.player.hand.Remove(card);
            foreach (var card in battleModel.enemySelectedCards) battleModel.enemy.hand.Remove(card);

            // 2. [체인 타이밍 4: 특수 경우] Rain 효과 카드 제출 시 Rain 스택 증가
            chainHandler?.OnCardSubmitted();

            // 3. 승패 판정 (Determine Winner)
            int winner = DetermineWinner();

            Debug.Log($"[Battle] Decision -> Winner: {winner} (P_Draw: {battleModel.playerChoseDraw}, E_Draw: {battleModel.enemyChoseDraw}, P_Cards: {battleModel.playerSelectedCards.Count}, E_Cards: {battleModel.enemySelectedCards.Count})");

            // 4. Draw 선택 시 카드 드로우 실행
            if (battleModel.playerChoseDraw) cardDealer?.DrawRandomCard(battleModel.player);
            if (battleModel.enemyChoseDraw) cardDealer?.DrawRandomCard(battleModel.enemy);

            // 5. 대미지 계산
            float pDamage = 0f;
            float eDamage = 0f;

            if (winner == 1) eDamage = BattleLogic.CalculateDamage(battleModel, 1);
            else if (winner == 2) pDamage = BattleLogic.CalculateDamage(battleModel, 2);
            else if (winner == 0)
            {
                // [Berserk] 무승부 시 상대방에게 DrawDamage 부여
                if (battleModel.player.isSpecialActive && battleModel.player.drawDamageOnTie > 0)
                {
                    float baseDmg = BattleLogic.CalculateTotalBaseDamage(battleModel.playerSelectedCards);
                    if (baseDmg <= 0) baseDmg = 1f;
                    eDamage += baseDmg * battleModel.player.drawDamageOnTie * battleModel.player.attackMultiplier;
                    Debug.Log($"[Berserk] Player Berserk DrawDamage on Tie: {eDamage}");
                }
                if (battleModel.enemy.isSpecialActive && battleModel.enemy.drawDamageOnTie > 0)
                {
                    float baseDmg = BattleLogic.CalculateTotalBaseDamage(battleModel.enemySelectedCards);
                    if (baseDmg <= 0) baseDmg = 1f;
                    pDamage += baseDmg * battleModel.enemy.drawDamageOnTie * battleModel.enemy.attackMultiplier;
                    Debug.Log($"[Berserk] Enemy Berserk DrawDamage on Tie: {pDamage}");
                }
            }

            // 6. 게이지 계산
            float pGauge = BattleLogic.CalculateTotalGauge(battleModel.playerSelectedCards);
            float eGauge = BattleLogic.CalculateTotalGauge(battleModel.enemySelectedCards);

            // [New 261013] 무메이의 문명 환경: 스킬 게이지 충전 속도 2배속
            if (battleModel.currentEnvironment.Value == EnvironmentType.MumeiCivilization)
            {
                pGauge *= 2.0f;
                eGauge *= 2.0f;
            }

            // 7. 결과 적용 콜백 (Apply Results)
            Action applyResults = () => {
                if (pDamage > 0)
                {
                    var pSkill = TableManager.Instance.GetTable<SkillTable>().Get(battleModel.player.tableData.specialSkillId);
                    if (battleModel.player.isSpecialActive && pSkill?.action == "LoseHeal" && winner == 2)
                    {
                        battleModel.player.Heal(pDamage);
                    }
                    else battleModel.player.TakeDamage(pDamage);
                }

                if (eDamage > 0)
                {
                    var eSkill = TableManager.Instance.GetTable<SkillTable>().Get(battleModel.enemy.tableData.specialSkillId);
                    if (battleModel.enemy.isSpecialActive && eSkill?.action == "LoseHeal" && winner == 1)
                    {
                        battleModel.enemy.Heal(eDamage);
                    }
                    else battleModel.enemy.TakeDamage(eDamage);
                }

                if (pGauge > 0) battleModel.player.AddGauge(pGauge);
                if (eGauge > 0) battleModel.enemy.AddGauge(eGauge);
            };

            // 8. [체인 타이밍 2: Attack 플로우 - 가위바위보 승리]
            Action applyAttackWinChain = () => {
                if (winner == 1) chainHandler?.OnAttackWin(1, battleModel.playerSelectedCards);
                else if (winner == 2) chainHandler?.OnAttackWin(2, battleModel.enemySelectedCards);
            };

            // 9. 스페셜 스킬 성공 컷인 연출 커맨드 추출
            ProductionCommand successCmd = null;
            if (specialSkillHandler != null)
            {
                if (battleModel.player.isSpecialActive)
                {
                    var pSkill = TableManager.Instance.GetTable<SkillTable>().Get(battleModel.player.tableData.specialSkillId);
                    if (pSkill != null) successCmd = specialSkillHandler.GetSpecialSuccessCommand(battleModel.player, pSkill, winner);
                }

                if (successCmd == null && battleModel.enemy.isSpecialActive)
                {
                    var eSkill = TableManager.Instance.GetTable<SkillTable>().Get(battleModel.enemy.tableData.specialSkillId);
                    if (eSkill != null) successCmd = specialSkillHandler.GetSpecialSuccessCommand(battleModel.enemy, eSkill, winner);
                }
            }

            float roundDamage = (winner == 1) ? eDamage : ((winner == 2) ? pDamage : 0f);

            return new BattleRoundResult
            {
                Winner = winner,
                PlayerDamage = pDamage,
                EnemyDamage = eDamage,
                PlayerGauge = pGauge,
                EnemyGauge = eGauge,
                RoundDamage = roundDamage,
                ApplyResults = applyResults,
                ApplyAttackWinChain = applyAttackWinChain,
                SuccessCommand = successCmd
            };
        }

        private int DetermineWinner()
        {
            if (battleModel.playerChoseDraw && battleModel.enemyChoseDraw)
            {
                return 0;
            }
            if (battleModel.playerChoseDraw)
            {
                // 플레이어가 Draw(카드 뽑기)를 선택한 경우:
                // 적이 제출한 카드가 Rain/Fool 등 특수 속성인 경우 묵찌빠 공격 카드가 아니므로 Draw(0) 처리
                bool isEnemySpecial = battleModel.enemySelectedCards.Count > 0 && battleModel.enemySelectedCards[0].IsSpecial;
                return (battleModel.enemySelectedCards.Count == 0 || isEnemySpecial) ? 0 : 2;
            }
            if (battleModel.enemyChoseDraw)
            {
                // 적이 Draw(카드 뽑기)를 선택한 경우:
                // 플레이어가 제출한 카드가 Rain/Fool 등 특수 속성인 경우 묵찌빠 공격 카드가 아니므로 Draw(0) 처리
                bool isPlayerSpecial = battleModel.playerSelectedCards.Count > 0 && battleModel.playerSelectedCards[0].IsSpecial;
                return (battleModel.playerSelectedCards.Count == 0 || isPlayerSpecial) ? 0 : 1;
            }
            if (battleModel.playerSelectedCards.Count > 0 && battleModel.enemySelectedCards.Count > 0)
            {
                return BattleLogic.DetermineWinner(
                    battleModel.playerSelectedCards[0].EffectiveElement,
                    battleModel.enemySelectedCards[0].EffectiveElement);
            }
            return 0;
        }
    }
}

