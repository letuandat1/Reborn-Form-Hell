using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Player_Health : MonoBehaviour
{
    // Singleton instance
    public static Player_Health Instance { get; private set; }

    // Thuộc tính cần dùng GetComponent để truy cập các thành phần khác
    [GetComponent] private Player_StateManager sm; // Quản lý trạng thái người chơi

    // Thuộc tính thông số nhân vật, dùng SerializeField để có thể chỉnh sửa trong Unity Editor
    [ValueType][SerializeField] float maxHealth = float.NaN; // Máu tối đa

    // Biến nội bộ để tính toán
    [RuntimeCalculated] private float health; // Máu của người chơi
    public void SetMaxHealth(float value)
    {
        maxHealth = value;
    }

    public float GetHealth()
    {
        return health;
    }
    [SerializeField] private SceneNameSO titleSceneNameSO; // Tham chiếu đến SceneName

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        sm = GetComponent<Player_StateManager>();
        InitializeHealth(); // Khởi tạo máu khi bắt đầu trò chơi
    }
    public void RestoreToFullHealth()
    {
        health = maxHealth;
        CheckHealthToHideBloodEffect(); // Kiểm tra và ẩn hiệu ứng máu nếu cần
        Debug.Log("Player's health restored to full.");
    }

    public void InitializeHealth()
    {
        health = maxHealth;
        Debug.Log("Player's health initialized to full.");
    }

    public void TakeDamage(float damage)
    {
        if (sm.IsPaused)
        {
            return; // Không xử lý nếu người chơi đang tạm dừng
        }
        health -= damage;
        CheckHealthToShowBloodEffect(); // Kiểm tra và hiển thị hiệu ứng máu nếu cần
        Console.WriteLine($"Player health before damage: {health + damage}");
        if (health <= 0 && sm.IsDead == false)
        {
            health = 0;
            sm.IsDead = true;

            // ✅ FREEZE PLAYER IMMEDIATELY ON DEATH!
            if (Player_Core.Instance != null)
            {
                Player_Core.Instance.SetUIPaused("Player Death - Freezing before respawn");
            }

            Debug.Log("Player is dead");

            // ✅ SAVE PROGRESS BEFORE RESPAWNING (while player is frozen)
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SaveGame(); // Save all progression and inventory
                Debug.Log("💾 Game saved before death respawn to preserve progress");
            }

            if (GameManager.Instance.HasValidCheckpoint())
            {
                GameManager.Instance.LoadToCurrentCheckpoint();
            }
            else
            {
                SceneLoader.LoadToTitleDead(titleSceneNameSO.GetSceneName());
            }
        }
    }
    public void Heal(float amount)
    {
        health += amount;
        CheckHealthToHideBloodEffect(); // Kiểm tra và ẩn hiệu ứng máu nếu cần
        if (health > maxHealth)
        {
            health = maxHealth; // Đảm bảo máu không vượt quá giới hạn tối đa
        }
        Debug.Log($"Player healed by {amount}, current health: {health}");
    }

    private void CheckHealthToShowBloodEffect()
    {
        if (health <= maxHealth * 0.8f)
        {
            BloodCanvasController.Instance.ShowBloodEffect1(); // Hiển thị hiệu ứng máu khi chết
        }
        if (health <= maxHealth * 0.6f)
        {
            BloodCanvasController.Instance.ShowBloodEffect2(); // Hiển thị hiệu ứng máu khi máu dưới 50%
        }
        if (health <= maxHealth * 0.4f)
        {
            BloodCanvasController.Instance.ShowBloodEffect3(); // Hiển thị hiệu ứng máu khi máu dưới 80%
        }
    }
    private void CheckHealthToHideBloodEffect()
    {
        if (health > maxHealth * 0.4f)
        {
            BloodCanvasController.Instance.HideBloodEffect3(); // Ẩn hiệu ứng máu khi máu trên 20%
        }
        if (health > maxHealth * 0.6f)
        {
            BloodCanvasController.Instance.HideBloodEffect2(); // Ẩn hiệu ứng máu khi máu trên 50%
        }
        if (health > maxHealth * 0.8f)
        {
            BloodCanvasController.Instance.HideBloodEffect1(); // Ẩn hiệu ứng máu khi máu trên 80%
        }
    }
}
