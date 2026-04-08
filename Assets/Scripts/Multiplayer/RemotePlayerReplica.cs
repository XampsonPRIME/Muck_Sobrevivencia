using UnityEngine;

public class RemotePlayerReplica : MonoBehaviour
{
    public string PlayerId { get; private set; }
    public string PlayerName { get; private set; }

    public float positionSmoothing = 12f;
    public float rotationSmoothing = 14f;
    public float animationSmoothing = 8f;

    Animator animator;
    Vector3 targetPosition;
    Quaternion targetRotation;
    float targetAnimationSpeed;
    bool hasReceivedInitialState;

    public static RemotePlayerReplica CreateFromPlayer(PlayerMovement sourcePlayer, string playerId, string playerName)
    {
        if (sourcePlayer == null)
            return null;

        GameObject root = new GameObject($"Remote_{playerName}");
        RemotePlayerReplica replica = root.AddComponent<RemotePlayerReplica>();
        replica.BuildVisualFromSource(sourcePlayer);
        replica.Initialize(playerId, playerName);
        return replica;
    }

    public void Initialize(string playerId, string playerName)
    {
        PlayerId = playerId;
        PlayerName = string.IsNullOrWhiteSpace(playerName) ? "Remote" : playerName;

        gameObject.tag = "Untagged";
        hasReceivedInitialState = false;
    }

    public void ApplyState(LanMultiplayerManager.LanPlayerState state)
    {
        if (state == null)
            return;

        targetPosition = state.position;
        targetRotation = state.rotation;
        targetAnimationSpeed = state.isDead ? 0f : state.animationSpeed;

        if (!hasReceivedInitialState)
        {
            transform.SetPositionAndRotation(targetPosition, targetRotation);
            hasReceivedInitialState = true;
        }
    }

    void Update()
    {
        float positionBlend = 1f - Mathf.Exp(-positionSmoothing * Time.deltaTime);
        float rotationBlend = 1f - Mathf.Exp(-rotationSmoothing * Time.deltaTime);
        float animationBlend = 1f - Mathf.Exp(-animationSmoothing * Time.deltaTime);

        transform.position = Vector3.Lerp(transform.position, targetPosition, positionBlend);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationBlend);

        if (animator != null)
        {
            float currentSpeed = animator.GetFloat("Speed");
            animator.SetFloat("Speed", Mathf.Lerp(currentSpeed, targetAnimationSpeed, animationBlend));
        }
    }

    void BuildVisualFromSource(PlayerMovement sourcePlayer)
    {
        Animator sourceAnimator = sourcePlayer.GetComponentInChildren<Animator>(true);
        if (sourceAnimator == null)
            return;

        GameObject visualRoot = Instantiate(sourceAnimator.gameObject, transform);
        visualRoot.name = "VisualReplica";
        visualRoot.transform.localPosition = sourceAnimator.transform.localPosition;
        visualRoot.transform.localRotation = sourceAnimator.transform.localRotation;
        visualRoot.transform.localScale = sourceAnimator.transform.localScale;
        visualRoot.SetActive(true);

        foreach (Camera cameraComponent in visualRoot.GetComponentsInChildren<Camera>(true))
            Destroy(cameraComponent.gameObject);

        foreach (AudioListener listener in visualRoot.GetComponentsInChildren<AudioListener>(true))
            Destroy(listener);

        foreach (Collider colliderComponent in visualRoot.GetComponentsInChildren<Collider>(true))
            Destroy(colliderComponent);

        foreach (Rigidbody rigidbodyComponent in visualRoot.GetComponentsInChildren<Rigidbody>(true))
            Destroy(rigidbodyComponent);

        foreach (MonoBehaviour behaviour in visualRoot.GetComponentsInChildren<MonoBehaviour>(true))
            behaviour.enabled = false;

        animator = visualRoot.GetComponentInChildren<Animator>(true);
        if (animator != null)
            animator.enabled = true;
    }
}
