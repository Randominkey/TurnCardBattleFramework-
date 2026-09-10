using UnityEngine;
using UnityEngine.UI;
using Core.Flow;

namespace Core.View
{
    /// <summary>
    /// 배틀 종료 시 나타나는 결과(승리/패배) 팝업을 관리하는 컴포넌트
    /// </summary>
    public class BattleResultPopup : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button continueButton; // 승리 시 다음 스테이지로
        [SerializeField] private Button restartButton;  // 패배 시 다시 시작
        [SerializeField] private Button titleButton;    // 타이틀로 이동

        private void Awake()
        {
            if (continueButton != null)
            {
                continueButton.onClick.AddListener(OnClickContinue);
            }

            if (restartButton != null)
            {
                restartButton.onClick.AddListener(OnClickRestart);
            }

            if (titleButton != null)
            {
                titleButton.onClick.AddListener(OnClickTitle);
            }
        }

        private void OnClickContinue()
        {
            Debug.Log("[BattleResult] Continuing to next stage.");
            gameObject.SetActive(false);
            FlowController.Instance.NextStep();
        }

        private void OnClickRestart()
        {
            Debug.Log("[BattleResult] Restarting current stage.");
            gameObject.SetActive(false);
            string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            SceneController.Instance.LoadScene(currentSceneName);
        }

        private void OnClickTitle()
        {
            Debug.Log("[BattleResult] Returning to TitleScene.");
            gameObject.SetActive(false);
            SceneController.Instance.LoadScene("TitleScene");
        }
    }
}
