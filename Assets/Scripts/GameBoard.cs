using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using DG.Tweening;
using UnityEngine.UI;

public class GameBoard : MonoBehaviour
{
    public static GameBoard Instance;

    public int width = 10;
    public int height = 10;
    public GameObject tilePrefab;

    [Header("动画参数")] public float animationDuration = 0.3f;

    private List<GameObject> matchedTiles = new List<GameObject>();
    private GameObject[,] allTiles;

    [Header("音频文件")] public AudioClip swapSound;
    public AudioClip matchSound;

    [Header("特效预制体")] public GameObject destructionEffectPrefab;

    [Header("UI布局引用")] public RectTransform tileGridPanel; // 用于放置所有Tile的容器Panel
    private GridLayoutGroup gridLayout; // 对布局组件的引用

    // 选中的方块
    public Tile selectedTile;

    public enum GameState
    {
        move,
        wait
    }

    public GameState currentState = GameState.move;

    void Start()
    {
        StartCoroutine(SetupBoard_Coroutine());
    }

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

        gridLayout = tileGridPanel.GetComponent<GridLayoutGroup>();
    }

// 将原来的 SetupBoard() 方法替换成下面这个协程
    private IEnumerator SetupBoard_Coroutine()
    {
        Debug.Log("启动了SetupBoard协程");
        // 等待当前帧的末尾，此时所有UI元素的大小和位置都已经计算完毕
        yield return new WaitForEndOfFrame();

        // 1. 开始前先清空容器
        foreach (Transform child in tileGridPanel)
        {
            Destroy(child.gameObject);
        }

        // 2. 动态计算每个格子的大小 (现在获取到的 panelWidth 就是正确的最终值了)
        float panelWidth = tileGridPanel.rect.width;
        float totalSpacingWidth = gridLayout.spacing.x * (width - 1);
        float totalPaddingWidth = gridLayout.padding.left + gridLayout.padding.right;
        float effectiveWidth = panelWidth - totalSpacingWidth - totalPaddingWidth;
        float cellWidth = effectiveWidth / width;
        gridLayout.cellSize = new Vector2(cellWidth, cellWidth);

        // 3. 循环生成格子 (后续逻辑保持不变)
        allTiles = new GameObject[width, height]; // 在生成前初始化数组
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                GameObject tileObject = Instantiate(tilePrefab, tileGridPanel);
                tileObject.name = $"Tile ({x},{y})";

                allTiles[x, y] = tileObject;
                Tile tileScript = tileObject.GetComponent<Tile>();

                // (为方块上色和设置坐标的逻辑和之前完全一样)
                #region Color and Data Setup
                int randomColorIndex = Random.Range(0, 5);
                Color newColor = GetPastelColorByIndex(randomColorIndex);

                // (此处可以加上避免开局匹配的逻辑)
                if (tileScript != null)
                {
                    if (tileScript.squareSprite != null)
                        tileScript.squareSprite.color = newColor;
                    if (tileScript.roundedSprite != null)
                    {
                        newColor.a = 0f;
                        tileScript.roundedSprite.color = newColor;
                    }

                    tileScript.x = x;
                    tileScript.y = y;
                    tileScript.board = this;
                }

                #endregion
            }
        }
    }

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

    // 一个辅助方法，用于判断两个方块是否相邻
    private bool AreTilesAdjacent(Tile tile1, Tile tile2)
    {
        if (tile1 == null || tile2 == null) return false;

        // 计算两个方块在x和y轴上的距离之和
        // 如果等于1，说明它们只在一个方向上相邻一格
        return (Mathf.Abs(tile1.x - tile2.x) + Mathf.Abs(tile1.y - tile2.y)) == 1;
    }

    // 处理方块点击事件的核心方法
    public void TileClicked(Tile clickedTile)
    {
        // 检查游戏状态是否允许操作
        if (currentState != GameState.move)
        {
            return;
        }

        // 情况1：当前没有任何方块被选中
        if (selectedTile == null)
        {
            selectedTile = clickedTile;
            selectedTile.SetSelected(true); // 激活选中效果
        }
        // 情况2：已经有一个方块被选中
        else
        {
            // 情况2a：点击了已经被选中的方块自己 -> 取消选中
            if (selectedTile == clickedTile)
            {
                selectedTile.SetSelected(false);
                selectedTile = null;
            }
            // 情况2b：点击了另一个方块
            else
            {
                // 如果点击的方块与已选中的方块相邻 -> 执行交换
                if (AreTilesAdjacent(selectedTile, clickedTile))
                {
                    selectedTile.SetSelected(false);
                    StartCoroutine(SwapAndCheckRoutine(selectedTile.x, selectedTile.y, clickedTile.x, clickedTile.y));
                    selectedTile = null;
                }
                // 如果不相邻 -> 将选中状态转移到新点击的方块上
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

    private IEnumerator SwapAndCheckRoutine(int x1, int y1, int x2, int y2)
    {
        currentState = GameState.wait;

        Tile tile1Script = allTiles[x1, y1].GetComponent<Tile>();
        Tile tile2Script = allTiles[x2, y2].GetComponent<Tile>();

        if (tile1Script != null && tile2Script != null)
        {
            // 交换数据
            allTiles[x1, y1] = tile2Script.gameObject;
            allTiles[x2, y2] = tile1Script.gameObject;
            tile1Script.x = x2;
            tile1Script.y = y2;
            tile2Script.x = x1;
            tile2Script.y = y1;

            // 【UI动画交换逻辑】
            int tile1SiblingIndex = tile1Script.transform.GetSiblingIndex();
            int tile2SiblingIndex = tile2Script.transform.GetSiblingIndex();

            Vector3 tile1Position = tile1Script.transform.position;
            Vector3 tile2Position = tile2Script.transform.position;

            // 播放交换音效
            AudioManager.Instance.PlaySFX(swapSound);

            Sequence swapSequence = DOTween.Sequence();
            swapSequence.Join(tile1Script.transform.DOMove(tile2Position, animationDuration))
                .Join(tile2Script.transform.DOMove(tile1Position, animationDuration));

            yield return swapSequence.WaitForCompletion();

            // 交换层级顺序，GridLayoutGroup会根据这个新顺序重新排列
            tile1Script.transform.SetSiblingIndex(tile2SiblingIndex);
            tile2Script.transform.SetSiblingIndex(tile1SiblingIndex);

            FindAllMatches();

            if (matchedTiles.Count > 0)
            {
                yield return StartCoroutine(DestroyAndRefillRoutine());
            }
            else // 如果没有匹配，再换回来
            {
                // 数据再次换回
                allTiles[x1, y1] = tile1Script.gameObject;
                allTiles[x2, y2] = tile2Script.gameObject;
                tile1Script.x = x1;
                tile1Script.y = y1;
                tile2Script.x = x2;
                tile2Script.y = y2;

                // 动画也播放回来
                Sequence swapBackSequence = DOTween.Sequence();
                swapBackSequence.Join(tile1Script.transform.DOMove(tile1Position, animationDuration))
                    .Join(tile2Script.transform.DOMove(tile2Position, animationDuration));
                yield return swapBackSequence.WaitForCompletion();

                // 层级顺序也换回来
                tile1Script.transform.SetSiblingIndex(tile1SiblingIndex);
                tile2Script.transform.SetSiblingIndex(tile2SiblingIndex);
            }
        }

        currentState = GameState.move;
    }


    private IEnumerator DestroyAndRefillRoutine()
    {
        while (matchedTiles.Count > 0)
        {
            // 等待销毁动画和数据清理完成
            yield return StartCoroutine(DestroyMatches());
            // 等待下落和填充动画完成
            yield return StartCoroutine(CollapseAndRefillRoutine());
        }
    }

    private IEnumerator CollapseAndRefillRoutine()
    {
        // --- 第一部分：数据层面的下落 ---
        // 视觉上下落动画在GridLayoutGroup中比较复杂，我们先实现一个瞬时下落的正确逻辑
        int nullCountInColumn = 0;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (allTiles[x, y] == null)
                {
                    nullCountInColumn++;
                }
                else if (nullCountInColumn > 0)
                {
                    // 将上方的格子数据移动到下方的空格子
                    allTiles[x, y - nullCountInColumn] = allTiles[x, y];
                    allTiles[x, y] = null;
                    // 更新格子的y坐标
                    allTiles[x, y - nullCountInColumn].GetComponent<Tile>().y = y - nullCountInColumn;
                }
            }

            nullCountInColumn = 0;
        }

        // --- 第二部分：刷新视觉层级，让GridLayoutGroup重新排列 ---
        // 我们需要根据 allTiles 数组中的新顺序，来重新排列Hierarchy中的子对象顺序
        List<GameObject> sortedTiles = new List<GameObject>();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (allTiles[x, y] != null)
                {
                    sortedTiles.Add(allTiles[x, y]);
                }
            }
        }

        // 根据数据顺序，重新设置Hierarchy中的顺序
        for (int i = 0; i < sortedTiles.Count; i++)
        {
            sortedTiles[i].transform.SetSiblingIndex(i);
        }

        // 等待一帧，让GridLayoutGroup完成重新布局
        yield return new WaitForEndOfFrame();


        // --- 第三部分：填充新元素 ---
        Sequence refillSequence = DOTween.Sequence();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (allTiles[x, y] == null)
                {
                    GameObject tileObject = Instantiate(tilePrefab, tileGridPanel);
                    tileObject.name = $"Tile ({x},{y})";

                    allTiles[x, y] = tileObject;
                    Tile newTileScript = tileObject.GetComponent<Tile>();

                    // (为新方块上色的逻辑)

                    #region Color and Data Setup

                    int randomColorIndex = Random.Range(0, 5);
                    Color newColor = GetPastelColorByIndex(randomColorIndex);
                    if (newTileScript != null)
                    {
                        if (newTileScript.squareSprite != null) newTileScript.squareSprite.color = newColor;
                        if (newTileScript.roundedSprite != null)
                        {
                            newColor.a = 0f;
                            newTileScript.roundedSprite.color = newColor;
                        }

                        newTileScript.x = x;
                        newTileScript.y = y;
                        newTileScript.board = this;
                    }

                    #endregion

                    // 新方块的出现动画（例如，从无到有缩放出来）
                    tileObject.transform.localScale = Vector3.zero;
                    refillSequence.Join(tileObject.transform.DOScale(1f, animationDuration).SetEase(Ease.OutBack));
                }
            }
        }

        yield return refillSequence.WaitForCompletion();

        FindAllMatches();
    }

    private void FindAllMatches()
    {
        matchedTiles.Clear();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                GameObject currentTileObject = allTiles[x, y];
                if (currentTileObject == null) continue;

                // 【核心修复】通过 Tile 脚本去获取颜色
                Tile currentTileScript = currentTileObject.GetComponent<Tile>();
                if (currentTileScript == null || currentTileScript.squareSprite == null) continue; // 安全检查
                Color currentColor = currentTileScript.squareSprite.color;

                // 水平检测 (向右检查两个)
                if (x < width - 2)
                {
                    GameObject tile1Object = allTiles[x + 1, y];
                    GameObject tile2Object = allTiles[x + 2, y];
                    if (tile1Object != null && tile2Object != null)
                    {
                        Tile tile1Script = tile1Object.GetComponent<Tile>();
                        Tile tile2Script = tile2Object.GetComponent<Tile>();

                        if (tile1Script != null && tile2Script != null &&
                            tile1Script.squareSprite.color == currentColor &&
                            tile2Script.squareSprite.color == currentColor)
                        {
                            if (!matchedTiles.Contains(currentTileObject)) matchedTiles.Add(currentTileObject);
                            if (!matchedTiles.Contains(tile1Object)) matchedTiles.Add(tile1Object);
                            if (!matchedTiles.Contains(tile2Object)) matchedTiles.Add(tile2Object);
                        }
                    }
                }

                // 垂直检测 (向上检查两个)
                if (y < height - 2)
                {
                    GameObject tile1Object = allTiles[x, y + 1];
                    GameObject tile2Object = allTiles[x, y + 2];
                    if (tile1Object != null && tile2Object != null)
                    {
                        Tile tile1Script = tile1Object.GetComponent<Tile>();
                        Tile tile2Script = tile2Object.GetComponent<Tile>();

                        if (tile1Script != null && tile2Script != null &&
                            tile1Script.squareSprite.color == currentColor &&
                            tile2Script.squareSprite.color == currentColor)
                        {
                            if (!matchedTiles.Contains(currentTileObject)) matchedTiles.Add(currentTileObject);
                            if (!matchedTiles.Contains(tile1Object)) matchedTiles.Add(tile1Object);
                            if (!matchedTiles.Contains(tile2Object)) matchedTiles.Add(tile2Object);
                        }
                    }
                }
            }
        }
    }

