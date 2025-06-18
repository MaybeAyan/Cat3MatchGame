using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Random = UnityEngine.Random;
using DG.Tweening.Core.Easing;

public class GameBoard : MonoBehaviour
{
    public static GameBoard Instance;

    [Header("棋盘参数")]
    public int width = 8;
    public int height = 8;
    public GameObject tilePrefab;

    [Header("视觉与动画")]
    public float animationDuration = 0.3f;
    public float fixedSpacing = 10f;
    public List<Sprite> tileSprites = new List<Sprite>();

    [Header("资源引用")]
    public AudioClip swapSound;
    public AudioClip matchSound;
    public GameObject destructionEffectPrefab;

    [Header("UI引用")]
    public RectTransform tileGridPanel;
    public Transform bottomLeftAnchor;
    public Transform topRightAnchor;

    // GameBoard内部状态，只关心能否操作
    public enum GameState { move, wait }
    public GameState currentState;

    public GameObject[,] allTiles;
    private List<GameObject> matchedTiles = new List<GameObject>();
    private float dynamicTileSize;
    public Tile selectedTile;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // --- 公共接口，供其他管理者调用 ---

    /// <summary>
    /// 由GameManager调用的公共方法，用于设置棋盘是否可操作
    /// </summary>
    public void SetBoardState(GameState newState)
    {
        currentState = newState;
    }

