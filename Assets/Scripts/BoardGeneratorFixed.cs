using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;

public class BoardGeneratorFixed : MonoBehaviour
{
    [Header("Board")]
    public int width = 8;
    public int height = 8;

    [Header("Prefab + Materials")]
    public GameObject tilePrefab;
    public Material blackMat;
    public Material whiteMat;

    [Header("Calibration (local space of this Board object)")]
    public Vector3 a1Local = Vector3.zero;
    public float step = 4f;

    public Vector3 fileDirLocal = Vector3.right; // A->B
    public Vector3 rankDirLocal = Vector3.back;  // 1->2

    [Header("Rounding")]
    public bool roundPositions = true;
    public float roundTo = 0.01f;

    [Header("Tile Highlight (optional)")]
    public Material tileHighlightMaterial;

    [Tooltip("If true, newly spawned tiles are forced to NOT highlighted to avoid highlight flash during spawn.")]
    public bool forceTilesNotHighlightedOnSpawn = true;

    [Header("Auto")]
    public bool generateOnStart = false; // keep false if GameController controls order

    public enum TileSpawnEffect
    {
        None,
        ScalePop,
        RaiseFromBelow,
        DropFromAbove,
        SlideFromRight,
        SlideFromLeft,
        SpinAndPop,
        PunchScale
    }

    public enum TileAnimOrder
    {
        LoopOrder,      // file loop then rank loop (your original)
        SnakeByRanks,   // A1->H1 then H2->A2...
        SnakeByFiles,   // A1->A8 then B8->B1...
        SpiralInward,   // outside ring -> center
        RandomStable    // stable random (same each spawn)
    }

    public enum TileSpawnTiming
    {
        Stagger,   // overlapping
        OneByOne   // strict sequential
    }

    [Header("DOTween Spawn (layout unchanged)")]
    public bool animateTiles = true;
    public TileSpawnEffect tileSpawnEffect = TileSpawnEffect.ScalePop;
    public TileAnimOrder tileAnimOrder = TileAnimOrder.SnakeByRanks;
    public TileSpawnTiming tileSpawnTiming = TileSpawnTiming.OneByOne;

    [Header("Timing")]
    [Min(0f)] public float spawnDuration = 0.25f;

    [Tooltip("Used when TileSpawnTiming = Stagger")]
    [Min(0f)] public float spawnStagger = 0.005f;

    [Tooltip("Used when TileSpawnTiming = OneByOne (gap between tiles)")]
    [Min(0f)] public float spawnGap = 0.01f;

    [Header("Effect Params")]
    [Tooltip("World-space distance for Raise/Drop (uses board's up direction).")]
    public float verticalDistance = 0.5f;

    [Tooltip("World-space distance for Slide (uses board's right direction).")]
    public float horizontalDistance = 0.5f;

    public Ease moveEase = Ease.OutCubic;
    public Ease scaleEase = Ease.OutBack;

    [Tooltip("Spin degrees used by SpinAndPop.")]
    public float spinDegrees = 180f;

    [Tooltip("Punch amount used by PunchScale.")]
    public float punchAmount = 0.12f;
    public int punchVibrato = 10;
    [Range(0f, 1f)] public float punchElasticity = 0.6f;

    void Start()
    {
        if (generateOnStart)
            GenerateBoard();
    }

    public float GetTotalTileAnimTime()
    {
        if (!animateTiles) return 0f;
        if (tileSpawnEffect == TileSpawnEffect.None) return 0f;

        int count = Mathf.Max(1, width * height);

        if (tileSpawnTiming == TileSpawnTiming.OneByOne)
            return count * spawnDuration + (count - 1) * spawnGap;

        return spawnDuration + (count - 1) * spawnStagger;
    }

