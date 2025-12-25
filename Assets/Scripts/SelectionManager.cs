using System.Collections.Generic;
using UnityEngine;

public class SelectionManager : MonoBehaviour
{
    [Header("References")]
    public Camera cam; // if null, uses Camera.main

    [Header("Raycast")]
    public LayerMask raycastMask = ~0; // everything by default

    [Header("Behavior")]
    public bool highlightAllTilesWhenPieceSelected = true;

    PieceSelectable selectedPiece;
    List<TileView> allTiles = new List<TileView>();

    void Start()
    {
        if (cam == null) cam = Camera.main;

        // Cache tiles (expects TileView on each tile)
        allTiles.Clear();
        allTiles.AddRange(FindObjectsOfType<TileView>(true));
        SetAllTilesHighlighted(false);
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
            HandleClick();
    }

    void HandleClick()
    {
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, raycastMask))
            return;

        // Clicked a piece?
        var piece = hit.collider.GetComponentInParent<PieceSelectable>();
        if (piece != null)
        {
            SelectPiece(piece);
            return;
        }

        // Clicked a tile?
        var tile = hit.collider.GetComponentInParent<TileView>();
        if (tile != null)
        {
            TryMoveSelectedPieceTo(tile);
            return;
        }

        // Clicked something else -> deselect
        DeselectPiece();
    }

    void SelectPiece(PieceSelectable piece)
    {
        if (selectedPiece == piece) return;

        if (selectedPiece != null)
            selectedPiece.SetSelected(false);

        selectedPiece = piece;
        selectedPiece.SetSelected(true);

        if (highlightAllTilesWhenPieceSelected)
            SetAllTilesHighlighted(true);
    }

    void DeselectPiece()
    {
        if (selectedPiece != null)
            selectedPiece.SetSelected(false);

        selectedPiece = null;
        SetAllTilesHighlighted(false);
    }

    void TryMoveSelectedPieceTo(TileView tile)
    {
        if (selectedPiece == null) return;

        // Move anywhere: keep the piece's current Y, snap XZ to tile
        Vector3 p = selectedPiece.transform.position;
        Vector3 t = tile.transform.position;
        selectedPiece.transform.position = new Vector3(t.x, p.y, t.z);

        // End selection after move (you can change this if you want)
        DeselectPiece();
    }

    void SetAllTilesHighlighted(bool on)
    {
        // Refresh list if needed (in case board is regenerated at runtime)
        if (allTiles.Count == 0)
            allTiles.AddRange(FindObjectsOfType<TileView>(true));

        foreach (var t in allTiles)
            if (t != null) t.SetHighlighted(on);
    }
}
