using Core.Table;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Core.Battle
{
    public static class BattleLogic
    {
        public static int DetermineWinner(SkillElement playerElement, SkillElement enemyElement)
        {
            if (playerElement == enemyElement) return 0;

            // [New 260921] 특수 속성 (Fool) 판정:
            // Fool은 묵/찌/빠가 아니며 자동 패배 속성이므로, Fool vs Fool 경합 시에는 무승부(0) 처리.
            // Fool vs 일반 속성(Rock, Scissors, Paper)인 경우, Fool 측이 무조건 패배.
            bool isPlayerSpecial = playerElement == SkillElement.Fool;
            bool isEnemySpecial = enemyElement == SkillElement.Fool;

            if (isPlayerSpecial && isEnemySpecial)
            {
                return 0; // Fool vs Fool 무승부
            }
            if (isPlayerSpecial)
            {
                return 2; // 플레이어 Fool 패배 (적 일반 승리)
            }
            if (isEnemySpecial)
            {
                return 1; // 적 Fool 패배 (플레이어 일반 승리)
            }

            /* [Joker 기획 변경에 따라 임시 보류]
            if (playerElement == SkillElement.Joker && enemyElement == SkillElement.Joker)
            {
                return 0;
            }
            if (playerElement == SkillElement.Joker)
            {
                return 1; // 플레이어 조커 승리
            }
            if (enemyElement == SkillElement.Joker)
            {
                return 2; // 적 조커 승리
            }
            */

            if ((playerElement == SkillElement.Rock && enemyElement == SkillElement.Scissors) ||
                (playerElement == SkillElement.Scissors && enemyElement == SkillElement.Paper) ||
                (playerElement == SkillElement.Paper && enemyElement == SkillElement.Rock))
            {
                return 1; // 플레이어 승
            }

            return 2; // 적 승
        }

        public static float CalculateTotalBaseDamage(List<CardInstance> submittedCards)
        {
            if (submittedCards == null || submittedCards.Count == 0) return 0;

            float totalBaseDamage = 0;
            foreach (var card in submittedCards)
            {
                totalBaseDamage += card.GetFinalDamage();
            }
            return totalBaseDamage;
        }

        public static float CalculateDamage(BattleModel model, int winner)
        {
            var winnerCards = (winner == 1) ? model.playerSelectedCards : model.enemySelectedCards;
            if (winnerCards == null || winnerCards.Count == 0) return 0;

            // 기획: 승리한 측이 입히는 데미지는 사용된 카드들의 최종 데미지 합산
            // (기존 리미트 라운드 초과 시 1.5배 엔드게임 데미지 배율 기믹 삭제)
            float totalBaseDamage = CalculateTotalBaseDamage(winnerCards);

            var winnerModel = (winner == 1) ? model.player : model.enemy;
            var loserModel = (winner == 1) ? model.enemy : model.player;

            // Berserk 스킬 효과 적용: 승자의 공격력 계수 & 패자의 피격 계수
            float atkMult = winnerModel.isSpecialActive ? winnerModel.attackMultiplier : 1.0f;
            float defMult = loserModel.isSpecialActive ? loserModel.defenseMultiplier : 1.0f;

            float finalDamage = totalBaseDamage * atkMult * defMult;

            // [Chain] DamageUp 효과 반영
            var chainRow = TableManager.Instance.GetTable<ChainTable>().Get(winnerModel.tableData.chainId);
            bool isDamageUp = chainRow != null && chainRow.action.Equals("DamageUp", System.StringComparison.OrdinalIgnoreCase);

            if (isDamageUp && winnerCards.Count >= 2)
            {
                float[] chainValues = winnerCards.Count switch
                {
                    2 => chainRow.Chain2Values,
                    3 => chainRow.Chain3Values,
                    4 => chainRow.Chain4Values,
                    5 => chainRow.Chain5Values,
                    _ => null
                };

                if (chainValues != null && chainValues.Length > 0)
                {
                    float bonus = chainValues[0];
                    finalDamage += bonus;
                    Debug.Log($"[Chain] DamageUp Applied: +{bonus} Damage. Total: {finalDamage}");
                }
            }

            Debug.Log($"[Battle] Damage Calculated: {winnerCards.Count} Cards, BaseSum: {totalBaseDamage} -> {finalDamage} Damage");
            return finalDamage;
        }

        /// <summary>
        /// 이번 라운드에 제출한 카드로 획득할 총 게이지 양을 계산합니다.
        /// </summary>
        public static float CalculateTotalGauge(List<CardInstance> submittedCards)
        {
            if (submittedCards == null || submittedCards.Count == 0) return 0;

            float totalGauge = 0;
            foreach (var card in submittedCards)
            {
                totalGauge += card.GetFinalGauge();
            }
            return totalGauge;
        }

        /// <summary>
        /// 승리 시 체인 수에 따른 특수 효과(Fever, DrawDamage, Rain, DiceRoll 등)를 적용합니다.
        /// </summary>
        public static void ApplyChainEffects(BattleModel model, int winner, List<CardInstance> winnerCards)
        {
            if (winner == 0 || winnerCards == null || winnerCards.Count < 2) return;

            var winnerModel = (winner == 1) ? model.player : model.enemy;
            var loserModel = (winner == 1) ? model.enemy : model.player;

            int chainId = winnerModel.tableData.chainId;
            var chainRow = TableManager.Instance.GetTable<ChainTable>().Get(chainId);
            if (chainRow == null) return;

            int chainCount = winnerCards.Count;
            float[] statusIds = chainCount switch
            {
                2 => chainRow.Chain2Values,
                3 => chainRow.Chain3Values,
                4 => chainRow.Chain4Values,
                5 => chainRow.Chain5Values,
                _ => null
            };

            // 대소문자 구분 없이 액션 타입 체크
            string action = chainRow.action.ToLower();

            if (action == "drawdamage")
            {
                if (statusIds != null && statusIds.Length == 1 && statusIds[0] >= 10000)
                {
                    float remnantId = statusIds[0];
                    float probInc = 0;
                    int damageInc = 0;

                    if (remnantId == 10051) { probInc = 0.1f; damageInc = 1; }
                    else if (remnantId == 10052) { probInc = 0.2f; damageInc = 1; }
                    else if (remnantId == 10053) { probInc = 0.3f; damageInc = 2; }
                    else if (remnantId == 10054) { probInc = 0.5f; damageInc = 2; }

                    if (probInc > 0 || damageInc > 0)
                    {
                        winnerModel.UpdateDrawDamage(probInc, damageInc);
                    }
                }
                else if (statusIds != null && statusIds.Length >= 2)
                {
                    winnerModel.UpdateDrawDamage(statusIds[0], (int)statusIds[1]);
                }
            }
            else if (action == "fever")
            {
                // Fever: 체인 수(2~5)에 대응하는 첫 번째 값을 체인 게이지로 추가
                if (statusIds != null && statusIds.Length > 0)
                {
                    float gaugeValue = statusIds[0];
                    winnerModel.AddChainGauge(gaugeValue);
                    Debug.Log($"[Chain] Fever: Added {gaugeValue} to {winnerModel.tableData.id}'s Chain Gauge.");
                }
            }
            else if (action == "rain")
            {
                // [New 260921] Rain 체인 (코보): 승리 시 상대방 카드에 일정 확률로 Rain 효과 부여
                // ChainTable의 ChainValues (statusIds)에서 확률 추출
                float rainProb = (statusIds != null && statusIds.Length > 0) ? statusIds[0] : 0.5f;
                float roll = Random.value;
                if (roll <= rainProb)
                {
                    var uninfectedCards = loserModel.hand.Where(c => !c.IsRain).ToList();
                    if (uninfectedCards.Count > 0)
                    {
                        int targetIdx = Random.Range(0, uninfectedCards.Count);
                        var targetCard = uninfectedCards[targetIdx];

                        // 기존 카드를 삭제하지 않고, 대상 카드에 Rain 효과(상태)만 부여 (묵찌빠 속성 유지)
                        targetCard.ApplyRainEffect();

                        Debug.Log($"[Chain: Rain] Success (Roll: {roll:F2} <= Prob: {rainProb:F2})! Applied Rain effect to {loserModel.tableData.id}'s {targetCard.EffectiveElement} card.");
                    }
                    else
                    {
                        Debug.Log($"[Chain: Rain] Success but {loserModel.tableData.id} already has all cards as Rain.");
                    }
                }
                else
                {
                    Debug.Log($"[Chain: Rain] Failed (Roll: {roll:F2} > Prob: {rainProb:F2}).");
                }
            }
            else if (action == "diceroll")
            {
                // [New 260921 / 261011 기획] DiceRoll 체인 (후부키): 승리 시 2~5체인 수에 따라 float[12] 테이블 기반 주사위 롤
                // 각 눈금 i (1~6): [(i-1)*2] = 데미지/회복 수치, [(i-1)*2 + 1] = 확률
                int diceEye = 1;
                float effectValue = 0f;

                if (statusIds != null && statusIds.Length >= 12)
                {
                    // 1. 전체 가중치 합산
                    float totalWeight = 0f;
                    for (int i = 0; i < 6; i++)
                    {
                        totalWeight += Mathf.Max(0f, statusIds[i * 2 + 1]);
                    }

                    // 2. 가중치 롤
                    if (totalWeight > 0f)
                    {
                        float roll = UnityEngine.Random.value * totalWeight;
                        float accumulated = 0f;
                        for (int i = 0; i < 6; i++)
                        {
                            accumulated += Mathf.Max(0f, statusIds[i * 2 + 1]);
                            if (roll <= accumulated)
                            {
                                diceEye = i + 1;
                                break;
                            }
                        }
                    }
                    else
                    {
                        diceEye = UnityEngine.Random.Range(1, 7);
                    }

                    effectValue = statusIds[(diceEye - 1) * 2];
                }
                else
                {
                    // Fallback (데이터 미지정 시 1~6 균등 롤)
                    diceEye = UnityEngine.Random.Range(1, 7);
                    effectValue = diceEye;
                }

                winnerModel.lastDiceRoll.Value = diceEye;
                winnerModel.lastDiceRollValue.Value = effectValue;
                Debug.Log($"[Chain: DiceRoll] {winnerModel.tableData.id} rolled {diceEye} (Effect Value: {effectValue}) on Chain {chainCount}! (will resolve on RpsEnd)");
            }
        }

        /// <summary>
        /// 기획서의 NPC 패턴 플로우 및 캐릭터별 체인 전략에 기반하여 행동을 결정합니다.
        /// </summary>
        public static List<CardInstance> CalculateNPCMove(BattleModel model)
        {
            return BattleAI.CalculateNPCMove(model);
        }


        /// <summary>
        /// [260921 & 261011 기획] 후부키 DiceRoll 체인: 굴린 주사위 눈금 및 테이블 수치에 따라 상대에게 데미지 부여 및 자신 회복
        /// </summary>
        public static void ProcessDiceRollResult(BattleCharacterModel user, BattleCharacterModel opponent, int diceRoll, float effectValue = -1f)
        {
            if (diceRoll <= 0) return;

            // effectValue가 지정되었으면 해당 수치를 대미지 및 회복에 적용
            float damage = effectValue >= 0f ? effectValue : diceRoll;
            float heal = effectValue >= 0f ? effectValue : Mathf.CeilToInt(diceRoll * 0.5f);

            opponent.TakeDamage(damage);
            user.Heal(heal);

            Debug.Log($"[Chain: DiceRoll] {user.tableData.id} resolved Dice {diceRoll} (Value: {effectValue})! Dealt {damage} damage to {opponent.tableData.id}, healed {heal} HP.");
        }
    }
}
