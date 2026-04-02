using UnityEngine;

public class Player_Damage : MonoBehaviour
{
    [SerializeField] private float damageAmount;
    public float DamageAmount => damageAmount; // Public property to access the damage amount    
}