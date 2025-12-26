using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using System;

public class ChessMoveExecutor : MonoBehaviour
{
    [Header("Refs")]
    public ChessBoardIndex board;

    [Header("Placement")]
    public bool preservePieceY = true;
    public float pieceMoveYOffset = 0f;

    [Header("Promotion (optional visuals)")]
    public GameObject whiteQueenPrefab;
    public GameObject blackQueenPrefab;

    [Header("DOTween - Move")]
    public bool animateMoves = true;
    public float moveDuration = 0.25f;
    public Ease moveEase = Ease.InOutSine;

    [Header("DOTween - Optional jump")]
    public bool useJump = false;
    public float jumpPower = 0.5f;
    public int jumpNumJumps = 1;

    [Header("DOTween - Capture (Sink + Vanish)")]
    public bool animateCaptures = true;           // if false -> destroy immediately (no visual)
    public float captureAnimDuration = 0.18f;     // sink+vanish duration
    public float captureSinkDistance = 0.35f;     // how far down it sinks
    public Ease captureEase = Ease.InBack;

    [Header("DOTween - Capture (optional spin)")]
    public bool captureSpin = false;
    public float captureSpinDegrees = 180f;

    [Header("DOTween - Capture timing")]
    [Range(0f, 1f)] public float captureStartNormalized = 0.85f; // start capture near end of move when animating moves

    /// <summary>
    /// Animated move. Returns the Sequence so caller can WaitForCompletion().
    /// onComplete receives the final PieceData (may be a new instance due to promotion).
    /// </summary>
    public Sequence ExecuteMoveAnimated(
        PieceData movingPiece,
        ChessMove move,
        ChessMove? lastMove,
        Action<PieceData> onComplete = null)
    {
        if (board == null)
        {
            Debug.LogError("ChessMoveExecutor: board reference missing.");
            onComplete?.Invoke(movingPiece);
            return DOTween.Sequence();
        }
        if (movingPiece == null)
        {
            onComplete?.Invoke(null);
            return DOTween.Sequence();
        }

        // Fallback to instant if animation disabled
        if (!animateMoves)
        {
            var result = ExecuteMoveInstant(movingPiece, move, lastMove);
            onComplete?.Invoke(result);
            return DOTween.Sequence();
        }

        // Build occupancy once at start
        var occ = board.BuildOccupancy(out _);

        // Resolve capture target (if any)
        PieceData captured = null;
        if (move.isEnPassant)
        {
            if (ChessMoveGenerator.TryParseSquare(move.enPassantCapturedSquare, out int cf, out int cr))
                captured = occ[cf, cr];
        }
        else
        {
            if (ChessMoveGenerator.TryParseSquare(move.to, out int tf, out int tr))
                captured = occ[tf, tr];
        }

        // Safety: don't "capture" same-color piece (shouldn't happen, but protects visuals)
        if (captured != null && !move.isEnPassant && captured.color == movingPiece.color)
            captured = null;

        // Resolve target tile
        if (!board.TryGetTile(move.to, out var targetTile))
        {
            Debug.LogError($"ChessMoveExecutor: Tile not found {move.to}");
            onComplete?.Invoke(movingPiece);
            return DOTween.Sequence();
        }

        Vector3 tilePos = targetTile.transform.position;
        Vector3 piecePos = movingPiece.transform.position;

        Vector3 targetPos = preservePieceY
            ? new Vector3(tilePos.x, piecePos.y, tilePos.z)
            : new Vector3(tilePos.x, tilePos.y + pieceMoveYOffset, tilePos.z);

        // Prepare castling rook (if needed)
        PieceData rook = null;
        Vector3 rookTargetPos = Vector3.zero;
        string rookTo = null;

        if (move.isCastleKingSide || move.isCastleQueenSide)
        {
            ResolveCastlingRook(movingPiece.color, move, out rook, out rookTo, out rookTargetPos);
        }

        // Build sequence
        var seq = DOTween.Sequence();

        // Capture visual (sink+vanish), delayed to start near end of the mover's travel
        if (captured != null)
        {
            if (animateCaptures)
            {
                float delay = Mathf.Max(0f, moveDuration * Mathf.Clamp01(captureStartNormalized));
                var capSeq = BuildCaptureSinkVanishSequence(captured, delay);
                seq.Join(capSeq);
            }
            else
            {
                Destroy(captured.gameObject);
            }
        }

        // Move piece
        Tween moveTween = useJump
            ? movingPiece.transform.DOJump(targetPos, jumpPower, jumpNumJumps, moveDuration).SetEase(moveEase)
            : movingPiece.transform.DOMove(targetPos, moveDuration).SetEase(moveEase);

        // Prevent selecting the moving piece mid-flight
        var movingCol = movingPiece.GetComponentInChildren<Collider>();
        if (movingCol != null) movingCol.enabled = false;

        seq.Join(moveTween);

        // Move rook (castle) at the same time
        if (rook != null)
        {
            var rookCol = rook.GetComponentInChildren<Collider>();
            if (rookCol != null) rookCol.enabled = false;

            seq.Join(rook.transform.DOMove(rookTargetPos, moveDuration).SetEase(moveEase));
        }

        // Apply state at the end (so visuals and logic align)
        seq.OnComplete(() =>
        {
            // Update moving piece state
            movingPiece.currentSquare = move.to;
            movingPiece.hasMoved = true;

            // Update rook state (if castling)
            if (rook != null && rookTo != null)
            {
                rook.currentSquare = rookTo;
                rook.hasMoved = true;

                var rookCol2 = rook.GetComponentInChildren<Collider>();
                if (rookCol2 != null) rookCol2.enabled = true;
            }

            // Re-enable moving piece collider
            var col2 = movingPiece.GetComponentInChildren<Collider>();
            if (col2 != null) col2.enabled = true;

            // Promotion (auto-queen)
            PieceData resultPiece = movingPiece;
            if (move.isPromotion && movingPiece.type == PieceType.Pawn)
            {
                resultPiece = PromotePawnToQueen(movingPiece);
                if (resultPiece != null)
                    resultPiece.transform.position = targetPos;
            }

            onComplete?.Invoke(resultPiece);
        });

        return seq;
    }

