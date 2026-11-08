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
        Draw = 3        // 드로우 할 상황 (카드 뽑기 행동 또는 Fool 등 공격불가 카드 포함)
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
    /// 기획 공식 스키마: Id, DevNote, QuestionStringId, AnswerStringIds, LieStringIds, EasterEggStringId, AnswerProp
    /// 테이블 로드 시점에 1회 파싱 및 유효성 검증을 거쳐 캐시됩니다.
    /// </summary>
    [Serializable]
    public class QuestionTableRow : TableRow
    {
        [XmlElement("DevNote")] public string devNote;
        [XmlElement("QuestionStringId")] public int questionStringId = -1;
        [XmlElement("AnswerStringIds")] public string answerStringIdsRaw;
        [XmlElement("LieStringIds")] public string lieStringIdsRaw;
        [XmlElement("EasterEggStringId")] public int easterEggStringId = -1;
        [XmlElement("AnswerProp")] public string answerPropRaw;

        private int[] cachedAnswerStringIds;
        private int[] cachedLieStringIds;
        private float[] cachedAnswerProp;
        private float[] normalizedProbabilities;

        public int[] AnswerStringIds => cachedAnswerStringIds;
        public int[] LieStringIds => cachedLieStringIds;
        public float[] AnswerProp => cachedAnswerProp;
        public float[] NormalizedProbabilities => normalizedProbabilities;

        public int CharacterId => id / 100;
        public int QuestionIndex => (id % 100) - 1;

        public void ParseAndValidate(int rowIndex)
        {
            // 1. AnswerStringIds (정확히 4개: Rock, Scissors, Paper, Draw)
            cachedAnswerStringIds = TableUtil.ParseIntArray(answerStringIdsRaw);
            if (cachedAnswerStringIds == null || cachedAnswerStringIds.Length != 4)
            {
                Debug.LogError($"[QuestionTable] Row Id {id} (Index {rowIndex}): AnswerStringIds must contain exactly 4 IDs (Rock, Scissors, Paper, Draw)! Actual: {cachedAnswerStringIds?.Length ?? 0}");
            }

            // 2. LieStringIds (정확히 4개: Rock, Scissors, Paper, Draw)
            cachedLieStringIds = TableUtil.ParseIntArray(lieStringIdsRaw);
            if (cachedLieStringIds == null || cachedLieStringIds.Length != 4)
            {
                Debug.LogError($"[QuestionTable] Row Id {id} (Index {rowIndex}): LieStringIds must contain exactly 4 IDs (Rock, Scissors, Paper, Draw)! Actual: {cachedLieStringIds?.Length ?? 0}");
            }

            // 3. EasterEggStringId
            if (easterEggStringId <= 0)
            {
                Debug.LogWarning($"[QuestionTable] Row Id {id} (Index {rowIndex}): EasterEggStringId is missing or <= 0 ({easterEggStringId})");
            }

            // 4. AnswerProp (정확히 3개 float: 진실, 거짓, 이스터에그)
            cachedAnswerProp = TableUtil.ParseFloatArray(answerPropRaw);
            if (cachedAnswerProp == null || cachedAnswerProp.Length != 3)
            {
                Debug.LogError($"[QuestionTable] Row Id {id} (Index {rowIndex}): AnswerProp must contain exactly 3 floats! Falling back to 60/38/2.");
                normalizedProbabilities = new float[] { 0.60f, 0.38f, 0.02f };
            }
            else
            {
                float pTruth = Mathf.Max(0f, cachedAnswerProp[0]);
                float pLie = Mathf.Max(0f, cachedAnswerProp[1]);
                float pEE = Mathf.Max(0f, cachedAnswerProp[2]);
                float sum = pTruth + pLie + pEE;

                if (sum <= 0.0001f)
                {
                    Debug.LogError($"[QuestionTable] Row Id {id}: AnswerProp sum is 0! Falling back to 60/38/2.");
                    normalizedProbabilities = new float[] { 0.60f, 0.38f, 0.02f };
                }
                else
                {
                    if (Mathf.Abs(sum - 1.0f) > 0.001f)
                    {
                        Debug.LogWarning($"[QuestionTable] Row Id {id}: AnswerProp sum is {sum:F4} != 1.0. Normalizing to sum 1.0.");
                    }
                    normalizedProbabilities = new float[] { pTruth / sum, pLie / sum, pEE / sum };
                }
            }
        }
    }

    [Serializable]
    [XmlRoot("QuestionTable")]
    public class QuestionTable : TableBase<QuestionTable, QuestionTableRow>
    {
        protected override string fileName => "QuestionTable";
        private bool isInitialized = false;

        public void EnsureInitialized()
        {
            if (isInitialized) return;
            isInitialized = true;

            var stringTable = TableManager.Instance?.GetTable<StringTable>();

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                row.ParseAndValidate(i);

                // StringTable 참조 무결성 교차 검증
                if (stringTable != null)
                {
                    if (row.questionStringId > 0 && stringTable.Get(row.questionStringId) == null)
                    {
                        Debug.LogWarning($"[QuestionTable] Row {row.id}: QuestionStringId {row.questionStringId} not found in StringTable!");
                    }
                    if (row.AnswerStringIds != null)
                    {
                        foreach (int sid in row.AnswerStringIds)
                        {
                            if (sid > 0 && stringTable.Get(sid) == null)
                                Debug.LogWarning($"[QuestionTable] Row {row.id}: AnswerStringId {sid} not found in StringTable!");
                        }
                    }
                    if (row.LieStringIds != null)
                    {
                        foreach (int sid in row.LieStringIds)
                        {
                            if (sid > 0 && stringTable.Get(sid) == null)
                                Debug.LogWarning($"[QuestionTable] Row {row.id}: LieStringId {sid} not found in StringTable!");
                        }
                    }
                    if (row.easterEggStringId > 0 && stringTable.Get(row.easterEggStringId) == null)
                    {
                        Debug.LogWarning($"[QuestionTable] Row {row.id}: EasterEggStringId {row.easterEggStringId} not found in StringTable!");
                    }
                }
            }
        }
    }

    /// <summary>
    /// QuestionTable.xml에서 질문 목록, 확률, 캐릭터별(질문4 x 상황4 x 대답3) 대답을 조회하는 프로바이더.
    /// 스크립트 내부에는 특정 IP 표현을 포함하지 않으며, 모든 대사와 확률은 XML 데이터로 로드됩니다.
    /// </summary>
    public static class QuestionDataProvider
    {
        public const float DEFAULT_TRUTH_PROB = 0.60f;
        public const float DEFAULT_LIE_PROB = 0.38f;
        public const float DEFAULT_EASTER_EGG_PROB = 0.02f;

        private static readonly List<QuestionChoiceData> fallbackQuestions = new List<QuestionChoiceData>
        {
            new QuestionChoiceData(0, 910001, "이번 라운드에는 어떤 카드를 낼 생각이야?"),
            new QuestionChoiceData(1, 910002, "네 손패의 주력 속성이 무엇인지 이미 읽었어."),
            new QuestionChoiceData(2, 910003, "잠깐, 이번 턴에는 카드를 뽑으며 숨을 고를 셈인가?"),
            new QuestionChoiceData(3, 910004, "피하지 말고 정면 승부로 부딪쳐 보자!")
        };

        private static bool IsXmlTableAvailable()
        {
            var asset = Core.Utils.ResourceUtil.AssetProvider.LoadXmlData("QuestionTable");
            return asset != null;
        }

        private static QuestionTable GetInitializedTable()
        {
            if (!IsXmlTableAvailable()) return null;
            var table = TableManager.Instance.GetTable<QuestionTable>();
            if (table != null)
            {
                table.EnsureInitialized();
            }
            return table;
        }

        /// <summary>
        /// 플레이어의 질문 선택지 목록을 반환합니다. (BattleTable의 QuestionIds 지정 시 해당 ID 목록 기준, 미지정 시 전체 테이블 기준)
        /// </summary>
        public static IReadOnlyList<QuestionChoiceData> GetQuestions(IReadOnlyList<int> questionIds = null)
        {
            var table = GetInitializedTable();
            if (table != null && table.rows != null && table.rows.Count > 0)
            {
                var stringTable = TableManager.Instance.GetTable<StringTable>();
                var currentLang = TableManager.Instance.currentLanguage.Value;

                // 1. BattleTable에서 QuestionIds를 명시적으로 지정한 경우 해당 ID 목록을 순서대로 구성
                if (questionIds != null && questionIds.Count > 0)
                {
                    var customList = new List<QuestionChoiceData>();
                    for (int i = 0; i < questionIds.Count; i++)
                    {
                        int qId = questionIds[i];
                        var row = table.rows.FirstOrDefault(r => r.id == qId);
                        int stringId = row?.questionStringId ?? -1;
                        string text = null;

                        if (stringId > 0 && stringTable != null)
                        {
                            text = stringTable.Get(stringId)?.Get(currentLang);
                        }
                        if (string.IsNullOrEmpty(text))
                        {
                            text = (i >= 0 && i < fallbackQuestions.Count)
                                ? fallbackQuestions[i].questionText
                                : $"질문 선택지 {i + 1}";
                        }

                        customList.Add(new QuestionChoiceData(i, stringId, text));
                    }

                    if (customList.Count > 0)
                        return customList;
                }

                // 2. 미지정 시 기존 QuestionIndex 기준 그룹화 폴백
                var distinctQuestions = table.rows
                    .GroupBy(r => r.QuestionIndex)
                    .OrderBy(g => g.Key)
                    .Select(g =>
                    {
                        var first = g.First();
                        int qIdx = g.Key;
                        int stringId = first.questionStringId;
                        string text = null;

                        if (stringId > 0 && stringTable != null)
                        {
                            text = stringTable.Get(stringId)?.Get(currentLang);
                        }
                        if (string.IsNullOrEmpty(text))
                        {
                            text = (qIdx >= 0 && qIdx < fallbackQuestions.Count)
                                ? fallbackQuestions[qIdx].questionText
                                : $"질문 선택지 {qIdx + 1}";
                        }

                        return new QuestionChoiceData(qIdx, stringId, text);
                    })
                    .ToList();

                if (distinctQuestions.Count > 0)
                    return distinctQuestions;
            }

            return fallbackQuestions;
        }

        /// <summary>
        /// 질문 행을 조회합니다. questionTableId가 양수이면 직접 ID로 조회하며,
        /// 아닐 경우 상대 캐릭터 ID와 질문 인덱스로 매핑 조회합니다.
        /// </summary>
        public static QuestionTableRow FindQuestionRow(int enemyCharacterId, int questionIndex, int questionTableId = -1)
        {
            var table = GetInitializedTable();
            if (table == null || table.rows == null || table.rows.Count == 0) return null;

            // 0순위: BattleTable에서 지정한 questionTableId가 유효한 경우 직결 조회
            if (questionTableId > 0)
            {
                var rowById = table.rows.FirstOrDefault(r => r.id == questionTableId);
                if (rowById != null) return rowById;
            }

            // 1순위: CharacterId & QuestionIndex 일치
            var row = table.rows.FirstOrDefault(r => r.CharacterId == enemyCharacterId && r.QuestionIndex == questionIndex);

            // 2순위: QuestionIndex만 일치 (데모 씬 또는 폴백)
            if (row == null)
            {
                row = table.rows.FirstOrDefault(r => r.QuestionIndex == questionIndex);
            }

            return row;
        }

        /// <summary>
        /// 테이블에 설정된 행별 AnswerProp 확률에 따라 대답 타입(진실/거짓/이스터에그)을 추첨합니다.
        /// </summary>
        public static QuestionAnswerType RollAnswerType(int enemyCharacterId, int questionIndex, float randomValue01, int questionTableId = -1)
        {
            var row = FindQuestionRow(enemyCharacterId, questionIndex, questionTableId);
            float truthProb = DEFAULT_TRUTH_PROB;
            float lieProb = DEFAULT_LIE_PROB;

            if (row != null && row.NormalizedProbabilities != null && row.NormalizedProbabilities.Length == 3)
            {
                truthProb = row.NormalizedProbabilities[0];
                lieProb = row.NormalizedProbabilities[1];
            }

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
            QuestionAnswerType answerType,
            int questionTableId = -1)
        {
            var row = FindQuestionRow(enemyCharacterId, questionIndex, questionTableId);
            int sitIdx = Mathf.Clamp((int)situation, 0, 3);
            int targetStringId = -1;

            if (row != null)
            {
                switch (answerType)
                {
                    case QuestionAnswerType.Truth:
                        if (row.AnswerStringIds != null && sitIdx < row.AnswerStringIds.Length)
                            targetStringId = row.AnswerStringIds[sitIdx];
                        break;

                    case QuestionAnswerType.Lie:
                        if (row.LieStringIds != null && sitIdx < row.LieStringIds.Length)
                            targetStringId = row.LieStringIds[sitIdx];
                        break;

                    case QuestionAnswerType.EasterEgg:
                        targetStringId = row.easterEggStringId;
                        break;
                }
            }

            string text = null;
            if (targetStringId > 0 && TableManager.Instance != null)
            {
                var stringTable = TableManager.Instance.GetTable<StringTable>();
                text = stringTable?.Get(targetStringId)?.Get(TableManager.Instance.currentLanguage.Value);
            }

            if (string.IsNullOrEmpty(text))
            {
                text = BuildNeutralFallbackAnswer(questionIndex, situation, answerType);
            }

            return new QuestionAnswerResult
            {
                questionIndex = questionIndex,
                situation = situation,
                answerType = answerType,
                answerStringId = targetStringId,
                answerText = text
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
