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

        public static float CalculateDamage(BattleModel model, int winner, bool isRoundFever = false)
        {
            var winnerCards = (winner == 1) ? model.playerSelectedCards : model.enemySelectedCards;
            if (winnerCards == null || winnerCards.Count == 0) return 0;

            var gameSet = TableManager.Instance.GetTable<GameSetTable>().Get(0);
            float feverMult = isRoundFever ? (gameSet?.endGameDamageValue ?? 2f) : 1f;

            // 기획: 승리한 측이 입히는 데미지는 사용된 카드들의 최종 데미지 합산
            float totalBaseDamage = CalculateTotalBaseDamage(winnerCards);

            var winnerModel = (winner == 1) ? model.player : model.enemy;
            var loserModel = (winner == 1) ? model.enemy : model.player;

            // Berserk 스킬 효과 적용: 승자의 공격력 계수 & 패자의 피격 계수
            float atkMult = winnerModel.isSpecialActive ? winnerModel.attackMultiplier : 1.0f;
            float defMult = loserModel.isSpecialActive ? loserModel.defenseMultiplier : 1.0f;

            float finalDamage = totalBaseDamage * feverMult * atkMult * defMult;

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

            Debug.Log($"[Battle] Damage Calculated: {winnerCards.Count} Cards, BaseSum: {totalBaseDamage}, RoundFever: {isRoundFever} -> {finalDamage} Damage");
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
        /// 승리 시 체인 수에 따른 특수 효과(WeakNess, DrawDamage, Fever 등)를 적용합니다.
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

            if (statusIds == null || statusIds.Length == 0) return;

            // 대소문자 구분 없이 액션 타입 체크
            string action = chainRow.action.ToLower();

            if (action == "drawdamage")
            {
                // [New] DrawDamage 로직: 확률(prob)과 데미지(value)가 누적됨
                // statusIds가 remnant(10051 등)인 경우 하드코딩된 값으로 대응
                // 만약 float[] 자료형으로 제대로 들어온다면 ([0]:prob, [1]:damage) 로 처리
                
                if (statusIds.Length == 1 && statusIds[0] >= 10000)
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
                else if (statusIds.Length >= 2)
                {
                    winnerModel.UpdateDrawDamage(statusIds[0], (int)statusIds[1]);
                }
            }
            else if (action == "fever")
            {
                // Fever: 체인 수(2~5)에 대응하는 첫 번째 값을 체인 게이지로 추가
                float gaugeValue = statusIds[0];
                winnerModel.AddChainGauge(gaugeValue);
                Debug.Log($"[Chain] Fever: Added {gaugeValue} to {winnerModel.tableData.id}'s Chain Gauge.");
            }
        }

        /// <summary>
        /// 기획서의 NPC 패턴 플로우 및 캐릭터별 체인 전략에 기반하여 행동을 결정합니다.
        /// </summary>
        public static List<CardInstance> CalculateNPCMove(BattleModel model)
        {
            var npcHand = model.enemy.hand;
            var pcHand = model.player.hand;
            var gameSet = TableManager.Instance.GetTable<GameSetTable>().Get(0);
            int maxCards = gameSet?.maxCards ?? 8;
            float maxCardsFloat = maxCards;

            // [New] 피버 모드 체크: 피버 중에는 드로우 금지 및 무조건 카드 1장 선택
            bool isFever = model.player.isFeverModeActive.Value || model.enemy.isFeverModeActive.Value;

            if (npcHand.Count == 0)
            {
                if (isFever) Debug.LogError("[Battle] NPC has NO cards during Fever! This should not happen.");
                model.enemyChoseDraw = true;
                return new List<CardInstance>();
            }

            // 1. 특수 스킬 체크 (생략 없이 추후 구현을 위한 자리 확보 - 현재는 통과)

            // 2. 드로우 버튼 사용 조건 체크 (피버 모드 아닐 때만)
            if (!isFever && npcHand.Count <= maxCards - 2)
            {
                if (npcHand.Count <= Mathf.CeilToInt(maxCardsFloat / 2) ||
                    pcHand.Count <= Mathf.CeilToInt(maxCardsFloat / 3))
                {
                    model.enemyChoseDraw = true;
                    return new List<CardInstance>();
                }
                else
                {
                    float npcHpRatio = model.enemy.hp.Value / model.enemy.tableData.maxHp;
                    float pcHpRatio = model.player.hp.Value / model.player.tableData.maxHp;

                    // 조건 A: NPC 체력이 PC보다 30% 이상 높고 NPC 체력이 10 이상일 때
                    bool hpAdvantage = (npcHpRatio - pcHpRatio) >= 0.3f && model.enemy.hp.Value >= 10;
                    // 조건 B: NPC 손패가 절반 이하이고 PC 손패가 1/3 이하일 때
                    bool bothLowHand = npcHand.Count <= (maxCards / 2) && pcHand.Count <= (maxCards / 3);

                    if (hpAdvantage || bothLowHand)
                    {
                        model.enemyChoseDraw = true;
                        return new List<CardInstance>();
                    }
                }
            }

            // 3. 카드 타입별 통계 계산
            int[] npcCounts = GetElementCounts(npcHand);
            
            // [Fix] 플레이어의 정보는 핸드 + 트레이(Selected)를 합산해서 인지하도록 수정
            var fullPlayerCards = pcHand.Concat(model.playerSelectedCards);
            int[] pcCounts = GetElementCounts(fullPlayerCards);
            
            float npcTotal = npcHand.Count;
            float pcTotal = fullPlayerCards.Count() > 0 ? fullPlayerCards.Count() : 1f;

            float[] npcDePercent = npcCounts.Select(c => c / npcTotal).ToArray();
            float[] pcDePercent = pcCounts.Select(c => c / pcTotal).ToArray();

            // 3.5 예외사항 체크
            // 현재는 폐기된 기획인 "Upgrade"의 잔재일 가능성이 큼. 기획 최신화 필요하니 기획자에게 요구할 부분으로 기록해둘 것.

            // 4. PON/Elite 가중치 계산 (NPC의 현재 HP 상태에 따라 가중치 인덱스 선택)
            int weightIdx = (int)model.enemy.hpState.Value;
            float pon = model.battleTable.PONValues.Length > weightIdx ? model.battleTable.PONValues[weightIdx] : 0.5f;
            float elite = model.battleTable.EliteValues.Length > weightIdx ? model.battleTable.EliteValues[weightIdx] : 0.5f;
            float totalWeight = pon + elite;

            if (totalWeight == 0)
            {
                pon = 0.5f;
                elite = 0.5f;
                totalWeight = 1f;
            }

            float[] correctValue = new float[3];
            float[] drawValue = new float[3];
            float[] loseValue = new float[3];
            float[] winValue = new float[3];

            // 0: 바위, 1: 가위, 2: 보 (묵찌빠)
            // 기획: pon은 잘못된 선택(패배)을 할 가중치, elite는 올바른 선택(승리)을 할 가중치

            for (int i = 0; i < 3; i++)
            {
                drawValue[i] = npcDePercent[i] * pon / totalWeight * pcDePercent[i] * 0.2f;
                loseValue[i] = npcDePercent[i] * pon / totalWeight * pcDePercent[(i + 2) % 3] * 0.8f; // 플레이어가 내는 것에 지는 가중치
                winValue[i] = npcDePercent[i] * elite / totalWeight * pcDePercent[(i + 1) % 3] * 0.8f; // 플레이어가 내는 것을 이기는 가중치
                correctValue[i] = drawValue[i] + loseValue[i] + winValue[i];
            }

            //float[] selectWeights = new float[3];
            //for (int i = 0; i < 3; i++) // 0:Rock, 1:Scissors, 2:Paper
            //{
            //    // PON 로직: 플레이어가 낼 확률이 높은 것을 이기는 패 선호 (R->S, S->P, P->R)
            //    float ponCorrect = pcDePercent[(i + 1) % 3] * 0.8f + pcDePercent[i] * 0.2f;

            //    // Elite 로직: 플레이어가 내는 패에 지는 패(심리전) 선호
            //    float eliteCorrect = pcDePercent[(i + 2) % 3];

            //    selectWeights[i] = (npcDePercent[i] * (pon / totalWeight) * ponCorrect)
            //                     + (npcDePercent[i] * (elite / totalWeight) * eliteCorrect);
            //}

            // 5. 타입 선택
            SkillElement selectedElement = (SkillElement)GetWeightedRandomIndex(correctValue);
            var availableCards = npcHand.Where(c => c.TableData.element == selectedElement).ToList();

            if (availableCards.Count == 0)
            {
                model.enemyChoseDraw = true;
                return new List<CardInstance>();
            }

            // 6. 체인 수 결정 (캐릭터별 ChainTable 액션 타입에 따라 분기)
            int finalChainCount = 1;
            
            // 피버 모드 시 강제로 1장만 제출
            if (model.player.isFeverModeActive.Value || model.enemy.isFeverModeActive.Value)
            {
                finalChainCount = 1;
            }
            else
            {
                int chainId = model.enemy.tableData.chainId;
                var chainRow = TableManager.Instance.GetTable<ChainTable>().Get(chainId);
                string actionType = chainRow?.action ?? "DrawDamage";

                int availableCount = availableCards.Count;

                if (actionType == "DrawDamage")
                {
                    if (availableCount >= 5) finalChainCount = 5;
                    else if (availableCount == 3 || availableCount == 4)
                    {
                        // 보유 갯수 그대로 선택
                        finalChainCount = availableCount;
                    }
                    else finalChainCount = availableCount;
                }
                else if (actionType == "DamageUp")
                {
                    // DamageUp 타입은 3장 이상일 때 공격적인 최대 체인 선호
                    if (availableCount >= 3) finalChainCount = Mathf.Min(availableCount, 5);
                    else finalChainCount = availableCount;
                }
                else
                {
                    finalChainCount = Mathf.Min(availableCount, 5);
                }
            }

            return availableCards.Take(finalChainCount).ToList();
        }

        private static int[] GetElementCounts(IEnumerable<CardInstance> hand)
        {
            int[] counts = new int[3];
            foreach (var card in hand)
            {
                if (card.TableData.element == SkillElement.Rock) counts[0]++;
                else if (card.TableData.element == SkillElement.Scissors) counts[1]++;
                else if (card.TableData.element == SkillElement.Paper) counts[2]++;
            }
            return counts;
        }

        private static int GetWeightedRandomIndex(float[] weights)
        {
            float sum = weights.Sum();
            if (sum <= 0) return Random.Range(0, 3);

            float r = Random.value * sum;
            float tempSum = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                tempSum += weights[i];
                if (r <= tempSum) return i;
            }
            return 0;
        }
    }
}
