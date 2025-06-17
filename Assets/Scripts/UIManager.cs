using System.Collections;
using UnityEngine;
using TMPro; // 引入TextMeshPro的命名空间
using DG.Tweening;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    public TextMeshProUGUI turnIndicatorText;

    [Header("动画参数")]
    public float fadeDuration = 0.5f; // 淡入淡出时长
    public float displayDuration = 1.0f; // 文本停留时长

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 一个通用的、带动画的提示信息显示方法
    public void ShowTurnIndicator(string message, Color color)
    {
        // 如果当前正在显示，则先停止旧的动画
        turnIndicatorText.DOKill();

        // 设置文本和颜色
        turnIndicatorText.text = message;
        turnIndicatorText.color = color;

        // 先将Alpha设为0，为淡入做准备
        turnIndicatorText.alpha = 0f;
        turnIndicatorText.gameObject.SetActive(true);

        // 创建一个淡入->停留->淡出的动画序列
        Sequence sequence = DOTween.Sequence();
        sequence.Append(turnIndicatorText.DOFade(1f, fadeDuration)) // 淡入
                .AppendInterval(displayDuration) // 停留
                .Append(turnIndicatorText.DOFade(0f, fadeDuration)) // 淡出
                .OnComplete(() =>
                {
                    // 动画结束后禁用对象
                    turnIndicatorText.gameObject.SetActive(false);
                });
    }
}