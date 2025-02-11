using UnityEngine;

public class SimpleMovement : MonoBehaviour
{
    [SerializeField] public float speed = 5f;

    void Update()
    {
        Vector3 move = new Vector3(
            Input.GetAxis("Horizontal"),
            0,
            Input.GetAxis("Vertical")
        );
        
        transform.Translate(move * speed * Time.deltaTime);
    }
} 