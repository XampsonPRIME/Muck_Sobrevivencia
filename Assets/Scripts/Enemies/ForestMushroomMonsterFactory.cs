using UnityEngine;

public static class ForestMushroomMonsterFactory
{
    public const string ResourcesPath = "Enemies/ForestMushroomMonster";
    public const string EntityIdPrefix = "ForestMushroomMonster";
    const string RootName = "ForestMushroomMonster";

    public static GameObject LoadPrefab()
    {
        return Resources.Load<GameObject>(ResourcesPath);
    }

    public static bool IsForestMushroomEntity(string entityId)
    {
        return !string.IsNullOrWhiteSpace(entityId) &&
               entityId.StartsWith(EntityIdPrefix + "|", System.StringComparison.Ordinal);
    }

    public static MiniKrug CreateInstance(GameObject sourcePrefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (sourcePrefab == null)
            return null;

        MiniKrugEnemyProfile profile = sourcePrefab.GetComponent<MiniKrugEnemyProfile>();

        GameObject rootObject = new GameObject(RootName);
        if (parent != null)
            rootObject.transform.SetParent(parent, false);

        rootObject.transform.SetPositionAndRotation(position, rotation);

        GameObject visualObject = Object.Instantiate(sourcePrefab, rootObject.transform);
        visualObject.name = sourcePrefab.name;

        MiniKrug enemy = Configure(rootObject, profile);
        if (enemy == null)
            return null;

        MiniKrugLegacyAnimationDriver animationDriver = rootObject.GetComponent<MiniKrugLegacyAnimationDriver>();
        if (animationDriver != null)
            animationDriver.animationComponent = visualObject.GetComponent<Animation>() ?? visualObject.GetComponentInChildren<Animation>(true);

        return enemy;
    }

    public static MiniKrug Configure(GameObject enemyObject, MiniKrugEnemyProfile profile = null)
    {
        if (enemyObject == null)
            return null;

        enemyObject.name = RootName;

        MiniKrug enemy = enemyObject.GetComponent<MiniKrug>();
        if (enemy == null)
            enemy = enemyObject.AddComponent<MiniKrug>();

        MiniKrugLegacyAnimationDriver animationDriver = enemyObject.GetComponent<MiniKrugLegacyAnimationDriver>();
        if (animationDriver == null)
            animationDriver = enemyObject.AddComponent<MiniKrugLegacyAnimationDriver>();

        if (animationDriver.animationComponent == null)
            animationDriver.animationComponent = enemyObject.GetComponentInChildren<Animation>(true);

        if (profile != null)
        {
            profile.ApplyTo(enemy);
            enemy.RefreshBaseStats();
        }

        enemyObject.transform.localScale = Vector3.one;

        return enemy;
    }
}
