using UnityEngine;
using TMPro; // 引入 TextMeshPro 的命名空间
using DG.Tweening;

public class PlayerTurnAnimator_TMP : MonoBehaviour
{
    [Header("主要组件引用")]
    public RectTransform mainContainer;
    public RectTransform cloudBoard;
    public TMP_Text notificationText;

    [Header("动画参数")]
    public float boardEnterDuration = 0.8f;
    public float letterFallDuration = 0.6f;
    public float letterStaggerDelay = 0.05f;
    public float finalScaleStrength = 0.05f;
    public float exitDuration = 0.5f;

    [Header("呼吸提醒参数")]
    public float reminderDelay = 3f; // 3秒后开始提醒
    public float breatheStrength = 0.1f; // 呼吸效果的强度
    public float breatheDuration = 1.5f; // 一次呼吸的时长

    private Vector2 boardInitialOffscreenPos;
    private Sequence currentSequence;
    private Sequence breatheSequence;
    private float lastActionTime;

    void Awake()
    {
        boardInitialOffscreenPos = new Vector2(0, Screen.height / 2 + cloudBoard.sizeDelta.y);
        cloudBoard.anchoredPosition = boardInitialOffscreenPos;
        mainContainer.gameObject.SetActive(false);
    }

    void Update()
    {
        // 检查是否需要开始呼吸提醒
        if (mainContainer.gameObject.activeInHierarchy &&
            Time.time - lastActionTime > reminderDelay &&
            (breatheSequence == null || !breatheSequence.IsActive()))
        {
            StartBreatheReminder();
        }
    }

    /// <summary>
    /// 播放动画，显示玩家回合要显示的文字
    /// </summary>
    /// <param name="textToShow">要显示的文本内容</param>
    public void PlayAnimation(string textToShow)
    {
        Debug.Log(textToShow);
        if (currentSequence != null && currentSequence.IsActive())
        {
            currentSequence.Kill();
        }

        // --- 准备阶段 ---
        mainContainer.gameObject.SetActive(true);
        cloudBoard.anchoredPosition = Vector2.zero;
        notificationText.text = textToShow;
        notificationText.alpha = 0;
        mainContainer.localScale = Vector3.one * 0.95f; // 初始略小

        // --- 创建动画序列 ---
        currentSequence = DOTween.Sequence();
        // 整体淡入+轻微缩放
        currentSequence.Append(notificationText.DOFade(1, boardEnterDuration).SetEase(Ease.InOutQuad));
        currentSequence.Join(mainContainer.DOScale(1.05f, boardEnterDuration * 0.7f).SetEase(Ease.InOutQuad))
                        .Append(mainContainer.DOScale(1f, boardEnterDuration * 0.3f).SetEase(Ease.InOutQuad));

        // 记录动画开始时间，用于呼吸提醒计时
        lastActionTime = Time.time;
        StopBreatheReminder();
    }

    /// <summary>
    /// 简单文字变化（用于额外回合，不播放完整入场动画）
    /// </summary>
    /// <param name="textToShow">要显示的文本内容</param>
    public void UpdateTextOnly(string textToShow)
    {
        Debug.Log("更新文字: " + textToShow);

        // 检查容器是否已经激活（说明之前已经有正常回合显示）
        bool wasAlreadyActive = mainContainer.gameObject.activeInHierarchy;

        if (!wasAlreadyActive)
        {
            // 如果是第一次显示额外回合，直接激活容器，画板位置保持在屏幕中央
            mainContainer.gameObject.SetActive(true);
            cloudBoard.anchoredPosition = Vector2.zero;
            notificationText.alpha = 1; // 确保文字可见
        }

        // 停止当前动画和呼吸提醒
        if (currentSequence != null && currentSequence.IsActive())
        {
            currentSequence.Kill();
        }
        StopBreatheReminder();

        // 简单的文字淡出淡入效果（画板位置保持不变）
        currentSequence = DOTween.Sequence();
        currentSequence.Append(notificationText.DOFade(0, 0.2f))
                      .AppendCallback(() => notificationText.text = textToShow)
                      .Append(notificationText.DOFade(1, 0.3f).SetEase(Ease.InOutQuad));

        // 记录动画开始时间
        lastActionTime = Time.time;
    }

    /// <summary>
    /// 开始呼吸提醒效果
    /// </summary>
    private void StartBreatheReminder()
    {
        if (breatheSequence != null && breatheSequence.IsActive())
            return;

        breatheSequence = DOTween.Sequence();
        breatheSequence.Append(mainContainer.DOScale(1f + breatheStrength, breatheDuration * 0.5f).SetEase(Ease.InOutSine))
                      .Append(mainContainer.DOScale(1f, breatheDuration * 0.5f).SetEase(Ease.InOutSine))
                      .SetLoops(-1); // 无限循环
    }

    /// <summary>
    /// 停止呼吸提醒效果
    /// </summary>
    private void StopBreatheReminder()
    {
        if (breatheSequence != null && breatheSequence.IsActive())
        {
            breatheSequence.Kill();
            // 确保容器缩放回到正常大小
            mainContainer.localScale = Vector3.one;
        }
    }

    /// <summary>
    /// 重置行动计时器（当玩家有操作时调用）
    /// </summary>
    public void ResetActionTimer()
    {
        lastActionTime = Time.time;
        StopBreatheReminder();
    }

    // 退出动画，简化为淡出
    public void PlayExitAnimation()
    {
        if (currentSequence != null && currentSequence.IsActive())
        {
            currentSequence.Kill();
        }

        // 停止呼吸提醒
        StopBreatheReminder();

        currentSequence = DOTween.Sequence();

        // 文字先淡出
        currentSequence.Append(notificationText.DOFade(0, exitDuration * 0.4f).SetEase(Ease.InQuad));

        // 背景板向上滑出
        currentSequence.Append(cloudBoard.DOAnchorPos(boardInitialOffscreenPos, exitDuration).SetEase(Ease.InQuart));

        // 动画完成后隐藏容器
        currentSequence.OnComplete(() =>
        {
            mainContainer.gameObject.SetActive(false);
        });
    }
}