    /// <summary>
    /// 由AI调用的公共方法，用于安全地获取棋盘的快照以进行模拟
    /// </summary>
    public Sprite[,] GetSpriteGridForSimulation()
    {
        Sprite[,] grid = new Sprite[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (allTiles[x, y] != null)
                {
                    grid[x, y] = allTiles[x, y].GetComponent<Tile>().squareSprite.sprite;
                }
            }
        }
        return grid;
    }

    /// <summary>
    /// 初始化棋盘，完成后通知GameManager
    /// </summary>
    public void SetupBoard()
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
                SetupNewTileAt(x, y, true);
            }
        }

        // 棋盘设置完毕，通知GameManager可以开始第一个回合了
        GameManager.Instance.OnBoardSetupComplete();
    }

    /// <summary>
    /// 玩家点击方块的入口
    /// </summary>
    public void TileClicked(Tile clickedTile)
    {
        if (currentState != GameState.move) return;

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
            else if (AreTilesAdjacent(selectedTile, clickedTile))
            {
                StartCoroutine(SwapAndCheckRoutine(selectedTile, clickedTile));
            }
            else
            {
                selectedTile.SetSelected(false);
                selectedTile = clickedTile;
                selectedTile.SetSelected(true);
            }
        }
    }

    // --- 核心协程 ---

    public IEnumerator SwapAndCheckRoutine(Tile tile1, Tile tile2)
    {
        Debug.Log(tile1);
        Debug.Log(tile2);

        if (selectedTile != null)
        {
            selectedTile.SetSelected(false);
            selectedTile = null;
        }

        currentState = GameState.wait;

        // --- 修正后的交换逻辑 ---
        GameObject tile1GO = tile1.gameObject;
        GameObject tile2GO = tile2.gameObject;
        Vector2 tile1Pos = GetPositionForTile(tile1.x, tile1.y);
        Vector2 tile2Pos = GetPositionForTile(tile2.x, tile2.y);

        // 1. 先交换数据数组中的引用
        allTiles[tile1.x, tile1.y] = tile2GO;
        allTiles[tile2.x, tile2.y] = tile1GO;

        // 2. 再交换Tile脚本内部的坐标数据
        (tile1.x, tile2.x) = (tile2.x, tile1.x);
        (tile1.y, tile2.y) = (tile2.y, tile1.y);

        // 3. 最后播放动画，移动到对方交换前的位置
        AudioManager.Instance.PlaySFX(swapSound);
        Sequence swapSequence = DOTween.Sequence();
        swapSequence.Join(tile1.GetComponent<RectTransform>().DOAnchorPos(tile2Pos, animationDuration))
                    .Join(tile2.GetComponent<RectTransform>().DOAnchorPos(tile1Pos, animationDuration));
        yield return swapSequence.WaitForCompletion();

        // --- 检查匹配与后续流程 ---
        bool extraTurn = FindAllMatches();

        Debug.Log(matchedTiles.Count);

        if (matchedTiles.Count > 0)
        {
            yield return StartCoroutine(DestroyAndRefillRoutine());
            // 【重要】操作成功，向GameManager汇报结果
            GameManager.Instance.OnMoveFinished(extraTurn);
        }
        else
        {
            // 无效交换，换回来 (使用完全相反的逻辑)
            allTiles[tile1.x, tile1.y] = tile2GO;
            allTiles[tile2.x, tile2.y] = tile1GO;
            (tile1.x, tile2.x) = (tile2.x, tile1.x);
            (tile1.y, tile2.y) = (tile2.y, tile1.y);

            Sequence swapBackSequence = DOTween.Sequence();
            swapBackSequence.Join(tile1.GetComponent<RectTransform>().DOAnchorPos(tile1Pos, animationDuration))
                            .Join(tile2.GetComponent<RectTransform>().DOAnchorPos(tile2Pos, animationDuration));
            yield return swapBackSequence.WaitForCompletion();
            currentState = GameState.move;
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

    private IEnumerator CollapseAndRefillRoutine()
    {
        List<GameObject> tilesToAnimate = new List<GameObject>();
        for (int x = 0; x < width; x++)
        {
            for (int writeIndex = 0, readIndex = 0; readIndex < height; readIndex++)
            {
                if (allTiles[x, readIndex] != null)
                {
                    if (writeIndex != readIndex)
                    {
                        GameObject tileToMove = allTiles[x, readIndex];
                        allTiles[x, writeIndex] = tileToMove;
                        allTiles[x, readIndex] = null;
                        tileToMove.GetComponent<Tile>().y = writeIndex;
                        tilesToAnimate.Add(tileToMove);
                    }
                    writeIndex++;
                }
            }
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (allTiles[x, y] == null)
                {
                    GameObject newTile = SetupNewTileAt(x, y, false);
                    tilesToAnimate.Add(newTile);
                    RectTransform rect = newTile.GetComponent<RectTransform>();
                    Vector2 finalPos = GetPositionForTile(x, y);
                    rect.anchoredPosition = new Vector2(finalPos.x, topRightAnchor.localPosition.y + dynamicTileSize);
                }
            }
        }

        if (tilesToAnimate.Count > 0)
        {
            Sequence sequence = DOTween.Sequence();
            foreach (var tileObject in tilesToAnimate)
            {
                Tile tileScript = tileObject.GetComponent<Tile>();
                Vector2 targetPosition = GetPositionForTile(tileScript.x, tileScript.y);
                sequence.Join(tileObject.GetComponent<RectTransform>().DOAnchorPos(targetPosition, animationDuration).SetEase(Ease.OutBounce));
            }
            yield return sequence.WaitForCompletion();
        }

        FindAllMatches();
    }

    private IEnumerator DestroyMatches()
    {
        if (matchedTiles.Count > 0) AudioManager.Instance.PlaySFX(matchSound);

        Sequence destroySequence = DOTween.Sequence();
        foreach (GameObject tile in matchedTiles)
        {
            if (tile != null)
            {
                if (destructionEffectPrefab != null) Instantiate(destructionEffectPrefab, tile.transform.position, Quaternion.identity);
                destroySequence.Join(tile.transform.DOScale(0f, animationDuration).SetEase(Ease.InBack));
            }
        }
        yield return destroySequence.WaitForCompletion();

        foreach (GameObject tile in matchedTiles)
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

    // --- 私有辅助方法 ---

    private bool FindAllMatches()
    {
        bool extraTurnGranted = false;
        matchedTiles.Clear();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width - 2;)
            {
                GameObject tile1 = allTiles[x, y];
                if (tile1 == null) { x++; continue; }
                Sprite s1 = tile1.GetComponent<Tile>().squareSprite.sprite;

                if (allTiles[x + 1, y] != null && allTiles[x + 2, y] != null &&
                    allTiles[x + 1, y].GetComponent<Tile>().squareSprite.sprite == s1 &&
                    allTiles[x + 2, y].GetComponent<Tile>().squareSprite.sprite == s1)
                {
                    int matchLength = 3;
                    for (int i = x + 3; i < width; i++)
                    {
                        if (allTiles[i, y] != null && allTiles[i, y].GetComponent<Tile>().squareSprite.sprite == s1) matchLength++;
                        else break;
                    }
                    if (matchLength >= 4) extraTurnGranted = true;
                    for (int i = 0; i < matchLength; i++)
                    {
                        if (!matchedTiles.Contains(allTiles[x + i, y])) matchedTiles.Add(allTiles[x + i, y]);
                    }
                    x += matchLength;
                }
                else
                {
                    x++;
                }
            }
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height - 2;)
            {
                GameObject tile1 = allTiles[x, y];
                if (tile1 == null) { y++; continue; }
                Sprite s1 = tile1.GetComponent<Tile>().squareSprite.sprite;

                if (allTiles[x, y + 1] != null && allTiles[x, y + 2] != null &&
                    allTiles[x, y + 1].GetComponent<Tile>().squareSprite.sprite == s1 &&
                    allTiles[x, y + 2].GetComponent<Tile>().squareSprite.sprite == s1)
                {
                    int matchLength = 3;
                    for (int i = y + 3; i < height; i++)
                    {
                        if (allTiles[x, i] != null && allTiles[x, i].GetComponent<Tile>().squareSprite.sprite == s1) matchLength++;
                        else break;
                    }
                    if (matchLength >= 4) extraTurnGranted = true;
                    for (int i = 0; i < matchLength; i++)
                    {
                        if (!matchedTiles.Contains(allTiles[x, y + i])) matchedTiles.Add(allTiles[x, y + i]);
                    }
                    y += matchLength;
                }
                else
                {
                    y++;
                }
            }
        }

        if (matchedTiles.Count >= 5) extraTurnGranted = true;
        return extraTurnGranted;
    }

    private GameObject SetupNewTileAt(int x, int y, bool checkInitialMatches)
    {
        GameObject tileObject = Instantiate(tilePrefab, tileGridPanel);
        tileObject.name = $"Tile ({x},{y})";
        allTiles[x, y] = tileObject;
        Tile tileScript = tileObject.GetComponent<Tile>();

        List<Sprite> possibleSprites = new List<Sprite>(tileSprites);
        if (checkInitialMatches)
        {
            if (x > 1)
            {
                Sprite left1 = allTiles[x - 1, y].GetComponent<Tile>().squareSprite.sprite;
                if (allTiles[x - 2, y].GetComponent<Tile>().squareSprite.sprite == left1) possibleSprites.Remove(left1);
            }
            if (y > 1)
            {
                Sprite down1 = allTiles[x, y - 1].GetComponent<Tile>().squareSprite.sprite;
                if (allTiles[x, y - 2].GetComponent<Tile>().squareSprite.sprite == down1) possibleSprites.Remove(down1);
            }
        }
        Sprite newSprite = possibleSprites.Count > 0 ? possibleSprites[Random.Range(0, possibleSprites.Count)] : tileSprites[Random.Range(0, tileSprites.Count)];

        if (tileScript != null)
        {
            tileScript.squareSprite.sprite = newSprite;
            tileScript.roundedSprite.sprite = newSprite;
            tileScript.squareSprite.color = Color.white;
            tileScript.roundedSprite.color = new Color(1, 1, 1, 0);
        }

        RectTransform rectTransform = tileObject.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(dynamicTileSize, dynamicTileSize);
        rectTransform.anchoredPosition = GetPositionForTile(x, y);

        if (tileScript != null)
        {
            tileScript.x = x;
            tileScript.y = y;
            tileScript.board = this;
        }
        return tileObject;
    }

    private Vector2 GetPositionForTile(int x, int y)
    {
        float posX = bottomLeftAnchor.localPosition.x + x * (dynamicTileSize + fixedSpacing);
        float posY = bottomLeftAnchor.localPosition.y + y * (dynamicTileSize + fixedSpacing);
        return new Vector2(posX, posY);
    }

    private bool AreTilesAdjacent(Tile tile1, Tile tile2)
    {
        if (tile1 == null || tile2 == null) return false;
        return (Mathf.Abs(tile1.x - tile2.x) + Mathf.Abs(tile1.y - tile2.y)) == 1;
    }

    public IEnumerator CheckBoardStateRoutine()
    {
        currentState = GameState.wait;
        // AI的大脑现在在 EnemyAI 脚本里
        List<Move> possibleMoves = EnemyAI.Instance.FindAllPossibleMoves();
        if (possibleMoves.Count == 0)
        {
            yield return StartCoroutine(ReshuffleBoardRoutine());
        }
        currentState = GameState.move;
    }

    private IEnumerator ReshuffleBoardRoutine()
    {
        UIManager.Instance.ShowTurnIndicator("正在洗牌...", Color.magenta);
        Sequence shrinkSequence = DOTween.Sequence();
        foreach (var tileObject in allTiles)
        {
            if (tileObject != null)
            {
                shrinkSequence.Join(tileObject.transform.DOScale(0, animationDuration).SetEase(Ease.InBack));
            }
        }
        yield return shrinkSequence.WaitForCompletion();

        List<GameObject> allExistingTiles = new List<GameObject>();
        for (int x = 0; x < width; x++) for (int y = 0; y < height; y++) if (allTiles[x, y] != null) allExistingTiles.Add(allTiles[x, y]);

        for (int i = 0; i < allExistingTiles.Count - 1; i++)
        {
            int randomIndex = Random.Range(i, allExistingTiles.Count);
            (allExistingTiles[randomIndex], allExistingTiles[i]) = (allExistingTiles[i], allExistingTiles[randomIndex]);
        }

        int tileIndex = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                allTiles[x, y] = allExistingTiles[tileIndex];
                Tile tileScript = allTiles[x, y].GetComponent<Tile>();
                tileScript.x = x;
                tileScript.y = y;
                allTiles[x, y].GetComponent<RectTransform>().anchoredPosition = GetPositionForTile(x, y);
                tileIndex++;
            }
        }

        // 确保洗牌后没有立刻能消除的
        FindAllMatches();
        if (matchedTiles.Count > 0)
        {
            Debug.LogWarning("洗牌后产生了新的匹配，正在重新洗牌...");
            yield return StartCoroutine(ReshuffleBoardRoutine());
            yield break;
        }

        Sequence growSequence = DOTween.Sequence();
        foreach (var tileObject in allTiles)
        {
            if (tileObject != null)
            {
                growSequence.Join(tileObject.transform.DOScale(1, animationDuration).SetEase(Ease.OutBack));
            }
        }
        yield return growSequence.WaitForCompletion();
    }
}
