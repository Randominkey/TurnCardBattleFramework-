using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using UnityEngine;

namespace Core.Table
{
    /// <summary>
    /// 질문(Question) 시점의 상대(NPC) 행동 상황 (총 4가지)
    /// </summary>
    public enum QuestionSituation
    {
        Rock = 0,       // 바위 낼 상황
        Scissors = 1,   // 가위 낼 상황
        Paper = 2,      // 보 낼 상황
        Draw = 3        // 드로우 할 상황 (Fool 등 예외 카드 포함)
    }

    /// <summary>
    /// 질문에 대한 상대 대답의 진위/성격 분류 (총 3가지)
    /// </summary>
    public enum QuestionAnswerType
    {
        Truth = 0,      // 진실 (기본 60%)
        Lie = 1,        // 거짓 (기본 38%)
        EasterEgg = 2   // 이스터에그 (기본 2%)
    }

    /// <summary>
    /// 플레이어의 질문 선택지 항목 데이터
    /// </summary>
    [Serializable]
    public struct QuestionChoiceData
    {
        public int questionIndex;
        public int stringId;
        public string questionText;

        public QuestionChoiceData(int index, int stringId, string questionText)
        {
            this.questionIndex = index;
            this.stringId = stringId;
            this.questionText = questionText;
        }
    }

    /// <summary>
    /// 질문 실행에 따른 상대 대답 결과 데이터
    /// </summary>
    [Serializable]
    public struct QuestionAnswerResult
    {
        public int questionIndex;
        public QuestionSituation situation;
        public QuestionAnswerType answerType;
        public int answerStringId;
        public string answerText;
    }

    /// <summary>
    /// QuestionTable.xml의 단일 행(Row) 스키마.
    /// StringTable ID(QuestionStringId, TruthStringId 등)와 직접 텍스트(QuestionText, TruthText 등)를 모두 지원합니다.
    /// </summary>
    [Serializable]
    public class QuestionTableRow : TableRow
    {
        [XmlElement("CharacterId")] public int characterId;
        [XmlElement("QuestionIndex")] public int questionIndex;
        [XmlElement("Situation")] public QuestionSituation situation;

        // StringTable ID 연동용 필드 (유효한 양수 ID가 있으면 우선 사용)
        [XmlElement("QuestionStringId")] public int questionStringId = -1;
        [XmlElement("TruthStringId")] public int truthStringId = -1;
        [XmlElement("LieStringId")] public int lieStringId = -1;
        [XmlElement("EasterEggStringId")] public int easterEggStringId = -1;

        // 더미 XML 직접 텍스트 지정용 필드
        [XmlElement("QuestionText")] public string questionText;
        [XmlElement("TruthText")] public string truthText;
        [XmlElement("LieText")] public string lieText;
        [XmlElement("EasterEggText")] public string easterEggText;

        // 확률 설정 (기본 60% / 38% / 2%)
        [XmlElement("TruthProb")] public float truthProb = 0.60f;
        [XmlElement("LieProb")] public float lieProb = 0.38f;
        [XmlElement("EasterEggProb")] public float easterEggProb = 0.02f;
    }

    [Serializable]
    [XmlRoot("QuestionTable")]
    public class QuestionTable : TableBase<QuestionTable, QuestionTableRow>
    {
        protected override string fileName => "QuestionTable";
    }

    /// <summary>
    /// QuestionTable.xml에서 질문 목록, 확률(60/38/2), 캐릭터별(질문4 x 상황4 x 대답3 = 48개) 대답을 조회하는 프로바이더.
    /// 스크립트 내부에는 특정 IP 표현을 포함하지 않으며, 모든 캐릭터 말투 및 데모 대사는 XML 데이터로 분리되어 제공됩니다.
    /// </summary>
    public static class QuestionDataProvider
    {
        public const float DEFAULT_TRUTH_PROB = 0.60f;
        public const float DEFAULT_LIE_PROB = 0.38f;
        public const float DEFAULT_EASTER_EGG_PROB = 0.02f;

        // XML 미존재 시에만 사용되는 최소한의 IP 중립 기본 질문
        private static readonly List<QuestionChoiceData> fallbackQuestions = new List<QuestionChoiceData>
        {
            new QuestionChoiceData(0, -1, "이번 라운드에는 어떤 카드를 낼 생각이야?"),
            new QuestionChoiceData(1, -1, "네 손패의 주력 속성이 무엇인지 이미 읽었어."),
            new QuestionChoiceData(2, -1, "잠깐, 이번 턴에는 카드를 뽑으며 숨을 고를 셈인가?"),
            new QuestionChoiceData(3, -1, "피하지 말고 정면 승부로 부딪쳐 보자!")
        };

        private static bool IsXmlTableAvailable()
        {
            var asset = Core.Utils.ResourceUtil.AssetProvider.LoadXmlData("QuestionTable");
            return asset != null;
        }

        /// <summary>
        /// 플레이어의 질문 선택지 목록을 반환합니다. (현재 기본 4개, 테이블 설정 시 가변 개수 대응)
        /// </summary>
        public static IReadOnlyList<QuestionChoiceData> GetQuestions()
        {
            if (IsXmlTableAvailable())
            {
                var table = TableManager.Instance.GetTable<QuestionTable>();
                if (table != null && table.rows != null && table.rows.Count > 0)
                {
                    var distinctQuestions = table.rows
                        .GroupBy(r => r.questionIndex)
                        .OrderBy(g => g.Key)
                        .Select(g =>
                        {
                            var first = g.First();
                            string text = !string.IsNullOrEmpty(first.questionText)
                                ? first.questionText
                                : (g.Key >= 0 && g.Key < fallbackQuestions.Count ? fallbackQuestions[g.Key].questionText : $"질문 선택지 {g.Key + 1}");
                            return new QuestionChoiceData(g.Key, first.questionStringId, text);
                        })
                        .ToList();

                    if (distinctQuestions.Count > 0)
                        return distinctQuestions;
                }
            }

            return fallbackQuestions;
        }

