using System;

[Serializable]
public struct ChessMove
{
    public string from;
    public string to;

    public bool isCapture;

    public bool isEnPassant;
    public string enPassantCapturedSquare; // square of the pawn removed (e.g. "D5")

    public bool isCastleKingSide;
    public bool isCastleQueenSide;

    public bool isPromotion;               // pawn reaches last rank
    public PieceType promotionType;        // we will use Queen

    public static ChessMove Normal(string from, string to, bool capture = false)
    {
        return new ChessMove
        {
            from = from,
            to = to,
            isCapture = capture,
            isEnPassant = false,
            enPassantCapturedSquare = null,
            isCastleKingSide = false,
            isCastleQueenSide = false,
            isPromotion = false,
            promotionType = PieceType.Queen
        };
    }
}
