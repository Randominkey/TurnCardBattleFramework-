using UnityEngine;
using Core.Flow;
using Core.Battle;

namespace Core.Utils
{
    public class DevBootstrapper : MonoBehaviour
    {
        public static DevBootstrapper Instance { get; private set; }

        [Header("Direct Battle Test")]
        [Tooltip("체크 시 ModeFlow를 거치지 않고 BattleTable ID로 직접 배틀을 시작합니다.")]
        [SerializeField] private bool useDirectBattleId = true;

        [Tooltip("직접 테스트할 BattleTable.xml의 Id (예: 1001, 1002)")]
        [SerializeField] private int testBattleId = 1002;

        [Header("ModeFlow Test")]
        [Tooltip("ModeFlowTable.xml의 Id를 통해 플로우 테스트 시 사용합니다.")]
        [SerializeField] private int testModeFlowId = 101;

        [Tooltip("0: Speak(대화)부터 시작, 1: Battle(전투)부터 직행")]
        [SerializeField] private int startStepIndex = 1;

        [Header("Battle Debug & Experiment Settings")]
        [Tooltip("에디터 한정: 적 핸드의 카드를 가리고, 격돌(Clash) 비교 시점에만 공개합니다.")]
        [SerializeField] private bool hideEnemyHand = true;

        [Tooltip("에디터 한정: 특정 환경 요소를 강제로 고정하여 테스트합니다. (None이면 랜덤 추첨)")]
        [SerializeField] private EnvironmentType environmentOverride = EnvironmentType.None;

        public bool HideEnemyHand => hideEnemyHand;
        public EnvironmentType EnvironmentOverride => environmentOverride;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (FlowController.Instance.CurrentExecuteId == -1)
            {
                if (useDirectBattleId)
                {
                    Debug.Log($"[Dev] Direct Battle Bootstrapping with BattleTable ID: {testBattleId}");
                    FlowController.Instance.StartDirectExecute(testBattleId);
                }
                else
                {
                    Debug.Log($"[Dev] Bootstrapping ModeFlow ID: {testModeFlowId} at Step: {startStepIndex}");
                    FlowController.Instance.StartFlow(testModeFlowId, startStepIndex);
                }
            }
        }

        [ContextMenu("Run Battle Domain Tests")]
        public void RunBattleDomainTests()
        {
            Core.Tests.BattleRuleTests.RunAllTests();
        }
    }
}
