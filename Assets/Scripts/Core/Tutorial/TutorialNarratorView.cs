using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Core.View;
using System;
using System.Collections.Generic;

namespace Core.Tutorial
{
    public enum NarratorPlacement
    {
        Auto,
        Top,
        Bottom,
        Center
    }

    /// <summary>
    /// 튜토리얼 가이드 캐릭터의 대사와 초상화를 보여주는 뷰.
    /// 화면 어디든 클릭하거나 키보드(Space, Enter)로 진행 가능하며,
    /// 대상 UI 위치에 따라 대화창 위치를 동적으로 자동 조정합니다.
    /// (단, ESC 키는 시스템 일시정지/메뉴 전용으로 보존됩니다.)
    /// </summary>
    public class TutorialNarratorView : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image portraitImage;
        [SerializeField] private TextIdHolder dialogueTextHolder;
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private GameObject nextArrow;

        [Header("Animation Settings")]
        [SerializeField] private float fadeDuration = 0.3f;
        [SerializeField] private float typingSpeed = 0.05f;

        [Header("Position Presets")]
        [SerializeField] private Vector2 topPosition = new Vector2(0f, 300f);
        [SerializeField] private Vector2 bottomPosition = new Vector2(0f, -300f);
        [SerializeField] private Vector2 centerPosition = Vector2.zero;

        private Action onCompleteAction;
        private bool isTyping = false;
        private bool canAdvanceOnClick = true;
        private string currentFullText;
        private Coroutine typingCoroutine;
        private RectTransform myRectTransform;

        // [New] 말풍선 분할 출력을 위한 문장 큐
        private List<string> messageQueue = new List<string>();
        private int currentQueueIndex = 0;

        private TextMeshProUGUI TargetTextComponent
        {
            get
            {
                if (dialogueTextHolder != null && dialogueTextHolder.TextComponent != null)
                    return dialogueTextHolder.TextComponent;
                if (dialogueText != null)
                    return dialogueText;
                dialogueText = GetComponentInChildren<TextMeshProUGUI>();
                return dialogueText;
            }
        }

        private void Awake()
        {
            myRectTransform = GetComponent<RectTransform>();

            if (dialogueTextHolder == null)
            {
                dialogueTextHolder = GetComponentInChildren<TextIdHolder>();
            }

            var canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 700;

            var raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster == null) gameObject.AddComponent<GraphicRaycaster>();

