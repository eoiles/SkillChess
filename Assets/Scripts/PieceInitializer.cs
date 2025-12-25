using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class PieceInitializer : MonoBehaviour
{
    [Header("Auto Init")]
    public bool autoInitializeOnStart = true;

    [Header("Tiles")]
    [Tooltip("Object that contains tiles named A1..H8. If tiles are nested, enable Include Nested Tiles. If empty, uses this transform.")]
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
        DropBounce,
        DropAndPop,
        RiseFromBelow,
        SlideFromRight,
        SlideFromLeft,
        SpinAndPop,
        FlipY,
        PunchScale,
        OvershootScale,
        ShakeIn
    }

    [Header("Spawn Animation (choose one)")]
    public SpawnEffect spawnEffect = SpawnEffect.DropAndPop;

    [Min(0f)]
    public float spawnDuration = 0.35f;

    [Min(0f)]
    public float spawnStagger = 0.02f;

    [Tooltip("Used by Drop/Rise effects (world units).")]
    public float verticalDistance = 0.6f;

    [Tooltip("Used by Slide effects (world units).")]
    public float horizontalDistance = 0.8f;

    public Ease moveEase = Ease.OutCubic;
    public Ease scaleEase = Ease.OutBack;

    [Tooltip("Used by SpinAndPop (degrees).")]
    public float spinDegrees = 360f;

    [Tooltip("Used by PunchScale.")]
    public float punchAmount = 0.25f;
    public int punchVibrato = 10;
    [Range(0f, 1f)]
    public float punchElasticity = 0.6f;

    [Tooltip("Used by ShakeIn.")]
    public float shakeStrength = 0.15f;

    private Dictionary<string, Transform> tileByName;

    // Optional callback for other systems
    public System.Action OnPiecesInitialized;

    private int spawnIndex = 0;

    private IEnumerator Start()
    {
        if (!autoInitializeOnStart) yield break;
        yield return null; // give board a frame to generate/names to exist
        InitializePieces();
    }

    [ContextMenu("Initialize Pieces")]
    public void InitializePieces()
    {
        EnsureRefs();
        CacheTiles();

        if (clearOldPiecesOnStart)
            ClearPieces();

        spawnIndex = 0;
        SpawnStandardChess();

        OnPiecesInitialized?.Invoke();
    }

    private void EnsureRefs()
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

    private void CacheTiles()
    {
        tileByName = new Dictionary<string, Transform>(64);

        if (!includeNestedTiles)
        {
            foreach (Transform t in tilesRoot)
            {
                if (IsSquareName(t.name))
                    tileByName[t.name] = t;
            }
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

    private static bool IsSquareName(string n)
    {
        if (string.IsNullOrEmpty(n) || n.Length < 2 || n.Length > 3) return false;
        char f = n[0];
        if (f < 'A' || f > 'H') return false;
        if (!int.TryParse(n.Substring(1), out int r)) return false;
        return r >= 1 && r <= 8;
    }

    private void ClearPieces()
    {
        ClearChildren(whitePiecesParent);
        ClearChildren(blackPiecesParent);
    }

    private void ClearChildren(Transform parent)
    {
        if (parent == null) return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
#if UNITY_EDITOR
            DestroyImmediate(parent.GetChild(i).gameObject);
#else
            Destroy(parent.GetChild(i).gameObject);
#endif
        }
    }

    private void SpawnStandardChess()
    {
        SpawnLine(whitePawn, 2, PieceColor.White, PieceType.Pawn, whitePiecesParent);

        Spawn(whiteRook,   "A1", PieceColor.White, PieceType.Rook,   whitePiecesParent);
        Spawn(whiteKnight, "B1", PieceColor.White, PieceType.Knight, whitePiecesParent);
        Spawn(whiteBishop, "C1", PieceColor.White, PieceType.Bishop, whitePiecesParent);
        Spawn(whiteQueen,  "D1", PieceColor.White, PieceType.Queen,  whitePiecesParent);
        Spawn(whiteKing,   "E1", PieceColor.White, PieceType.King,   whitePiecesParent);
        Spawn(whiteBishop, "F1", PieceColor.White, PieceType.Bishop, whitePiecesParent);
        Spawn(whiteKnight, "G1", PieceColor.White, PieceType.Knight, whitePiecesParent);
        Spawn(whiteRook,   "H1", PieceColor.White, PieceType.Rook,   whitePiecesParent);

        SpawnLine(blackPawn, 7, PieceColor.Black, PieceType.Pawn, blackPiecesParent);

        Spawn(blackRook,   "A8", PieceColor.Black, PieceType.Rook,   blackPiecesParent);
        Spawn(blackKnight, "B8", PieceColor.Black, PieceType.Knight, blackPiecesParent);
        Spawn(blackBishop, "C8", PieceColor.Black, PieceType.Bishop, blackPiecesParent);
        Spawn(blackQueen,  "D8", PieceColor.Black, PieceType.Queen,  blackPiecesParent);
        Spawn(blackKing,   "E8", PieceColor.Black, PieceType.King,   blackPiecesParent);
        Spawn(blackBishop, "F8", PieceColor.Black, PieceType.Bishop, blackPiecesParent);
        Spawn(blackKnight, "G8", PieceColor.Black, PieceType.Knight, blackPiecesParent);
        Spawn(blackRook,   "H8", PieceColor.Black, PieceType.Rook,   blackPiecesParent);
    }

    private void SpawnLine(GameObject prefab, int rank, PieceColor color, PieceType type, Transform parent)
    {
        for (char file = 'A'; file <= 'H'; file++)
            Spawn(prefab, $"{file}{rank}", color, type, parent);
    }

    private void Spawn(GameObject prefab, string square, PieceColor color, PieceType type, Transform parent)
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

        var sel = piece.GetComponent<PieceSelectable>();
        if (sel == null) sel = piece.AddComponent<PieceSelectable>();

        EnsureCollider(piece);
    }

    private void ApplySpawnEffect(Transform t, Vector3 targetPos)
    {
        t.DOKill();

        float delay = spawnIndex * spawnStagger;
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

            case SpawnEffect.DropBounce:
                t.position = targetPos + Vector3.up * verticalDistance;
                t.localScale = defaultScale;
                t.DOMove(targetPos, spawnDuration).SetDelay(delay).SetEase(Ease.OutBounce);
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
                    .SetDelay(delay)
                    .SetEase(Ease.OutCubic);

                t.DOScale(defaultScale, spawnDuration)
                    .SetDelay(delay)
                    .SetEase(scaleEase);
                return;

            case SpawnEffect.FlipY:
                t.position = targetPos;
                t.localScale = new Vector3(0f, 1f, 1f);
                t.DOScale(defaultScale, spawnDuration).SetDelay(delay).SetEase(Ease.OutCubic);
                return;

            case SpawnEffect.PunchScale:
                t.position = targetPos;
                t.localScale = defaultScale;
                t.DOPunchScale(Vector3.one * punchAmount, spawnDuration, punchVibrato, punchElasticity)
                    .SetDelay(delay);
                return;

            case SpawnEffect.OvershootScale:
                t.position = targetPos;
                t.localScale = Vector3.zero;

                // Overshoot manually: 0 -> 1.15 -> 1
                Sequence s = DOTween.Sequence();
                s.SetDelay(delay);
                s.Append(t.DOScale(defaultScale * 1.15f, spawnDuration * 0.65f).SetEase(Ease.OutCubic));
                s.Append(t.DOScale(defaultScale, spawnDuration * 0.35f).SetEase(Ease.InOutSine));
                return;

            case SpawnEffect.ShakeIn:
                t.position = targetPos;
                t.localScale = Vector3.zero;

                Sequence sh = DOTween.Sequence();
                sh.SetDelay(delay);
                sh.Append(t.DOScale(defaultScale, spawnDuration).SetEase(scaleEase));
                sh.Join(t.DOShakePosition(spawnDuration, shakeStrength, 12, 90f, false, true));
                return;
        }
    }

    private void EnsureCollider(GameObject piece)
    {
        if (piece.GetComponentInChildren<Collider>() != null)
            return;

        var meshFilters = piece.GetComponentsInChildren<MeshFilter>(true);
        bool addedAny = false;

        foreach (var mf in meshFilters)
        {
            if (mf == null || mf.sharedMesh == null) continue;

            var mr = mf.GetComponent<MeshRenderer>();
            if (mr != null && !mr.enabled) continue;

            var mc = mf.gameObject.GetComponent<MeshCollider>();
            if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>();

            mc.sharedMesh = mf.sharedMesh;
            mc.convex = false;
            mc.isTrigger = false;

            addedAny = true;
        }

        if (addedAny) return;

        var skinned = piece.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (skinned.Length > 0)
        {
            var baked = new Mesh();
            skinned[0].BakeMesh(baked);

            var mc = piece.AddComponent<MeshCollider>();
            mc.sharedMesh = baked;
            mc.convex = false;
            mc.isTrigger = false;
            return;
        }

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
