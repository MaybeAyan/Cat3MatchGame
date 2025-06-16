using UnityEngine;
using DG.Tweening;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Tile : MonoBehaviour,IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    // --- 基础数据 ---
    public int x;
    public int y;
    public GameBoard board;
    
    [Header("分层动画组件")]
    public Transform tileGfxTransform; 
    public Image squareSprite;    // <-- 从 SpriteRenderer 改为 Image
    public Image roundedSprite;   // <-- 从 SpriteRenderer 改为 Image
    public GameObject shadowObject;

    // 输入处理相关变量
    [Header("输入设置")]
    [Tooltip("鼠标拖拽多远才被视为一次“滑动”而不是“点击”")]
    public float dragThreshold = 50f; // 注意：这里我们用屏幕像素距离，更稳定
    private Vector2 mouseDownPosition;
    private Vector2 mouseUpPosition;

    private Sequence breathingAnimation;
    
    // 这个方法会替代 OnMouseDown
    public void OnPointerDown(PointerEventData eventData)
    {
        // 记录鼠标按下的屏幕位置（虽然是UI，但这个逻辑依然有效）
        mouseDownPosition = eventData.position;
        Debug.Log("点击了棋盘");
    }

    // 这个方法会替代 OnMouseUp
    public void OnPointerUp(PointerEventData eventData)
    {
        mouseUpPosition = eventData.position;
        float swipeDistance = Vector2.Distance(mouseDownPosition, mouseUpPosition);

        // 后续逻辑和之前完全一样，判断是点击还是滑动
        if (swipeDistance < dragThreshold)
        {
            if (board != null)
            {
                board.TileClicked(this);
            }
        }
        else
        {
            HandleSwipe();
        }
    }

    // 你甚至可以实现 IDragHandler 接口来处理拖拽过程中的事件
    public void OnDrag(PointerEventData eventData)
    {
        // 可以在这里做一些拖拽过程中的视觉效果
    }

    
    private void HandleSwipe()
    {
        Vector2 swipeDirection = mouseUpPosition - mouseDownPosition;
        float angle = Mathf.Atan2(swipeDirection.y, swipeDirection.x) * Mathf.Rad2Deg;

        int targetX = x, targetY = y;

        if (angle > -45 && angle <= 45) // 右滑
        {
            targetX++;
        }
        else if (angle > 45 && angle <= 135) // 上滑
        {
            targetY++;
        }
        else if (angle > 135 || angle <= -135) // 左滑
        {
            targetX--;
        }
        else if (angle < -45 && angle >= -135) // 下滑
        {
            targetY--;
        }

        // 检查目标位置是否有效，如果有效则请求交换
        if (targetX >= 0 && targetX < board.width && targetY >= 0 && targetY < board.height)
        {
            board.RequestSwap(x, y, targetX, targetY);
        }
    }

    // 设置选中状态的动画（保持不变）
    public void SetSelected(bool isSelected)
    {
        if (breathingAnimation != null && breathingAnimation.IsActive())
        {
            breathingAnimation.Kill();
        }

        tileGfxTransform.DOKill();
        squareSprite.DOKill();
        roundedSprite.DOKill();

        if (isSelected)
        {
            shadowObject.SetActive(true);
            roundedSprite.gameObject.SetActive(true);

            Sequence selectionSequence = DOTween.Sequence();
            
            selectionSequence
                .Join(squareSprite.DOFade(0f, 0.2f))
                .Join(roundedSprite.DOFade(1f, 0.2f))
                .Join(tileGfxTransform.DOLocalRotate(new Vector3(-10, 0, 0), 0.2f))
                .Join(tileGfxTransform.DOScale(1.1f, 0.2f));

            selectionSequence.OnComplete(() =>
            {
                breathingAnimation = DOTween.Sequence();
                breathingAnimation.Append(tileGfxTransform.DOScale(1.05f, 0.7f))
                                  .SetLoops(-1, LoopType.Yoyo)
                                  .SetEase(Ease.InOutSine);
            });
        }
        else
        {
            Sequence deselectionSequence = DOTween.Sequence();
            
            deselectionSequence
                .Join(squareSprite.DOFade(1f, 0.2f))
                .Join(roundedSprite.DOFade(0f, 0.2f))
                .Join(tileGfxTransform.DOLocalRotate(Vector3.zero, 0.2f))
                .Join(tileGfxTransform.DOScale(1f, 0.2f));

            deselectionSequence.OnComplete(() =>
            {
                roundedSprite.gameObject.SetActive(false);
                shadowObject.SetActive(false);
            });
        }
    }
}