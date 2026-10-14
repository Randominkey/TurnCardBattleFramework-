using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Core.Battle;

namespace Core.View
{
    /// <summary>
    /// BattleScreenPresenter 생성에 필요한 UI 의존성 묶음
    /// </summary>
    public class BattleScreenContext
    {
        public CanvasGroup inGameUIGroup;
        public CanvasGroup rootCanvasGroup;
        public RectTransform playerHandArea;
        public RectTransform enemyHandArea;
        public float slideOffset;
        public TextMeshProUGUI clashResultText;
        public Image environmentIcon;
        public Sprite irysHopeSprite;
        public Sprite mumeiCivilizationSprite;
        public AudioClip warningSfx;
    }

    /// <summary>
    /// 배틀 화면 전환, 핸드 슬라이드 인/아웃, 환경 배너 및 아이콘, 경고 팝업, 승패 결과 연출을 전담하는 프리젠터
    /// </summary>
    public class BattleScreenPresenter
    {
        private readonly BattleScreenContext ctx;

        public BattleScreenPresenter(BattleScreenContext context)
        {
            this.ctx = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void InitializeUIState()
        {
            if (ctx.inGameUIGroup != null)
            {
                ctx.inGameUIGroup.alpha = 0f;
                ctx.inGameUIGroup.blocksRaycasts = false;
            }
        }

        public void FadeInUI(float duration, Action onComplete)
        {
            if (ctx.inGameUIGroup != null)
            {
                ctx.inGameUIGroup.blocksRaycasts = true;
                ctx.inGameUIGroup.DOFade(1f, duration).OnComplete(() => onComplete?.Invoke());
            }
            else
            {
                onComplete?.Invoke();
            }
        }

        public void SlideOutHands(float duration, Action onComplete)
        {
            if (ctx.playerHandArea != null) ctx.playerHandArea.DOAnchorPosY(-ctx.slideOffset, duration).SetEase(Ease.InCubic);
            if (ctx.enemyHandArea != null) ctx.enemyHandArea.DOAnchorPosY(ctx.slideOffset, duration).SetEase(Ease.InCubic);
            DOVirtual.DelayedCall(duration, () => onComplete?.Invoke());
        }

        public void SlideInHands(float duration, Action onComplete)
        {
            if (ctx.playerHandArea != null) ctx.playerHandArea.DOAnchorPosY(0f, duration).SetEase(Ease.OutCubic);
            if (ctx.enemyHandArea != null) ctx.enemyHandArea.DOAnchorPosY(0f, duration).SetEase(Ease.OutCubic);
            DOVirtual.DelayedCall(duration, () => onComplete?.Invoke());
        }

        public void PlayRoundTransition(Action onComplete)
        {
            if (ctx.rootCanvasGroup == null)
            {
                onComplete?.Invoke();
                return;
            }

            Sequence seq = DOTween.Sequence();
            seq.Append(ctx.rootCanvasGroup.DOFade(0.3f, 0.2f));
            seq.Append(ctx.rootCanvasGroup.DOFade(1.0f, 0.2f));
            seq.OnComplete(() => onComplete?.Invoke());
        }

        public void ShowWarningMessage(string message)
        {
            if (ctx.clashResultText == null) return;

            if (ctx.warningSfx != null) Core.Audio.SoundManager.Instance.PlaySe(ctx.warningSfx);
            else Core.Audio.SoundManager.Instance.PlaySe("UI_Warning");

            ctx.clashResultText.text = message;
            ctx.clashResultText.color = Color.red;
            ctx.clashResultText.gameObject.SetActive(true);
            ctx.clashResultText.transform.localScale = Vector3.zero;

            Sequence seq = DOTween.Sequence();
            seq.Append(ctx.clashResultText.transform.DOScale(1.2f, 0.3f).SetEase(Ease.OutBack));
            seq.AppendInterval(1.0f);
            seq.Append(ctx.clashResultText.transform.DOScale(0f, 0.2f).SetEase(Ease.InBack));
            seq.OnComplete(() => {
                ctx.clashResultText.gameObject.SetActive(false);
                ctx.clashResultText.color = Color.white;
            });
        }

        public void ShowEnvironmentBanner(EnvironmentType envType, Action onComplete = null)
        {
            if (envType == EnvironmentType.None || ctx.clashResultText == null)
            {
                onComplete?.Invoke();
                return;
            }

            string title = "";
            string desc = "";
            Color titleColor = Color.white;

            switch (envType)
            {
                case EnvironmentType.IrysHope:
                    title = "🕊️ [아이리스의 희망]";
                    desc = "패가 항상 가득 차며, 쓴 만큼 보충됩니다!";
                    titleColor = new Color(0.6f, 0.8f, 1f);
                    break;
                case EnvironmentType.MumeiCivilization:
                    title = "🦉 [무메이의 문명]";
                    desc = "스킬 게이지 충전 속도가 2배로 증가합니다!";
                    titleColor = new Color(1f, 0.85f, 0.4f);
                    break;
            }

            ctx.clashResultText.text = $"<size=120%><color=#{ColorUtility.ToHtmlStringRGB(titleColor)}>{title}</color></size>\n<size=70%>{desc}</size>";
            ctx.clashResultText.gameObject.SetActive(true);
            ctx.clashResultText.transform.localScale = Vector3.zero;

            Core.Audio.SoundManager.Instance?.PlaySe("UI_Round_Start");

            Sequence seq = DOTween.Sequence();
            seq.Append(ctx.clashResultText.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack));
            seq.AppendInterval(1.2f);
            seq.Append(ctx.clashResultText.transform.DOScale(0f, 0.3f).SetEase(Ease.InBack));
            seq.OnComplete(() => {
                ctx.clashResultText.gameObject.SetActive(false);
                onComplete?.Invoke();
            });
        }

        public void SetupEnvironmentIcon(EnvironmentType envType)
        {
            if (ctx.environmentIcon == null) return;

            if (envType == EnvironmentType.None)
            {
                ctx.environmentIcon.gameObject.SetActive(false);
                return;
            }

            ctx.environmentIcon.gameObject.SetActive(true);

            if (envType == EnvironmentType.IrysHope && ctx.irysHopeSprite != null)
            {
                ctx.environmentIcon.sprite = ctx.irysHopeSprite;
            }
            else if (envType == EnvironmentType.MumeiCivilization && ctx.mumeiCivilizationSprite != null)
            {
                ctx.environmentIcon.sprite = ctx.mumeiCivilizationSprite;
            }

            var trigger = ctx.environmentIcon.GetComponent<TooltipTrigger>();
            if (trigger == null) trigger = ctx.environmentIcon.gameObject.AddComponent<TooltipTrigger>();

            trigger.SetData((int)envType, TooltipTrigger.TooltipType.Environment);
        }

        public void ShowBattleResult(bool isWin, Action onComplete)
        {
            if (ctx.clashResultText == null)
            {
                onComplete?.Invoke();
                return;
            }

            ctx.clashResultText.text = isWin ? "<color=#00FF00>VICTORY</color>" : "<color=#FF0000>DEFEAT</color>";
            ctx.clashResultText.gameObject.SetActive(true);
            ctx.clashResultText.transform.localScale = Vector3.zero;

            Sequence seq = DOTween.Sequence();
            seq.Append(ctx.clashResultText.transform.DOScale(1.5f, 0.5f).SetEase(Ease.OutBack));
            seq.AppendInterval(2.0f);
            seq.OnComplete(() => {
                onComplete?.Invoke();
            });
        }
    }
}

