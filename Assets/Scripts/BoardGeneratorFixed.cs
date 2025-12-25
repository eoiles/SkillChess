using UnityEngine;

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
            }
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
