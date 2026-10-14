namespace Core.Battle
{
    /// <summary>
    /// 스테이지 시작 시 랜덤으로 부여되는 게임 전체 환경 변화 요소 (변화 요소 중 1개만 활성화)
    /// </summary>
    public enum EnvironmentType
    {
        None = 0,

        /// <summary>
        /// [확정] 아이리스의 희망: 최초 카드를 풀(8장)로 채워주고, 매 라운드 카드를 소모한 만큼 다시 드로우하여 상시 풀 핸드 유지
        /// </summary>
        IrysHope = 1,

        /// <summary>
        /// [확정] 무메이의 문명: 스킬 게이지 충전 속도 2배속 (카드로 획득하는 게이지 2배)
        /// </summary>
        MumeiCivilization = 2,

        // ========================================================
        // [후속 과제 - 기획 파트 확정 후 구현 예정]
        // ========================================================
        /// <summary>
        /// [재고필요] 크로니의 시간: 각 턴 제한시간 생성 (8~10초)
        /// </summary>
        KroniiTime = 3,

        /// <summary>
        /// [재고필요] 벨즈의 혼돈: 가위바위보 상성 역전
        /// </summary>
        BaelzChaos = 4,

        /// <summary>
        /// [재고필요] 파우나의 수호: n턴마다 한 번씩 대미지 방어 보호막 생성
        /// </summary>
        FaunaProtection = 5
    }
}
