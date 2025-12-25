using System.Collections.Generic;
using UnityEngine;

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

    public PieceData ExecuteMove(PieceData movingPiece, ChessMove move, ChessMove? lastMove)
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
                if (captured != null) Object.Destroy(captured.gameObject);
            }
        }
        else
        {
            if (ChessMoveGenerator.TryParseSquare(move.to, out int tf, out int tr))
            {
                var captured = occ[tf, tr];
                if (captured != null && captured.color != movingPiece.color)
                    Object.Destroy(captured.gameObject);
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

        if (preservePieceY)
            movingPiece.transform.position = new Vector3(tilePos.x, piecePos.y, tilePos.z);
        else
            movingPiece.transform.position = new Vector3(tilePos.x, tilePos.y + pieceMoveYOffset, tilePos.z);

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

        // No prefab -> logical-only
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

        Object.Destroy(pawn.gameObject);

        var q = Object.Instantiate(prefab, parent);
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
