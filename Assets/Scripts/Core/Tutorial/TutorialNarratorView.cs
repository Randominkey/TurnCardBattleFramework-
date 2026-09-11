using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Core.View;
using System;

namespace Core.Tutorial
{
    /// <summary>
    /// 튜토리얼 가이드 캐릭터의 대사와 초상화를 보여주는 뷰
    /// </summary>
    public class TutorialNarratorView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image portraitImage;
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private GameObject nextArrow;
        [SerializeField] private float fadeDuration = 0.3f;
        [SerializeField] private float typingSpeed = 0.05f;

        private Action onCompleteAction;
        private bool isTyping = false;
        private string currentFullText;
        private Coroutine typingCoroutine;

        private void Awake()
        {
            if (canvasGroup != null) canvasGroup.alpha = 0;
            gameObject.SetActive(false);
            if (nextArrow != null) nextArrow.SetActive(false);
        }

        public void ShowMessage(string message, Sprite portrait = null, Action onComplete = null)
        {
            gameObject.SetActive(true);
            onCompleteAction = onComplete;
            currentFullText = message;

            if (portraitImage != null && portrait != null)
            {
                portraitImage.sprite = portrait;
                portraitImage.gameObject.SetActive(true);
            }
            else if (portraitImage != null)
            {
                portraitImage.gameObject.SetActive(false);
            }

            if (canvasGroup != null && canvasGroup.alpha < 1)
            {
                canvasGroup.DOFade(1f, fadeDuration);
            }

            StartTyping(message);
        }

        private void StartTyping(string message)
        {
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            typingCoroutine = StartCoroutine(TypeText(message));
        }

        private System.Collections.IEnumerator TypeText(string message)
        {
            isTyping = true;
            if (nextArrow != null) nextArrow.SetActive(false);
            dialogueText.text = "";

            foreach (char c in message)
            {
                dialogueText.text += c;
                yield return new WaitForSeconds(typingSpeed);
            }

            isTyping = false;
            if (nextArrow != null) nextArrow.SetActive(true);
        }

        public void OnClickBackground()
        {
            if (isTyping)
            {
                // 타이핑 중이면 즉시 전체 텍스트 표시
                if (typingCoroutine != null) StopCoroutine(typingCoroutine);
                dialogueText.text = currentFullText;
                isTyping = false;
                if (nextArrow != null) nextArrow.SetActive(true);
            }
            else
            {
                // 완료 후 클릭 시 콜백 실행
                onCompleteAction?.Invoke();
            }
        }

        public void Hide()
        {
            if (canvasGroup != null)
            {
                canvasGroup.DOFade(0f, fadeDuration).OnComplete(() => gameObject.SetActive(false));
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
