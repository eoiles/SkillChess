using System.Collections.Generic;

public static class ChessMoveGenerator
{
    public const int Size = 8;

    public static bool TryParseSquare(string square, out int file, out int rank)
    {
        file = -1; rank = -1;
        if (string.IsNullOrEmpty(square) || square.Length < 2 || square.Length > 3) return false;

        char f = square[0];
        if (f < 'A' || f > 'H') return false;

        if (!int.TryParse(square.Substring(1), out int r)) return false;
        if (r < 1 || r > 8) return false;

        file = f - 'A';
        rank = r - 1;
        return true;
    }

    public static string ToSquare(int file, int rank)
    {
        char f = (char)('A' + file);
        int r = rank + 1;
        return $"{f}{r}";
    }

    public static bool InBounds(int file, int rank)
    {
        return file >= 0 && file < Size && rank >= 0 && rank < Size;
    }

    public static List<ChessMove> GeneratePseudoLegalMoves(
        PieceData piece,
        PieceData[,] occupancy,
        ChessMove? lastMoveNullable)
    {
        var moves = new List<ChessMove>(32);

        if (!TryParseSquare(piece.currentSquare, out int f0, out int r0))
            return moves;

        switch (piece.type)
        {
            case PieceType.Knight:
                AddKnightMoves(piece, occupancy, f0, r0, moves);
                break;

            case PieceType.Bishop:
                AddSliderMoves(piece, occupancy, f0, r0, moves,
                    new (int df, int dr)[] { (1,1), (1,-1), (-1,1), (-1,-1) });
                break;

            case PieceType.Rook:
                AddSliderMoves(piece, occupancy, f0, r0, moves,
                    new (int df, int dr)[] { (1,0), (-1,0), (0,1), (0,-1) });
                break;

            case PieceType.Queen:
                AddSliderMoves(piece, occupancy, f0, r0, moves,
                    new (int df, int dr)[] { (1,0), (-1,0), (0,1), (0,-1), (1,1), (1,-1), (-1,1), (-1,-1) });
                break;

            case PieceType.King:
                AddKingMoves(piece, occupancy, f0, r0, moves);
                AddCastlingPseudoMoves(piece, occupancy, f0, r0, moves);
                break;

            case PieceType.Pawn:
                AddPawnMoves(piece, occupancy, f0, r0, moves, lastMoveNullable);
                break;
        }

        return moves;
    }

    static void AddKnightMoves(PieceData piece, PieceData[,] occ, int f0, int r0, List<ChessMove> moves)
    {
        int[] df = { 1, 2, 2, 1, -1, -2, -2, -1 };
        int[] dr = { 2, 1, -1, -2, -2, -1, 1, 2 };

        for (int i = 0; i < 8; i++)
        {
            int f = f0 + df[i];
            int r = r0 + dr[i];
            if (!InBounds(f, r)) continue;

            var target = occ[f, r];
            if (target == null)
                moves.Add(ChessMove.Normal(ToSquare(f0, r0), ToSquare(f, r), false));
            else if (target.color != piece.color)
                moves.Add(ChessMove.Normal(ToSquare(f0, r0), ToSquare(f, r), true));
        }
    }

    static void AddKingMoves(PieceData piece, PieceData[,] occ, int f0, int r0, List<ChessMove> moves)
    {
        for (int df = -1; df <= 1; df++)
        for (int dr = -1; dr <= 1; dr++)
        {
            if (df == 0 && dr == 0) continue;
            int f = f0 + df;
            int r = r0 + dr;
            if (!InBounds(f, r)) continue;

            var target = occ[f, r];
            if (target == null)
                moves.Add(ChessMove.Normal(ToSquare(f0, r0), ToSquare(f, r), false));
            else if (target.color != piece.color)
                moves.Add(ChessMove.Normal(ToSquare(f0, r0), ToSquare(f, r), true));
        }
    }

    static void AddSliderMoves(
        PieceData piece, PieceData[,] occ, int f0, int r0, List<ChessMove> moves,
        (int df, int dr)[] directions)
    {
        foreach (var (df, dr) in directions)
        {
            int f = f0 + df;
            int r = r0 + dr;

            while (InBounds(f, r))
            {
                var target = occ[f, r];
                if (target == null)
                {
                    moves.Add(ChessMove.Normal(ToSquare(f0, r0), ToSquare(f, r), false));
                }
                else
                {
                    if (target.color != piece.color)
                        moves.Add(ChessMove.Normal(ToSquare(f0, r0), ToSquare(f, r), true));
                    break;
                }

                f += df;
                r += dr;
            }
        }
    }

