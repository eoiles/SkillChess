using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PieceInitializer : MonoBehaviour
{
    [Header("Auto Init")]
    public bool autoInitializeOnStart = true;

    [Header("Tiles")]
    [Tooltip("Object that contains tiles named A1..H8 as direct children. If empty, uses this transform.")]
    public Transform tilesRoot;

    [Header("Piece Parents (assign these to your existing WhitePieces / BlackPieces objects)")]
    public Transform whitePiecesParent;
    public Transform blackPiecesParent;

    [Header("Placement")]
    public float pieceYOffset = 0.0f;
    public bool clearOldPiecesOnStart = true;

    [Header("White Prefabs")]
    public GameObject whitePawn;
    public GameObject whiteRook;
    public GameObject whiteKnight;
    public GameObject whiteBishop;
    public GameObject whiteQueen;
    public GameObject whiteKing;

    [Header("Black Prefabs")]
    public GameObject blackPawn;
    public GameObject blackRook;
    public GameObject blackKnight;
    public GameObject blackBishop;
    public GameObject blackQueen;
    public GameObject blackKing;

    Dictionary<string, Transform> tileByName;

    IEnumerator Start()
    {
        if (!autoInitializeOnStart) yield break;

        // Wait 1 frame so BoardGeneratorFixed.Start() can create/rename tiles first
        yield return null;

        InitializePieces();
    }

    [ContextMenu("Initialize Pieces")]
    public void InitializePieces()
    {
        EnsureRefs();
        CacheTiles();

        if (clearOldPiecesOnStart)
            ClearPieces();

        SpawnStandardChess();
    }

    void EnsureRefs()
    {
        if (tilesRoot == null) tilesRoot = transform;

        if (whitePiecesParent == null)
        {
            var go = new GameObject("WhitePieces");
            go.transform.SetParent(transform, false);
            whitePiecesParent = go.transform;
        }

        if (blackPiecesParent == null)
        {
            var go = new GameObject("BlackPieces");
            go.transform.SetParent(transform, false);
            blackPiecesParent = go.transform;
        }
    }

    void CacheTiles()
    {
        tileByName = new Dictionary<string, Transform>(64);

        foreach (Transform t in tilesRoot)
        {
            if (IsSquareName(t.name))
                tileByName[t.name] = t;
        }
    }

    static bool IsSquareName(string n)
    {
        if (string.IsNullOrEmpty(n) || n.Length < 2 || n.Length > 3) return false;

        char f = n[0];
        if (f < 'A' || f > 'H') return false;

        if (!int.TryParse(n.Substring(1), out int r)) return false;
        return r >= 1 && r <= 8;
    }

    void ClearPieces()
    {
        ClearChildren(whitePiecesParent);
        ClearChildren(blackPiecesParent);
    }

    void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
#if UNITY_EDITOR
            DestroyImmediate(parent.GetChild(i).gameObject);
#else
            Destroy(parent.GetChild(i).gameObject);
#endif
        }
    }

    void SpawnStandardChess()
    {
        // White
        SpawnLine(whitePawn, 2, whitePiecesParent);
        Spawn(whiteRook,   "A1", whitePiecesParent);
        Spawn(whiteKnight, "B1", whitePiecesParent);
        Spawn(whiteBishop, "C1", whitePiecesParent);
        Spawn(whiteQueen,  "D1", whitePiecesParent);
        Spawn(whiteKing,   "E1", whitePiecesParent);
        Spawn(whiteBishop, "F1", whitePiecesParent);
        Spawn(whiteKnight, "G1", whitePiecesParent);
        Spawn(whiteRook,   "H1", whitePiecesParent);

        // Black
        SpawnLine(blackPawn, 7, blackPiecesParent);
        Spawn(blackRook,   "A8", blackPiecesParent);
        Spawn(blackKnight, "B8", blackPiecesParent);
        Spawn(blackBishop, "C8", blackPiecesParent);
        Spawn(blackQueen,  "D8", blackPiecesParent);
        Spawn(blackKing,   "E8", blackPiecesParent);
        Spawn(blackBishop, "F8", blackPiecesParent);
        Spawn(blackKnight, "G8", blackPiecesParent);
        Spawn(blackRook,   "H8", blackPiecesParent);
    }

    void SpawnLine(GameObject prefab, int rank, Transform parent)
    {
        for (char file = 'A'; file <= 'H'; file++)
            Spawn(prefab, $"{file}{rank}", parent);
    }

    void Spawn(GameObject prefab, string square, Transform parent)
    {
        if (prefab == null)
        {
            Debug.LogError($"Missing prefab for {square}");
            return;
        }

        if (tileByName == null || !tileByName.TryGetValue(square, out var tile))
        {
            Debug.LogError($"Tile not found: {square}. Ensure tiles are named A1..H8 and tilesRoot points to them.");
            return;
        }

        var piece = Instantiate(prefab, parent);
        piece.name = $"{prefab.name}_{square}";
        piece.transform.position = tile.position + Vector3.up * pieceYOffset;
        piece.transform.rotation = prefab.transform.rotation;

        // Optional: ensure selectable exists (for your highlight/click system)
        if (piece.GetComponentInChildren<PieceSelectable>() == null)
            piece.AddComponent<PieceSelectable>();
    }
}
