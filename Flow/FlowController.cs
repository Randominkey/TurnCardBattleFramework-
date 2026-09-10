using UnityEngine;
using UnityEngine.SceneManagement;
using Core.Utils;
using Core.Table;
using System.Linq;

namespace Core.Flow
{
    public class FlowController : Singleton<FlowController>
    {
        private ModeFlowTableRow currentModeRow;
        private int currentModeFlowId;
        private int currentStepIndex = 0;
        private BaseFlowExecutor currentExecutor;
        
        public int CurrentModeFlowId => currentModeFlowId;
        public int CurrentStepIndex => currentStepIndex;
        public BaseFlowExecutor CurrentExecutor => currentExecutor;

        public int CurrentExecuteId => (currentModeRow != null && currentStepIndex < currentModeRow.ExecuteIds.Length) 
            ? currentModeRow.ExecuteIds[currentStepIndex] 
            : -1;

        public void StartFlow(int modeFlowId, int startStepIndex = 0)
        {
            this.currentModeFlowId = modeFlowId;
            var modeFlowTable = TableManager.Instance.GetTable<ModeFlowTable>();
            currentModeRow = modeFlowTable.Get(modeFlowId);

            if (currentModeRow == null)
            {
                Debug.LogError($"[Flow] Cannot find ModeFlow ID: {modeFlowId}");
                return;
            }

            currentStepIndex = startStepIndex;
            ExecuteCurrentStep();
        }

        public void RestartFlow()
        {
            Debug.Log($"[Flow] Restarting Flow: {currentModeFlowId} at Step: {currentStepIndex}");
            ExecuteCurrentStep();
        }

        public void NextStep()
        {
            currentStepIndex++;
            ExecuteCurrentStep();
        }

        private void ExecuteCurrentStep()
        {
            if (currentModeRow == null || currentStepIndex >= currentModeRow.ExecuteIds.Length)
            {
                Debug.Log("[Flow] End of Flow. Returning to TitleScene.");
                SceneController.Instance.LoadScene("TitleScene");
                return;
            }

            SceneType sceneType = currentModeRow.ExecuteTypes[currentStepIndex];
            int executeId = currentModeRow.ExecuteIds[currentStepIndex];
            string targetSceneName = $"{sceneType}Scene";

            Debug.Log($"[Flow] Step {currentStepIndex}: {sceneType} (ID: {executeId})");

            // --- 무한 루프 방지 로직 ---
            if (SceneManager.GetActiveScene().name == targetSceneName)
            {
                Debug.Log($"[Flow] Already in {targetSceneName}. Initializing current executor.");
                if (currentExecutor != null)
                {
                    currentExecutor.Initialize(executeId);
                }
            }
            else
            {
                SceneController.Instance.LoadSceneWithFade(targetSceneName);
            }
        }

        private int pendingDirectExecuteId = -1;

        public void StartDirectExecute(int executeId)
        {
            Debug.Log($"[Flow] Direct Execution Started for ExecuteId: {executeId}");
            if (currentExecutor != null)
            {
                currentExecutor.Initialize(executeId);
            }
            else
            {
                pendingDirectExecuteId = executeId;
            }
        }

        public void RegisterExecutor(BaseFlowExecutor executor)
        {
            currentExecutor = executor;
            if (pendingDirectExecuteId != -1)
            {
                int idToRun = pendingDirectExecuteId;
                pendingDirectExecuteId = -1;
                executor.Initialize(idToRun);
            }
            else if (CurrentExecuteId != -1)
            {
                executor.Initialize(CurrentExecuteId);
            }
        }
    }
}
