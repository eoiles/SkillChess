using UnityEngine;
using DG.Tweening;

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
    public Material tileHighlightMaterial; // optional; if null, TileView will auto-create

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

    [Header("DOTween Spawn (does NOT change final layout)")]
    public bool animateTiles = true;
    public TileSpawnEffect tileSpawnEffect = TileSpawnEffect.ScalePop;

    [Min(0f)] public float spawnDuration = 0.25f;
    [Min(0f)] public float spawnStagger = 0.005f;

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
        GenerateBoard();
    }

    [ContextMenu("Generate Board")]
    public void GenerateBoard()
    {
        if (tilePrefab == null || blackMat == null || whiteMat == null)
        {
            Debug.LogError("Assign tilePrefab, blackMat, whiteMat in Inspector.");
            return;
        }

        // Clear existing children (tiles)
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
#if UNITY_EDITOR
            DestroyImmediate(transform.GetChild(i).gameObject);
#else
            Destroy(transform.GetChild(i).gameObject);
#endif
        }

        Vector3 fileDir = (fileDirLocal.sqrMagnitude < 0.0001f) ? Vector3.right : fileDirLocal.normalized;
        Vector3 rankDir = (rankDirLocal.sqrMagnitude < 0.0001f) ? Vector3.back : rankDirLocal.normalized;

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

                // Color pattern: A1 is black
                bool isBlack = ((file + rank) % 2 == 0);
                var r = tile.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = isBlack ? blackMat : whiteMat;

                if (tile.GetComponent<Collider>() == null)
                    tile.AddComponent<BoxCollider>();

                // Ensure TileView exists for highlighting + click detection
                var tv = tile.GetComponent<TileView>();
                if (tv == null) tv = tile.AddComponent<TileView>();
                tv.Setup(squareName);
                if (tileHighlightMaterial != null)
                    tv.highlightMaterial = tileHighlightMaterial;

                if (animateTiles && tileSpawnEffect != TileSpawnEffect.None)
                {
                    // Delay strictly matches your original spawn order: file loop then rank loop
                    int index = file * height + rank;
                    ApplyTileSpawnEffect(tile.transform, worldPos, index);
                }
            }
        }
    }

    void ApplyTileSpawnEffect(Transform t, Vector3 targetPos, int index)
    {
        // Preserve prefab's instantiated scale/rotation (so layout stays identical)
        Vector3 targetScale = t.localScale;
        Quaternion targetRot = t.rotation;

        // Use board orientation for offsets
        Vector3 up = transform.up;
        Vector3 right = transform.right;

        float delay = index * spawnStagger;

        t.DOKill();

        switch (tileSpawnEffect)
        {
            case TileSpawnEffect.ScalePop:
                t.position = targetPos;
                t.localScale = Vector3.zero;
                t.DOScale(targetScale, spawnDuration).SetDelay(delay).SetEase(scaleEase);
                break;

            case TileSpawnEffect.RaiseFromBelow:
                t.position = targetPos - up * verticalDistance;
                t.localScale = targetScale;
                t.DOMove(targetPos, spawnDuration).SetDelay(delay).SetEase(moveEase);
                break;

            case TileSpawnEffect.DropFromAbove:
                t.position = targetPos + up * verticalDistance;
                t.localScale = targetScale;
                t.DOMove(targetPos, spawnDuration).SetDelay(delay).SetEase(Ease.OutBounce);
                break;

            case TileSpawnEffect.SlideFromRight:
                t.position = targetPos + right * horizontalDistance;
                t.localScale = targetScale;
                t.DOMove(targetPos, spawnDuration).SetDelay(delay).SetEase(moveEase);
                break;

            case TileSpawnEffect.SlideFromLeft:
                t.position = targetPos - right * horizontalDistance;
                t.localScale = targetScale;
                t.DOMove(targetPos, spawnDuration).SetDelay(delay).SetEase(moveEase);
                break;

            case TileSpawnEffect.SpinAndPop:
                t.position = targetPos;
                t.localScale = Vector3.zero;
                t.rotation = targetRot;

                t.DOScale(targetScale, spawnDuration)
                    .SetDelay(delay)
                    .SetEase(scaleEase);

                // Spin around Y but end exactly at targetRot
                t.DORotateQuaternion(targetRot * Quaternion.Euler(0f, spinDegrees, 0f), spawnDuration)
                    .From(targetRot * Quaternion.Euler(0f, -spinDegrees, 0f))
                    .SetDelay(delay)
                    .SetEase(Ease.OutCubic);
                break;

            case TileSpawnEffect.PunchScale:
                t.position = targetPos;
                t.localScale = targetScale;
                t.DOPunchScale(Vector3.one * punchAmount, spawnDuration, punchVibrato, punchElasticity)
                    .SetDelay(delay);
                break;
        }

        // Safety: force final values when tween completes (layout guaranteed)
        DOTween.Sequence()
            .SetDelay(delay + spawnDuration)
            .AppendCallback(() =>
            {
                if (t == null) return;
                t.position = targetPos;
                t.localScale = targetScale;
                t.rotation = targetRot;
            });
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
