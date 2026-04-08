using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LanNetworkEntity : MonoBehaviour
{
    [SerializeField] string entityId;

    public string EntityId => entityId;

    void Awake()
    {
        if (string.IsNullOrWhiteSpace(entityId))
            entityId = BuildStableId();
    }

    public static LanNetworkEntity Ensure(Component owner)
    {
        if (owner == null)
            return null;

        LanNetworkEntity entity = owner.GetComponent<LanNetworkEntity>();
        if (entity == null)
            entity = owner.gameObject.AddComponent<LanNetworkEntity>();

        return entity;
    }

    public static LanNetworkEntity Ensure(Component owner, string forcedEntityId)
    {
        LanNetworkEntity entity = Ensure(owner);
        if (entity != null && !string.IsNullOrWhiteSpace(forcedEntityId))
            entity.entityId = forcedEntityId;

        return entity;
    }

    string BuildStableId()
    {
        StringBuilder builder = new StringBuilder();
        builder.Append(SceneManager.GetActiveScene().name);
        builder.Append('|');
        builder.Append(gameObject.name);
        builder.Append('|');
        builder.Append(BuildTransformPath(transform));
        builder.Append('|');
        builder.Append(Mathf.RoundToInt(transform.position.x * 100f));
        builder.Append(',');
        builder.Append(Mathf.RoundToInt(transform.position.y * 100f));
        builder.Append(',');
        builder.Append(Mathf.RoundToInt(transform.position.z * 100f));
        return builder.ToString();
    }

    static string BuildTransformPath(Transform current)
    {
        if (current == null)
            return "null";

        StringBuilder builder = new StringBuilder(current.name);
        Transform cursor = current.parent;

        while (cursor != null)
        {
            builder.Insert(0, '/');
            builder.Insert(0, cursor.name);
            cursor = cursor.parent;
        }

        return builder.ToString();
    }
}
