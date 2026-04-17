using UnityEngine;

public static class ForestMushroomBossFactory
{
    public const string EntityId = "ForestMushroomBoss|World";
    public const string PrimaryResourcesPath = "Enemies/ForestMushroomMonsterBoss";
    public const string LegacyResourcesPath = "Enemies/ForestMushroomMonsterBoos";
    const string RootName = "ForestMushroomBoss";

    public static GameObject LoadPrefab()
    {
        GameObject prefab = Resources.Load<GameObject>(PrimaryResourcesPath);
        if (prefab == null)
            prefab = Resources.Load<GameObject>(LegacyResourcesPath);

        return prefab;
    }

    public static bool IsForestMushroomBossEntity(string entityId)
    {
        return string.Equals(entityId, EntityId, System.StringComparison.Ordinal);
    }

    public static BossEnemy CreateInstance(GameObject sourcePrefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (sourcePrefab == null)
            return null;

        BossEnemyProfile profile = sourcePrefab.GetComponent<BossEnemyProfile>();

        GameObject rootObject = new GameObject(RootName);
        if (parent != null)
            rootObject.transform.SetParent(parent, false);

        rootObject.transform.SetPositionAndRotation(position, rotation);

        GameObject visualObject = Object.Instantiate(sourcePrefab, rootObject.transform);
        visualObject.name = sourcePrefab.name;

        BossEnemy boss = Configure(rootObject, profile);
        if (boss == null)
            return null;

        BossLegacyAnimationDriver animationDriver = rootObject.GetComponent<BossLegacyAnimationDriver>();
        if (animationDriver != null)
            animationDriver.animationComponent = visualObject.GetComponent<Animation>() ?? visualObject.GetComponentInChildren<Animation>(true);

        return boss;
    }

    public static BossEnemy Configure(GameObject enemyObject, BossEnemyProfile profile = null)
    {
        if (enemyObject == null)
            return null;

        enemyObject.name = RootName;

        BossEnemy boss = enemyObject.GetComponent<BossEnemy>();
        if (boss == null)
            boss = enemyObject.AddComponent<BossEnemy>();

        BossLegacyAnimationDriver animationDriver = enemyObject.GetComponent<BossLegacyAnimationDriver>();
        if (animationDriver == null)
            animationDriver = enemyObject.AddComponent<BossLegacyAnimationDriver>();

        if (animationDriver.animationComponent == null)
            animationDriver.animationComponent = enemyObject.GetComponentInChildren<Animation>(true);

        if (profile != null)
        {
            profile.ApplyTo(boss);
            boss.RefreshBaseStats();
        }

        enemyObject.transform.localScale = Vector3.one;
        return boss;
    }
}
