using UnityEngine;

public class PieceSelectable : MonoBehaviour
{
    [Header("Highlight")]
    public Material highlightMaterial; // optional, auto-created if null
    public float highlightAlpha = 0.35f;
    public float ringRadius = 0.6f;
    public float ringThickness = 0.06f;
    public float ringYOffset = 0.02f;

    GameObject ringObj;

    void Awake()
    {
        EnsureRing();
        SetSelected(false);
    }

    public void SetSelected(bool on)
    {
        EnsureRing();
        if (ringObj != null)
            ringObj.SetActive(on);
    }

    void EnsureRing()
    {
        if (ringObj != null) return;

        // Flat cylinder ring under piece
        ringObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ringObj.name = "PieceHighlight";
        ringObj.transform.SetParent(transform, false);

        // Position at base area (approx). If your pieces float/sink, adjust ringYOffset.
        ringObj.transform.localPosition = new Vector3(0f, ringYOffset, 0f);
        ringObj.transform.localRotation = Quaternion.identity;

        // Cylinder default height is 2, radius 0.5
        float diameter = ringRadius * 2f;
        ringObj.transform.localScale = new Vector3(diameter, ringThickness, diameter);

        // Remove collider so it doesn't block clicks
        var col = ringObj.GetComponent<Collider>();
        if (col != null) Destroy(col);

        var r = ringObj.GetComponent<Renderer>();

        if (highlightMaterial == null)
            highlightMaterial = CreateDefaultHighlightMaterial(highlightAlpha);

        r.material = highlightMaterial;
    }

    static Material CreateDefaultHighlightMaterial(float alpha)
    {
        Shader s =
            Shader.Find("Universal Render Pipeline/Unlit") ??
            Shader.Find("Unlit/Color") ??
            Shader.Find("Standard");

        var m = new Material(s);
        var c = new Color(0.25f, 0.9f, 1f, alpha); // cyan-ish

        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        else if (m.HasProperty("_Color")) m.SetColor("_Color", c);

        return m;
    }
}
