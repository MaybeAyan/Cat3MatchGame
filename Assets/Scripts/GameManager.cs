using System.Collections;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public enum GameFlowState { Initializing, PlayerTurn, EnemyTurn, Resolving, Paused }
    [Tooltip("�����ڵ��Թ۲죬�����ֶ��޸�")]
    public GameFlowState currentState;

    // Turnö��������GameManager��˽��״̬���ⲿ�޷�����
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

    // ��GameBoard��SetupBoard��ɺ����
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
        if (UIManager.Instance != null) UIManager.Instance.ShowTurnIndicator("��Ļغ�", Color.cyan);
        StartCoroutine(GameBoard.Instance.CheckBoardStateRoutine());
        // CheckBoardStateRoutine �������GameBoard��״̬��Ϊ move
    }

    private void SwitchToEnemyTurn()
    {
        currentTurn = Turn.Enemy;
        currentState = GameFlowState.EnemyTurn;
        GameBoard.Instance.SetBoardState(GameBoard.GameState.wait);
        if (UIManager.Instance != null) UIManager.Instance.ShowTurnIndicator("���˻غ�", Color.red);
        StartCoroutine(EnemyTurnRoutine());
    }

    public void OnMoveFinished(bool wasExtraTurn)
    {
        currentState = GameFlowState.Resolving;

        if (wasExtraTurn)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowTurnIndicator("����غϣ�", Color.yellow);
            if (currentTurn == Turn.Player)
            {
                SwitchToPlayerTurn();
                // ANCHOR: AI_EXTRA_TURN
            }
            else
            {
                // AI��ö���غϣ��ٴ����������ж�����
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
        Debug.Log("<color=orange>���˻غϿ�ʼ������˼��...</color>");
        yield return StartCoroutine(GameBoard.Instance.CheckBoardStateRoutine());
        yield return new WaitForSeconds(1.0f);

        Move bestMove = EnemyAI.Instance.FindBestMove();

        if (bestMove.score > -1)
        {
            Debug.Log($"<color=red>AI����ִ���ƶ�������: {bestMove.score}</color>");
            Tile t1 = GameBoard.Instance.allTiles[bestMove.x1, bestMove.y1].GetComponent<Tile>();
            Tile t2 = GameBoard.Instance.allTiles[bestMove.x2, bestMove.y2].GetComponent<Tile>();

            // �������޸���ʹ�� yield return ���ȴ�����Э��ִ�����
            // ���ʹ EnemyTurnRoutine �ڴ���ͣ��ֱ�������ϵ�����������Ӧ������
            yield return GameBoard.Instance.StartCoroutine(GameBoard.Instance.SwapAndCheckRoutine(t1, t2));
        }
        else
        {
            Debug.LogWarning("AI�Ҳ������ƶ�������ֱ�ӽ�����һغ�");
            SwitchToPlayerTurn(); // AI�����غ�
        }
    }
}
