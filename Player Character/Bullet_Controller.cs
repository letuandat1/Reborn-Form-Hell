using UnityEngine;

public class Bullet_Controller : MonoBehaviour
{
    public Vector3 velocity; // Public velocity property to be set during instantiation
    [SerializeField] private float lifetime = 5f; // Lifetime of the bullet before it gets destroyed

    [SerializeField] private float damageAmount = 10f; // Sát thương của viên đạn
    public float DamageAmount => damageAmount; // Property để truy cập từ ngoài

    private void Start()
    {
        // Destroy the bullet after its lifetime expires
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        // Move the bullet based on its velocity
        transform.position += velocity * Time.deltaTime;
    }
}