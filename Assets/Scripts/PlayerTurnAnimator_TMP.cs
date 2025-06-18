using UnityEngine;
using TMPro; // 引入 TextMeshPro 的命名空间
using DG.Tweening;

public class PlayerTurnAnimator_TMP : MonoBehaviour
{
    [Header("动画对象引用")]
    public RectTransform mainContainer;
    public RectTransform cloudBoard;
    public TMP_Text notificationText;

    [Header("动画参数")]
    public float boardEnterDuration = 0.5f;
    public float letterFallDuration = 0.4f;
    public float letterStaggerDelay = 0.1f;
    public float finalPopStrength = 0.1f;
    public float exitDuration = 0.3f;

    private Vector2 boardInitialOffscreenPos;
    private Sequence currentSequence;

    void Awake()
    {
        boardInitialOffscreenPos = new Vector2(0, Screen.height / 2 + cloudBoard.sizeDelta.y);
        cloudBoard.anchoredPosition = boardInitialOffscreenPos;
        mainContainer.gameObject.SetActive(false);
    }

    /// <summary>
    /// 播放动画，并传入要显示的文字
    /// </summary>
    /// <param name="textToShow">要显示的文本内容</param>
    public void PlayAnimation(string textToShow)
    {
        Debug.Log(textToShow);
        if (currentSequence != null && currentSequence.IsActive())
        {
            currentSequence.Kill();
        }

        // --- 准备工作 ---
        mainContainer.gameObject.SetActive(true);
        cloudBoard.anchoredPosition = boardInitialOffscreenPos;
        notificationText.text = textToShow; // 设置文本
        notificationText.alpha = 0; // 初始时完全透明

        // 强制TextMeshPro立即更新几何信息，这样我们才能访问到每个字符的位置
        notificationText.ForceMeshUpdate();

        // --- 创建动画序列 ---
        currentSequence = DOTween.Sequence();

        // 1. 木板入场
        currentSequence.Append(cloudBoard.DOAnchorPos(Vector2.zero, boardEnterDuration).SetEase(Ease.OutBounce));

        // 2. 文字动画 (核心技巧)
        // 我们让文字整体淡入，同时给每个字符一个从上到下的位移动画
        currentSequence.Insert(0.2f, notificationText.DOFade(1, letterFallDuration));

        TMP_TextInfo textInfo = notificationText.textInfo;
        Debug.Log(textInfo);
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            if (!textInfo.characterInfo[i].isVisible) continue; // 跳过空格等不可见字符

            int charIndex = i;
            // 获取每个字符的顶点
            var verts = textInfo.meshInfo[textInfo.characterInfo[charIndex].materialReferenceIndex].vertices;

            // 为每个字符创建一个独立的位移动画
            for (int j = 0; j < 4; j++)
            {
                var origPos = verts[textInfo.characterInfo[charIndex].vertexIndex + j];
                var newPos = origPos + new Vector3(0, 50, 0); // 从上方50个单位处开始

                DOTween.To(() => newPos, x => newPos = x, origPos, letterFallDuration)
                    .SetDelay(0.2f + (charIndex * letterStaggerDelay)) // 依次延迟
                    .SetEase(Ease.OutBounce)
                    .OnUpdate(() => {
                        // 在动画每一帧更新顶点位置
                        var currentVerts = textInfo.meshInfo[textInfo.characterInfo[charIndex].materialReferenceIndex].vertices;
                        currentVerts[textInfo.characterInfo[charIndex].vertexIndex + j] = newPos;
                        notificationText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
                    });
            }
        }

        // 3. 整体放大回弹
        currentSequence.Append(mainContainer.DOPunchScale(new Vector3(finalPopStrength, finalPopStrength, 0), 0.4f, 10, 1));
    }

    // 退场动画可以简化为整体飞出
    public void PlayExitAnimation()
    {
        // ... (退场动画逻辑可以类似，让整个mainContainer飞走或淡出)
    }
}