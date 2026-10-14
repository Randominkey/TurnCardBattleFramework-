namespace Core.Common
{
    /// <summary>
    /// 게임 전체에서 사용하는 공용 상수 및 고정 텍스트 ID 정의
    /// </summary>
    public static class GameConstants
    {
        // 툴팁 관련 StringTable ID
        public const int STR_ID_HEADER_SKILL = 10007; // "Special" (SKILL)
        public const int STR_ID_HEADER_CHAIN = 10008; // "Chain"
        
        // TODO: "특수 스킬" 및 "체인 스킬"에 해당하는 정확한 ID로 수정 필요
        public const int STR_ID_SUBHEADER_SPECIAL_SKILL = 10007; 
        public const int STR_ID_SUBHEADER_CHAIN_SKILL = 10008; 

        // 튜토리얼 스테이지 ID 범위 (1001 ~ 1099)
        public const int TUTORIAL_STAGE_START_ID = 1000;
        public const int TUTORIAL_STAGE_END_ID = 1100;

        /// <summary>
        /// 주어진 스테이지(Execute) ID가 튜토리얼 범위에 속하는지 여부
        /// </summary>
        public static bool IsTutorialStage(int executeId) => executeId >= TUTORIAL_STAGE_START_ID && executeId < TUTORIAL_STAGE_END_ID;
    }
}
