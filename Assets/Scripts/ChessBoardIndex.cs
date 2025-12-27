// ChessBoardIndex.cs
using System.Collections.Generic;
using UnityEngine;

public class ChessBoardIndex : MonoBehaviour
{
    [Header("Tiles")]
    [Tooltip("Transform that contains tiles named A1..H8 as direct children. If empty, uses this transform.")]
    public Transform tilesRoot;

    Dictionary<string, TileView> tilesByName = new Dictionary<string, TileView>(64);

    // --- Unity 2023+ safe find helpers (avoids CS0618) ---
    static T[] FindAll<T>(bool includeInactive) where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<T>(
            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );
#else
        return Object.FindObjectsOfType<T>(includeInactive);
#endif
    }

    void Awake()
    {
        if (tilesRoot == null) tilesRoot = transform;
        CacheTiles();
    }

    public void CacheTiles()
    {
        tilesByName.Clear();

        // Prefer direct children A1..H8
        foreach (Transform child in tilesRoot)
        {
            var tv = child.GetComponent<TileView>();
            if (tv == null) continue;

            string key = !string.IsNullOrEmpty(tv.squareName) ? tv.squareName : child.name;
            if (!string.IsNullOrEmpty(key))
                tilesByName[key] = tv;
        }

        // Fallback: find any TileView in scene
        if (tilesByName.Count < 60)
        {
            var all = FindAll<TileView>(true);
            foreach (var tv in all)
            {
                if (tv == null) continue;
                string key = !string.IsNullOrEmpty(tv.squareName) ? tv.squareName : tv.name;
                if (!string.IsNullOrEmpty(key))
                    tilesByName[key] = tv;
            }
        }
    }

    public bool TryGetTile(string square, out TileView tile)
    {
        if (string.IsNullOrEmpty(square))
        {
            tile = null;
            return false;
        }

        if (tilesByName.Count < 60) CacheTiles();
        return tilesByName.TryGetValue(square, out tile) && tile != null;
    }

    public void ClearAllTileHighlights()
    {
        if (tilesByName.Count < 60) CacheTiles();
        foreach (var kv in tilesByName)
        {
            if (kv.Value != null)
                kv.Value.SetHighlighted(false);
        }
    }

    /// <summary>
    /// Root pieces = PieceData on the same GameObject as PieceSelectable.
    /// This prevents child-mesh PieceData issues.
    /// </summary>
    public List<PieceData> FindAllRootPieces()
    {
        var all = FindAll<PieceData>(true);
        var roots = new List<PieceData>(all.Length);

        for (int i = 0; i < all.Length; i++)
        {
            var pd = all[i];
            if (pd == null) continue;
            if (pd.GetComponent<PieceSelectable>() == null) continue;
            roots.Add(pd);
        }

        return roots;
    }

    public PieceData[,] BuildOccupancy(out bool hasBothKings)
    {
        var occ = new PieceData[8, 8];
        bool hasWhiteKing = false;
        bool hasBlackKing = false;

        var pieces = FindAllRootPieces();
        for (int i = 0; i < pieces.Count; i++)
        {
            var p = pieces[i];
            if (p == null) continue;

            if (!ChessMoveGenerator.TryParseSquare(p.currentSquare, out int f, out int r)) continue;
            if (!ChessMoveGenerator.InBounds(f, r)) continue;

            if (occ[f, r] != null && occ[f, r] != p)
                Debug.LogError($"Two pieces claim square {p.currentSquare}: {occ[f, r].name} and {p.name}");

            occ[f, r] = p;

            if (p.type == PieceType.King)
            {
                if (p.color == PieceColor.White) hasWhiteKing = true;
                if (p.color == PieceColor.Black) hasBlackKing = true;
            }
        }

        hasBothKings = hasWhiteKing && hasBlackKing;
        return occ;
    }
}
