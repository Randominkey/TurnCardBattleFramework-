using UnityEngine;
using System.Collections.Generic;
using Core.Battle;
using Core.Table;
using Core.Flow.SpecialSkill;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Core.Tests
{
    /// <summary>
    /// 순수 배틀 도메인 로직(가위바위보 상성, Fool 특수 속성, 스킬 판정)을 자동 검증하는 단위 테스트 러너.
    /// Unity 상단 메뉴 [Tools > Run Battle Domain Tests] 또는 DevBootstrapper 인스펙터에서 즉시 실행 가능합니다.
    /// </summary>
    public static class BattleRuleTests
    {
#if UNITY_EDITOR
        [MenuItem("Tools/Run Battle Domain Tests")]
#endif
        public static bool RunAllTests()
        {
            int passCount = 0;
            int totalCount = 0;

            void AssertEqual<T>(T expected, T actual, string testName)
            {
                totalCount++;
                if (EqualityComparer<T>.Default.Equals(expected, actual))
                {
                    passCount++;
                }
                else
                {
                    Debug.LogError($"[TEST FAIL] {testName} - Expected: {expected}, Actual: {actual}");
                }
            }

            Debug.Log("[Battle Tests] Starting Domain Unit Tests...");

            // --- 1. 가위바위보 기본 상성 판정 (9종) ---
            AssertEqual(1, BattleLogic.DetermineWinner(SkillElement.Rock, SkillElement.Scissors), "Rock beats Scissors (Player Win)");
            AssertEqual(1, BattleLogic.DetermineWinner(SkillElement.Scissors, SkillElement.Paper), "Scissors beats Paper (Player Win)");
            AssertEqual(1, BattleLogic.DetermineWinner(SkillElement.Paper, SkillElement.Rock), "Paper beats Rock (Player Win)");
            AssertEqual(2, BattleLogic.DetermineWinner(SkillElement.Scissors, SkillElement.Rock), "Scissors loses to Rock (Enemy Win)");
            AssertEqual(2, BattleLogic.DetermineWinner(SkillElement.Paper, SkillElement.Scissors), "Paper loses to Scissors (Enemy Win)");
            AssertEqual(2, BattleLogic.DetermineWinner(SkillElement.Rock, SkillElement.Paper), "Rock loses to Paper (Enemy Win)");
            AssertEqual(0, BattleLogic.DetermineWinner(SkillElement.Rock, SkillElement.Rock), "Rock vs Rock is Tie");
            AssertEqual(0, BattleLogic.DetermineWinner(SkillElement.Scissors, SkillElement.Scissors), "Scissors vs Scissors is Tie");
            AssertEqual(0, BattleLogic.DetermineWinner(SkillElement.Paper, SkillElement.Paper), "Paper vs Paper is Tie");

            // --- 2. Fool 특수 속성 자동 패배/무승부 규칙 (7종) ---
            AssertEqual(2, BattleLogic.DetermineWinner(SkillElement.Fool, SkillElement.Rock), "Player Fool loses to Rock");
            AssertEqual(2, BattleLogic.DetermineWinner(SkillElement.Fool, SkillElement.Scissors), "Player Fool loses to Scissors");
            AssertEqual(2, BattleLogic.DetermineWinner(SkillElement.Fool, SkillElement.Paper), "Player Fool loses to Paper");
            AssertEqual(1, BattleLogic.DetermineWinner(SkillElement.Rock, SkillElement.Fool), "Enemy Fool loses to Rock");
            AssertEqual(1, BattleLogic.DetermineWinner(SkillElement.Scissors, SkillElement.Fool), "Enemy Fool loses to Scissors");
            AssertEqual(1, BattleLogic.DetermineWinner(SkillElement.Paper, SkillElement.Fool), "Enemy Fool loses to Paper");
            AssertEqual(0, BattleLogic.DetermineWinner(SkillElement.Fool, SkillElement.Fool), "Fool vs Fool is Tie");

            // --- 3. 스페셜 스킬 격돌 조건 판정 (LoseHeal, Berserk) ---
            var dummyCharData = new BattleCharacterTableRow { id = 101, maxHp = 100, specialSkillGauge = 100 };
            var dummyBattleRow = new BattleTableRow();
            var battleModel = new BattleModel(dummyBattleRow, dummyCharData, dummyCharData);
            var requireSkillRow = new SkillTableRow { id = 101, element = SkillElement.Require, action = "skill" };
            
            // 플레이어(User = player) 시점의 컨텍스트 생성
            var playerContext = new SpecialSkillContext(battleModel, null, null, battleModel.player, requireSkillRow);

            // LoseHeal: 패배(winnerIndex = 2) 시 성공 반전 검증
            var loseHeal = new LoseHealSkillAction();
            AssertEqual(true, loseHeal.CheckSuccess(playerContext, 2), "LoseHeal on Defeat (winnerIndex=2) should succeed");
            AssertEqual(false, loseHeal.CheckSuccess(playerContext, 1), "LoseHeal on Win (winnerIndex=1) should fail");
            AssertEqual(false, loseHeal.CheckSuccess(playerContext, 0), "LoseHeal on Tie (winnerIndex=0) should fail");

            // Berserk: 기본 성공 판정 (시전자 승리 시 true, 패배 시 false)
            var berserk = new BerserkSkillAction();
            AssertEqual(true, berserk.CheckSuccess(playerContext, 1), "Berserk on Win (winnerIndex=1) should succeed");
            AssertEqual(false, berserk.CheckSuccess(playerContext, 2), "Berserk on Defeat (winnerIndex=2) should fail");

            bool allPassed = passCount == totalCount;
            if (allPassed)
            {
                Debug.Log($"<color=#44FF44><b>[Battle Tests ALL PASSED]</b></color> Successfully verified {passCount}/{totalCount} rules without errors.");
            }
            else
            {
                Debug.LogError($"<color=#FF4444><b>[Battle Tests FAILED]</b></color> Passed {passCount}/{totalCount} tests.");
            }

            return allPassed;
        }
    }
}
