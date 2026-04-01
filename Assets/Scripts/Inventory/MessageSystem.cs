using UnityEngine;

public class MessageSystem : MonoBehaviour
{
    public static MessageSystem Instance;

    public GameObject messagePrefab;
    public Transform panel;

    void Awake()
    {
        Instance = this;
    }

    public void ShowMessage(string message)
    {
        GameObject obj = Instantiate(messagePrefab, panel);

        MessageItem item = obj.GetComponent<MessageItem>();
        item.Setup(message);
    }
}