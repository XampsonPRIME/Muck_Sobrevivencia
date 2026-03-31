using UnityEngine;

public class TreeInteractable : MonoBehaviour
{
    public int life = 3;

    public void Hit()
    {
        life--;

        if (life <= 0)
        {
            Destroy(gameObject);
        }
    }
}