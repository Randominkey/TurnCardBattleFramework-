using Core.Table;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Core.Battle
{
    /// <summary>
    /// 기획서의 NPC 패턴 플로우(가중치 선택, 드로우 판단, 체인 전략)를 계산하는 순수 인공지능 모듈
    /// </summary>
    public static class BattleAI
    {
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

            // 5. 타입 선택
            SkillElement selectedElement = (SkillElement)GetWeightedRandomIndex(correctValue);
            var availableCards = npcHand.Where(c => c.EffectiveElement == selectedElement && !c.IsFool).ToList();

            if (availableCards.Count == 0)
            {
                // 선호 타입이 없으면 다른 일반 카드 탐색
                availableCards = npcHand.Where(c => !c.IsFool && (c.EffectiveElement == SkillElement.Rock || c.EffectiveElement == SkillElement.Scissors || c.EffectiveElement == SkillElement.Paper)).ToList();
            }

            if (availableCards.Count == 0)
            {
                // Fool 카드만 남아있는 경우
                availableCards = npcHand.ToList();
            }

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
                if (card.EffectiveElement == SkillElement.Rock) counts[0]++;
                else if (card.EffectiveElement == SkillElement.Scissors) counts[1]++;
                else if (card.EffectiveElement == SkillElement.Paper) counts[2]++;
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