// 在 GameBoard.cs 中

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
                // --- 1. 播放粒子特效 ---
                if (destructionEffectPrefab != null)
                {
                    // 【核心修复】通过 Tile 脚本去获取颜色，不再直接GetComponent<SpriteRenderer>()
                    Tile tileScriptForEffect = tile.GetComponent<Tile>();
                    if (tileScriptForEffect != null && tileScriptForEffect.squareSprite != null)
                    {
                        // 从正确的子对象上获取颜色
                        Color tileColor = tileScriptForEffect.squareSprite.color;

                        GameObject effect = Instantiate(destructionEffectPrefab, tile.transform.position,
                            Quaternion.identity);

                        var mainModule = effect.GetComponent<ParticleSystem>().main;
                        mainModule.startColor = new ParticleSystem.MinMaxGradient(tileColor);
                    }
                }

                // --- 2. 创建方块自身的动画序列 ---
                Sequence tileAnimSequence = DOTween.Sequence();
                tileAnimSequence
                    .Append(tile.transform.DOScale(1.2f, animationDuration * 0.3f).SetEase(Ease.OutQuad))
                    .Append(tile.transform.DOScale(0f, animationDuration * 0.7f).SetEase(Ease.InBack));

                destroySequence.Join(tileAnimSequence);

                // 我们需要从 Tile 脚本引用中获取 SpriteRenderer
                Tile tileScriptForFade = tile.GetComponent<Tile>();
                if (tileScriptForFade != null)
                {
                    // 同时让两个 Sprite 都淡出，确保无论选中与否，表现都正确
                    destroySequence.Join(tileScriptForFade.squareSprite.DOFade(0f, animationDuration));
                    destroySequence.Join(tileScriptForFade.roundedSprite.DOFade(0f, animationDuration));
                }
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
}