    // Your existing synchronous behavior, updated to support capture sink+vanish without breaking logic.
    // NOTE: If you rely on instant moves, we "logically remove" captured piece immediately by invalidating its square.
    public PieceData ExecuteMoveInstant(PieceData movingPiece, ChessMove move, ChessMove? lastMove)
    {
        if (board == null)
        {
            Debug.LogError("ChessMoveExecutor: board reference missing.");
            return movingPiece;
        }
        if (movingPiece == null) return null;

        var occ = board.BuildOccupancy(out _);

        // --- Capture
        if (move.isEnPassant)
        {
            if (ChessMoveGenerator.TryParseSquare(move.enPassantCapturedSquare, out int cf, out int cr))
            {
                var captured = occ[cf, cr];
                if (captured != null)
                {
                    HandleCaptureInstantButAnimatedIfEnabled(captured);
                }
            }
        }
        else
        {
            if (ChessMoveGenerator.TryParseSquare(move.to, out int tf, out int tr))
            {
                var captured = occ[tf, tr];
                if (captured != null && captured.color != movingPiece.color)
                {
                    HandleCaptureInstantButAnimatedIfEnabled(captured);
                }
            }
        }

        // --- Move to tile
        if (!board.TryGetTile(move.to, out var targetTile))
        {
            Debug.LogError($"ChessMoveExecutor: Tile not found {move.to}");
            return movingPiece;
        }

        Vector3 tilePos = targetTile.transform.position;
        Vector3 piecePos = movingPiece.transform.position;

        movingPiece.transform.position = preservePieceY
            ? new Vector3(tilePos.x, piecePos.y, tilePos.z)
            : new Vector3(tilePos.x, tilePos.y + pieceMoveYOffset, tilePos.z);

        movingPiece.currentSquare = move.to;
        movingPiece.hasMoved = true;

        // --- Castling rook
        if (move.isCastleKingSide || move.isCastleQueenSide)
            ExecuteCastleRookMove(movingPiece.color, move);

        // --- Promotion (auto-queen)
        if (move.isPromotion && movingPiece.type == PieceType.Pawn)
            movingPiece = PromotePawnToQueen(movingPiece);

        return movingPiece;
    }

    // Builds a DOTween sequence that sinks and vanishes a captured piece, then destroys it.
    // delay is used for animated moves so the capture happens as the attacker arrives.
    Sequence BuildCaptureSinkVanishSequence(PieceData captured, float delay)
    {
        var seq = DOTween.Sequence();
        if (captured == null) return seq;

        // Stop any existing tweens on the captured piece
        captured.transform.DOKill(false);

        // Prevent clicks/selection immediately
        var col = captured.GetComponentInChildren<Collider>();
        if (col != null) col.enabled = false;

        var selectable = captured.GetComponent<PieceSelectable>();
        if (selectable != null) selectable.enabled = false;

        // Ensure it no longer participates in logic if someone rebuilds occupancy before Destroy completes
        captured.currentSquare = null;
        captured.enabled = false;

        Vector3 startPos = captured.transform.position;
        Vector3 sinkPos = startPos + Vector3.down * captureSinkDistance;
        Vector3 startScale = captured.transform.localScale;

        if (delay > 0f)
            seq.AppendInterval(delay);

        seq.Join(captured.transform.DOMove(sinkPos, captureAnimDuration).SetEase(captureEase));
        seq.Join(captured.transform.DOScale(Vector3.zero, captureAnimDuration).SetEase(captureEase));

        if (captureSpin)
        {
            seq.Join(captured.transform.DORotate(
                new Vector3(0f, captureSpinDegrees, 0f),
                captureAnimDuration,
                RotateMode.FastBeyond360));
        }

        seq.OnComplete(() =>
        {
            if (captured != null && captured.gameObject != null)
                Destroy(captured.gameObject);
        });

        // If something interrupts, try to clean up
        seq.OnKill(() =>
        {
            if (captured != null && captured.gameObject != null)
            {
                // Keep it removed logically
                captured.currentSquare = null;
                captured.enabled = false;
            }
        });

        return seq;
    }

