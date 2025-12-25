using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class ChessGameController : MonoBehaviour
{
    [Header("Refs")]
    public BoardGeneratorFixed boardGenerator;
    public ChessBoardIndex board;
    public ChessMoveExecutor executor;
    public PieceInitializer pieceInitializer;
    public ChessUIController ui;

    [Header("Game State")]
    public PieceColor sideToMove = PieceColor.White;
    public bool gameOver = false;

    public ChessMove? lastMove = null;
    public string statusMessage = "Waiting for pieces...";

    void Awake()
    {
        DOTween.SetTweensCapacity(1000, 50);
        if (boardGenerator == null) boardGenerator = FindObjectOfType<BoardGeneratorFixed>();
        if (board == null) board = FindObjectOfType<ChessBoardIndex>();
        if (executor == null) executor = FindObjectOfType<ChessMoveExecutor>();
        if (pieceInitializer == null) pieceInitializer = FindObjectOfType<PieceInitializer>();
        if (ui == null) ui = FindObjectOfType<ChessUIController>();

        if (ui == null) ui = gameObject.AddComponent<ChessUIController>();
        ui.EnsureUI(RestartGame);

        if (executor != null && executor.board == null)
            executor.board = board;
    }

    void Start()
    {
        if (pieceInitializer != null)
        {
            pieceInitializer.OnPiecesInitialized -= OnPiecesInitialized;
            pieceInitializer.OnPiecesInitialized += OnPiecesInitialized;
        }

        RefreshUI();
    }

    void OnDestroy()
    {
        if (pieceInitializer != null)
            pieceInitializer.OnPiecesInitialized -= OnPiecesInitialized;
    }

    void OnPiecesInitialized()
    {
        sideToMove = PieceColor.White;
        lastMove = null;
        gameOver = false;

        UpdateStatusMessage();
        RefreshUI();
    }

    public Dictionary<string, ChessMove> GetLegalMoveMap(PieceData piece)
    {
        var map = new Dictionary<string, ChessMove>(32);

        if (board == null || piece == null) return map;
        if (gameOver) return map;
        if (piece.color != sideToMove) return map;

        var occ = board.BuildOccupancy(out bool hasBothKings);
        if (!hasBothKings) return map;

        var legal = ChessRules.GenerateLegalMoves(piece, occ, lastMove);
        for (int i = 0; i < legal.Count; i++)
            map[legal[i].to] = legal[i];

        return map;
    }

    public bool TryApplyMove(PieceData movingPiece, ChessMove move)
    {
        if (board == null || executor == null) return false;
        if (gameOver) return false;
        if (movingPiece == null) return false;
        if (movingPiece.color != sideToMove) return false;

        var map = GetLegalMoveMap(movingPiece);
        if (!map.TryGetValue(move.to, out var legalMove))
            return false;

        executor.ExecuteMove(movingPiece, legalMove, lastMove);
        lastMove = legalMove;

        sideToMove = (sideToMove == PieceColor.White) ? PieceColor.Black : PieceColor.White;

        EvaluateEndConditions();
        if (!gameOver)
            UpdateStatusMessage();

        RefreshUI();
        return true;
    }

    void UpdateStatusMessage()
    {
        if (board == null)
        {
            statusMessage = "Board missing";
            return;
        }

        var occ = board.BuildOccupancy(out bool hasBothKings);
        if (!hasBothKings)
        {
            statusMessage = "Waiting for pieces...";
            return;
        }

        bool inCheck = ChessRules.IsKingInCheck(sideToMove, occ);
        statusMessage = inCheck ? $"{sideToMove} is in CHECK" : "OK";
    }

    void EvaluateEndConditions()
    {
        var occ = board.BuildOccupancy(out bool hasBothKings);
        if (!hasBothKings)
        {
            gameOver = false;
            statusMessage = "Waiting for pieces...";
            return;
        }

        bool hasAnyMove = SideHasAnyLegalMove(sideToMove, occ);
        if (hasAnyMove)
        {
            gameOver = false;
            return;
        }

        bool inCheck = ChessRules.IsKingInCheck(sideToMove, occ);
        gameOver = true;

        if (inCheck)
        {
            PieceColor winner = (sideToMove == PieceColor.White) ? PieceColor.Black : PieceColor.White;
            statusMessage = $"CHECKMATE! {winner} wins.";
        }
        else
        {
            statusMessage = "STALEMATE! Draw.";
        }
    }

    bool SideHasAnyLegalMove(PieceColor side, PieceData[,] occ)
    {
        var pieces = board.FindAllRootPieces();
        for (int i = 0; i < pieces.Count; i++)
        {
            var p = pieces[i];
            if (p == null || p.color != side) continue;

            var legal = ChessRules.GenerateLegalMoves(p, occ, lastMove);
            if (legal.Count > 0) return true;
        }
        return false;
    }

    public void RestartGame()
    {
        StopAllCoroutines();

        gameOver = false;
        sideToMove = PieceColor.White;
        lastMove = null;
        statusMessage = "Restarting...";

        // Board first, then pieces (next frame)
        StartCoroutine(RestartSequence());
    }

    IEnumerator RestartSequence()
    {
        // 1) Respawn board tiles
        if (boardGenerator != null)
            boardGenerator.GenerateBoard();
        else
            Debug.LogWarning("BoardGeneratorFixed not assigned; tiles will not respawn.");

        // Let Unity finish destroying/instantiating tiles this frame
        yield return null;

        // 2) Clear highlights (tiles are new)
        if (board != null)
            board.ClearAllTileHighlights();

        // 3) Respawn pieces
        if (pieceInitializer != null)
            pieceInitializer.InitializePieces(); // triggers OnPiecesInitialized
        else
        {
            statusMessage = "Assign PieceInitializer to restart.";
            RefreshUI();
        }
    }

    void RefreshUI()
    {
        if (ui == null) return;
        ui.SetTurn($"Turn: {sideToMove}");
        ui.SetStatus(statusMessage);
    }
}
