using UnityEngine;
using UnityEngine.UI;
using Core.Flow;
using Core.Audio;

namespace Core.View
{
    /// <summary>
    /// 타이틀 화면의 UI 인터랙션을 담당하는 클래스
    /// </summary>
    public class TitleView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button storyModeButton;
        [SerializeField] private Button exitButton;
        
        [Header("Settings")]
        [SerializeField] private int startModeFlowId = 101; // ModeFlowTable.xml의 시작 ID
        [SerializeField] private string titleBgmName = "BGM_T_Title"; // 기본 타이틀 배경음

        private void Awake()
        {
            if (storyModeButton != null)
            {
                storyModeButton.onClick.AddListener(OnStoryModeClicked);
            }
            
            if (exitButton != null)
            {
                exitButton.onClick.AddListener(OnExitClicked);
            }
        }

        private void Start()
        {
            // [New] 타이틀 배경음 재생
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayBgm(titleBgmName);
            }
        }

        private void OnStoryModeClicked()
        {
            Debug.Log($"[Title] Starting Story Mode with Flow ID: {startModeFlowId}");
            
            if (FlowController.Instance != null)
            {
                FlowController.Instance.StartFlow(startModeFlowId);
            }
        }

        private void OnExitClicked()
        {
            Debug.Log("[Title] Exit Button Clicked. Closing Application...");
            
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }
    }
}
