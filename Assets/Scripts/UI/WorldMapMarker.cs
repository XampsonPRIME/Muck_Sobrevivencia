using System.Collections.Generic;
using UnityEngine;

public class WorldMapMarker : MonoBehaviour
{
    static readonly List<WorldMapMarker> activeMarkers = new List<WorldMapMarker>();

    public string label = "Marker";
    public Color markerColor = new Color(1f, 0.82f, 0.24f, 0.95f);
    [Min(4f)] public float markerSize = 10f;
    public bool showOnMap = true;
    public Transform targetOverride;

    public static IReadOnlyList<WorldMapMarker> ActiveMarkers => activeMarkers;
    public Vector3 WorldPosition => targetOverride != null ? targetOverride.position : transform.position;

    void OnEnable()
    {
        if (!activeMarkers.Contains(this))
            activeMarkers.Add(this);
    }

    void OnDisable()
    {
        activeMarkers.Remove(this);
    }
}
