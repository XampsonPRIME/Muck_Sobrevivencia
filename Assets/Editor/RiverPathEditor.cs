using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RiverPath))]
public class RiverPathEditor : Editor
{
    const float DefaultPointSpacing = 18f;

    RiverPath River => (RiverPath)target;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Ferramentas do Rio", EditorStyles.boldLabel);

        if (!River.useChildrenAsWaypoints)
        {
            EditorGUILayout.HelpBox("Para usar o editor visual, deixe 'useChildrenAsWaypoints' ligado.", MessageType.Info);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Adicionar no fim"))
                AddPointAtEnd();

            if (GUILayout.Button("Adicionar no inicio"))
                AddPointAtStart();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Criar 2 pontos"))
                EnsureStarterPoints();

            if (GUILayout.Button("Ajustar ao chao"))
                SnapAllPointsToGround();
        }

        EditorGUILayout.HelpBox("Na Scene View: arraste os pontos azuis. Use os botoes '+' entre os segmentos para inserir novas curvas.", MessageType.None);
    }

    void OnSceneGUI()
    {
        if (!River.useChildrenAsWaypoints)
            return;

        Transform root = River.transform;
        int childCount = root.childCount;

        Handles.color = new Color(0.2f, 0.75f, 1f, 1f);

        for (int i = 0; i < childCount; i++)
        {
            Transform point = root.GetChild(i);
            if (point == null)
                continue;

            EditorGUI.BeginChangeCheck();
            Vector3 newPosition = Handles.PositionHandle(point.position, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(point, "Mover ponto do rio");
                point.position = SnapPointToGround(newPosition);
                EditorUtility.SetDirty(point);
                EditorUtility.SetDirty(River);
            }

            Handles.Label(point.position + Vector3.up * 0.8f, $"P{i}");
        }

        for (int i = 0; i < childCount - 1; i++)
        {
            Transform a = root.GetChild(i);
            Transform b = root.GetChild(i + 1);
            if (a == null || b == null)
                continue;

            Vector3 midpoint = (a.position + b.position) * 0.5f;
            float size = HandleUtility.GetHandleSize(midpoint) * 0.12f;

            if (Handles.Button(midpoint + Vector3.up * 0.35f, Quaternion.identity, size, size * 1.25f, Handles.SphereHandleCap))
                InsertPointBetween(i, i + 1);
        }
    }

    void EnsureStarterPoints()
    {
        if (River.transform.childCount > 0)
            return;

        CreatePoint("P0", SnapPointToGround(River.transform.position));
        CreatePoint("P1", SnapPointToGround(River.transform.position + River.transform.forward * DefaultPointSpacing));
    }

    void AddPointAtEnd()
    {
        Transform root = River.transform;

        if (root.childCount == 0)
        {
            EnsureStarterPoints();
            return;
        }

        if (root.childCount == 1)
        {
            Vector3 basePosition = root.GetChild(0).position;
            CreatePoint($"P{root.childCount}", SnapPointToGround(basePosition + root.forward * DefaultPointSpacing));
            return;
        }

        Vector3 last = root.GetChild(root.childCount - 1).position;
        Vector3 previous = root.GetChild(root.childCount - 2).position;
        Vector3 direction = (last - previous).normalized;
        if (direction.sqrMagnitude < 0.001f)
            direction = root.forward;

        CreatePoint($"P{root.childCount}", SnapPointToGround(last + direction * DefaultPointSpacing));
    }

    void AddPointAtStart()
    {
        Transform root = River.transform;

        if (root.childCount == 0)
        {
            EnsureStarterPoints();
            return;
        }

        Vector3 first = root.GetChild(0).position;
        Vector3 direction = root.forward * -1f;

        if (root.childCount > 1)
        {
            Vector3 second = root.GetChild(1).position;
            direction = (first - second).normalized;
            if (direction.sqrMagnitude < 0.001f)
                direction = root.forward * -1f;
        }

        GameObject point = CreatePointObject("P0", SnapPointToGround(first + direction * DefaultPointSpacing));
        point.transform.SetSiblingIndex(0);
        RenumberChildren();
    }

    void InsertPointBetween(int firstIndex, int secondIndex)
    {
        Transform root = River.transform;
        if (firstIndex < 0 || secondIndex >= root.childCount)
            return;

        Transform a = root.GetChild(firstIndex);
        Transform b = root.GetChild(secondIndex);
        Vector3 midpoint = SnapPointToGround((a.position + b.position) * 0.5f);

        GameObject point = CreatePointObject($"P{secondIndex}", midpoint);
        point.transform.SetSiblingIndex(secondIndex);
        RenumberChildren();
    }

    void SnapAllPointsToGround()
    {
        Transform root = River.transform;
        Undo.RecordObjects(root.GetComponentsInChildren<Transform>(), "Ajustar pontos do rio ao chao");

        for (int i = 0; i < root.childCount; i++)
        {
            Transform point = root.GetChild(i);
            point.position = SnapPointToGround(point.position);
            EditorUtility.SetDirty(point);
        }

        EditorUtility.SetDirty(River);
    }

    void CreatePoint(string name, Vector3 position)
    {
        CreatePointObject(name, position);
        RenumberChildren();
    }

    GameObject CreatePointObject(string name, Vector3 position)
    {
        GameObject point = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(point, "Criar ponto do rio");
        point.transform.SetParent(River.transform, true);
        point.transform.position = position;
        point.transform.rotation = Quaternion.identity;
        point.transform.localScale = Vector3.one;
        Selection.activeGameObject = point;
        EditorUtility.SetDirty(River);
        return point;
    }

    void RenumberChildren()
    {
        Transform root = River.transform;
        for (int i = 0; i < root.childCount; i++)
            root.GetChild(i).name = $"P{i}";
    }

    Vector3 SnapPointToGround(Vector3 position)
    {
        Vector3 origin = position + Vector3.up * 300f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 1000f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point;

        return position;
    }
}
