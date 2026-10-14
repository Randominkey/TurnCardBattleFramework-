using UnityEngine;
using Core.Battle;
using Core.Table;
using System.Collections.Generic;
using System.Linq;

namespace Core.Tutorial
{
    /// <summary>
    /// 이로하 튜토리얼 배틀 전용 스크립트 컨트롤러.
    /// TutorialScenario.html의 1~3라운드 핸드 상태, 확정 드로우 및 카운터 패 제출을 제어합니다.
    /// </summary>
    public class TutorialBattleScriptController : MonoBehaviour
    {
        [Header("Tutorial Battle Settings")]
        [Tooltip("튜토리얼 강제 배틀 스크립트를 활성화할지 여부")]
        [SerializeField] private bool enableTutorialOverride = true;

        [Tooltip("튜토리얼을 적용할 배틀 ID (이로하 전투: 1002)")]
        [SerializeField] private int targetBattleId = 1002;

        public bool IsEnabled => enableTutorialOverride;

        /// <summary>
        /// 현재 배틀이 튜토리얼 스크립트 적용 대상인지 확인합니다.
        /// </summary>
        public bool IsApplicable(int battleId, int currentRound)
        {
            if (!enableTutorialOverride) return false;
            if (battleId != targetBattleId) return false;
            // 1R, 2R, 3R 동안만 스크립트 오버라이드 적용 (4R부터는 자유 실전 배틀)
            return currentRound >= 1 && currentRound <= 3;
        }

        /// <summary>
        /// 1라운드 시작 시 유저와 NPC의 초기 핸드를 2장/2장/2장 (R2, S2, P2)으로 고정 설정합니다.
        /// </summary>
        public bool SetupInitialHands(BattleCharacterModel player, BattleCharacterModel enemy)
        {
            if (!enableTutorialOverride) return false;

            player.hand.Clear();
            enemy.hand.Clear();

            AddCardsByElement(player, SkillElement.Rock, 2);
            AddCardsByElement(player, SkillElement.Scissors, 2);
            AddCardsByElement(player, SkillElement.Paper, 2);

            AddCardsByElement(enemy, SkillElement.Rock, 2);
            AddCardsByElement(enemy, SkillElement.Scissors, 2);
            AddCardsByElement(enemy, SkillElement.Paper, 2);

            Debug.Log("[TutorialScript] Initial Hands Setup Complete: Player(R2 S2 P2), Enemy(R2 S2 P2)");
            return true;
        }

        /// <summary>
        /// 라운드 전환 시 시나리오에 정의된 확정 드로우 카드를 반환합니다.
        /// 대상이 아니면 null을 반환하여 기본 랜덤 드로우를 수행하도록 합니다.
        /// </summary>
        public CardInstance GetScriptedDrawCard(bool isPlayer, int nextRound, SkillElement playerFirstRoundElement = SkillElement.Rock)
        {
            if (!enableTutorialOverride) return null;

            SkillElement targetElement;

            if (nextRound == 2)
            {
                // [시나리오 1R -> 2R 드로우]
                // 유저는 1R 선택(R)에 지는 카드(S) 1장 드로우 -> R0 S3 P2 완성
                // NPC는 1R 선택(R)을 이기는 카드(P) 1장 드로우 -> R2 S0 P3 완성
                if (isPlayer)
                {
                    targetElement = GetLosingElement(playerFirstRoundElement); // 기본: Scissors
                }
                else
                {
                    targetElement = GetWinningElement(playerFirstRoundElement); // 기본: Paper
                }
            }
            else if (nextRound == 3)
            {
                // [시나리오 2R -> 3R 드로우]
                // 유저는 첫 선택 카드와 동일한 (R) 1장 드로우 -> R1 S0 P2 완성
                // NPC는 지는 카드 (S) 1장 드로우 -> R0 S1 P3 완성
                if (isPlayer)
                {
                    targetElement = playerFirstRoundElement; // 기본: Rock
                }
                else
                {
                    targetElement = GetLosingElement(playerFirstRoundElement); // 기본: Scissors
                }
            }
            else
            {
                // 4R 이후는 일반 랜덤 드로우
                return null;
            }

            var card = CreateCardByElement(targetElement);
            if (card != null)
            {
                Debug.Log($"[TutorialScript] Scripted Draw for {(isPlayer ? "Player" : "NPC")} (R{nextRound}): {targetElement}");
            }
            return card;
        }

