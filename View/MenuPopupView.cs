using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Core.Flow;
using Core.Audio;
using System.Collections.Generic;
using System.Linq;

namespace Core.View
{
    public class MenuPopupView : MonoBehaviour
    {
        [Header("Main Panels")]
        [SerializeField] private GameObject mainPanel; // 메인 패널 (전체 윈도우)

        [Header("Header")]
        [SerializeField] private TextMeshProUGUI headerText;

        [Header("Screen Settings")]
        [SerializeField] private Toggle windowModeToggle;
        [SerializeField] private Toggle borderlessModeToggle;
        [SerializeField] private Toggle fullscreenModeToggle;
        [SerializeField] private TMP_Dropdown resolutionDropdown;

        [Header("Sound Settings")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Toggle masterToggle;
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Toggle bgmToggle;
        [SerializeField] private Slider seSlider;
        [SerializeField] private Toggle seToggle;
        [SerializeField] private Slider voiceSlider;
        [SerializeField] private Toggle voiceToggle;

        [Header("Language Settings")]
        [SerializeField] private TMP_Dropdown languageDropdown;

        [Header("Bottom Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button exitButton;

        [Header("Confirmation Popups")]
        [SerializeField] private GameObject restartConfirmPanel;
        [SerializeField] private Button restartConfirmYesButton;
        [SerializeField] private Button restartConfirmNoButton;

        [SerializeField] private GameObject exitConfirmPanel;
        [SerializeField] private Button exitConfirmYesButton;
        [SerializeField] private Button exitConfirmNoButton;

        private bool isRefreshing = false; // UI 갱신 중 이벤트 트리거 방지

        private void Awake()
        {
            InitializeButtons();
            InitializeSliders();
            InitializeDropdowns();

            HideAllPopups();
            DontDestroyOnLoad(gameObject);
        }

        private void InitializeButtons()
        {
            // Screen Mode
            windowModeToggle?.onValueChanged.AddListener(isOn =>
            {
                if (isOn && !isRefreshing)
                {
                    MenuController.Instance.SetScreenMode(FullScreenMode.Windowed);
                    RefreshUI();
                }
            });
            borderlessModeToggle?.onValueChanged.AddListener(isOn =>
            {
                if (isOn && !isRefreshing)
                {
                    MenuController.Instance.SetScreenMode(FullScreenMode.FullScreenWindow);
                    RefreshUI();
                }
            });
            fullscreenModeToggle?.onValueChanged.AddListener(isOn =>
            {
                if (isOn && !isRefreshing)
                {
                    MenuController.Instance.SetScreenMode(FullScreenMode.ExclusiveFullScreen);
                    RefreshUI();
                }
            });

            // Bottom
            resumeButton?.onClick.AddListener(() => MenuController.Instance.ResumeGame());
            restartButton?.onClick.AddListener(() => ShowConfirm(restartConfirmPanel));
            exitButton?.onClick.AddListener(() => ShowConfirm(exitConfirmPanel));

            // Confirmation
            restartConfirmYesButton?.onClick.AddListener(() => {
                MenuController.Instance.ResumeGame(); // [Fix] Hide UI and reset time
                MenuController.Instance.RestartCurrentScene();
            });
            restartConfirmNoButton?.onClick.AddListener(() => HideConfirm(restartConfirmPanel));
            
            exitConfirmYesButton?.onClick.AddListener(() => {
                MenuController.Instance.ResumeGame(); // [Fix] Hide UI and reset time
                MenuController.Instance.GoToTitle();
            });
            exitConfirmNoButton?.onClick.AddListener(() => HideConfirm(exitConfirmPanel));

            // Sound Toggles
            masterToggle?.onValueChanged.AddListener(isOn =>
            {
                if (!isRefreshing)
                {
                    SoundManager.Instance.IsMasterMuted = !isOn;
                    RefreshUI();
                }
            });
            bgmToggle?.onValueChanged.AddListener(isOn =>
            {
                if (!isRefreshing)
                {
                    SoundManager.Instance.IsBgmMuted = !isOn;
                    RefreshUI();
                }
            });
            seToggle?.onValueChanged.AddListener(isOn =>
            {
                if (!isRefreshing)
                {
                    SoundManager.Instance.IsSeMuted = !isOn;
                    RefreshUI();
                }
            });
            voiceToggle?.onValueChanged.AddListener(isOn =>
            {
                if (!isRefreshing)
                {
                    SoundManager.Instance.IsVoiceMuted = !isOn;
                    RefreshUI();
                }
            });
        }

        private void InitializeSliders()
        {
            masterSlider?.onValueChanged.AddListener(val =>
            {
                if (!isRefreshing)
                {
                    SoundManager.Instance.MasterVolume = val;
                    SoundManager.Instance.IsMasterMuted = false;
                    RefreshUI();
                }
            });
            bgmSlider?.onValueChanged.AddListener(val =>
            {
                if (!isRefreshing)
                {
                    SoundManager.Instance.BgmVolume = val;
                    SoundManager.Instance.IsBgmMuted = false;
                    RefreshUI();
                }
            });
            seSlider?.onValueChanged.AddListener(val =>
            {
                if (!isRefreshing)
                {
                    SoundManager.Instance.SeVolume = val;
                    SoundManager.Instance.IsSeMuted = false;
                    RefreshUI();
                }
            });
            voiceSlider?.onValueChanged.AddListener(val =>
            {
                if (!isRefreshing)
                {
                    SoundManager.Instance.VoiceVolume = val;
                    SoundManager.Instance.IsVoiceMuted = false;
                    RefreshUI();
                }
            });
        }

        private void InitializeDropdowns()
        {
            // Resolution
            if (resolutionDropdown != null)
            {
                resolutionDropdown.ClearOptions();
                var options = new List<string> { "1920*1080", "1600*900", "1280*720" };
                resolutionDropdown.AddOptions(options);
                resolutionDropdown.onValueChanged.AddListener(idx =>
                {
                    if (!isRefreshing)
                    {
                        var parts = options[idx].Split('*');
                        MenuController.Instance.SetResolution(int.Parse(parts[0]), int.Parse(parts[1]));
                        RefreshUI();
                    }
                });
            }

            // Language
            if (languageDropdown != null)
            {
                languageDropdown.ClearOptions();
                languageDropdown.AddOptions(new List<string> { "한국어", "English", "日本語" });
                languageDropdown.onValueChanged.AddListener(idx =>
                {
                    if (!isRefreshing)
                    {
                        MenuController.Instance.SetLanguage((Table.Language)idx);
                        RefreshUI();
                    }
                });
            }
        }

        public void Show()
        {
            Debug.Log("[Menu] Show() called. Activating Popup and MainPanel.");
            gameObject.SetActive(true);
            if (mainPanel != null) mainPanel.SetActive(true);
            HideAllPopups();
            RefreshUI();
        }

        public void Hide()
        {
            Debug.Log("[Menu] Hide() called. Deactivating entire Popup.");
            gameObject.SetActive(false);
        }

        private void RefreshUI()
        {
            if (isRefreshing) return;
            isRefreshing = true;
            Debug.Log("[Menu] Refreshing UI Values...");

            // Sound
            if (masterSlider != null) masterSlider.value = SoundManager.Instance.MasterVolume;
            if (bgmSlider != null) bgmSlider.value = SoundManager.Instance.BgmVolume;
            if (seSlider != null) seSlider.value = SoundManager.Instance.SeVolume;
            if (voiceSlider != null) voiceSlider.value = SoundManager.Instance.VoiceVolume;

            // Sound Toggles
            if (masterToggle != null) masterToggle.isOn = !SoundManager.Instance.IsMasterMuted;
            if (bgmToggle != null) bgmToggle.isOn = !SoundManager.Instance.IsBgmMuted;
            if (seToggle != null) seToggle.isOn = !SoundManager.Instance.IsSeMuted;
            if (voiceToggle != null) voiceToggle.isOn = !SoundManager.Instance.IsVoiceMuted;

            // Language
            if (languageDropdown != null) languageDropdown.value = (int)Table.TableManager.Instance.currentLanguage.Value;

            // Screen Mode
            var currentMode = MenuController.Instance.CurrentScreenMode;
            if (windowModeToggle != null) windowModeToggle.isOn = (currentMode == FullScreenMode.Windowed);
            if (borderlessModeToggle != null) borderlessModeToggle.isOn = (currentMode == FullScreenMode.FullScreenWindow);
            if (fullscreenModeToggle != null) fullscreenModeToggle.isOn = (currentMode == FullScreenMode.ExclusiveFullScreen);

            isRefreshing = false;
        }

        private void ShowConfirm(GameObject panel)
        {
            if (panel != null)
            {
                Debug.Log($"[Menu] Showing Confirmation Panel: {panel.name}");
                panel.SetActive(true);
            }
        }

        private void HideConfirm(GameObject panel)
        {
            if (panel != null)
            {
                Debug.Log($"[Menu] Hiding Confirmation Panel: {panel.name}");
                panel.SetActive(false);
            }
        }

        private void HideAllPopups()
        {
            if (restartConfirmPanel != null) restartConfirmPanel.SetActive(false);
            if (exitConfirmPanel != null) exitConfirmPanel.SetActive(false);
        }
    }
}
