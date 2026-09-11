using UnityEngine;

namespace Core.Flow
{
    public abstract class BaseFlowExecutor : MonoBehaviour
    {
        protected int executeId = -1;

        public virtual void Initialize(int id)
        {
            // 유효하지 않은 ID(-1)가 들어오면 실행하지 않고 기다립니다.
            if (id == -1) 
            {
                Debug.Log($"[Flow] {GetType().Name} waiting for a valid ID...");
                return;
            }

            this.executeId = id;
            Debug.Log($"[Flow] {GetType().Name} Initialized with ID: {id}");
            Execute();
        }

        protected abstract void Execute();

        protected void FinishStep()
        {
            Debug.Log($"[Flow] {GetType().Name} Finished. Requesting Next Step.");
            FlowController.Instance.NextStep();
        }
    }
}
