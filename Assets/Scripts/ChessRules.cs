using System.Collections.Generic;
using UnityEngine;

public static class ChessRules
{
    public static List<ChessMove> GenerateLegalMoves(PieceData piece, PieceData[,] occupancy, ChessMove? lastMove)
    {
        var pseudo = ChessMoveGenerator.GeneratePseudoLegalMoves(piece, occupancy, lastMove);
        var legal = new List<ChessMove>(pseudo.Count);

        if (!ChessMoveGenerator.TryParseSquare(piece.currentSquare, out int fromF, out int fromR))
            return legal;

        for (int i = 0; i < pseudo.Count; i++)
        {
            var move = pseudo[i];

            if (!ChessMoveGenerator.TryParseSquare(move.to, out int toF, out int toR))
                continue;

            // Additional castling rule: king cannot be in check, and cannot pass through attacked squares.
            if (move.isCastleKingSide || move.isCastleQueenSide)
            {
                if (!IsCastlingPathLegal(piece, occupancy, move))
                    continue;
            }

            // Simulate move
            var undo = ApplyMoveForTest(piece, occupancy, move);

            bool kingInCheck = IsKingInCheck(piece.color, occupancy);

            // Undo
            UndoTestMove(occupancy, undo);

            if (!kingInCheck)
                legal.Add(move);
        }

        return legal;
    }

    static bool IsCastlingPathLegal(PieceData king, PieceData[,] occ, ChessMove move)
    {
        // Must not be currently in check
        if (IsKingInCheck(king.color, occ))
            return false;

        // King starts at E1/E8
        if (!ChessMoveGenerator.TryParseSquare(king.currentSquare, out int kf, out int kr))
            return false;

        PieceColor attacker = (king.color == PieceColor.White) ? PieceColor.Black : PieceColor.White;

        // Squares king crosses:
        // King side: E -> F -> G
        // Queen side: E -> D -> C
        if (move.isCastleKingSide)
        {
            if (IsSquareAttacked(5, kr, attacker, occ)) return false; // F
            if (IsSquareAttacked(6, kr, attacker, occ)) return false; // G
        }
        else
        {
            if (IsSquareAttacked(3, kr, attacker, occ)) return false; // D
            if (IsSquareAttacked(2, kr, attacker, occ)) return false; // C
        }

        return true;
    }

    public static bool IsKingInCheck(PieceColor kingColor, PieceData[,] occupancy)
    {
        if (!TryFindKing(kingColor, occupancy, out int kf, out int kr))
        {
            Debug.LogWarning($"King not found for {kingColor}. Check PieceData on king and its currentSquare.");
            return false;
        }

        PieceColor attacker = (kingColor == PieceColor.White) ? PieceColor.Black : PieceColor.White;
        return IsSquareAttacked(kf, kr, attacker, occupancy);
    }

    static bool TryFindKing(PieceColor color, PieceData[,] occ, out int file, out int rank)
    {
        file = -1; rank = -1;
        for (int f = 0; f < 8; f++)
        {
            for (int r = 0; r < 8; r++)
            {
                var p = occ[f, r];
                if (p != null && p.color == color && p.type == PieceType.King)
                {
                    file = f; rank = r;
                    return true;
                }
            }
        }
        return false;
    }

    public static bool IsSquareAttacked(int file, int rank, PieceColor byColor, PieceData[,] occ)
    {
        if (IsAttackedByPawn(file, rank, byColor, occ)) return true;
        if (IsAttackedByKnight(file, rank, byColor, occ)) return true;
        if (IsAttackedByKing(file, rank, byColor, occ)) return true;
        if (IsAttackedBySliders(file, rank, byColor, occ)) return true;
        return false;
    }

    static bool IsAttackedByPawn(int file, int rank, PieceColor byColor, PieceData[,] occ)
    {
        int dir = (byColor == PieceColor.White) ? +1 : -1;
        int originRank = rank - dir;

        int f1 = file - 1;
        int f2 = file + 1;

        if (ChessMoveGenerator.InBounds(f1, originRank))
        {
            var p = occ[f1, originRank];
            if (p != null && p.color == byColor && p.type == PieceType.Pawn) return true;
        }

        if (ChessMoveGenerator.InBounds(f2, originRank))
        {
            var p = occ[f2, originRank];
            if (p != null && p.color == byColor && p.type == PieceType.Pawn) return true;
        }

        return false;
    }

    static bool IsAttackedByKnight(int file, int rank, PieceColor byColor, PieceData[,] occ)
    {
        int[] df = { 1, 2, 2, 1, -1, -2, -2, -1 };
        int[] dr = { 2, 1, -1, -2, -2, -1, 1, 2 };

        for (int i = 0; i < 8; i++)
        {
            int f = file + df[i];
            int r = rank + dr[i];
            if (!ChessMoveGenerator.InBounds(f, r)) continue;

            var p = occ[f, r];
            if (p != null && p.color == byColor && p.type == PieceType.Knight) return true;
        }
        return false;
    }

