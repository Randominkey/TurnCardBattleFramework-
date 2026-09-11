using UnityEngine;
using Core.Utils;
using Core.Audio;
using UnityEngine.SceneManagement;
using DG.Tweening;

namespace Core.Flow
{
    /// <summary>
    /// 게임 전체의 일시정지 및 메뉴 시스템을 관리하는 컨트롤러
    /// </summary>
    public class MenuController : Singleton<MenuController>
    {
        [SerializeField] private GameObject menuPopupPrefab;
        private Core.View.MenuPopupView activeMenuView;
        
        public bool IsMenuOpen => activeMenuView != null && activeMenuView.gameObject.activeSelf;

        private FullScreenMode cachedScreenMode;
        public FullScreenMode CurrentScreenMode => cachedScreenMode;

        private const string RES_WIDTH_KEY = "ResWidth";
        private const string RES_HEIGHT_KEY = "ResHeight";
        private const string FULLSCREEN_MODE_KEY = "FullScreenMode";
        private const string LANG_KEY = "CurrentLanguage";

        private bool suppressMenuThisFrame = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            // 게임 시작 시 메뉴 컨트롤러 자동 생성 (어느 씬에서든 ESC 작동 보장)
            var instance = MenuController.Instance;
        }

        public override void Awake()
        {
            base.Awake();
            ApplySavedSettings();
        }

        private void ApplySavedSettings()
        {
            // 해상도 및 화면 모드 적용
            int width = PlayerPrefs.GetInt(RES_WIDTH_KEY, 1920);
            int height = PlayerPrefs.GetInt(RES_HEIGHT_KEY, 1080);
            int mode = PlayerPrefs.GetInt(FULLSCREEN_MODE_KEY, (int)FullScreenMode.FullScreenWindow);
            
            cachedScreenMode = (FullScreenMode)mode; // 캐시 업데이트
            Screen.SetResolution(width, height, cachedScreenMode);

            // 언어 적용
            int lang = PlayerPrefs.GetInt(LANG_KEY, 0);
            Table.TableManager.Instance.currentLanguage.Value = (Table.Language)lang;
        }

        /// <summary>
        /// [New] 이번 프레임에서 메뉴가 열리는 것을 방지합니다. (Hide 모드 해제 등 ESC 중복 사용 방지용)
        /// </summary>
        public void SuppressMenuThisFrame()
        {
            suppressMenuThisFrame = true;
        }

        private void Update()
        {
            // Update에서는 다른 로직 처리
        }

        private void LateUpdate()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (suppressMenuThisFrame)
                {
                    suppressMenuThisFrame = false;
                    return;
                }
                ToggleMenu();
            }
            
            // 매 프레임 끝에 플래그 리셋
            if (suppressMenuThisFrame && !Input.GetKeyDown(KeyCode.Escape))
            {
                suppressMenuThisFrame = false;
            }
        }

        public void ToggleMenu()
        {
            if (IsMenuOpen)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }

        public void PauseGame()
        {
            if (activeMenuView == null)
            {
                if (menuPopupPrefab == null)
                {
                    // [Fix] 프리팹 경로 수정 (Resources 폴더 내로 이동 필요)
                    menuPopupPrefab = Resources.Load<GameObject>("Prefabs/Common/PauseMenuPopupWindow");
                }

                if (menuPopupPrefab != null)
                {
                    var go = Instantiate(menuPopupPrefab);
                    activeMenuView = go.GetComponent<Core.View.MenuPopupView>();

                    // UI가 화면에 정상적으로 출력되도록 Canvas 설정 확인 및 보정
                    Canvas canvas = go.GetComponent<Canvas>();
                    if (canvas == null)
                    {
                        canvas = go.AddComponent<Canvas>();
                        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                        canvas.sortingOrder = 999; // 최상단 출력
                        
                        var scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>();
                        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                        scaler.referenceResolution = new Vector2(1920, 1080);
                        
                        go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                    }
                    else
                    {
                        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                        canvas.sortingOrder = 999;
                    }
                }
            }

            if (activeMenuView != null)
                activeMenuView.Show();

            Time.timeScale = 0f;
            Debug.Log("[Menu] Game Paused.");
        }

        public void ResumeGame()
        {
            if (activeMenuView != null)
                activeMenuView.Hide();

            Time.timeScale = 1f;
            
            SaveGlobalSettings();
            Debug.Log("[Menu] Game Resumed.");
        }

        public void SaveGlobalSettings()
        {
            SoundManager.Instance.SaveSettings();
            
            // 현재 설정값들 저장
            PlayerPrefs.SetInt(RES_WIDTH_KEY, Screen.width);
            PlayerPrefs.SetInt(RES_HEIGHT_KEY, Screen.height);
            PlayerPrefs.SetInt(FULLSCREEN_MODE_KEY, (int)Screen.fullScreenMode);
            PlayerPrefs.SetInt(LANG_KEY, (int)Table.TableManager.Instance.currentLanguage.Value);
            
            PlayerPrefs.Save();
            Debug.Log("[Menu] All Settings Saved.");
        }

        public void SetResolution(int width, int height)
        {
            Screen.SetResolution(width, height, Screen.fullScreenMode);
        }

        public void SetScreenMode(FullScreenMode mode)
        {
            cachedScreenMode = mode; // 캐시 업데이트
            Screen.SetResolution(Screen.width, Screen.height, mode);
            PlayerPrefs.SetInt(FULLSCREEN_MODE_KEY, (int)mode);
            PlayerPrefs.Save();
            Debug.Log($"[Menu] Screen Mode Set to: {mode}");
        }

        public void SetLanguage(Table.Language lang)
        {
            Table.TableManager.Instance.currentLanguage.Value = lang;
        }

        public void RestartCurrentScene()
        {
            ResumeGame(); // [Fix] Centralized cleanup (Time, DOTween, UI)
            string currentSceneName = SceneManager.GetActiveScene().name;
            SceneController.Instance.LoadScene(currentSceneName);
        }

        public void GoToTitle()
        {
            ResumeGame(); // [Fix] Centralized cleanup
            SceneController.Instance.LoadScene("TitleScene");
        }

        public void QuitGame()
        {
            Debug.Log("[Menu] Quitting Game...");
            Application.Quit();
        }
    }
}
