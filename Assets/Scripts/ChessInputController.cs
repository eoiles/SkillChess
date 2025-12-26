using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class ChessInputController : MonoBehaviour
{
    [Header("Refs")]
    public Camera cam;
    public ChessBoardIndex board;
    public ChessGameController game;
    public PieceInitializer pieceInitializer;

    [Header("Raycast")]
    public LayerMask raycastMask = ~0;

    PieceData selectedPiece;
    PieceSelectable selectedSelectable;
    Dictionary<string, ChessMove> allowedMoves = new Dictionary<string, ChessMove>(32);

    void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (board == null) board = FindObjectOfType<ChessBoardIndex>();
        if (game == null) game = FindObjectOfType<ChessGameController>();
        if (pieceInitializer == null) pieceInitializer = FindObjectOfType<PieceInitializer>();
    }

    void OnEnable()
    {
        if (pieceInitializer != null)
        {
            pieceInitializer.OnPiecesInitialized -= OnPiecesInitialized;
            pieceInitializer.OnPiecesInitialized += OnPiecesInitialized;
        }
    }

    void OnDisable()
    {
        if (pieceInitializer != null)
            pieceInitializer.OnPiecesInitialized -= OnPiecesInitialized;
    }

    void OnPiecesInitialized()
    {
        // Ensure we don't keep references to destroyed pieces after restart.
        Deselect();

        // Re-find in case objects were recreated / references changed.
        if (board == null) board = FindObjectOfType<ChessBoardIndex>();
        if (game == null) game = FindObjectOfType<ChessGameController>();
    }

    void Update()
    {
        if (game != null && game.gameOver) return;

        if (Input.GetMouseButtonDown(0))
            HandleClick();
    }

    void HandleClick()
    {
        // Prevent the "Restart" click from also raycasting the 3D world.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, raycastMask))
            return;

        var piece = hit.collider.GetComponentInParent<PieceData>();
        if (piece != null)
        {
            if (selectedPiece != null && piece.color != selectedPiece.color)
            {
                TryMoveTo(piece.currentSquare);
                return;
            }

            SelectPiece(piece);
            return;
        }

        var tile = hit.collider.GetComponentInParent<TileView>();
        if (tile != null)
        {
            string sq = !string.IsNullOrEmpty(tile.squareName) ? tile.squareName : tile.name;
            TryMoveTo(sq);
            return;
        }

        Deselect();
    }

    void SelectPiece(PieceData piece)
    {
        if (piece == null || game == null) return;
        if (piece.color != game.sideToMove) return;

        if (selectedPiece == piece)
        {
            Deselect();
            return;
        }

        Deselect();

        selectedPiece = piece;
        selectedSelectable = piece.GetComponent<PieceSelectable>();
        if (selectedSelectable != null)
            selectedSelectable.SetSelected(true);

        allowedMoves = game.GetLegalMoveMap(selectedPiece);
        HighlightAllowedTiles();
    }

    void TryMoveTo(string targetSquare)
    {
        if (selectedPiece == null || game == null) return;
        if (string.IsNullOrEmpty(targetSquare)) return;

        if (!allowedMoves.TryGetValue(targetSquare, out var move))
            return;

        bool moved = game.TryApplyMove(selectedPiece, move);
        if (moved)
            Deselect();
    }

    void HighlightAllowedTiles()
    {
        if (board == null) return;

        board.ClearAllTileHighlights();
        foreach (var kv in allowedMoves)
        {
            if (board.TryGetTile(kv.Key, out var tile) && tile != null)
                tile.SetHighlighted(true);
        }
    }

    public void Deselect()
    {
        if (selectedSelectable != null)
            selectedSelectable.SetSelected(false);

        selectedPiece = null;
        selectedSelectable = null;

        allowedMoves.Clear();
        if (board != null) board.ClearAllTileHighlights();
    }
}
