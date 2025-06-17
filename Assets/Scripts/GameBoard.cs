using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Random = UnityEngine.Random;


public struct Move
{
    public int x1, y1, x2, y2;
    public int score; // 这次移动评出的分数；
}

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

    public enum Turn { Player, Enemy }
    public Turn currentTurn;
    private bool extraTurnGranted = false; // 用于标记是否获得了额外回合

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
        // 游戏开始时，设置为玩家回合
        currentTurn = Turn.Player;
        UIManager.Instance.ShowTurnIndicator("你的回合", Color.cyan);
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

    // 在 GameBoard.cs 中

    private GameObject SetupNewTileAt(int x, int y)
    {
        GameObject tileObject = Instantiate(tilePrefab, tileGridPanel);
        tileObject.name = $"Tile ({x},{y})";

        allTiles[x, y] = tileObject;
        Tile tileScript = tileObject.GetComponent<Tile>();

        // --- 核心改动：避免开局匹配的逻辑 ---
        if (tileSprites != null && tileSprites.Count > 0)
        {
            // 1. 创建一个当前位置可用的Sprite列表
            List<Sprite> possibleSprites = new List<Sprite>(tileSprites);

            // 2. 检查左侧是否形成三连
            if (x > 1)
            {
                Sprite left1 = allTiles[x - 1, y].GetComponent<Tile>().squareSprite.sprite;
                Sprite left2 = allTiles[x - 2, y].GetComponent<Tile>().squareSprite.sprite;
                if (left1 == left2)
                {
                    // 如果左边两个Sprite相同，则从可用列表中移除该Sprite
                    possibleSprites.Remove(left1);
                }
            }

            // 3. 检查下方是否形成三连
            if (y > 1)
            {
                Sprite down1 = allTiles[x, y - 1].GetComponent<Tile>().squareSprite.sprite;
                Sprite down2 = allTiles[x, y - 2].GetComponent<Tile>().squareSprite.sprite;
                if (down1 == down2)
                {
                    // 如果下方两个Sprite相同，则从可用列表中移除该Sprite
                    possibleSprites.Remove(down1);
                }
            }

            // 4. 从“安全”的列表中随机选择一个Sprite
            int spriteIndex = Random.Range(0, possibleSprites.Count);
            Sprite newSprite = possibleSprites[spriteIndex];

            // 5. 将这个安全的Sprite赋给Image组件
            if (tileScript != null)
            {
                tileScript.squareSprite.sprite = newSprite;
                tileScript.roundedSprite.sprite = newSprite;
                tileScript.squareSprite.color = Color.white;
                tileScript.roundedSprite.color = new Color(1, 1, 1, 0);
            }
        }

        // 设置大小和位置
        RectTransform rectTransform = tileObject.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(dynamicTileSize, dynamicTileSize);
        rectTransform.anchoredPosition = GetPositionForTile(x, y);

        // 设置数据
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

    private IEnumerator SwapAndCheckRoutine(int x1, int y1, int x2, int y2)
    {
        extraTurnGranted = false;
        currentState = GameState.wait;

        Tile tile1Script = allTiles[x1, y1].GetComponent<Tile>();
        Tile tile2Script = allTiles[x2, y2].GetComponent<Tile>();

        if (tile1Script != null && tile2Script != null)
        {
            // ... (交换数据和播放动画的部分不变) ...
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
                // --- 成功匹配的逻辑 ---
                yield return StartCoroutine(DestroyAndRefillRoutine());

                // 在所有连锁结束后，再判断是否要切换回合
                if (extraTurnGranted)
                {
                    Debug.Log("<color=yellow>获得额外回合！</color>");
                    // 在获得额外回合后，也需要检查一下棋盘是否陷入死局
                    yield return StartCoroutine(CheckBoardStateRoutine());
                }
                else
                {
                    // 没有额外回合，切换回合
                    SwitchTurn();
                }
            }
            else
            {
                // --- 【修复】交换失败的逻辑 ---
                AudioManager.Instance.PlaySFX(swapSound);

                // 数据和动画都换回来
                allTiles[x1, y1] = tile1Script.gameObject;
                allTiles[x2, y2] = tile2Script.gameObject;
                tile1Script.x = x1; tile1Script.y = y1;
                tile2Script.x = x2; tile2Script.y = y2;

                Sequence swapBackSequence = DOTween.Sequence();
                swapBackSequence.Join(tile1Script.GetComponent<RectTransform>().DOAnchorPos(tile1Pos, animationDuration))
                                .Join(tile2Script.GetComponent<RectTransform>().DOAnchorPos(tile2Pos, animationDuration));
                yield return swapBackSequence.WaitForCompletion();

                // 无效交换，不切换回合，直接把操作权还给当前玩家
                currentState = GameState.move;
            }
        }
    }

    private void SwitchTurn()
    {
        if (currentTurn == Turn.Player)
        {
            Debug.Log("玩家回合结束，切换到敌人回合。");
            currentTurn = Turn.Enemy;
            UIManager.Instance.ShowTurnIndicator("敌人回合", Color.red);
            StartCoroutine(EnemyTurnRoutine());
        }
        else
        {
            Debug.Log("敌人回合结束，切换到玩家回合。");
            currentTurn = Turn.Player;
            UIManager.Instance.ShowTurnIndicator("你的回合", Color.cyan);
            currentState = GameState.move;
            StartCoroutine(CheckBoardStateRoutine()); // 在玩家开始前检查棋盘
        }
    }

    // 模拟敌人回合
    private IEnumerator EnemyTurnRoutine()
    {
        currentState = GameState.wait;
        Debug.Log("<color=orange>敌人的回合开始...</color>");

        // 使用do-while循环，让AI可以连续行动
        do
        {
            // 在AI的每一次具体行动前，都重置“额外回合”标志
            extraTurnGranted = false;

            // 【新增】AI行动前也检查一下死局
            yield return StartCoroutine(CheckBoardStateRoutine());

            // 模拟思考过程
            yield return new WaitForSeconds(0.75f);

            // 1. 找到所有可行的移动
            List<Move> possibleMoves = FindAllPossibleMoves();

            if (possibleMoves.Count > 0)
            {
                // 2. 找出分数最高的最佳移动
                Move bestMove = new Move();
                int bestScore = -1; // 初始化为-1，确保任何有效移动都能被选中
                foreach (var move in possibleMoves)
                {
                    if (move.score > bestScore)
                    {
                        bestScore = move.score;
                        bestMove = move;
                    }
                }

                Debug.Log($"<color=red>AI找到了最佳移动！分数: {bestScore}</color>");

                // 3. 执行这个最佳移动
                // AI调用和玩家完全相同的交换方法，它产生的所有后续效果（连锁，额外回合）都会被正确处理
                yield return StartCoroutine(SwapAndCheckRoutine(bestMove.x1, bestMove.y1, bestMove.x2, bestMove.y2));
            }
            else
            {
                // 如果AI找不到任何可行的移动，结束它的回合
                Debug.LogWarning("AI没有找到任何可行的移动！");
                extraTurnGranted = false; // 确保退出循环
            }

            // 循环条件：如果上一步的SwapAndCheckRoutine将extraTurnGranted设为了true，则继续循环
        } while (extraTurnGranted);

        // 当AI的所有行动（包括所有额外回合）都结束后，才真正结束它的回合
        Debug.Log("<color=orange>敌人回合结束。</color>");

        // 将回合交还给玩家
        currentTurn = Turn.Player;
        currentState = GameState.move;
    }

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
        // 增加一个检查，确保只有在玩家回合且游戏不在等待状态时，才响应点击
        if (currentState != GameState.move || currentTurn != Turn.Player)
        {
            return;
        }

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
        if (currentTurn != Turn.Player) return;

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


    // 在 GameBoard.cs 中

    private void FindAllMatches()
    {
        matchedTiles.Clear();
        // extraTurnGranted 在 SwapAndCheckRoutine 开始时被重置，这里我们只负责将它设为true

        // --- 水平方向查找所有匹配 ---
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width - 2;)
            {
                GameObject tile1 = allTiles[x, y];
                if (tile1 == null)
                {
                    x++;
                    continue;
                }
                Sprite s1 = tile1.GetComponent<Tile>().squareSprite.sprite;

                if (allTiles[x + 1, y] != null && allTiles[x + 2, y] != null &&
                    allTiles[x + 1, y].GetComponent<Tile>().squareSprite.sprite == s1 &&
                    allTiles[x + 2, y].GetComponent<Tile>().squareSprite.sprite == s1)
                {
                    // 找到了一个基础的3连，现在继续向右延伸，看看能有多长
                    int matchLength = 3;
                    for (int i = x + 3; i < width; i++)
                    {
                        if (allTiles[i, y] != null && allTiles[i, y].GetComponent<Tile>().squareSprite.sprite == s1)
                        {
                            matchLength++;
                        }
                        else
                        {
                            break;
                        }
                    }

                    // 【判定】根据这条线的长度，决定是否奖励额外回合
                    if (matchLength >= 4)
                    {
                        extraTurnGranted = true;
                    }

                    // 将所有匹配的方块加入列表
                    for (int i = 0; i < matchLength; i++)
                    {
                        if (!matchedTiles.Contains(allTiles[x + i, y]))
                        {
                            matchedTiles.Add(allTiles[x + i, y]);
                        }
                    }
                    // 跳过已经检查过的方块，提高效率
                    x += matchLength;
                }
                else
                {
                    x++;
                }
            }
        }

        // --- 垂直方向查找所有匹配 ---
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height - 2;)
            {
                GameObject tile1 = allTiles[x, y];
                if (tile1 == null)
                {
                    y++;
                    continue;
                }
                Sprite s1 = tile1.GetComponent<Tile>().squareSprite.sprite;

                if (allTiles[x, y + 1] != null && allTiles[x, y + 2] != null &&
                    allTiles[x, y + 1].GetComponent<Tile>().squareSprite.sprite == s1 &&
                    allTiles[x, y + 2].GetComponent<Tile>().squareSprite.sprite == s1)
                {
                    int matchLength = 3;
                    for (int i = y + 3; i < height; i++)
                    {
                        if (allTiles[x, i] != null && allTiles[x, i].GetComponent<Tile>().squareSprite.sprite == s1)
                        {
                            matchLength++;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (matchLength >= 4)
                    {
                        extraTurnGranted = true;
                    }

                    for (int i = 0; i < matchLength; i++)
                    {
                        if (!matchedTiles.Contains(allTiles[x, y + i]))
                        {
                            matchedTiles.Add(allTiles[x, y + i]);
                        }
                    }
                    y += matchLength;
                }
                else
                {
                    y++;
                }
            }
        }

        // 【判定】对于L型和T型，它们的总数会大于等于5，这个最终检查依然是必要的
        if (matchedTiles.Count >= 5)
        {
            extraTurnGranted = true;
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


    // --- AI 核心逻辑 ---

    // 1. 寻找所有可能产生匹配的移动
    private List<Move> FindAllPossibleMoves()
    {
        List<Move> possibleMoves = new List<Move>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // 只检查与右边和上边的交换，避免重复
                if (x < width - 1)
                {
                    // 模拟与右边方块的交换
                    int score = ScoreMove(x, y, x + 1, y);
                    if (score > 0)
                    {
                        possibleMoves.Add(new Move { x1 = x, y1 = y, x2 = x + 1, y2 = y, score = score });
                    }
                }
                if (y < height - 1)
                {
                    // 模拟与上边方块的交换
                    int score = ScoreMove(x, y, x, y + 1);
                    if (score > 0)
                    {
                        possibleMoves.Add(new Move { x1 = x, y1 = y, x2 = x, y2 = y + 1, score = score });
                    }
                }
            }
        }
        return possibleMoves;
    }

    // 2. 为一次移动打分
    private int ScoreMove(int x1, int y1, int x2, int y2)
    {
        // 创建一个临时的Sprite数据副本，用于模拟
        Sprite[,] tempGrid = new Sprite[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (allTiles[x, y] != null)
                {
                    tempGrid[x, y] = allTiles[x, y].GetComponent<Tile>().squareSprite.sprite;
                }
            }
        }

        // 在副本上执行交换
        Sprite temp = tempGrid[x1, y1];
        tempGrid[x1, y1] = tempGrid[x2, y2];
        tempGrid[x2, y2] = temp;

        // 检查交换后的两个位置是否能产生匹配
        int totalScore = 0;
        totalScore += CheckMatchesAt(x1, y1, tempGrid).Count;
        totalScore += CheckMatchesAt(x2, y2, tempGrid).Count;

        // 如果一次交换能同时在两个地方引发消除（比如形成T或L型），给予额外加分
        if (CheckMatchesAt(x1, y1, tempGrid).Count > 0 && CheckMatchesAt(x2, y2, tempGrid).Count > 0)
        {
            totalScore += 10; // 奖励分数
        }

        return totalScore;
    }

    // 3. 检查一个点在模拟网格中能产生的匹配长度
    private List<GameObject> CheckMatchesAt(int x, int y, Sprite[,] grid)
    {
        List<GameObject> matches = new List<GameObject>();
        Sprite currentSprite = grid[x, y];
        if (currentSprite == null) return matches;

        // --- 水平检查（从(x,y)向左右两端延伸）---
        List<GameObject> horizontalMatches = new List<GameObject> { allTiles[x, y] };
        // 向左
        for (int i = x - 1; i >= 0; i--)
        {
            if (grid[i, y] == currentSprite) { horizontalMatches.Add(allTiles[i, y]); }
            else { break; }
        }
        // 向右
        for (int i = x + 1; i < width; i++)
        {
            if (grid[i, y] == currentSprite) { horizontalMatches.Add(allTiles[i, y]); }
            else { break; }
        }
        if (horizontalMatches.Count >= 3) matches.AddRange(horizontalMatches);

        // --- 垂直检查（从(x,y)向上下两端延伸）---
        List<GameObject> verticalMatches = new List<GameObject> { allTiles[x, y] };
        // 向下
        for (int i = y - 1; i >= 0; i--)
        {
            if (grid[x, i] == currentSprite) { verticalMatches.Add(allTiles[x, i]); }
            else { break; }
        }
        // 向上
        for (int i = y + 1; i < height; i++)
        {
            if (grid[x, i] == currentSprite) { verticalMatches.Add(allTiles[x, i]); }
            else { break; }
        }
        if (verticalMatches.Count >= 3) matches.AddRange(verticalMatches);

        // 使用HashSet去重，因为T型或L型的中心点会被加两次
        HashSet<GameObject> finalMatches = new HashSet<GameObject>(matches);
        return new List<GameObject>(finalMatches);
    }

    // 检查棋盘状态
    private IEnumerator CheckBoardStateRoutine()
    {
        // 锁定操作，直到检查和可能的洗牌完成
        currentState = GameState.wait;

        // 调用AI的“大脑”来检查是否有可行的移动
        List<Move> possibleMoves = FindAllPossibleMoves();
        if (possibleMoves.Count == 0)
        {
            // 如果没有，则启动洗牌程序
            yield return StartCoroutine(ReshuffleBoardRoutine());
        }

        // 检查完毕（或洗牌完毕），将操作权交给当前回合的玩家/AI
        currentState = GameState.move;
    }


    // 洗牌算法
    private IEnumerator ReshuffleBoardRoutine()
    {
        // 1. 宣布洗牌并锁定操作
        currentState = GameState.wait;
        Debug.Log("没有可移动的步数了，开始洗牌！");
        // TODO: 在这里可以显示一个“正在洗牌...”的UI提示

        // 2. 播放“缩小消失”动画
        Sequence shrinkSequence = DOTween.Sequence();
        foreach (var tileObject in allTiles)
        {
            if (tileObject != null)
            {
                shrinkSequence.Join(tileObject.transform.DOScale(0, animationDuration).SetEase(Ease.InBack));
            }
        }
        yield return shrinkSequence.WaitForCompletion();

        // 3. 在后台进行数据洗牌
        List<GameObject> allExistingTiles = new List<GameObject>();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (allTiles[x, y] != null)
                {
                    allExistingTiles.Add(allTiles[x, y]);
                }
            }
        }

        // Fisher-Yates 洗牌算法
        for (int i = 0; i < allExistingTiles.Count - 1; i++)
        {
            int randomIndex = Random.Range(i, allExistingTiles.Count);
            GameObject temp = allExistingTiles[randomIndex];
            allExistingTiles[randomIndex] = allExistingTiles[i];
            allExistingTiles[i] = temp;
        }

        // 4. 将洗牌后的数据重新填充到 allTiles 数组，并更新每个Tile的坐标
        int tileIndex = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                allTiles[x, y] = allExistingTiles[tileIndex];
                Tile tileScript = allTiles[x, y].GetComponent<Tile>();
                tileScript.x = x;
                tileScript.y = y;

                // 立刻将方块移动到它洗牌后的新位置（但保持缩小状态）
                allTiles[x, y].GetComponent<RectTransform>().anchoredPosition = GetPositionForTile(x, y);
                tileIndex++;
            }
        }

        // 5. 播放“放大出现”动画
        yield return new WaitForSeconds(0.2f);
        Sequence growSequence = DOTween.Sequence();
        foreach (var tileObject in allTiles)
        {
            if (tileObject != null)
            {
                growSequence.Join(tileObject.transform.DOScale(1, animationDuration).SetEase(Ease.OutBack));
            }
        }
        yield return growSequence.WaitForCompletion();

        // 6. 洗牌后再次检查，确保新棋盘不是死局（这是一个进阶逻辑，暂时简化）
        // TODO: 可以添加循环，直到生成一个非死局的棋盘

        // 7. 解锁操作
        currentState = GameState.move;
    }
}