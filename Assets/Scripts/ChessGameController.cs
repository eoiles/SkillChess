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

    [Header("Optional: Position Loader (FEN/CSV)")]
    public ChessPositionCSVParser positionLoader;
    public bool usePositionLoaderOnRestart = true;

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

    // If a loader wants to set side-to-move (from FEN), it sets this before spawn.
    PieceColor? pendingSideToMoveOverride = null;

    // --- Unity 2023+ safe find helpers (avoids CS0618) ---
    static T FindFirst<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<T>();
#else
        return Object.FindObjectOfType<T>();
#endif
    }

    void Awake()
    {
        DOTween.SetTweensCapacity(1000, 100);

        if (boardGenerator == null) boardGenerator = FindFirst<BoardGeneratorFixed>();
        if (board == null) board = FindFirst<ChessBoardIndex>();
        if (executor == null) executor = FindFirst<ChessMoveExecutor>();
        if (pieceInitializer == null) pieceInitializer = FindFirst<PieceInitializer>();
        if (ui == null) ui = FindFirst<ChessUIController>();
        if (positionLoader == null) positionLoader = FindFirst<ChessPositionCSVParser>();

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

    // Called when PieceInitializer finishes spawning (including stagger/one-by-one animations)
    void OnPiecesInitialized()
    {
        // Always reset baseline state.
        lastMove = null;
        gameOver = false;
        isMoveInProgress = false;

        // Apply pending side-to-move if loader requested it; otherwise default white.
        sideToMove = pendingSideToMoveOverride ?? PieceColor.White;
        pendingSideToMoveOverride = null;

        UpdateStatusMessage();
        RefreshUI();
    }

    /// <summary>
    /// Called by the position loader BEFORE it spawns pieces to set side-to-move after spawn completes.
    /// </summary>
    public void SetPendingSideToMoveOverride(PieceColor? side)
    {
        pendingSideToMoveOverride = side;
    }

    public void RestartGame()
    {
        DOTween.KillAll(false);
        StopAllCoroutines();
        StartCoroutine(RestartRoutine());
    }

    IEnumerator RestartRoutine()
    {
        statusMessage = "Restarting...";
        RefreshUI();

        // Stop selection/highlights + (optional) stop clicks during restart
        var input = FindFirst<ChessInputController>();
        if (input != null)
        {
            input.Deselect();
            if (lockInputDuringMoveAnimation) input.enabled = false;
        }

        isMoveInProgress = false;
        gameOver = false;
        lastMove = null;
        pendingSideToMoveOverride = null;

        // prevent auto-spawn from other scripts
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

        // 4) Refresh board tile cache if needed
        if (board != null)
        {
            board.SendMessage("RebuildTiles", SendMessageOptions.DontRequireReceiver);
            board.SendMessage("CacheTiles", SendMessageOptions.DontRequireReceiver);
            board.ClearAllTileHighlights();
        }

        // 5) Pieces: either load from FEN/CSV (recommended) or standard setup
        bool usedLoader = false;
        if (usePositionLoaderOnRestart && positionLoader != null && positionLoader.isActiveAndEnabled)
        {
            // IMPORTANT: do not let loader clear pieces again, since we already cleared.
            positionLoader.LoadAndApply(clearOldPiecesOverride: false, controller: this);
            usedLoader = true;
        }

        if (!usedLoader && pieceInitializer != null)
        {
            pieceInitializer.InitializePieces();
        }

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

        var input = FindFirst<ChessInputController>();
        if (input != null)
        {
            input.Deselect();
            if (lockInputDuringMoveAnimation) input.enabled = false;
        }

        if (board != null) board.ClearAllTileHighlights();

        PieceData resultPiece = movingPiece;

        Sequence seq = executor.ExecuteMoveAnimated(
            movingPiece,
            legalMove,
            lastMove,
            pd => resultPiece = pd
        );

        if (seq != null)
            yield return seq.WaitForCompletion();
        else
            yield return null;

        lastMove = legalMove;
        sideToMove = (sideToMove == PieceColor.White) ? PieceColor.Black : PieceColor.White;

        EvaluateEndConditions();
        if (!gameOver) UpdateStatusMessage();

        RefreshUI();

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
