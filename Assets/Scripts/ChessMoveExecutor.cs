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

    [Header("DOTween - Capture (optional)")]
    public bool animateCaptures = false;
    public float captureAnimDuration = 0.12f;

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

        // Optional capture animation: disable collider immediately to avoid selection/raycast issues during tween
        if (captured != null)
        {
            var col = captured.GetComponentInChildren<Collider>();
            if (col != null) col.enabled = false;

            if (animateCaptures)
            {
                seq.Join(captured.transform
                    .DOScale(0f, captureAnimDuration)
                    .SetEase(Ease.InBack));
                seq.AppendCallback(() =>
                {
                    if (captured != null) Destroy(captured.gameObject);
                });
            }
            else
            {
                // Logical + visual removal immediately (matches your current behavior)
                Destroy(captured.gameObject);
            }
        }

        // Move piece
        Tween moveTween = useJump
            ? movingPiece.transform.DOJump(targetPos, jumpPower, jumpNumJumps, moveDuration).SetEase(moveEase)
            : movingPiece.transform.DOMove(targetPos, moveDuration).SetEase(moveEase);

        seq.Join(moveTween);

        // Move rook (castle) at the same time
        if (rook != null)
        {
            // Disable rook collider during animation too (optional but safe)
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

                // Re-enable rook collider
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
                // Ensure promoted piece ends exactly on the target
                if (resultPiece != null)
                    resultPiece.transform.position = targetPos;
            }

            onComplete?.Invoke(resultPiece);
        });

        return seq;
    }

    // Your existing synchronous behavior, extracted unchanged
    public PieceData ExecuteMoveInstant(PieceData movingPiece, ChessMove move, ChessMove? lastMove)
    {
        if (board == null)
        {
            Debug.LogError("ChessMoveExecutor: board reference missing.");
            return movingPiece;
        }
        if (movingPiece == null) return null;

        var occ = board.BuildOccupancy(out _);

        if (move.isEnPassant)
        {
            if (ChessMoveGenerator.TryParseSquare(move.enPassantCapturedSquare, out int cf, out int cr))
            {
                var captured = occ[cf, cr];
                if (captured != null) Destroy(captured.gameObject);
            }
        }
        else
        {
            if (ChessMoveGenerator.TryParseSquare(move.to, out int tf, out int tr))
            {
                var captured = occ[tf, tr];
                if (captured != null && captured.color != movingPiece.color)
                    Destroy(captured.gameObject);
            }
        }

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

        if (move.isCastleKingSide || move.isCastleQueenSide)
            ExecuteCastleRookMove(movingPiece.color, move);

        if (move.isPromotion && movingPiece.type == PieceType.Pawn)
            movingPiece = PromotePawnToQueen(movingPiece);

        return movingPiece;
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
        // keep your existing method (unchanged)
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