    [ContextMenu("Generate Board")]
    public void GenerateBoard()
    {
        if (tilePrefab == null || blackMat == null || whiteMat == null)
        {
            Debug.LogError("Assign tilePrefab, blackMat, whiteMat in Inspector.");
            return;
        }

        // Clear existing children (tiles) safely in play mode
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child != null) child.DOKill(true);

#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(child.gameObject);
            else Destroy(child.gameObject);
#else
            Destroy(child.gameObject);
#endif
        }

        Vector3 fileDir = (fileDirLocal.sqrMagnitude < 0.0001f) ? Vector3.right : fileDirLocal.normalized;
        Vector3 rankDir = (rankDirLocal.sqrMagnitude < 0.0001f) ? Vector3.back : rankDirLocal.normalized;

        int total = width * height;
        int[] orderIndex = BuildOrderIndex(total);

        for (int file = 0; file < width; file++)
        {
            for (int rank = 0; rank < height; rank++)
            {
                Vector3 localPos = a1Local + fileDir * (file * step) + rankDir * (rank * step);
                if (roundPositions) localPos = RoundVec3(localPos, roundTo);

                Vector3 worldPos = transform.TransformPoint(localPos);

                GameObject tile = Instantiate(tilePrefab, worldPos, tilePrefab.transform.rotation, transform);

                string squareName = ToSquareName(file, rank);
                tile.name = squareName;

                bool isBlack = ((file + rank) % 2 == 0);

                // IMPORTANT:
                // Apply base material to ALL renderers and ALL material slots.
                // This prevents “top face” (2nd material slot) from flashing magenta.
                ApplyBaseMaterialAllSlots(tile, isBlack ? blackMat : whiteMat);

                if (tile.GetComponent<Collider>() == null)
                    tile.AddComponent<BoxCollider>();

                var tv = tile.GetComponent<TileView>();
                if (tv == null) tv = tile.AddComponent<TileView>();

                tv.Setup(squareName);

                // Assign highlight material if provided
                if (tileHighlightMaterial != null)
                    tv.highlightMaterial = tileHighlightMaterial;

                // CRITICAL:
                // Force newly created tiles to start NOT highlighted to prevent highlight flash during spawn.
                if (forceTilesNotHighlightedOnSpawn)
                {
                    // Avoid compile issues if you rename methods later:
                    // Try direct call (most likely exists) + fallback SendMessage.
                    try
                    {
                        tv.SetHighlighted(false);
                    }
                    catch
                    {
                        tile.SendMessage("SetHighlighted", false, SendMessageOptions.DontRequireReceiver);
                    }
                }

                if (animateTiles && tileSpawnEffect != TileSpawnEffect.None)
                {
                    int id = file * height + rank;
                    int index = orderIndex[id];
                    ApplyTileSpawnEffect(tile.transform, worldPos, index);
                }
            }
        }
    }

    static void ApplyBaseMaterialAllSlots(GameObject tile, Material mat)
    {
        if (tile == null || mat == null) return;

        // handle root + child renderers
        var renderers = tile.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;

            var mats = r.sharedMaterials;
            if (mats == null || mats.Length == 0)
            {
                r.sharedMaterial = mat;
                continue;
            }

            for (int m = 0; m < mats.Length; m++)
                mats[m] = mat;

            r.sharedMaterials = mats;
        }
    }

    int[] BuildOrderIndex(int total)
    {
        int[] orderIndex = new int[total];

        switch (tileAnimOrder)
        {
            case TileAnimOrder.LoopOrder:
                for (int id = 0; id < total; id++) orderIndex[id] = id;
                break;

            case TileAnimOrder.SnakeByRanks:
            {
                int idx = 0;
                for (int rank = 0; rank < height; rank++)
                {
                    bool leftToRight = (rank % 2 == 0);
                    if (leftToRight)
                    {
                        for (int file = 0; file < width; file++)
                            orderIndex[file * height + rank] = idx++;
                    }
                    else
                    {
                        for (int file = width - 1; file >= 0; file--)
                            orderIndex[file * height + rank] = idx++;
                    }
                }
            }
            break;

            case TileAnimOrder.SnakeByFiles:
            {
                int idx = 0;
                for (int file = 0; file < width; file++)
                {
                    bool bottomToTop = (file % 2 == 0);
                    if (bottomToTop)
                    {
                        for (int rank = 0; rank < height; rank++)
                            orderIndex[file * height + rank] = idx++;
                    }
                    else
                    {
                        for (int rank = height - 1; rank >= 0; rank--)
                            orderIndex[file * height + rank] = idx++;
                    }
                }
            }
            break;

            case TileAnimOrder.SpiralInward:
            {
                int idx = 0;
                int left = 0, right = width - 1, bottom = 0, top = height - 1;

                while (left <= right && bottom <= top)
                {
                    for (int x = left; x <= right; x++)
                        orderIndex[x * height + bottom] = idx++;

                    for (int y = bottom + 1; y <= top; y++)
                        orderIndex[right * height + y] = idx++;

                    if (bottom != top)
                        for (int x = right - 1; x >= left; x--)
                            orderIndex[x * height + top] = idx++;

                    if (left != right)
                        for (int y = top - 1; y > bottom; y--)
                            orderIndex[left * height + y] = idx++;

                    left++; right--; bottom++; top--;
                }
            }
            break;

            case TileAnimOrder.RandomStable:
            {
                var pairs = new List<(int id, int score)>(total);
                for (int file = 0; file < width; file++)
                {
                    for (int rank = 0; rank < height; rank++)
                    {
                        int id = file * height + rank;
                        int score = Hash(file, rank);
                        pairs.Add((id, score));
                    }
                }
                pairs.Sort((a, b) => a.score.CompareTo(b.score));
                for (int i = 0; i < pairs.Count; i++)
                    orderIndex[pairs[i].id] = i;
            }
            break;
        }

        return orderIndex;
    }

    static int Hash(int a, int b)
    {
        unchecked { return (a * 73856093) ^ (b * 19349663); }
    }

    float GetDelay(int index)
    {
        if (tileSpawnTiming == TileSpawnTiming.OneByOne)
            return index * (spawnDuration + spawnGap);
        return index * spawnStagger;
    }

    void ApplyTileSpawnEffect(Transform t, Vector3 targetPos, int index)
    {
        Vector3 targetScale = t.localScale;
        Quaternion targetRot = t.rotation;

        Vector3 up = transform.up;
        Vector3 right = transform.right;

        float delay = GetDelay(index);

        t.DOKill();

        Tween mainTween = null;

        switch (tileSpawnEffect)
        {
            case TileSpawnEffect.ScalePop:
                t.position = targetPos;
                t.localScale = Vector3.zero;
                mainTween = t.DOScale(targetScale, spawnDuration).SetDelay(delay).SetEase(scaleEase);
                break;

            case TileSpawnEffect.RaiseFromBelow:
                t.position = targetPos - up * verticalDistance;
                t.localScale = targetScale;
                mainTween = t.DOMove(targetPos, spawnDuration).SetDelay(delay).SetEase(moveEase);
                break;

            case TileSpawnEffect.DropFromAbove:
                t.position = targetPos + up * verticalDistance;
                t.localScale = targetScale;
                mainTween = t.DOMove(targetPos, spawnDuration).SetDelay(delay).SetEase(Ease.OutBounce);
                break;

            case TileSpawnEffect.SlideFromRight:
                t.position = targetPos + right * horizontalDistance;
                t.localScale = targetScale;
                mainTween = t.DOMove(targetPos, spawnDuration).SetDelay(delay).SetEase(moveEase);
                break;

            case TileSpawnEffect.SlideFromLeft:
                t.position = targetPos - right * horizontalDistance;
                t.localScale = targetScale;
                mainTween = t.DOMove(targetPos, spawnDuration).SetDelay(delay).SetEase(moveEase);
                break;

            case TileSpawnEffect.SpinAndPop:
                t.position = targetPos;
                t.localScale = Vector3.zero;
                t.rotation = targetRot;

                t.DOScale(targetScale, spawnDuration).SetDelay(delay).SetEase(scaleEase);

                mainTween = t.DORotateQuaternion(targetRot * Quaternion.Euler(0f, spinDegrees, 0f), spawnDuration)
                    .From(targetRot * Quaternion.Euler(0f, -spinDegrees, 0f))
                    .SetDelay(delay)
                    .SetEase(Ease.OutCubic);
                break;

            case TileSpawnEffect.PunchScale:
                t.position = targetPos;
                t.localScale = targetScale;
                mainTween = t.DOPunchScale(Vector3.one * punchAmount, spawnDuration, punchVibrato, punchElasticity)
                    .SetDelay(delay);
                break;
        }

        if (mainTween != null)
        {
            mainTween.OnComplete(() =>
            {
                if (!t) return;
                t.position = targetPos;
                t.localScale = targetScale;
                t.rotation = targetRot;
            });
        }
    }

    static string ToSquareName(int file, int rank)
    {
        char fileChar = (char)('A' + file);
        int rankNum = rank + 1;
        return $"{fileChar}{rankNum}";
    }

    static Vector3 RoundVec3(Vector3 v, float step)
    {
        float R(float x) => Mathf.Round(x / step) * step;
        return new Vector3(R(v.x), R(v.y), R(v.z));
    }
}
