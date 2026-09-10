using UnityEngine;
using Core.Table;

namespace Core.View
{
    /// <summary>
    /// 단일 연출에 대한 독립 ScriptableObject 에셋 클래스
    /// </summary>
    [CreateAssetMenu(fileName = "Prod_Type_SubKey", menuName = "ProjectH/Battle/ProductionEntrySO")]
    public class ProductionEntrySO : ScriptableObject
    {
        [Tooltip("연출의 종류")]
        public ProductionType type;

        [Tooltip("캐릭터 스킬 등 세부 구분이 필요할 때 사용하는 키 (Optional)")]
        public CharacterKeyName subKey = CharacterKeyName.None;
        
        [Tooltip("생성할 연출 프리팹")]
        public GameObject prefab;

        [Tooltip("오브젝트 풀링 사용 여부")]
        public bool usePooling = false;

        [Tooltip("이 연출에서 하이라이트할 UI 대상")]
        public HighlightMask highlightMask = HighlightMask.None;

        [Header("Timing Settings")]
        [Tooltip("연출 요청 후 딤드 활성화까지의 대기 시간")]
        public float dimDelay = 0f;

        [Tooltip("연출 요청 후 하이라이트 활성화까지의 대기 시간")]
        public float highlightDelay = 0f;

        [Tooltip("딤드 활성화 후 프리팹 생성까지의 대기 시간")]
        public float spawnDelay = 0.2f;

        [Tooltip("연출 전체 지속 시간 (이 시간 동안 시퀀서가 대기함)")]
        public float duration = 1.5f;

        [Tooltip("프리팹 제거 후 딤드/하이라이트 해제까지의 여운 시간")]
        public float postWait = 0.5f;

        [Tooltip("로직 적용 전 추가 대기 (ActionSequencer용)")]
        public float preWait = 0.5f;
    }
}
