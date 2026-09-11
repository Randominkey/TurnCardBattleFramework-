using UnityEngine;
using UnityEngine.SceneManagement;
using Core.Utils;
using System;
using UnityEngine.UI;
using DG.Tweening;
using Core.Audio;

namespace Core.Flow
{
    /// <summary>
    /// 씬 전환을 관리하는 클래스
    /// </summary>
    public class SceneController : Singleton<SceneController>
    {
        [SerializeField] private CanvasGroup fadeCanvasGroup;
        [SerializeField] private float defaultFadeDuration = 0.5f;

        public override void Awake()
        {
            base.Awake();
            
            // 페이드용 UI가 없으면 동적으로 생성하거나 연결 확인
            if (fadeCanvasGroup == null)
            {
                CreateFadeOverlay();
            }
        }

        private void CreateFadeOverlay()
        {
            var go = new GameObject("FadeOverlay");
            go.transform.SetParent(this.transform);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999; // 최상단 노출
            
            // 해상도 대응을 위한 CanvasScaler 추가 (선택 사항이나 권장)
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            go.AddComponent<GraphicRaycaster>();
            fadeCanvasGroup = go.AddComponent<CanvasGroup>();
            fadeCanvasGroup.alpha = 0;
            fadeCanvasGroup.blocksRaycasts = false;

            var imageGo = new GameObject("BlackBackground");
            imageGo.transform.SetParent(go.transform);
            var img = imageGo.AddComponent<Image>();
            img.color = Color.black;
            
            var rt = img.GetComponent<RectTransform>();
            // [Fix] 전체 화면을 꽉 채우도록 앵커와 피벗 설정 강제
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }

        public void LoadScene(string sceneName, Action onSceneLoaded = null)
        {
            Debug.Log($"[Scene] Loading Scene: {sceneName}");
            
            // [New] 씬 전환 시 BGM 정지
            if (SoundManager.Instance != null) SoundManager.Instance.StopBgm();
            
            SceneManager.LoadScene(sceneName);
            onSceneLoaded?.Invoke();
        }

        public void LoadSceneWithFade(string sceneName, Action onSceneLoaded = null)
        {
            fadeCanvasGroup.blocksRaycasts = true;
            fadeCanvasGroup.DOFade(1f, defaultFadeDuration).OnComplete(() => {
                LoadScene(sceneName, () => {
                    onSceneLoaded?.Invoke();
                    fadeCanvasGroup.DOFade(0f, defaultFadeDuration).OnComplete(() => {
                        fadeCanvasGroup.blocksRaycasts = false;
                    });
                });
            });
        }

        public void FadeIn(float duration, Action onComplete = null)
        {
            fadeCanvasGroup.blocksRaycasts = true;
            fadeCanvasGroup.DOFade(1f, duration).OnComplete(() => onComplete?.Invoke());
        }

        public void FadeOut(float duration, Action onComplete = null)
        {
            fadeCanvasGroup.DOFade(0f, duration).OnComplete(() => {
                fadeCanvasGroup.blocksRaycasts = false;
                onComplete?.Invoke();
            });
        }
    }
}