        /// <summary>
        /// 진실(60%) / 거짓(38%) / 이스터에그(2%) 확률 설정값을 반환합니다.
        /// </summary>
        public static (float truthProb, float lieProb, float easterEggProb) GetProbabilities()
        {
            if (IsXmlTableAvailable())
            {
                var table = TableManager.Instance.GetTable<QuestionTable>();
                if (table != null && table.rows != null && table.rows.Count > 0)
                {
                    var first = table.rows[0];
                    float sum = first.truthProb + first.lieProb + first.easterEggProb;
                    if (sum > 0.001f)
                    {
                        return (first.truthProb / sum, first.lieProb / sum, first.easterEggProb / sum);
                    }
                }
            }

            return (DEFAULT_TRUTH_PROB, DEFAULT_LIE_PROB, DEFAULT_EASTER_EGG_PROB);
        }

        /// <summary>
        /// 0.0 ~ 1.0 사이의 난수를 받아 확률 테이블(기본 60% / 38% / 2%)에 따라 대답 타입을 결정합니다.
        /// </summary>
        public static QuestionAnswerType RollAnswerType(float randomValue01)
        {
            var (truthProb, lieProb, _) = GetProbabilities();
            if (randomValue01 < truthProb)
            {
                return QuestionAnswerType.Truth;
            }
            if (randomValue01 < truthProb + lieProb)
            {
                return QuestionAnswerType.Lie;
            }
            return QuestionAnswerType.EasterEgg;
        }

        /// <summary>
        /// 상대 캐릭터 ID, 질문 번호, 현재 NPC 상황, 결정된 대답 타입(진실/거짓/이스터에그)에 맞는 대답 결과를 반환합니다.
        /// </summary>
        public static QuestionAnswerResult GetAnswer(
            int enemyCharacterId,
            int questionIndex,
            QuestionSituation situation,
            QuestionAnswerType answerType)
        {
            if (IsXmlTableAvailable())
            {
                var table = TableManager.Instance.GetTable<QuestionTable>();
                if (table != null && table.rows != null && table.rows.Count > 0)
                {
                    // 1순위: 해당 캐릭터 ID와 정확히 일치하는 행 검색
                    var row = table.rows.FirstOrDefault(r =>
                        r.characterId == enemyCharacterId &&
                        r.questionIndex == questionIndex &&
                        r.situation == situation);

                    // 2순위: 캐릭터 ID가 매칭되지 않을 경우 동일 질문/상황의 첫 번째 행으로 폴백
                    if (row == null)
                    {
                        row = table.rows.FirstOrDefault(r =>
                            r.questionIndex == questionIndex &&
                            r.situation == situation);
                    }

                    if (row != null)
                    {
                        int targetStringId = answerType switch
                        {
                            QuestionAnswerType.Truth => row.truthStringId,
                            QuestionAnswerType.Lie => row.lieStringId,
                            QuestionAnswerType.EasterEgg => row.easterEggStringId,
                            _ => row.truthStringId
                        };

                        string targetText = answerType switch
                        {
                            QuestionAnswerType.Truth => row.truthText,
                            QuestionAnswerType.Lie => row.lieText,
                            QuestionAnswerType.EasterEgg => row.easterEggText,
                            _ => row.truthText
                        };

                        if (string.IsNullOrEmpty(targetText))
                        {
                            targetText = BuildNeutralFallbackAnswer(questionIndex, situation, answerType);
                        }

                        return new QuestionAnswerResult
                        {
                            questionIndex = questionIndex,
                            situation = situation,
                            answerType = answerType,
                            answerStringId = targetStringId,
                            answerText = targetText
                        };
                    }
                }
            }

            return new QuestionAnswerResult
            {
                questionIndex = questionIndex,
                situation = situation,
                answerType = answerType,
                answerStringId = -1,
                answerText = BuildNeutralFallbackAnswer(questionIndex, situation, answerType)
            };
        }

        private static string BuildNeutralFallbackAnswer(int questionIndex, QuestionSituation situation, QuestionAnswerType answerType)
        {
            if (answerType == QuestionAnswerType.EasterEgg)
            {
                return "심리전에 말려들 생각은 없다. 직접 부딪쳐서 확인해 봐라!";
            }

            QuestionSituation implied = situation;
            if (answerType == QuestionAnswerType.Lie)
            {
                int offset = (questionIndex % 3) + 1;
                implied = (QuestionSituation)(((int)situation + offset) % 4);
            }

            return implied switch
            {
                QuestionSituation.Rock => "이번에는 묵직한 바위 공격으로 정면 돌파하겠다.",
                QuestionSituation.Scissors => "이번에는 예리한 가위 공격으로 승부를 보겠다.",
                QuestionSituation.Paper => "이번에는 보 속성 카드로 너의 수를 제압하겠다.",
                _ => "이번 라운드에는 카드를 한 장 드로우하며 다음 수를 준비하겠다."
            };
        }
    }
}

