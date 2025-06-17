using UnityEngine;
using DG.Tweening;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Tile : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    // --- 基础数据 ---
    public int x;
    public int y;
    public GameBoard board;

    // --- 视觉组件引用 ---
    [Header("视觉组件")]
    public Transform tileGfxTransform; // 用于播放跳动动画
    public Image squareSprite;
    public Image roundedSprite; // 我们保留它，用于未来可能的形状切换
    public GameObject shadowObject; // 阴影对象
    public Image selectorImage; // 高光/边框的Image组件

    // --- 输入处理 ---
    [Header("输入设置")]
    public float dragThreshold = 50f;
    private Vector2 mouseDownPosition;
    private Vector2 mouseUpPosition;

    // 用于停止循环动画
    private Tween breathingTween;

    void OnDestroy()
    {
        // 确保在对象销毁时，所有相关的DoTween动画都被杀死，防止内存泄漏
        breathingTween?.Kill();
        tileGfxTransform.DOKill();
    }

    #region Input Handlers
    public void OnPointerDown(PointerEventData eventData)
    {
        mouseDownPosition = eventData.position;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        mouseUpPosition = eventData.position;
        float swipeDistance = Vector2.Distance(mouseDownPosition, mouseUpPosition);

        if (swipeDistance < dragThreshold)
        {
            if (board != null)
            {
                board.TileClicked(this);
            }
        }
    }

    public void OnDrag(PointerEventData eventData) { }

    #endregion

    // --- 【最终版】选中/取消选中动画 ---
    public void SetSelected(bool isSelected)
    {
        // 无论如何，先停止所有可能在运行的动画
        breathingTween?.Kill();
        tileGfxTransform.DOKill();
        selectorImage.DOKill();
        if (shadowObject.GetComponent<Image>() != null) shadowObject.GetComponent<Image>().DOKill();

        if (isSelected)
        {
            // --- 播放“选中”动画 ---

            // 1. 伪3D跳动效果：让图形部分（TileGFX）播放一个“冲击缩放”动画
            tileGfxTransform.DOPunchScale(new Vector3(0.1f, 0.1f, 0), 0.3f, 10, 1);

            // 2. 激活并淡入阴影
            shadowObject.SetActive(true);
            shadowObject.GetComponent<Image>().DOFade(0.5f, 0.2f); // 假设阴影Image初始alpha为0

            // 3. 激活并淡入高光框，然后开始呼吸
            selectorImage.gameObject.SetActive(true);
            selectorImage.color = new Color(1, 1, 1, 0); // 先确保是透明的
            breathingTween = selectorImage.DOFade(0.8f, 0.5f).SetLoops(-1, LoopType.Yoyo); // 淡入并开始循环呼吸
        }
        else
        {
            // --- 播放“取消选中”动画 ---

            // 1. 恢复图形部分的原始状态
            tileGfxTransform.DOScale(1f, 0.2f);

            // 2. 淡出并禁用阴影
            shadowObject.GetComponent<Image>().DOFade(0f, 0.2f).OnComplete(() => shadowObject.SetActive(false));

            // 3. 淡出并禁用高光框
            selectorImage.DOFade(0f, 0.2f).OnComplete(() => selectorImage.gameObject.SetActive(false));
        }
    }
}