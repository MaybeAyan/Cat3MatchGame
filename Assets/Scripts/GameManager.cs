using System.Collections;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public enum GameFlowState { Initializing, PlayerTurn, EnemyTurn, Resolving, Paused }
    [Tooltip("仅用于调试观察，请勿手动修改")]
    public GameFlowState currentState;

    // Turn枚举现在是GameManager的私有状态，外部无法访问
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

    // 由GameBoard在SetupBoard完成后调用
    public void OnBoardSetupComplete()
    {
        StartCoroutine(StartFirstTurn());
    }

    private IEnumerator StartFirstTurn()
    {
        yield return new WaitForSeconds(0.5f);
        SwitchToPlayerTurn();
    }

    private void SwitchToPlayerTurn()
    {
        currentTurn = Turn.Player;
        currentState = GameFlowState.PlayerTurn;
        if (UIManager.Instance != null) UIManager.Instance.ShowTurnIndicator("你的回合", Color.cyan);
        StartCoroutine(GameBoard.Instance.CheckBoardStateRoutine());
        // CheckBoardStateRoutine 会在最后将GameBoard的状态设为 move
    }

    private void SwitchToEnemyTurn()
    {
        currentTurn = Turn.Enemy;
        currentState = GameFlowState.EnemyTurn;
        GameBoard.Instance.SetBoardState(GameBoard.GameState.wait);
        if (UIManager.Instance != null) UIManager.Instance.ShowTurnIndicator("敌人回合", Color.red);
        StartCoroutine(EnemyTurnRoutine());
    }

    public void OnMoveFinished(bool wasExtraTurn)
    {
        currentState = GameFlowState.Resolving;

        if (wasExtraTurn)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowTurnIndicator("额外回合！", Color.yellow);
            if (currentTurn == Turn.Player)
            {
                SwitchToPlayerTurn();
                // ANCHOR: AI_EXTRA_TURN
            }
            else
            {
                // AI获得额外回合，再次启动它的行动程序
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
            Debug.Log($"<color=red>AI决定执行移动，分数: {bestMove.score}</color>");
            Tile t1 = GameBoard.Instance.allTiles[bestMove.x1, bestMove.y1].GetComponent<Tile>();
            Tile t2 = GameBoard.Instance.allTiles[bestMove.x2, bestMove.y2].GetComponent<Tile>();

            // 【核心修复】使用 yield return 来等待交换协程执行完毕
            // 这会使 EnemyTurnRoutine 在此暂停，直到棋盘上的所有连锁反应都结束
            yield return GameBoard.Instance.StartCoroutine(GameBoard.Instance.SwapAndCheckRoutine(t1, t2));
        }
        else
        {
            Debug.LogWarning("AI找不到可移动步数，直接进入玩家回合");
            SwitchToPlayerTurn(); // AI跳过回合
        }
    }
}