    static bool IsAttackedByKing(int file, int rank, PieceColor byColor, PieceData[,] occ)
    {
        for (int df = -1; df <= 1; df++)
        for (int dr = -1; dr <= 1; dr++)
        {
            if (df == 0 && dr == 0) continue;
            int f = file + df;
            int r = rank + dr;
            if (!ChessMoveGenerator.InBounds(f, r)) continue;

            var p = occ[f, r];
            if (p != null && p.color == byColor && p.type == PieceType.King) return true;
        }
        return false;
    }

    static bool IsAttackedBySliders(int file, int rank, PieceColor byColor, PieceData[,] occ)
    {
        if (RayAttack(file, rank, byColor, occ, 1, 0, PieceType.Rook, PieceType.Queen)) return true;
        if (RayAttack(file, rank, byColor, occ, -1, 0, PieceType.Rook, PieceType.Queen)) return true;
        if (RayAttack(file, rank, byColor, occ, 0, 1, PieceType.Rook, PieceType.Queen)) return true;
        if (RayAttack(file, rank, byColor, occ, 0, -1, PieceType.Rook, PieceType.Queen)) return true;

        if (RayAttack(file, rank, byColor, occ, 1, 1, PieceType.Bishop, PieceType.Queen)) return true;
        if (RayAttack(file, rank, byColor, occ, 1, -1, PieceType.Bishop, PieceType.Queen)) return true;
        if (RayAttack(file, rank, byColor, occ, -1, 1, PieceType.Bishop, PieceType.Queen)) return true;
        if (RayAttack(file, rank, byColor, occ, -1, -1, PieceType.Bishop, PieceType.Queen)) return true;

        return false;
    }

    static bool RayAttack(int file, int rank, PieceColor byColor, PieceData[,] occ, int df, int dr, PieceType t1, PieceType t2)
    {
        int f = file + df;
        int r = rank + dr;

        while (ChessMoveGenerator.InBounds(f, r))
        {
            var p = occ[f, r];
            if (p == null)
            {
                f += df;
                r += dr;
                continue;
            }

            if (p.color == byColor && (p.type == t1 || p.type == t2))
                return true;

            return false;
        }

        return false;
    }

    // ---- Simulation helpers (for legality test) ----

    struct UndoInfo
    {
        public PieceData movedPiece;
        public int fromF, fromR, toF, toR;
        public PieceData capturedPiece;
        public int capF, capR;
        public bool capturedWasEnPassant;

        public PieceData rookPiece;
        public int rookFromF, rookFromR, rookToF, rookToR;
        public bool wasCastle;
    }

    static UndoInfo ApplyMoveForTest(PieceData piece, PieceData[,] occ, ChessMove move)
    {
        ChessMoveGenerator.TryParseSquare(move.from, out int fromF, out int fromR);
        ChessMoveGenerator.TryParseSquare(move.to, out int toF, out int toR);

        var undo = new UndoInfo
        {
            movedPiece = piece,
            fromF = fromF, fromR = fromR,
            toF = toF, toR = toR,
            capturedPiece = null,
            capF = -1, capR = -1,
            capturedWasEnPassant = false,
            rookPiece = null,
            wasCastle = false
        };

        // Capture (normal)
        if (!move.isEnPassant)
        {
            undo.capturedPiece = occ[toF, toR];
            undo.capF = toF;
            undo.capR = toR;
        }
        else
        {
            // en passant capture square
            if (ChessMoveGenerator.TryParseSquare(move.enPassantCapturedSquare, out int ef, out int er))
            {
                undo.capturedPiece = occ[ef, er];
                undo.capF = ef;
                undo.capR = er;
                undo.capturedWasEnPassant = true;
                occ[ef, er] = null;
            }
        }

        // Move piece
        occ[toF, toR] = piece;
        occ[fromF, fromR] = null;

        // Castling rook move
        if (move.isCastleKingSide || move.isCastleQueenSide)
        {
            undo.wasCastle = true;

            int homeRank = fromR;
            if (move.isCastleKingSide)
            {
                // rook H -> F : (7,home) -> (5,home)
                var rook = occ[7, homeRank];
                undo.rookPiece = rook;
                undo.rookFromF = 7; undo.rookFromR = homeRank;
                undo.rookToF = 5; undo.rookToR = homeRank;

                occ[5, homeRank] = rook;
                occ[7, homeRank] = null;
            }
            else
            {
                // rook A -> D : (0,home) -> (3,home)
                var rook = occ[0, homeRank];
                undo.rookPiece = rook;
                undo.rookFromF = 0; undo.rookFromR = homeRank;
                undo.rookToF = 3; undo.rookToR = homeRank;

                occ[3, homeRank] = rook;
                occ[0, homeRank] = null;
            }
        }

        return undo;
    }

    static void UndoTestMove(PieceData[,] occ, UndoInfo undo)
    {
        // Undo castling rook
        if (undo.wasCastle && undo.rookPiece != null)
        {
            occ[undo.rookFromF, undo.rookFromR] = undo.rookPiece;
            occ[undo.rookToF, undo.rookToR] = null;
        }

        // Undo moved piece
        occ[undo.fromF, undo.fromR] = undo.movedPiece;
        occ[undo.toF, undo.toR] = null;

        // Restore captured
        if (undo.capturedPiece != null)
        {
            occ[undo.capF, undo.capR] = undo.capturedPiece;
        }
    }
}
