using System.Collections;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public enum GameFlowState { Initializing, PlayerTurn, EnemyTurn, Resolving, Paused }
    [Tooltip("用于调试观察，可以手动修改")]
    public GameFlowState currentState;

    // Turn枚举仅作为GameManager的私有状态，外部无法访问
    private enum Turn { Player, Enemy };
    private Turn currentTurn;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        StartNewGame();
    }

    void StartNewGame()
    {
        currentState = GameFlowState.Initializing;
        GameBoard.Instance.SetupBoard();
    }

    // 在GameBoard的SetupBoard完成后调用
    public void OnBoardSetupComplete()
    {
        StartCoroutine(StartFirstTurn());
    }

    private IEnumerator StartFirstTurn()
    {
        yield return new WaitForSeconds(0.5f);
        SwitchToPlayerTurn();
    }

    private void SwitchToPlayerTurn(bool showUI = true)
    {
        currentTurn = Turn.Player;
        currentState = GameFlowState.PlayerTurn;
        if (showUI && UIManager.Instance != null) UIManager.Instance.ShowPlayerTurn();
        StartCoroutine(GameBoard.Instance.CheckBoardStateRoutine());
        // CheckBoardStateRoutine 会设置GameBoard的状态设为 move
    }

    private void SwitchToEnemyTurn()
    {
        currentTurn = Turn.Enemy;
        currentState = GameFlowState.EnemyTurn;
        GameBoard.Instance.SetBoardState(GameBoard.GameState.wait);
        if (UIManager.Instance != null) UIManager.Instance.ShowEnemyTurn();
        StartCoroutine(EnemyTurnRoutine());
    }

    public void OnMoveFinished(bool wasExtraTurn)
    {
        currentState = GameFlowState.Resolving;

        if (wasExtraTurn)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowExtraTurn();
            if (currentTurn == Turn.Player)
            {
                SwitchToPlayerTurn(false); // 额外回合不显示UI，因为已经显示了"额外回合"
                // ANCHOR: AI_EXTRA_TURN
            }
            else
            {
                // AI获得额外回合，再次运行敌人回合判断逻辑
                StartCoroutine(EnemyTurnRoutine());
                // ANCHOR_END: AI_EXTRA_TURN
            }
        }
        else
        {
            if (currentTurn == Turn.Player)
            {
                SwitchToEnemyTurn();
            }
            else
            {
                SwitchToPlayerTurn();
            }
        }
    }

    private IEnumerator EnemyTurnRoutine()
    {
        Debug.Log("<color=orange>敌人回合开始，正在思考...</color>");
        yield return StartCoroutine(GameBoard.Instance.CheckBoardStateRoutine());
        yield return new WaitForSeconds(1.0f);

        Move bestMove = EnemyAI.Instance.FindBestMove();

        if (bestMove.score > -1)
        {
            Debug.Log($"<color=red>AI决定执行移动，得分: {bestMove.score}</color>");
            Tile t1 = GameBoard.Instance.allTiles[bestMove.x1, bestMove.y1].GetComponent<Tile>();
            Tile t2 = GameBoard.Instance.allTiles[bestMove.x2, bestMove.y2].GetComponent<Tile>();

            // 重要的修改：使用 yield return 来等待这个协程执行完毕
            // 让我们使 EnemyTurnRoutine 在此处暂停，直到玩家交互系统调用相应的回调
            yield return GameBoard.Instance.StartCoroutine(GameBoard.Instance.SwapAndCheckRoutine(t1, t2));
        }
        else
        {
            Debug.LogWarning("AI找不到有效移动，直接进入下一回合");
            SwitchToPlayerTurn(); // AI跳过回合
        }
    }
}
