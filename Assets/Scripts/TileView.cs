using UnityEngine;

public class TileView : MonoBehaviour
{
    [Header("Runtime")]
    public string squareName; // e.g. A1

    [Header("Highlight")]
    public Material highlightMaterial; // optional, can be null (auto created)
    public float highlightAlpha = 0.35f;
    public float highlightYOffset = 0.02f;   // above tile top
    public float highlightScale = 0.92f;     // smaller than tile

    GameObject highlightObj;
    Renderer highlightRenderer;

    void Awake()
    {
        EnsureHighlightObject();
        SetHighlighted(false);
    }

    public void Setup(string squareName)
    {
        this.squareName = squareName;
    }

    public void SetHighlighted(bool on)
    {
        EnsureHighlightObject();
        if (highlightObj != null)
            highlightObj.SetActive(on);
    }

    void EnsureHighlightObject()
    {
        if (highlightObj != null) return;

        // Create a flat quad overlay
        highlightObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        highlightObj.name = "TileHighlight";
        highlightObj.transform.SetParent(transform, false);

        // Place slightly above tile top
        float tileTopY = 0.5f;
        var col = GetComponent<Collider>();
        if (col != null)
            tileTopY = transform.InverseTransformPoint(col.bounds.max).y;

        highlightObj.transform.localPosition = new Vector3(0f, tileTopY + highlightYOffset, 0f);
        highlightObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        highlightObj.transform.localScale = new Vector3(highlightScale, highlightScale, 1f);

        // Remove collider so it doesn't block clicks
        var hCol = highlightObj.GetComponent<Collider>();
        if (hCol != null) Destroy(hCol);

        highlightRenderer = highlightObj.GetComponent<Renderer>();

        if (highlightMaterial == null)
            highlightMaterial = CreateDefaultHighlightMaterial(highlightAlpha);

        highlightRenderer.material = highlightMaterial;
    }

    static Material CreateDefaultHighlightMaterial(float alpha)
    {
        Shader s =
            Shader.Find("Universal Render Pipeline/Unlit") ??
            Shader.Find("Unlit/Color") ??
            Shader.Find("Standard");

        var m = new Material(s);

        // Default: yellow-ish with alpha (works best with URP Unlit)
        var c = new Color(1f, 0.92f, 0.25f, alpha);

        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        else if (m.HasProperty("_Color")) m.SetColor("_Color", c);

        // If Standard, try make it transparent-ish
        // (Not perfect, but acceptable for milestone 1)
        return m;
    }
}