            if (canvasGroup != null) canvasGroup.alpha = 0;
            gameObject.SetActive(false);
            if (nextArrow != null) nextArrow.SetActive(false);
        }

        private void Update()
        {
            if (!gameObject.activeInHierarchy) return;

            // ESC 키는 시스템 일시정지/설정 메뉴 전용이므로 절대 가로채지 않음
            if (Input.GetKeyDown(KeyCode.Escape)) return;

            // 화면 어디든 마우스 좌클릭 또는 Space / Enter 입력 시 진행
            if (Input.GetMouseButtonDown(0) || 
                Input.GetKeyDown(KeyCode.Space) || 
                Input.GetKeyDown(KeyCode.Return) || 
                Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                HandleAdvanceInput();
            }
        }

        /// <summary>
        /// TextIdHolder를 통해 다국어 StringId를 설정하고 대사를 출력합니다.
        /// </summary>
        public void ShowMessage(int stringId, Sprite portrait = null, Action onComplete = null, bool allowAdvanceOnClick = true)
        {
            if (dialogueTextHolder != null)
            {
                dialogueTextHolder.SetId(stringId);
                string localized = dialogueTextHolder.GetLocalizedText(stringId);
                ShowMessage(localized, portrait, onComplete, allowAdvanceOnClick);
            }
            else
            {
                ShowMessage($"ID:{stringId}", portrait, onComplete, allowAdvanceOnClick);
            }
        }

        /// <summary>
        /// 여러 개의 문장(말풍선)을 순차적으로 출력합니다. 클릭할 때마다 다음 말풍선으로 넘어갑니다.
        /// </summary>
        public void ShowMessages(IList<string> messages, Sprite portrait = null, Action onComplete = null, bool allowAdvanceOnClick = true)
        {
            if (messages == null || messages.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            messageQueue.Clear();
            foreach (var m in messages)
            {
                if (!string.IsNullOrEmpty(m)) messageQueue.Add(m.Trim());
            }

            if (messageQueue.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            gameObject.SetActive(true);
            onCompleteAction = onComplete;
            canAdvanceOnClick = allowAdvanceOnClick;
            currentQueueIndex = 0;

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
                canvasGroup.DOKill();
                canvasGroup.DOFade(1f, fadeDuration);
            }

            StartTypingCurrentQueueItem();
        }

        /// <summary>
        /// 단일 텍스트 또는 개행(\n)으로 구분된 다중 문장을 말풍선 단위로 분할하여 출력합니다.
        /// </summary>
        public void ShowMessage(string message, Sprite portrait = null, Action onComplete = null, bool allowAdvanceOnClick = true)
        {
            if (string.IsNullOrEmpty(message))
            {
                onComplete?.Invoke();
                return;
            }

            // 개행문자(\n)가 있으면 말풍선을 자동으로 분할하여 출력
            var lines = message.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            ShowMessages(lines, portrait, onComplete, allowAdvanceOnClick);
        }

        private void StartTypingCurrentQueueItem()
        {
            if (currentQueueIndex >= 0 && currentQueueIndex < messageQueue.Count)
            {
                currentFullText = messageQueue[currentQueueIndex];
                StartTyping(currentFullText);
            }
        }

        /// <summary>
        /// 강조 대상 UI를 가리지 않도록 대사창의 위치를 동적으로 스마트 배치합니다.
        /// </summary>
        public void SetPlacement(NarratorPlacement placement = NarratorPlacement.Auto, RectTransform target = null)
        {
            if (myRectTransform == null) myRectTransform = GetComponent<RectTransform>();
            if (myRectTransform == null) return;

            Vector2 destination = bottomPosition;

            switch (placement)
            {
                case NarratorPlacement.Top:
                    destination = topPosition;
                    break;
                case NarratorPlacement.Bottom:
                    destination = bottomPosition;
                    break;
                case NarratorPlacement.Center:
                    destination = centerPosition;
                    break;
                case NarratorPlacement.Auto:
                default:
                    if (target != null)
                    {
                        // 타겟의 월드 좌표를 스크린 좌표로 변환하여 상/하 판단
                        Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(null, target.position);
                        if (screenPos.y < Screen.height * 0.45f)
                        {
                            // 타겟이 화면 하단(핸드, 버튼 등)에 있으면 대사창은 상단으로
                            destination = topPosition;
                        }
                        else
                        {
                            // 타겟이 화면 상단(체력바 등)에 있으면 대사창은 하단으로
                            destination = bottomPosition;
                        }
                    }
                    else
                    {
                        destination = bottomPosition;
                    }
                    break;
            }

            myRectTransform.DOKill();
            myRectTransform.DOAnchorPos(destination, 0.25f).SetEase(Ease.OutCubic);
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
            if (TargetTextComponent != null) TargetTextComponent.text = "";

            foreach (char c in message)
            {
                if (TargetTextComponent != null) TargetTextComponent.text += c;
                yield return new WaitForSeconds(typingSpeed);
            }

            isTyping = false;
            // 일반 대사 스텝에서만 다음 화살표 표시
            if (canAdvanceOnClick && nextArrow != null) nextArrow.SetActive(true);
        }

        private void HandleAdvanceInput()
        {
            if (isTyping)
            {
                // 타이핑 중이면 즉시 전체 텍스트 표시
                if (typingCoroutine != null) StopCoroutine(typingCoroutine);
                if (TargetTextComponent != null) TargetTextComponent.text = currentFullText;
                isTyping = false;
                if (canAdvanceOnClick && nextArrow != null) nextArrow.SetActive(true);
            }
            else if (canAdvanceOnClick)
            {
                // [New] 다음 말풍선(서브 문장)이 남아있으면 다음 문장 출력
                if (currentQueueIndex + 1 < messageQueue.Count)
                {
                    currentQueueIndex++;
                    StartTypingCurrentQueueItem();
                }
                else
                {
                    // 모든 문장을 다 보았으면 다음 튜토리얼 스텝 진행
                    onCompleteAction?.Invoke();
                }
            }
        }

        /// <summary>
        /// 대화창 본체 클릭 시 호출되는 기존 이벤트 핸들러
        /// </summary>
        public void OnClickBackground()
        {
            HandleAdvanceInput();
        }

        public void Hide()
        {
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            if (nextArrow != null) nextArrow.SetActive(false);

            if (canvasGroup != null)
            {
                canvasGroup.DOKill();
                canvasGroup.DOFade(0f, fadeDuration).OnComplete(() => gameObject.SetActive(false));
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