    static void AddPawnMoves(PieceData piece, PieceData[,] occ, int f0, int r0, List<ChessMove> moves, ChessMove? lastMoveNullable)
    {
        int dir = (piece.color == PieceColor.White) ? +1 : -1;
        int startRank = (piece.color == PieceColor.White) ? 1 : 6; // 2->1, 7->6
        int promotionRank = (piece.color == PieceColor.White) ? 7 : 0;

        string fromSq = ToSquare(f0, r0);

        // Forward 1
        int r1 = r0 + dir;
        if (InBounds(f0, r1) && occ[f0, r1] == null)
        {
            var m = ChessMove.Normal(fromSq, ToSquare(f0, r1), false);
            if (r1 == promotionRank) { m.isPromotion = true; m.promotionType = PieceType.Queen; }
            moves.Add(m);

            // Forward 2
            int r2 = r0 + 2 * dir;
            if (r0 == startRank && InBounds(f0, r2) && occ[f0, r2] == null)
                moves.Add(ChessMove.Normal(fromSq, ToSquare(f0, r2), false));
        }

        // Captures
        int[] capFiles = { f0 - 1, f0 + 1 };
        foreach (int f in capFiles)
        {
            if (!InBounds(f, r1)) continue;
            var target = occ[f, r1];
            if (target != null && target.color != piece.color)
            {
                var m = ChessMove.Normal(fromSq, ToSquare(f, r1), true);
                if (r1 == promotionRank) { m.isPromotion = true; m.promotionType = PieceType.Queen; }
                moves.Add(m);
            }
        }

        // En passant (pseudo)
        if (lastMoveNullable.HasValue)
        {
            var lm = lastMoveNullable.Value;

            // Last move must be a pawn moving two squares, landing adjacent to this pawn.
            if (TryParseSquare(lm.from, out int lf0, out int lr0) &&
                TryParseSquare(lm.to, out int lf1, out int lr1))
            {
                int movedTwo = System.Math.Abs(lr1 - lr0);
                if (movedTwo == 2)
                {
                    // The pawn that moved is on (lf1, lr1). For en passant:
                    // our pawn must be on same rank as that pawn, adjacent file.
                    if (lr1 == r0 && System.Math.Abs(lf1 - f0) == 1)
                    {
                        // target square is behind that pawn in our forward direction
                        int epTargetFile = lf1;
                        int epTargetRank = r0 + dir;

                        if (InBounds(epTargetFile, epTargetRank) && occ[epTargetFile, epTargetRank] == null)
                        {
                            var ep = ChessMove.Normal(fromSq, ToSquare(epTargetFile, epTargetRank), true);
                            ep.isEnPassant = true;
                            ep.enPassantCapturedSquare = ToSquare(lf1, lr1);
                            moves.Add(ep);
                        }
                    }
                }
            }
        }
    }

    static void AddCastlingPseudoMoves(PieceData king, PieceData[,] occ, int f0, int r0, List<ChessMove> moves)
    {
        // Only from starting squares, and king must not have moved
        if (king.hasMoved) return;

        // White king start = E1 (4,0), Black = E8 (4,7)
        bool isWhite = king.color == PieceColor.White;
        int homeRank = isWhite ? 0 : 7;
        if (r0 != homeRank || f0 != 4) return;

        string fromSq = ToSquare(f0, r0);

        // King side: rook at H1/H8 => (7,homeRank), squares between (5,6) must be empty
        var rookK = occ[7, homeRank];
        if (rookK != null && rookK.color == king.color && rookK.type == PieceType.Rook && !rookK.hasMoved)
        {
            if (occ[5, homeRank] == null && occ[6, homeRank] == null)
            {
                var m = ChessMove.Normal(fromSq, ToSquare(6, homeRank), false);
                m.isCastleKingSide = true;
                moves.Add(m);
            }
        }

        // Queen side: rook at A1/A8 => (0,homeRank), squares between (1,2,3) empty
        var rookQ = occ[0, homeRank];
        if (rookQ != null && rookQ.color == king.color && rookQ.type == PieceType.Rook && !rookQ.hasMoved)
        {
            if (occ[1, homeRank] == null && occ[2, homeRank] == null && occ[3, homeRank] == null)
            {
                var m = ChessMove.Normal(fromSq, ToSquare(2, homeRank), false);
                m.isCastleQueenSide = true;
                moves.Add(m);
            }
        }
    }
}