        /// <summary>
        /// 플레이어가 카드를 냈을 때, 시나리오에 정의된 NPC의 대응패를 반환합니다.
        /// 4라운드 이후이거나 비활성화 시 null을 반환하여 일반 AI가 동작하도록 합니다.
        /// </summary>
        public List<CardInstance> GetScriptedNPCMove(BattleModel model)
        {
            if (!enableTutorialOverride) return null;

            int round = model.currentRound.Value;
            if (round > 3) return null;

            var npcHand = model.enemy.hand;
            var playerCards = model.playerSelectedCards;
            if (playerCards == null || playerCards.Count == 0) return null;

            SkillElement playerElement = playerCards[0].TableData.element;
            List<CardInstance> npcCards = new List<CardInstance>();

            if (round == 1)
            {
                // [1R]: 유저 R2 vs NPC S2 (무조건 플레이어 승리)
                // NPC 핸드에서 플레이어가 낸 것에 지는 속성 2장 선택
                SkillElement loseElement = GetLosingElement(playerElement);
                npcCards = npcHand.Where(c => c.TableData.element == loseElement).Take(2).ToList();
            }
            else if (round == 2)
            {
                // [2R]: 유저 S3 vs NPC R2 (무조건 NPC 승리)
                // NPC 핸드에서 플레이어가 낸 것을 이기는 속성 2장 선택
                SkillElement winElement = GetWinningElement(playerElement);
                npcCards = npcHand.Where(c => c.TableData.element == winElement).Take(2).ToList();
            }
            else if (round == 3)
            {
                // [3R]: 유저 1장(R 또는 P) vs NPC 1장(P 또는 S) (무조건 NPC 승리)
                // NPC 핸드에서 플레이어가 낸 것을 이기는 속성 1장 선택
                SkillElement winElement = GetWinningElement(playerElement);
                npcCards = npcHand.Where(c => c.TableData.element == winElement).Take(1).ToList();
            }

            if (npcCards.Count > 0)
            {
                Debug.Log($"[TutorialScript] R{round} Scripted NPC Move: {npcCards.Count}x {npcCards[0].TableData.element} vs Player {playerCards.Count}x {playerElement}");
                return npcCards;
            }

            // 만약 핸드에서 찾지 못했을 경우 null 반환 (Fallback)
            return null;
        }

        private SkillElement GetWinningElement(SkillElement element)
        {
            return element switch
            {
                SkillElement.Rock => SkillElement.Paper,
                SkillElement.Scissors => SkillElement.Rock,
                SkillElement.Paper => SkillElement.Scissors,
                _ => SkillElement.Paper
            };
        }

        private SkillElement GetLosingElement(SkillElement element)
        {
            return element switch
            {
                SkillElement.Rock => SkillElement.Scissors,
                SkillElement.Scissors => SkillElement.Paper,
                SkillElement.Paper => SkillElement.Rock,
                _ => SkillElement.Scissors
            };
        }

        private void AddCardsByElement(BattleCharacterModel character, SkillElement element, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var card = CreateCardByElement(element, character.tableData.SkillIds);
                if (card != null) character.hand.Add(card);
            }
        }

        private CardInstance CreateCardByElement(SkillElement element, int[] candidateSkillIds = null)
        {
            var skillTable = TableManager.Instance.GetTable<SkillTable>();
            if (skillTable == null) return null;

            // 캐릭터가 가진 스킬 ID 후보군에서 해당 속성을 우선 검색
            if (candidateSkillIds != null)
            {
                foreach (var id in candidateSkillIds)
                {
                    var row = skillTable.Get(id);
                    if (row != null && row.element == element)
                    {
                        return new CardInstance(row);
                    }
                }
            }

            // 없으면 테이블 전체에서 해당 속성 기본 스킬(Attack 등) 검색
            foreach (var row in skillTable.rows)
            {
                if (row.element == element && row.action.Equals("Attack", System.StringComparison.OrdinalIgnoreCase))
                {
                    return new CardInstance(row);
                }
            }

            // 그래도 없으면 단순 첫 번째 매칭
            var fallback = skillTable.rows.FirstOrDefault(r => r.element == element);
            return fallback != null ? new CardInstance(fallback) : null;
        }
    }
}
