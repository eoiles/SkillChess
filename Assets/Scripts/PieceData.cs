using UnityEngine;

public enum PieceColor { White, Black }
public enum PieceType { Pawn, Rook, Knight, Bishop, Queen, King }

public class PieceData : MonoBehaviour
{
    [Header("Identity")]
    public PieceColor color;
    public PieceType type;

    [Header("Board State")]
    [Tooltip("Example: E2. Must match a tile name.")]
    public string currentSquare = "A1";

    [Header("Flags")]
    public bool hasMoved = false; // needed for castling + pawn double step rules

    public void Set(PieceColor color, PieceType type, string square)
    {
        this.color = color;
        this.type = type;
        this.currentSquare = square;
        this.hasMoved = false;
    }
}
