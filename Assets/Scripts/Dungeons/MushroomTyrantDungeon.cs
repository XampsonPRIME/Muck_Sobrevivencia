using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MushroomTyrantDungeon : MonoBehaviour
{
    enum DungeonState
    {
        Idle,
        MobRoom,
        TransitioningToBoss,
        BossRoom,
        Completed
    }

    [Header("Entrada")]
    [SerializeField] float interiorDepth = 180f;
    [SerializeField] Vector3 entryPlatformSize = new Vector3(16f, 1.5f, 16f);
    [SerializeField] float npcDistanceFromEntrance = 3.5f;
    [SerializeField] float returnDistanceFromEntrance = 6f;
    [SerializeField] float environmentClearRadius = 16f;
    [SerializeField] float environmentClearHeight = 10f;

    [Header("Sala de Mobs")]
    [SerializeField] Vector3 mobRoomSize = new Vector3(22f, 6f, 22f);
    [SerializeField] int mobCount = 4;
    [SerializeField] float mobSpawnRadius = 6f;
    [SerializeField] float moveToBossDelay = 1.25f;

    [Header("Sala do Boss")]
    [SerializeField] Vector3 bossRoomSize = new Vector3(30f, 8f, 30f);
    [SerializeField] float bossSpawnRadius = 4f;
    [SerializeField] float returnToEntranceDelay = 2f;

    [Header("Prefabs")]
    [SerializeField] GameObject entranceVisualPrefab;
    [SerializeField] GameObject npcVisualPrefab;
    [SerializeField] GameObject mobEnemyPrefab;
    [SerializeField] GameObject bossEnemyPrefab;

    [Header("Mensagens")]
    [SerializeField] string mobRoomClearMessage = "Todos os mobs cairam. Indo para a sala do boss...";
    [SerializeField] string bossDefeatedMessage = "Calabouco concluido.";

    Transform entranceRoot;
    Transform interiorRoot;
    Transform mobRoomPlayerSpawn;
    Transform mobRoomEnemyAnchor;
    Transform bossRoomPlayerSpawn;
    Transform bossRoomEnemyAnchor;
    Transform returnPoint;
    Bounds entranceBounds;
    bool hasEntranceBounds;

    readonly List<MiniKrug> activeMobEnemies = new List<MiniKrug>();
    PlayerInteraction activePlayerInteraction;
    BossEnemy activeBoss;
    DungeonState state;
    bool bossSpawned;
    bool completionScheduled;

    public void Initialize(GameObject entrancePrefab)
    {
        entranceVisualPrefab = entrancePrefab;
        BuildDungeon();
    }

    public bool CanAcceptNewEntry(PlayerInteraction playerInteraction)
    {
        if (playerInteraction == null)
            return false;

        if (activePlayerInteraction == null)
            return true;

        if (activePlayerInteraction == playerInteraction)
            return true;

        return state == DungeonState.Completed || state == DungeonState.Idle;
    }

    public void EnterDungeon(PlayerInteraction playerInteraction)
    {
        if (playerInteraction == null)
            return;

        activePlayerInteraction = playerInteraction;

        if (state == DungeonState.Completed || state == DungeonState.Idle)
            StartNewRun();

        WarpPlayerToCurrentStage();
    }

    void Update()
    {
        if (state == DungeonState.BossRoom && bossSpawned && !completionScheduled && activeBoss == null)
        {
            completionScheduled = true;
            StartCoroutine(FinishDungeonAfterDelay());
        }
    }

    void BuildDungeon()
    {
        state = DungeonState.Idle;

        entranceRoot = new GameObject("DungeonEntranceRoot").transform;
        entranceRoot.SetParent(transform, false);

        if (entranceVisualPrefab != null)
        {
            GameObject entranceInstance = Instantiate(entranceVisualPrefab, entranceRoot);
            entranceInstance.name = entranceVisualPrefab.name;
            AlignVisualToGround(entranceInstance);
            CacheEntranceBounds(entranceInstance);
            EnsureEntranceCollision(entranceInstance);
        }
        else
        {
            CreateFallbackEntranceVisual(entranceRoot);
            CacheEntranceBounds(entranceRoot.gameObject);
            EnsureEntranceCollision(entranceRoot.gameObject);
        }

        ClearEnvironmentAroundEntrance();
        CreateEntryPlatform();
        CreateNpc();
        CreateInterior();
    }

    void StartNewRun()
    {
        ClearActiveEnemies();
        state = DungeonState.MobRoom;
        bossSpawned = false;
        completionScheduled = false;
        SpawnMobWave();
    }

    void WarpPlayerToCurrentStage()
    {
        PlayerMovement playerMovement = activePlayerInteraction != null
            ? activePlayerInteraction.GetComponent<PlayerMovement>()
            : null;

        if (playerMovement == null)
            return;

        Transform target = state == DungeonState.BossRoom ? bossRoomPlayerSpawn : mobRoomPlayerSpawn;
        if (target == null)
            return;

        playerMovement.WarpToSafePosition(target.position, target.rotation);
    }

    void SpawnMobWave()
    {
        ClearActiveEnemies();

        GameObject prefab = mobEnemyPrefab != null ? mobEnemyPrefab : ForestMushroomMonsterFactory.LoadPrefab();
        if (prefab == null || mobRoomEnemyAnchor == null)
            return;

        for (int i = 0; i < Mathf.Max(1, mobCount); i++)
        {
            float angle = i * Mathf.PI * 2f / Mathf.Max(1, mobCount);
            Vector3 spawnOffset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * mobSpawnRadius;
            Vector3 spawnPosition = mobRoomEnemyAnchor.position + spawnOffset;

            MiniKrug enemy = ForestMushroomMonsterFactory.CreateInstance(
                prefab,
                spawnPosition,
                Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
                mobRoomEnemyAnchor);

            if (enemy == null)
                continue;

            enemy.SetDeathCallback(HandleMobEnemyDeath);
            activeMobEnemies.Add(enemy);
        }
    }

    void HandleMobEnemyDeath(MiniKrug enemy)
    {
        activeMobEnemies.Remove(enemy);
        activeMobEnemies.RemoveAll(entry => entry == null);

        if (state != DungeonState.MobRoom || activeMobEnemies.Count > 0)
            return;

        MessageSystem.Instance?.ShowMessage(mobRoomClearMessage);
        StartCoroutine(MovePlayerToBossRoomAfterDelay());
    }

    IEnumerator MovePlayerToBossRoomAfterDelay()
    {
        state = DungeonState.TransitioningToBoss;
        yield return new WaitForSeconds(Mathf.Max(0.1f, moveToBossDelay));

        SpawnBoss();
        state = DungeonState.BossRoom;
        WarpPlayerToCurrentStage();
    }

    void SpawnBoss()
    {
        bossSpawned = false;
        activeBoss = null;

        GameObject prefab = bossEnemyPrefab != null ? bossEnemyPrefab : ForestMushroomBossFactory.LoadPrefab();
        if (prefab == null || bossRoomEnemyAnchor == null)
            return;

        Vector3 spawnPosition = bossRoomEnemyAnchor.position + Random.insideUnitSphere * bossSpawnRadius;
        spawnPosition.y = bossRoomEnemyAnchor.position.y;

        activeBoss = ForestMushroomBossFactory.CreateInstance(
            prefab,
            spawnPosition,
            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
            bossRoomEnemyAnchor);

        bossSpawned = activeBoss != null;
    }

    IEnumerator FinishDungeonAfterDelay()
    {
        state = DungeonState.Completed;
        MessageSystem.Instance?.ShowMessage(bossDefeatedMessage);
        yield return new WaitForSeconds(Mathf.Max(0.1f, returnToEntranceDelay));

        PlayerMovement playerMovement = activePlayerInteraction != null
            ? activePlayerInteraction.GetComponent<PlayerMovement>()
            : null;

        if (playerMovement != null && returnPoint != null)
            playerMovement.WarpToSafePosition(returnPoint.position, returnPoint.rotation);

        ClearActiveEnemies();
        activeBoss = null;
    }

    void ClearActiveEnemies()
    {
        for (int i = 0; i < activeMobEnemies.Count; i++)
        {
            if (activeMobEnemies[i] != null)
                Destroy(activeMobEnemies[i].gameObject);
        }

        activeMobEnemies.Clear();

        if (activeBoss != null)
        {
            Destroy(activeBoss.gameObject);
            activeBoss = null;
        }
    }

    void CreateEntryPlatform()
    {
        GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platform.name = "DungeonEntryPlatform";
        platform.transform.SetParent(entranceRoot, false);
        Vector3 frontPoint = GetEntranceFrontPoint(2.5f);
        platform.transform.position = new Vector3(frontPoint.x, transform.position.y - 0.75f, frontPoint.z);
        platform.transform.localScale = entryPlatformSize;
        SetupRenderer(platform, new Color(0.24f, 0.12f, 0.22f, 1f));

        returnPoint = new GameObject("DungeonReturnPoint").transform;
        returnPoint.SetParent(transform, true);
        Vector3 returnPosition = GetEntranceFrontPoint(returnDistanceFromEntrance);
        returnPoint.position = new Vector3(returnPosition.x, transform.position.y + 0.2f, returnPosition.z);
        returnPoint.rotation = Quaternion.LookRotation(transform.forward, Vector3.up);
    }

    void CreateNpc()
    {
        GameObject npcRoot = new GameObject("DungeonGateNpc");
        npcRoot.transform.SetParent(transform, true);
        Vector3 npcPosition = GetEntranceFrontPoint(npcDistanceFromEntrance);
        npcRoot.transform.position = new Vector3(npcPosition.x, transform.position.y, npcPosition.z);
        npcRoot.transform.LookAt(GetEntranceCenter() + Vector3.up * 1.2f);

        CapsuleCollider collider = npcRoot.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0f, 0.95f, 0f);
        collider.height = 1.9f;
        collider.radius = 0.35f;

        DungeonGateNpc npc = npcRoot.AddComponent<DungeonGateNpc>();
        npc.SetDungeon(this);

        if (npcVisualPrefab != null)
        {
            GameObject npcVisual = Instantiate(npcVisualPrefab, npcRoot.transform);
            npcVisual.name = npcVisualPrefab.name;
        }
        else
        {
            CreateFallbackNpcVisual(npcRoot.transform);
        }
    }

    void CreateInterior()
    {
        interiorRoot = new GameObject("DungeonInteriorRoot").transform;
        interiorRoot.SetParent(transform, false);
        interiorRoot.position = transform.position + Vector3.down * interiorDepth;

        Transform mobRoom = CreateRoom(interiorRoot, "MobRoom", Vector3.zero, mobRoomSize, new Color(0.16f, 0.18f, 0.14f, 1f));
        mobRoomPlayerSpawn = CreateMarker(mobRoom, "MobRoomPlayerSpawn", new Vector3(0f, 0.2f, -mobRoomSize.z * 0.35f));
        mobRoomEnemyAnchor = CreateMarker(mobRoom, "MobRoomEnemyAnchor", Vector3.zero);
        DecorateDungeonRoom(mobRoom, mobRoomSize, false);

        Transform bossRoom = CreateRoom(
            interiorRoot,
            "BossRoom",
            new Vector3(0f, 0f, mobRoomSize.z + bossRoomSize.z + 8f),
            bossRoomSize,
            new Color(0.24f, 0.12f, 0.28f, 1f));

        bossRoomPlayerSpawn = CreateMarker(bossRoom, "BossRoomPlayerSpawn", new Vector3(0f, 0.2f, -bossRoomSize.z * 0.32f));
        bossRoomEnemyAnchor = CreateMarker(bossRoom, "BossRoomEnemyAnchor", new Vector3(0f, 0.2f, bossRoomSize.z * 0.1f));
        DecorateDungeonRoom(bossRoom, bossRoomSize, true);
    }

    Transform CreateRoom(Transform parent, string roomName, Vector3 localPosition, Vector3 size, Color color)
    {
        Transform roomRoot = new GameObject(roomName).transform;
        roomRoot.SetParent(parent, false);
        roomRoot.localPosition = localPosition;

        CreateCube(roomRoot, "Floor", new Vector3(0f, -0.5f, 0f), new Vector3(size.x, 1f, size.z), color);
        CreateCube(roomRoot, "WallNorth", new Vector3(0f, size.y * 0.5f - 0.5f, size.z * 0.5f), new Vector3(size.x, size.y, 1f), color * 0.85f);
        CreateCube(roomRoot, "WallSouth", new Vector3(0f, size.y * 0.5f - 0.5f, -size.z * 0.5f), new Vector3(size.x, size.y, 1f), color * 0.85f);
        CreateCube(roomRoot, "WallEast", new Vector3(size.x * 0.5f, size.y * 0.5f - 0.5f, 0f), new Vector3(1f, size.y, size.z), color * 0.9f);
        CreateCube(roomRoot, "WallWest", new Vector3(-size.x * 0.5f, size.y * 0.5f - 0.5f, 0f), new Vector3(1f, size.y, size.z), color * 0.9f);

        return roomRoot;
    }

    void DecorateDungeonRoom(Transform roomRoot, Vector3 size, bool bossRoom)
    {
        Color runeColor = bossRoom
            ? new Color(0.82f, 0.18f, 1f, 1f)
            : new Color(0.24f, 0.82f, 0.54f, 1f);
        Color crystalColor = bossRoom
            ? new Color(1f, 0.36f, 0.18f, 1f)
            : new Color(0.2f, 0.72f, 1f, 1f);

        float zOffset = size.z * 0.24f;
        for (int i = 0; i < 4; i++)
        {
            float x = i < 2 ? -size.x * 0.36f : size.x * 0.36f;
            float z = (i % 2 == 0 ? -zOffset : zOffset);
            GameObject pillar = CreateCube(roomRoot, $"Pillar_{i}", new Vector3(x, 1.45f, z), new Vector3(0.8f, 3.8f, 0.8f), new Color(0.18f, 0.14f, 0.19f, 1f));
            DisableLocalCollider(pillar);
        }

        int runeCount = bossRoom ? 7 : 5;
        for (int i = 0; i < runeCount; i++)
        {
            float t = runeCount <= 1 ? 0f : i / (float)(runeCount - 1);
            Vector3 localPosition = new Vector3(Mathf.Lerp(-size.x * 0.25f, size.x * 0.25f, t), 0.04f, Mathf.Lerp(-size.z * 0.28f, size.z * 0.18f, t));
            GameObject rune = CreateCube(roomRoot, $"FloorRune_{i}", localPosition, new Vector3(0.85f, 0.05f, 0.18f), runeColor);
            rune.transform.localRotation = Quaternion.Euler(0f, 32f, 0f);
            DisableLocalCollider(rune);
        }

        for (int i = 0; i < 3; i++)
        {
            Vector3 localPosition = new Vector3((i - 1) * 2.2f, 0.65f, bossRoom ? size.z * 0.25f : size.z * 0.12f);
            GameObject crystal = CreateCube(roomRoot, $"CrystalCluster_{i}", localPosition, new Vector3(0.45f, 1.3f, 0.45f), crystalColor);
            crystal.transform.localRotation = Quaternion.Euler(0f, 45f + i * 23f, 0f);
            DisableLocalCollider(crystal);
        }

        AddRoomLight(roomRoot, bossRoom ? "BossRoomLight" : "MobRoomLight", new Vector3(0f, 4.2f, 0f), bossRoom ? new Color(0.95f, 0.28f, 1f, 1f) : new Color(0.22f, 0.72f, 0.55f, 1f));
    }

    Transform CreateMarker(Transform parent, string markerName, Vector3 localPosition)
    {
        Transform marker = new GameObject(markerName).transform;
        marker.SetParent(parent, false);
        marker.localPosition = localPosition;
        marker.localRotation = Quaternion.identity;
        return marker;
    }

    GameObject CreateCube(Transform parent, string objectName, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = objectName;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = localPosition;
        cube.transform.localScale = localScale;
        SetupRenderer(cube, color);
        return cube;
    }

    void SetupRenderer(GameObject target, Color color)
    {
        if (target == null)
            return;

        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null)
            return;

        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        material.color = color;
        renderer.sharedMaterial = material;
    }

    void DisableLocalCollider(GameObject target)
    {
        Collider collider = target != null ? target.GetComponent<Collider>() : null;
        if (collider != null)
            Object.Destroy(collider);
    }

    void AddRoomLight(Transform parent, string objectName, Vector3 localPosition, Color color)
    {
        AddRoomLight(parent, objectName, localPosition, color, 18f, 1.35f);
    }

    void AddRoomLight(Transform parent, string objectName, Vector3 localPosition, Color color, float range, float intensity)
    {
        GameObject lightObject = new GameObject(objectName);
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.localPosition = localPosition;
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
    }

    void CreateFallbackEntranceVisual(Transform parent)
    {
        GameObject arch = CreateCube(parent, "FallbackDungeonArch", new Vector3(0f, 2f, 0f), new Vector3(6.4f, 4.2f, 1.05f), new Color(0.22f, 0.14f, 0.24f, 1f));
        DisableLocalCollider(arch);

        GameObject opening = CreateCube(parent, "FallbackDungeonOpening", new Vector3(0f, 1.3f, 0.58f), new Vector3(2.8f, 2.85f, 0.38f), new Color(0.015f, 0.005f, 0.03f, 1f));
        DisableLocalCollider(opening);

        GameObject lintel = CreateCube(parent, "FallbackDungeonLintel", new Vector3(0f, 3.95f, 0.05f), new Vector3(7f, 0.55f, 1.35f), new Color(0.38f, 0.24f, 0.4f, 1f));
        DisableLocalCollider(lintel);

        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            GameObject pillar = CreateCube(parent, $"FallbackDungeonPillar_{i}", new Vector3(side * 3.15f, 1.85f, 0.08f), new Vector3(0.72f, 3.6f, 1.25f), new Color(0.28f, 0.2f, 0.32f, 1f));
            DisableLocalCollider(pillar);
        }

        for (int i = 0; i < 4; i++)
        {
            float x = Mathf.Lerp(-2.25f, 2.25f, i / 3f);
            GameObject rune = CreateCube(parent, $"FallbackDungeonRune_{i}", new Vector3(x, 3.28f, 0.68f), new Vector3(0.22f, 0.55f, 0.06f), new Color(0.8f, 0.18f, 1f, 1f));
            rune.transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
            DisableLocalCollider(rune);
        }

        AddRoomLight(parent, "DungeonEntranceLight", new Vector3(0f, 2.7f, 1.4f), new Color(0.78f, 0.22f, 1f, 1f), 12f, 1.7f);
    }

    void CreateFallbackNpcVisual(Transform parent)
    {
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(parent, false);
        body.transform.localPosition = new Vector3(0f, 0.95f, 0f);
        body.transform.localScale = new Vector3(0.75f, 0.95f, 0.75f);
        SetupRenderer(body, new Color(0.52f, 0.28f, 0.62f, 1f));

        TextMesh label = new GameObject("Label").AddComponent<TextMesh>();
        label.transform.SetParent(parent, false);
        label.transform.localPosition = new Vector3(0f, 2.4f, 0f);
        label.transform.localRotation = Quaternion.identity;
        label.text = "Guardiao do Calabouco";
        label.fontSize = 48;
        label.characterSize = 0.08f;
        label.anchor = TextAnchor.MiddleCenter;
    }

    void AlignVisualToGround(GameObject visual)
    {
        if (visual == null)
            return;

        Collider[] colliders = visual.GetComponentsInChildren<Collider>(true);
        float lowestY = float.MaxValue;
        bool found = false;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null || col.isTrigger)
                continue;

            lowestY = Mathf.Min(lowestY, col.bounds.min.y);
            found = true;
        }

        if (!found)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                lowestY = Mathf.Min(lowestY, renderer.bounds.min.y);
                found = true;
            }
        }

        if (!found)
            return;

        visual.transform.position += Vector3.up * (transform.position.y - lowestY);
    }

    void CacheEntranceBounds(GameObject target)
    {
        if (target == null)
            return;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (!hasEntranceBounds)
            {
                entranceBounds = renderer.bounds;
                hasEntranceBounds = true;
            }
            else
            {
                entranceBounds.Encapsulate(renderer.bounds);
            }
        }
    }

    void EnsureEntranceCollision(GameObject target)
    {
        if (target == null)
            return;

        if (!hasEntranceBounds)
            CacheEntranceBounds(target);

        BoxCollider collider = target.GetComponent<BoxCollider>();
        if (collider == null)
            collider = target.AddComponent<BoxCollider>();

        collider.isTrigger = false;

        Bounds localBounds = new Bounds(target.transform.InverseTransformPoint(entranceBounds.center), Vector3.zero);
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        bool initialized = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Bounds rendererBounds = renderer.bounds;
            Vector3 localCenter = target.transform.InverseTransformPoint(rendererBounds.center);
            Bounds converted = new Bounds(localCenter, rendererBounds.size);
            if (!initialized)
            {
                localBounds = converted;
                initialized = true;
            }
            else
            {
                localBounds.Encapsulate(converted.min);
                localBounds.Encapsulate(converted.max);
            }
        }

        if (!initialized)
            return;

        collider.center = localBounds.center;
        collider.size = localBounds.size + new Vector3(0.5f, 0.5f, 0.5f);
    }

    void ClearEnvironmentAroundEntrance()
    {
        float clearRadius = Mathf.Max(4f, environmentClearRadius);
        float clearRadiusSqr = clearRadius * clearRadius;
        Vector3 center = GetEntranceCenter();

        TreeInteractable[] trees = FindObjectsByType<TreeInteractable>(FindObjectsSortMode.None);
        for (int i = 0; i < trees.Length; i++)
        {
            TreeInteractable tree = trees[i];
            if (tree == null)
                continue;

            Vector3 treePosition = tree.transform.position;
            if (Mathf.Abs(treePosition.y - center.y) > environmentClearHeight)
                continue;

            if ((treePosition - center).sqrMagnitude <= clearRadiusSqr)
                Destroy(tree.gameObject);
        }

        ResourceNode[] resources = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
        for (int i = 0; i < resources.Length; i++)
        {
            ResourceNode resource = resources[i];
            if (resource == null || !ShouldClearResource(resource))
                continue;

            Vector3 resourcePosition = resource.transform.position;
            if (Mathf.Abs(resourcePosition.y - center.y) > environmentClearHeight)
                continue;

            if ((resourcePosition - center).sqrMagnitude <= clearRadiusSqr)
                Destroy(resource.gameObject);
        }
    }

    bool ShouldClearResource(ResourceNode resource)
    {
        if (resource == null)
            return false;

        string itemName = resource.itemName ?? string.Empty;
        string objectName = resource.gameObject.name ?? string.Empty;

        return ContainsKeyword(itemName, "madeira", "tree", "arvore", "mushroom", "cogumelo") ||
               ContainsKeyword(objectName, "tree", "arvore", "mushroom", "cogumelo");
    }

    bool ContainsKeyword(string value, params string[] keywords)
    {
        if (string.IsNullOrWhiteSpace(value) || keywords == null)
            return false;

        for (int i = 0; i < keywords.Length; i++)
        {
            string keyword = keywords[i];
            if (string.IsNullOrWhiteSpace(keyword))
                continue;

            if (value.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    Vector3 GetEntranceCenter()
    {
        if (hasEntranceBounds)
            return entranceBounds.center;

        return transform.position;
    }

    Vector3 GetEntranceFrontPoint(float extraDistance)
    {
        Vector3 center = GetEntranceCenter();
        float depth = hasEntranceBounds ? Vector3.Dot(entranceBounds.extents, AbsVector(transform.forward)) : 2f;
        return center + transform.forward * (depth + Mathf.Max(0f, extraDistance));
    }

    Vector3 AbsVector(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }
}
