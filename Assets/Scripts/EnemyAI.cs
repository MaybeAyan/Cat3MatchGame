using System.Collections.Generic;
using UnityEngine;

public struct Move
{
    public int x1, y1, x2, y2;
    public int score;
}

public class EnemyAI : MonoBehaviour
{
    public static EnemyAI Instance;
    private GameBoard board;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        board = GameBoard.Instance;
    }

    public Move FindBestMove()
    {
        List<Move> possibleMoves = FindAllPossibleMoves();
        if (possibleMoves.Count > 0)
        {
            Move bestMove = new Move();
            int bestScore = -1;
            foreach (var move in possibleMoves)
            {
                if (move.score > bestScore)
                {
                    bestScore = move.score;
                    bestMove = move;
                }
            }
            return bestMove;
        }
        return new Move { score = -1 };
    }

    public List<Move> FindAllPossibleMoves()
    {
        List<Move> possibleMoves = new List<Move>();
        Sprite[,] grid = board.GetSpriteGridForSimulation();

        for (int x = 0; x < board.width; x++)
        {
            for (int y = 0; y < board.height; y++)
            {
                if (grid[x, y] == null) continue;

                // 【修复】确保要交换的目标位置不是空的
                if (x < board.width - 1 && grid[x + 1, y] != null)
                {
                    int score = ScoreMove(x, y, x + 1, y, grid);
                    if (score > 0) possibleMoves.Add(new Move { x1 = x, y1 = y, x2 = x + 1, y2 = y, score = score });
                }
                if (y < board.height - 1 && grid[x, y + 1] != null)
                {
                    int score = ScoreMove(x, y, x, y + 1, grid);
                    if (score > 0) possibleMoves.Add(new Move { x1 = x, y1 = y, x2 = x, y2 = y + 1, score = score });
                }
            }
        }
        return possibleMoves;
    }

    private int ScoreMove(int x1, int y1, int x2, int y2, Sprite[,] originalGrid)
    {
        Sprite[,] tempGrid = (Sprite[,])originalGrid.Clone();
        Sprite temp = tempGrid[x1, y1];
        tempGrid[x1, y1] = tempGrid[x2, y2];
        tempGrid[x2, y2] = temp;

        HashSet<Vector2Int> matchedPositions = new HashSet<Vector2Int>();

        CheckMatchesAt(x1, y1, tempGrid, matchedPositions);
        CheckMatchesAt(x2, y2, tempGrid, matchedPositions);

        int score = matchedPositions.Count;
        if (score >= 5) score += 10;
        return score;
    }

    private void CheckMatchesAt(int x, int y, Sprite[,] grid, HashSet<Vector2Int> matches)
    {
        Sprite currentSprite = grid[x, y];
        if (currentSprite == null) return;

        List<Vector2Int> horizontalMatches = new List<Vector2Int> { new Vector2Int(x, y) };
        for (int i = x - 1; i >= 0 && grid[i, y] == currentSprite; i--) { horizontalMatches.Add(new Vector2Int(i, y)); }
        for (int i = x + 1; i < board.width && grid[i, y] == currentSprite; i++) { horizontalMatches.Add(new Vector2Int(i, y)); }
        if (horizontalMatches.Count >= 3) foreach (var pos in horizontalMatches) matches.Add(pos);

        List<Vector2Int> verticalMatches = new List<Vector2Int> { new Vector2Int(x, y) };
        for (int i = y - 1; i >= 0 && grid[x, i] == currentSprite; i--) { verticalMatches.Add(new Vector2Int(x, i)); }
        for (int i = y + 1; i < board.height && grid[x, i] == currentSprite; i++) { verticalMatches.Add(new Vector2Int(x, i)); }
        if (verticalMatches.Count >= 3) foreach (var pos in verticalMatches) matches.Add(pos);
    }
}