    // For instant moves: keep rules/logic correct by immediately removing the piece from the board logic,
    // but still play the sink+vanish tween if enabled.
    void HandleCaptureInstantButAnimatedIfEnabled(PieceData captured)
    {
        if (captured == null) return;

        if (!animateCaptures)
        {
            Destroy(captured.gameObject);
            return;
        }

        // Play immediately (no delay)
        BuildCaptureSinkVanishSequence(captured, 0f);
    }

    void ResolveCastlingRook(PieceColor moverColor, ChessMove move, out PieceData rook, out string rookTo, out Vector3 rookTargetPos)
    {
        rook = null;
        rookTo = null;
        rookTargetPos = Vector3.zero;

        string rookFrom;
        bool white = moverColor == PieceColor.White;

        if (move.isCastleKingSide)
        {
            rookFrom = white ? "H1" : "H8";
            rookTo = white ? "F1" : "F8";
        }
        else
        {
            rookFrom = white ? "A1" : "A8";
            rookTo = white ? "D1" : "D8";
        }

        var pieces = board.FindAllRootPieces();
        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i] != null && pieces[i].currentSquare == rookFrom)
            {
                rook = pieces[i];
                break;
            }
        }

        if (rook == null) return;

        if (!board.TryGetTile(rookTo, out var rookTile))
        {
            rook = null;
            return;
        }

        Vector3 rt = rookTile.transform.position;
        Vector3 rp = rook.transform.position;

        rookTargetPos = preservePieceY
            ? new Vector3(rt.x, rp.y, rt.z)
            : new Vector3(rt.x, rt.y + pieceMoveYOffset, rt.z);
    }

    void ExecuteCastleRookMove(PieceColor moverColor, ChessMove move)
    {
        string rookFrom, rookTo;

        bool white = moverColor == PieceColor.White;
        if (move.isCastleKingSide)
        {
            rookFrom = white ? "H1" : "H8";
            rookTo = white ? "F1" : "F8";
        }
        else
        {
            rookFrom = white ? "A1" : "A8";
            rookTo = white ? "D1" : "D8";
        }

        var pieces = board.FindAllRootPieces();
        PieceData rook = null;
        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i] != null && pieces[i].currentSquare == rookFrom)
            {
                rook = pieces[i];
                break;
            }
        }

        if (rook == null)
        {
            Debug.LogError($"Castling rook not found at {rookFrom}");
            return;
        }

        if (!board.TryGetTile(rookTo, out var rookTile))
        {
            Debug.LogError($"Castling rook tile not found: {rookTo}");
            return;
        }

        Vector3 rt = rookTile.transform.position;
        Vector3 rp = rook.transform.position;

        rook.transform.position = preservePieceY
            ? new Vector3(rt.x, rp.y, rt.z)
            : new Vector3(rt.x, rt.y + pieceMoveYOffset, rt.z);

        rook.currentSquare = rookTo;
        rook.hasMoved = true;
    }

    PieceData PromotePawnToQueen(PieceData pawn)
    {
        GameObject prefab = (pawn.color == PieceColor.White) ? whiteQueenPrefab : blackQueenPrefab;

        if (prefab == null)
        {
            pawn.type = PieceType.Queen;
            return pawn;
        }

        Transform parent = pawn.transform.parent;
        Vector3 pos = pawn.transform.position;
        Quaternion rot = pawn.transform.rotation;
        string square = pawn.currentSquare;
        PieceColor color = pawn.color;

        Destroy(pawn.gameObject);

        var q = Instantiate(prefab, parent);
        q.transform.position = pos;
        q.transform.rotation = rot;
        q.name = $"{prefab.name}_{square}";

        var pd = q.GetComponent<PieceData>();
        if (pd == null) pd = q.AddComponent<PieceData>();
        pd.color = color;
        pd.type = PieceType.Queen;
        pd.currentSquare = square;
        pd.hasMoved = true;

        if (q.GetComponent<PieceSelectable>() == null)
            q.AddComponent<PieceSelectable>();

        return pd;
    }
}
