using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class PieceInitializer : MonoBehaviour
{
    [Header("Auto Init")]
    public bool autoInitializeOnStart = false;

    [Header("Tiles")]
    [Tooltip("Object that contains tiles named A1..H8. If empty, uses this transform.")]
    public Transform tilesRoot;

    [Tooltip("If true, searches tilesRoot recursively (supports nested tile hierarchy).")]
    public bool includeNestedTiles = true;

    [Header("Parents (assign existing objects or leave empty to auto-create)")]
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

    public enum SpawnEffect
    {
        None,
        ScalePop,
        DropAndPop,
        RiseFromBelow,
        SlideFromRight,
        SlideFromLeft,
        SpinAndPop,
        PunchScale
    }

    public enum SpawnTiming
    {
        Stagger,   // overlap (old)
        OneByOne   // STRICT sequential
    }

    public enum SpawnOrder
    {
        ClassicRows,   // pawns row then back row etc. (old)
        SnakeByRanks,  // A1->H1 then H2->A2 ...
        SnakeByFiles,  // A1->A8 then B8->B1 ...
        SpiralInward,  // outside -> center
        Random
    }

    [Header("Spawn Animation")]
    public SpawnEffect spawnEffect = SpawnEffect.DropAndPop;

    [Header("Timing")]
    public SpawnTiming spawnTiming = SpawnTiming.OneByOne;

    [Tooltip("Used when SpawnTiming = Stagger")]
    [Min(0f)] public float spawnStagger = 0.02f;

    [Tooltip("Used when SpawnTiming = OneByOne")]
    [Min(0f)] public float spawnGap = 0.03f;

    [Min(0f)] public float spawnDuration = 0.35f;

    [Header("Order")]
    public SpawnOrder spawnOrder = SpawnOrder.SnakeByRanks;

    [Header("Effect Params")]
    public float verticalDistance = 0.6f;
    public float horizontalDistance = 0.8f;

    public Ease moveEase = Ease.OutCubic;
    public Ease scaleEase = Ease.OutBack;

    public float spinDegrees = 360f;

    public float punchAmount = 0.25f;
    public int punchVibrato = 10;
    [Range(0f, 1f)] public float punchElasticity = 0.6f;

    Dictionary<string, Transform> tileByName;

    public System.Action OnPiecesInitialized;

    int spawnIndex = 0;
    Coroutine initRoutine;

    IEnumerator Start()
    {
        if (!autoInitializeOnStart) yield break;
        yield return null;
        InitializePieces();
    }

    [ContextMenu("Initialize Pieces")]
    public void InitializePieces()
    {
        if (initRoutine != null) StopCoroutine(initRoutine);
        initRoutine = StartCoroutine(InitializePiecesRoutine());
    }

    public void ClearPiecesOnly()
    {
        EnsureRefs();
        ClearPiecesInternal();
    }

    IEnumerator InitializePiecesRoutine()
    {
        EnsureRefs();
        CacheTiles();

        if (clearOldPiecesOnStart)
        {
            ClearPiecesInternal();
            if (Application.isPlaying)
                yield return null; // ensure old colliders are gone
        }

        spawnIndex = 0;

        // Build all spawn requests first, then order them, then spawn in that order.
        var requests = BuildStandardRequests();
        ApplyOrder(requests);

        for (int i = 0; i < requests.Count; i++)
        {
            var r = requests[i];
            Spawn(r.prefab, r.square, r.color, r.type, r.parent);
        }

        OnPiecesInitialized?.Invoke();
        initRoutine = null;
    }

    struct SpawnRequest
    {
        public GameObject prefab;
        public string square;
        public PieceColor color;
        public PieceType type;
        public Transform parent;

        public SpawnRequest(GameObject p, string sq, PieceColor c, PieceType t, Transform par)
        { prefab = p; square = sq; color = c; type = t; parent = par; }
    }

    List<SpawnRequest> BuildStandardRequests()
    {
        var list = new List<SpawnRequest>(32);

        // White pawns
        for (char f = 'A'; f <= 'H'; f++)
            list.Add(new SpawnRequest(whitePawn, $"{f}2", PieceColor.White, PieceType.Pawn, whitePiecesParent));

        // White back rank
        list.Add(new SpawnRequest(whiteRook,   "A1", PieceColor.White, PieceType.Rook,   whitePiecesParent));
        list.Add(new SpawnRequest(whiteKnight, "B1", PieceColor.White, PieceType.Knight, whitePiecesParent));
        list.Add(new SpawnRequest(whiteBishop, "C1", PieceColor.White, PieceType.Bishop, whitePiecesParent));
        list.Add(new SpawnRequest(whiteQueen,  "D1", PieceColor.White, PieceType.Queen,  whitePiecesParent));
        list.Add(new SpawnRequest(whiteKing,   "E1", PieceColor.White, PieceType.King,   whitePiecesParent));
        list.Add(new SpawnRequest(whiteBishop, "F1", PieceColor.White, PieceType.Bishop, whitePiecesParent));
        list.Add(new SpawnRequest(whiteKnight, "G1", PieceColor.White, PieceType.Knight, whitePiecesParent));
        list.Add(new SpawnRequest(whiteRook,   "H1", PieceColor.White, PieceType.Rook,   whitePiecesParent));

        // Black pawns
        for (char f = 'A'; f <= 'H'; f++)
            list.Add(new SpawnRequest(blackPawn, $"{f}7", PieceColor.Black, PieceType.Pawn, blackPiecesParent));

        // Black back rank
        list.Add(new SpawnRequest(blackRook,   "A8", PieceColor.Black, PieceType.Rook,   blackPiecesParent));
        list.Add(new SpawnRequest(blackKnight, "B8", PieceColor.Black, PieceType.Knight, blackPiecesParent));
        list.Add(new SpawnRequest(blackBishop, "C8", PieceColor.Black, PieceType.Bishop, blackPiecesParent));
        list.Add(new SpawnRequest(blackQueen,  "D8", PieceColor.Black, PieceType.Queen,  blackPiecesParent));
        list.Add(new SpawnRequest(blackKing,   "E8", PieceColor.Black, PieceType.King,   blackPiecesParent));
        list.Add(new SpawnRequest(blackBishop, "F8", PieceColor.Black, PieceType.Bishop, blackPiecesParent));
        list.Add(new SpawnRequest(blackKnight, "G8", PieceColor.Black, PieceType.Knight, blackPiecesParent));
        list.Add(new SpawnRequest(blackRook,   "H8", PieceColor.Black, PieceType.Rook,   blackPiecesParent));

        return list;
    }

    void ApplyOrder(List<SpawnRequest> reqs)
    {
        if (spawnOrder == SpawnOrder.ClassicRows) return;

        if (spawnOrder == SpawnOrder.Random)
        {
            for (int i = 0; i < reqs.Count; i++)
            {
                int j = Random.Range(i, reqs.Count);
                (reqs[i], reqs[j]) = (reqs[j], reqs[i]);
            }
            return;
        }

        if (spawnOrder == SpawnOrder.SpiralInward)
        {
            var spiral = BuildSpiralSquares8x8();
            var idx = new Dictionary<string, int>(64);
            for (int i = 0; i < spiral.Count; i++) idx[spiral[i]] = i;

            reqs.Sort((a, b) =>
            {
                int ia = idx.TryGetValue(a.square, out var va) ? va : 999;
                int ib = idx.TryGetValue(b.square, out var vb) ? vb : 999;
                return ia.CompareTo(ib);
            });
            return;
        }

        // Snake sort by computed index on an 8x8 grid (A1 = 0,0)
        reqs.Sort((a, b) =>
        {
            SquareToCoord(a.square, out int ax, out int ay);
            SquareToCoord(b.square, out int bx, out int by);

            int oa = (spawnOrder == SpawnOrder.SnakeByRanks) ? SnakeRankIndex(ax, ay) : SnakeFileIndex(ax, ay);
            int ob = (spawnOrder == SpawnOrder.SnakeByRanks) ? SnakeRankIndex(bx, by) : SnakeFileIndex(bx, by);
            return oa.CompareTo(ob);
        });
    }

    int SnakeRankIndex(int x, int y)
    {
        // y=0..7 ranks 1..8. Even rank -> left to right. Odd -> right to left.
        return (y * 8) + ((y % 2 == 0) ? x : (7 - x));
    }

    int SnakeFileIndex(int x, int y)
    {
        // x=0..7 files A..H. Even file -> bottom to top. Odd -> top to bottom.
        return (x * 8) + ((x % 2 == 0) ? y : (7 - y));
    }

    static void SquareToCoord(string sq, out int x, out int y)
    {
        x = sq[0] - 'A';
        y = int.Parse(sq.Substring(1)) - 1;
    }

    static string CoordToSquare(int x, int y)
    {
        char f = (char)('A' + x);
        int r = y + 1;
        return $"{f}{r}";
    }

    List<string> BuildSpiralSquares8x8()
    {
        var res = new List<string>(64);
        int left = 0, right = 7, bottom = 0, top = 7;

        while (left <= right && bottom <= top)
        {
            for (int x = left; x <= right; x++) res.Add(CoordToSquare(x, bottom));
            for (int y = bottom + 1; y <= top; y++) res.Add(CoordToSquare(right, y));

            if (bottom != top)
                for (int x = right - 1; x >= left; x--) res.Add(CoordToSquare(x, top));

            if (left != right)
                for (int y = top - 1; y > bottom; y--) res.Add(CoordToSquare(left, y));

            left++; right--; bottom++; top--;
        }

        return res;
    }

    void EnsureRefs()
    {
        if (tilesRoot == null) tilesRoot = transform;

        if (whitePiecesParent == null)
        {
            var existing = transform.Find("WhitePieces");
            if (existing != null) whitePiecesParent = existing;
            else
            {
                var go = new GameObject("WhitePieces");
                go.transform.SetParent(transform, false);
                whitePiecesParent = go.transform;
            }
        }

        if (blackPiecesParent == null)
        {
            var existing = transform.Find("BlackPieces");
            if (existing != null) blackPiecesParent = existing;
            else
            {
                var go = new GameObject("BlackPieces");
                go.transform.SetParent(transform, false);
                blackPiecesParent = go.transform;
            }
        }
    }

    void CacheTiles()
    {
        tileByName = new Dictionary<string, Transform>(64);

        if (!includeNestedTiles)
        {
            foreach (Transform t in tilesRoot)
                if (IsSquareName(t.name))
                    tileByName[t.name] = t;
            return;
        }

        var all = tilesRoot.GetComponentsInChildren<Transform>(true);
        foreach (var t in all)
        {
            if (t == tilesRoot) continue;
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

    void ClearPiecesInternal()
    {
        ClearChildrenSafe(whitePiecesParent);
        ClearChildrenSafe(blackPiecesParent);
    }

    void ClearChildrenSafe(Transform parent)
    {
        if (parent == null) return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            child.DOKill(true);

#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(child.gameObject);
            else Destroy(child.gameObject);
#else
            Destroy(child.gameObject);
#endif
        }
    }

    float GetDelay(int index)
    {
        if (spawnTiming == SpawnTiming.OneByOne)
            return index * (spawnDuration + spawnGap); // STRICT sequential
        return index * spawnStagger;                  // old overlapping stagger
    }

    void Spawn(GameObject prefab, string square, PieceColor color, PieceType type, Transform parent)
    {
        if (prefab == null)
        {
            Debug.LogError($"Missing prefab for {color} {type} at {square}");
            return;
        }

        if (tileByName == null || !tileByName.TryGetValue(square, out var tile))
        {
            Debug.LogError($"Tile not found: {square}. Ensure tiles are named A1..H8 and tilesRoot points to them.");
            return;
        }

        var piece = Instantiate(prefab, parent);
        piece.name = $"{prefab.name}_{square}";
        piece.transform.rotation = prefab.transform.rotation;

        Vector3 targetPos = tile.position + Vector3.up * pieceYOffset;
        ApplySpawnEffect(piece.transform, targetPos);

        var data = piece.GetComponent<PieceData>();
        if (data == null) data = piece.AddComponent<PieceData>();
        data.Set(color, type, square);

        if (piece.GetComponent<PieceSelectable>() == null)
            piece.AddComponent<PieceSelectable>();

        EnsureCollider(piece);
    }

    void ApplySpawnEffect(Transform t, Vector3 targetPos)
    {
        t.DOKill();

        float delay = GetDelay(spawnIndex);
        spawnIndex++;

        Vector3 defaultScale = Vector3.one;

        switch (spawnEffect)
        {
            case SpawnEffect.None:
                t.position = targetPos;
                t.localScale = defaultScale;
                return;

            case SpawnEffect.ScalePop:
                t.position = targetPos;
                t.localScale = Vector3.zero;
                t.DOScale(defaultScale, spawnDuration).SetDelay(delay).SetEase(scaleEase);
                return;

            case SpawnEffect.DropAndPop:
                t.position = targetPos + Vector3.up * verticalDistance;
                t.localScale = Vector3.zero;
                t.DOMove(targetPos, spawnDuration).SetDelay(delay).SetEase(moveEase);
                t.DOScale(defaultScale, spawnDuration).SetDelay(delay).SetEase(scaleEase);
                return;

            case SpawnEffect.RiseFromBelow:
                t.position = targetPos - Vector3.up * verticalDistance;
                t.localScale = defaultScale;
                t.DOMove(targetPos, spawnDuration).SetDelay(delay).SetEase(moveEase);
                return;

            case SpawnEffect.SlideFromRight:
                t.position = targetPos + Vector3.right * horizontalDistance;
                t.localScale = defaultScale;
                t.DOMove(targetPos, spawnDuration).SetDelay(delay).SetEase(moveEase);
                return;

            case SpawnEffect.SlideFromLeft:
                t.position = targetPos + Vector3.left * horizontalDistance;
                t.localScale = defaultScale;
                t.DOMove(targetPos, spawnDuration).SetDelay(delay).SetEase(moveEase);
                return;

            case SpawnEffect.SpinAndPop:
                t.position = targetPos;
                t.localScale = Vector3.zero;
                t.localRotation = Quaternion.identity;
                t.DORotate(new Vector3(0f, spinDegrees, 0f), spawnDuration, RotateMode.FastBeyond360)
                    .SetDelay(delay).SetEase(Ease.OutCubic);
                t.DOScale(defaultScale, spawnDuration).SetDelay(delay).SetEase(scaleEase);
                return;

            case SpawnEffect.PunchScale:
                t.position = targetPos;
                t.localScale = defaultScale;
                t.DOPunchScale(Vector3.one * punchAmount, spawnDuration, punchVibrato, punchElasticity)
                    .SetDelay(delay);
                return;
        }
    }

    void EnsureCollider(GameObject piece)
    {
        if (piece.GetComponentInChildren<Collider>() != null) return;

        var renderers = piece.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            var box = piece.AddComponent<BoxCollider>();
            box.center = Vector3.up * 0.5f;
            box.size = Vector3.one;
            return;
        }

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);

        var boxCol = piece.AddComponent<BoxCollider>();
        boxCol.center = piece.transform.InverseTransformPoint(b.center);
        boxCol.size = new Vector3(
            b.size.x / piece.transform.lossyScale.x,
            b.size.y / piece.transform.lossyScale.y,
            b.size.z / piece.transform.lossyScale.z
        );
    }
}
