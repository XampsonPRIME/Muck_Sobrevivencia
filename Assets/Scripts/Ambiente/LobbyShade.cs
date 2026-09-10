using UnityEngine;
using UnityEngine.UI;

/// <summary>Soft directional scrim that keeps menu typography readable over artwork.</summary>
public sealed class LobbyShade : MaskableGraphic
{
    protected override void Awake() { base.Awake(); raycastTarget = false; }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        float[] stops = { 0, 0.36f, 0.64f, 1 };
        float[] opacity = { 0.8f, 0.64f, 0.14f, 0.06f };
        for (int i = 0; i < stops.Length; i++)
        {
            float x = Mathf.Lerp(r.xMin, r.xMax, stops[i]);
            var c = new Color(0.015f, 0.027f, 0.03f, opacity[i]);
            vh.AddVert(new Vector3(x, r.yMin), c, Vector2.zero);
            vh.AddVert(new Vector3(x, r.yMax), c, Vector2.one);
            if (i == 0) continue;
            int n = i * 2;
            vh.AddTriangle(n - 2, n - 1, n); vh.AddTriangle(n, n - 1, n + 1);
        }
    }
}
