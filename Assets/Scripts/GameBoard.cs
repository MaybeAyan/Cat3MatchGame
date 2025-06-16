using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Random = UnityEngine.Random;

public class GameBoard : MonoBehaviour
{
    public static GameBoard Instance;

    public int width = 8;
    public int height = 8;
    public GameObject tilePrefab;

    [Header("动画参数")]
    public float animationDuration = 0.3f;

    [Header("音频文件")]
    public AudioClip swapSound;
    public AudioClip matchSound;

    [Header("特效预制体")]
    public GameObject destructionEffectPrefab;

    [Header("视觉调整")]
    public float fixedSpacing = 10f; 

    [Header("UI布局引用")]
    public RectTransform tileGridPanel;
    public Transform bottomLeftAnchor;
    public Transform topRightAnchor;


    [Header("方块美术资源")]
    public List<Sprite> tileSprites = new List<Sprite>();

    private GameObject[,] allTiles;
    private List<GameObject> matchedTiles = new List<GameObject>();
    private float dynamicTileSize; // 用于存储动态计算出的格子大小
    public Tile selectedTile;

    public enum GameState { move, wait }
    public GameState currentState = GameState.move;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("场景中发现多个 GameBoard 实例！");
        }
    }

    void Start()
    {
        SetupBoard();
    }

    private Vector2 GetPositionForTile(int x, int y)
    {
        // 位置 = 左下角锚点 + x * (格子大小 + 间距)
        float posX = bottomLeftAnchor.localPosition.x + x * (dynamicTileSize + fixedSpacing);
        float posY = bottomLeftAnchor.localPosition.y + y * (dynamicTileSize + fixedSpacing);
        return new Vector2(posX, posY);
    }

    void SetupBoard()
    {
        allTiles = new GameObject[width, height];

        float areaWidth = topRightAnchor.localPosition.x - bottomLeftAnchor.localPosition.x;
        float totalSpacingWidth = fixedSpacing * (width - 1);
        float effectiveWidthForTiles = areaWidth - totalSpacingWidth;
        this.dynamicTileSize = effectiveWidthForTiles / width;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // 直接调用我们的新方法
                SetupNewTileAt(x, y);
            }
        }
    }

    private GameObject SetupNewTileAt(int x, int y)
    {
        GameObject tileObject = Instantiate(tilePrefab, tileGridPanel);
        tileObject.name = $"Tile ({x},{y})";

        allTiles[x, y] = tileObject;
        Tile tileScript = tileObject.GetComponent<Tile>();

        // --- 核心改动：从颜色逻辑切换到图片逻辑 ---

        // 1. 从我们的图片列表中随机选择一个Sprite
        if (tileSprites != null && tileSprites.Count > 0)
        {
            int spriteIndex = Random.Range(0, tileSprites.Count);
            Sprite newSprite = tileSprites[spriteIndex];

            // 2. 将这个Sprite赋给Image组件
            if (tileScript != null)
            {
                tileScript.squareSprite.sprite = newSprite;
                tileScript.roundedSprite.sprite = newSprite; // 圆角和直角的图片要保持一致
                                                             // 将Image颜色设为白色，以显示图片的原始颜色
                tileScript.squareSprite.color = Color.white;
                tileScript.roundedSprite.color = new Color(1, 1, 1, 0); // 保持透明
            }
        }

        // 3. 设置大小和位置
        RectTransform rectTransform = tileObject.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(dynamicTileSize, dynamicTileSize);
        rectTransform.anchoredPosition = GetPositionForTile(x, y);

        // 4. 设置数据
        if (tileScript != null)
        {
            tileScript.x = x;
            tileScript.y = y;
            tileScript.board = this;
        }

        return tileObject;
    }

    private IEnumerator CollapseAndRefillRoutine()
    {
        List<Tile> movingTiles = new List<Tile>();

        // 步骤1：数据下落，并收集需要移动的旧方块
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (allTiles[x, y] == null)
                {
                    for (int y2 = y + 1; y2 < height; y2++)
                    {
                        if (allTiles[x, y2] != null)
                        {
                            GameObject tileToMove = allTiles[x, y2];
                            allTiles[x, y] = tileToMove;
                            allTiles[x, y2] = null;

                            Tile tileScript = tileToMove.GetComponent<Tile>();
                            tileScript.y = y;
                            movingTiles.Add(tileScript);
                            break;
                        }
                    }
                }
            }
        }

        // --- 【动画修改】第一阶段：只播放旧方块的下落动画 ---
        if (movingTiles.Count > 0)
        {
            Sequence collapseSequence = DOTween.Sequence();
            foreach (var tile in movingTiles)
            {
                Vector2 targetPosition = GetPositionForTile(tile.x, tile.y);
                collapseSequence.Join(tile.GetComponent<RectTransform>().DOAnchorPos(targetPosition, animationDuration).SetEase(Ease.OutBounce));
            }
            yield return collapseSequence.WaitForCompletion();
        }


        // --- 【动画修改】第二阶段：在下落动画完成后，再处理新方块的填充和掉落 ---
        List<Tile> newTiles = new List<Tile>();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (allTiles[x, y] == null)
                {
                    // 只创建和设置数据，动画稍后统一处理
                    GameObject tileObject = Instantiate(tilePrefab, tileGridPanel);
                    tileObject.name = $"Tile ({x},{y})";
                    tileObject.GetComponent<RectTransform>().sizeDelta = new Vector2(dynamicTileSize, dynamicTileSize);
                    allTiles[x, y] = tileObject;
                    Tile newTileScript = tileObject.GetComponent<Tile>();
                    #region New Tile Setup
                    Sprite newSprite = tileSprites[Random.Range(0, tileSprites.Count)];
                    if (newTileScript != null)
                    {
                        newTileScript.squareSprite.sprite = newSprite;
                        newTileScript.roundedSprite.sprite = newSprite;
                        newTileScript.squareSprite.color = Color.white;
                        newTileScript.roundedSprite.color = new Color(1, 1, 1, 0);
                        newTileScript.x = x;
                        newTileScript.y = y;
                        newTileScript.board = this;
                    }
                    #endregion
                    newTiles.Add(newTileScript);
                }
            }
        }

        if (newTiles.Count > 0)
        {
            Sequence refillSequence = DOTween.Sequence();
            foreach (var tile in newTiles)
            {
                RectTransform rectTransform = tile.GetComponent<RectTransform>();
                Vector2 finalPos = GetPositionForTile(tile.x, tile.y);
                rectTransform.anchoredPosition = new Vector2(finalPos.x, topRightAnchor.localPosition.y + 100);
                refillSequence.Join(rectTransform.DOAnchorPos(finalPos, animationDuration).SetEase(Ease.OutBounce));
            }
            yield return refillSequence.WaitForCompletion();
        }

        FindAllMatches();
    }

    // 【回归】手动动画的SwapAndCheckRoutine
    private IEnumerator SwapAndCheckRoutine(int x1, int y1, int x2, int y2)
    {
        currentState = GameState.wait;

        Tile tile1Script = allTiles[x1, y1].GetComponent<Tile>();
        Tile tile2Script = allTiles[x2, y2].GetComponent<Tile>();

        if (tile1Script != null && tile2Script != null)
        {
            allTiles[x1, y1] = tile2Script.gameObject;
            allTiles[x2, y2] = tile1Script.gameObject;
            tile1Script.x = x2; tile1Script.y = y2;
            tile2Script.x = x1; tile2Script.y = y1;

            Vector2 tile1Pos = tile1Script.GetComponent<RectTransform>().anchoredPosition;
            Vector2 tile2Pos = tile2Script.GetComponent<RectTransform>().anchoredPosition;

            Sequence swapSequence = DOTween.Sequence();
            swapSequence.Join(tile1Script.GetComponent<RectTransform>().DOAnchorPos(tile2Pos, animationDuration))
                        .Join(tile2Script.GetComponent<RectTransform>().DOAnchorPos(tile1Pos, animationDuration));
            yield return swapSequence.WaitForCompletion();

            FindAllMatches();

            if (matchedTiles.Count > 0)
            {
                yield return StartCoroutine(DestroyAndRefillRoutine());
            }
            else
            {
                // 如果没有匹配，数据和动画都换回来，而且播放音效；
                AudioManager.Instance.PlaySFX(swapSound);

                allTiles[x1, y1] = tile1Script.gameObject;
                allTiles[x2, y2] = tile2Script.gameObject;
                tile1Script.x = x1; tile1Script.y = y1;
                tile2Script.x = x2; tile2Script.y = y2;

                Sequence swapBackSequence = DOTween.Sequence();
                swapBackSequence.Join(tile1Script.GetComponent<RectTransform>().DOAnchorPos(tile1Pos, animationDuration))
                                .Join(tile2Script.GetComponent<RectTransform>().DOAnchorPos(tile2Pos, animationDuration));
                yield return swapBackSequence.WaitForCompletion();
            }
        }

        currentState = GameState.move;
    }

    // 其他方法保持我们之前修复过的最终版本即可
    #region Other Methods
    private Color GetPastelColorByIndex(int index)
    {
        switch (index)
        {
            case 0: return new Color(0.88f, 0.68f, 0.68f);
            case 1: return new Color(0.68f, 0.88f, 0.68f);
            case 2: return new Color(0.68f, 0.82f, 0.88f);
            case 3: return new Color(0.88f, 0.87f, 0.68f);
            case 4: return new Color(0.8f, 0.68f, 0.88f);
            default: return Color.white;
        }
    }

    private bool AreTilesAdjacent(Tile tile1, Tile tile2)
    {
        if (tile1 == null || tile2 == null) return false;
        return (Mathf.Abs(tile1.x - tile2.x) + Mathf.Abs(tile1.y - tile2.y)) == 1;
    }

    public void TileClicked(Tile clickedTile)
    {
        if (currentState != GameState.move) { return; }
        if (selectedTile == null)
        {
            selectedTile = clickedTile;
            selectedTile.SetSelected(true);
        }
        else
        {
            if (selectedTile == clickedTile)
            {
                selectedTile.SetSelected(false);
                selectedTile = null;
            }
            else
            {
                if (AreTilesAdjacent(selectedTile, clickedTile))
                {
                    selectedTile.SetSelected(false);
                    StartCoroutine(SwapAndCheckRoutine(selectedTile.x, selectedTile.y, clickedTile.x, clickedTile.y));
                    selectedTile = null;
                }
                else
                {
                    selectedTile.SetSelected(false);
                    selectedTile = clickedTile;
                    selectedTile.SetSelected(true);
                }
            }
        }
    }

    public void RequestSwap(int x1, int y1, int x2, int y2)
    {
        if (currentState == GameState.move)
        {
            StartCoroutine(SwapAndCheckRoutine(x1, y1, x2, y2));
        }
    }

    private IEnumerator DestroyAndRefillRoutine()
    {
        while (matchedTiles.Count > 0)
        {
            yield return StartCoroutine(DestroyMatches());
            yield return StartCoroutine(CollapseAndRefillRoutine());
        }
    }

    private void FindAllMatches()
    {
        matchedTiles.Clear();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (allTiles[x, y] == null) continue;

                // 【修改】获取当前格子的Sprite
                Tile currentTileScript = allTiles[x, y].GetComponent<Tile>();
                if (currentTileScript == null || currentTileScript.squareSprite.sprite == null) continue;
                Sprite currentSprite = currentTileScript.squareSprite.sprite;

                // 水平检测
                if (x < width - 2)
                {
                    if (allTiles[x + 1, y] != null && allTiles[x + 2, y] != null &&
                        allTiles[x + 1, y].GetComponent<Tile>().squareSprite.sprite == currentSprite && // 比较Sprite
                        allTiles[x + 2, y].GetComponent<Tile>().squareSprite.sprite == currentSprite)   // 比较Sprite
                    {
                        if (!matchedTiles.Contains(allTiles[x, y])) matchedTiles.Add(allTiles[x, y]);
                        if (!matchedTiles.Contains(allTiles[x + 1, y])) matchedTiles.Add(allTiles[x + 1, y]);
                        if (!matchedTiles.Contains(allTiles[x + 2, y])) matchedTiles.Add(allTiles[x + 2, y]);
                    }
                }

                // 垂直检测
                if (y < height - 2)
                {
                    if (allTiles[x, y + 1] != null && allTiles[x, y + 2] != null &&
                        allTiles[x, y + 1].GetComponent<Tile>().squareSprite.sprite == currentSprite && // 比较Sprite
                        allTiles[x, y + 2].GetComponent<Tile>().squareSprite.sprite == currentSprite)   // 比较Sprite
                    {
                        if (!matchedTiles.Contains(allTiles[x, y])) matchedTiles.Add(allTiles[x, y]);
                        if (!matchedTiles.Contains(allTiles[x, y + 1])) matchedTiles.Add(allTiles[x, y + 1]);
                        if (!matchedTiles.Contains(allTiles[x, y + 2])) matchedTiles.Add(allTiles[x, y + 2]);
                    }
                }
            }
        }
    }

    private IEnumerator DestroyMatches()
    {
        List<GameObject> tilesToDestroy = new List<GameObject>(matchedTiles);
        matchedTiles.Clear();

        if (tilesToDestroy.Count > 0)
        {
            AudioManager.Instance.PlaySFX(matchSound);
        }

        Sequence destroySequence = DOTween.Sequence();

        foreach (GameObject tile in tilesToDestroy)
        {
            if (tile != null)
            {
                if (destructionEffectPrefab != null)
                {
                    Instantiate(destructionEffectPrefab, tile.transform.position, Quaternion.identity);
                }

                destroySequence.Join(tile.transform.DOScale(0f, animationDuration).SetEase(Ease.InBack));
            }
        }

        yield return destroySequence.WaitForCompletion();

        foreach (GameObject tile in tilesToDestroy)
        {
            if (tile != null)
            {
                Tile tileScript = tile.GetComponent<Tile>();
                if (allTiles[tileScript.x, tileScript.y] == tile)
                {
                    allTiles[tileScript.x, tileScript.y] = null;
                }
                Destroy(tile);
            }
        }
    }
    #endregion
}