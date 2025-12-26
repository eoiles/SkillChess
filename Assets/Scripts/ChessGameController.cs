// ChessGameController.cs
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

    [Header("Restart Timing")]
    [Min(0f)] public float extraDelayAfterTiles = 0.05f;

    [Header("Move Animation / Input")]
    public bool lockInputDuringMoveAnimation = true;

    [Header("Game State")]
    public PieceColor sideToMove = PieceColor.White;
    public bool gameOver = false;

    public ChessMove? lastMove = null;
    public string statusMessage = "Waiting for pieces...";

    bool isMoveInProgress = false;

    void Awake()
    {
        DOTween.SetTweensCapacity(1000, 100);

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

        RestartGame();
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
        isMoveInProgress = false;

        UpdateStatusMessage();
        RefreshUI();
    }

    public void RestartGame()
    {
        // Ensure any in-flight tweens are stopped so state can’t “finish later”
        DOTween.KillAll(false);

        StopAllCoroutines();
        StartCoroutine(RestartRoutine());
    }

    IEnumerator RestartRoutine()
    {
        statusMessage = "Restarting...";
        RefreshUI();

        // Stop selection/highlights + (optional) stop clicks during restart
        var input = FindObjectOfType<ChessInputController>();
        if (input != null)
        {
            input.Deselect();
            if (lockInputDuringMoveAnimation) input.enabled = false;
        }

        isMoveInProgress = false;
        gameOver = false;
        sideToMove = PieceColor.White;
        lastMove = null;

        // IMPORTANT: prevent auto-spawn from other scripts
        if (boardGenerator != null) boardGenerator.generateOnStart = false;
        if (pieceInitializer != null) pieceInitializer.autoInitializeOnStart = false;

        // 1) Clear pieces first so nothing overlaps
        if (pieceInitializer != null)
        {
            pieceInitializer.ClearPiecesOnly();
            yield return null; // let Destroy() remove colliders
        }

        // 2) Tiles
        if (boardGenerator != null)
        {
            boardGenerator.GenerateBoard();
            yield return null; // let tile colliders exist
        }

        // 3) Wait for tile animation to finish BEFORE pieces
        float tileAnim = boardGenerator != null ? boardGenerator.GetTotalTileAnimTime() : 0f;
        if (tileAnim > 0f)
            yield return new WaitForSeconds(tileAnim + extraDelayAfterTiles);

        // 4) Refresh board tile cache if needed (safe no-op if not implemented)
        if (board != null)
        {
            board.SendMessage("RebuildTiles", SendMessageOptions.DontRequireReceiver);
            board.SendMessage("CacheTiles", SendMessageOptions.DontRequireReceiver);
            board.ClearAllTileHighlights();
        }

        // 5) Pieces
        if (pieceInitializer != null)
            pieceInitializer.InitializePieces();

        // Re-enable input after pieces spawn
        if (input != null && lockInputDuringMoveAnimation)
            input.enabled = true;
    }

    public Dictionary<string, ChessMove> GetLegalMoveMap(PieceData piece)
    {
        var map = new Dictionary<string, ChessMove>(32);

        if (board == null || piece == null) return map;
        if (gameOver) return map;
        if (isMoveInProgress) return map;
        if (piece.color != sideToMove) return map;

        var occ = board.BuildOccupancy(out bool hasBothKings);
        if (!hasBothKings) return map;

        var legal = ChessRules.GenerateLegalMoves(piece, occ, lastMove);
        for (int i = 0; i < legal.Count; i++)
            map[legal[i].to] = legal[i];

        return map;
    }

    /// <summary>
    /// Validates move and starts an animation coroutine (or instant move, depending on executor settings).
    /// Returns true if the move was accepted/started.
    /// </summary>
    public bool TryApplyMove(PieceData movingPiece, ChessMove move)
    {
        if (board == null || executor == null) return false;
        if (gameOver) return false;
        if (isMoveInProgress) return false;
        if (movingPiece == null) return false;
        if (movingPiece.color != sideToMove) return false;

        var map = GetLegalMoveMap(movingPiece);
        if (!map.TryGetValue(move.to, out var legalMove))
            return false;

        StartCoroutine(ApplyMoveRoutine(movingPiece, legalMove));
        return true;
    }

    IEnumerator ApplyMoveRoutine(PieceData movingPiece, ChessMove legalMove)
    {
        isMoveInProgress = true;

        // Stop selection/highlights and lock input during animation
        var input = FindObjectOfType<ChessInputController>();
        if (input != null)
        {
            input.Deselect();
            if (lockInputDuringMoveAnimation) input.enabled = false;
        }
        if (board != null) board.ClearAllTileHighlights();

        PieceData resultPiece = movingPiece;

        // If you didn’t paste the animated executor yet, this still works because ExecuteMoveAnimated
        // falls back to instant when animateMoves = false.
        Sequence seq = executor.ExecuteMoveAnimated(movingPiece, legalMove, lastMove, pd => resultPiece = pd);

        if (seq != null)
            yield return seq.WaitForCompletion();
        else
            yield return null;

        // Apply turn/state AFTER movement finished (prevents mid-animation logic/input issues)
        lastMove = legalMove;
        sideToMove = (sideToMove == PieceColor.White) ? PieceColor.Black : PieceColor.White;

        EvaluateEndConditions();
        if (!gameOver) UpdateStatusMessage();

        RefreshUI();

        // Re-enable input
        if (input != null && lockInputDuringMoveAnimation)
            input.enabled = true;

        isMoveInProgress = false;
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

        statusMessage = inCheck
            ? $"CHECKMATE! {(sideToMove == PieceColor.White ? PieceColor.Black : PieceColor.White)} wins."
            : "STALEMATE! Draw.";
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

    void RefreshUI()
    {
        if (ui == null) return;
        ui.SetTurn($"Turn: {sideToMove}");
        ui.SetStatus(statusMessage);
    }
}
