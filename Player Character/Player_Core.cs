using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_Core : MonoBehaviour
{
    public static Player_Core Instance { get; private set; }

    #region Component References
    [GetComponent] private Transform tf;
    [GetComponent] private Rigidbody2D rb;
    [GetComponent] private Player_StateManager sm;
    [GetComponent] private Player_Health playerHealth;
    [GetComponent] private Player_Inventory playerInventory;
    [GetComponent] private Player_Animate playerAnimate;
    #endregion

    #region Serialized References
    [SerializeFieldInternal][SerializeField] private Transform cameraTarget;
    [SerializeFieldInternal][SerializeField] private Transform knifeHitbox;
    [SerializeFieldInternal][SerializeField] private Transform playerInteractHitboxObject;
    [SerializeFieldInternal][SerializeField] private Transform bullet;
    [SerializeFieldInternal][SerializeField] private Transform bulletSpawnPoint;
    #endregion

    #region Values
    [ValueType][SerializeField] private float MaxStamina = float.NaN;
    [ValueType][SerializeField] private float Movespeed = float.NaN;
    [ValueType][SerializeField] private float RunSpeed = float.NaN;
    [ValueType][SerializeField] private float AttackStaminaCost = float.NaN;
    [ValueType][SerializeField] private float StaminaDrainRate = float.NaN;
    [ValueType][SerializeField] private float StaminaRegenRate = float.NaN;
    #endregion

    #region Runtime Properties
    [RuntimeCalculated] private float stamina;
    [RuntimeCalculated] private Vector2 moveInput;
    [RuntimeCalculated] private float currentSpeed;
    private Player_Interact_Hitbox interactHitbox;
    private string lastUsedDoorID;
    #endregion

    #region Cutscene Control
    private Coroutine currentCutsceneMovement;
    private Vector3 cutsceneTargetPosition;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // Singleton implementation
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeComponents();
    }

    private void Start()
    {
        moveInput = Vector3.zero;
        stamina = MaxStamina;
        ProfileManager.Instance.SetStaminaSliderMaxValue(MaxStamina);
    }

    private void Update()
    {
        // ✅ NEW: State-based input handling
        if (sm.CanAcceptInteractionInput)
        {
            HandleInteractInput();
            HandleConsumeItemInput();
            HandleEquipWeaponInput();
        }

        if (sm.CanAcceptAttackInput)
        {
            HandleAttackInput();
        }

        if (sm.CanAcceptMovementInput)
        {
            HandleInputAndStamina();
        }

        // ✅ NEW: Handle forced idle state
        if (sm.ShouldForceIdleAnimation)
        {
            ForceIdleState();
        }
    }

    private void FixedUpdate()
    {
        // ✅ NEW: State-based movement handling
        switch (sm.ControlState)
        {
            case Player_StateManager.PlayerControlState.Normal:
                HandleNormalMovement();
                break;

            case Player_StateManager.PlayerControlState.CutsceneControlled:
                HandleCutsceneMovement();
                break;

            case Player_StateManager.PlayerControlState.UIPaused:
            case Player_StateManager.PlayerControlState.SceneTransition:
            case Player_StateManager.PlayerControlState.AttackLocked:
                // No movement allowed - freeze in place
                rb.linearVelocity = Vector2.zero;
                break;
        }
    }
    #endregion

    #region Initialization
    private void InitializeComponents()
    {
        tf = GetComponent<Transform>();
        rb = GetComponent<Rigidbody2D>();
        sm = GetComponent<Player_StateManager>();
        playerHealth = GetComponent<Player_Health>();
        playerInventory = GetComponent<Player_Inventory>();
        playerAnimate = GetComponent<Player_Animate>();

        if (playerInteractHitboxObject != null)
        {
            interactHitbox = playerInteractHitboxObject.GetComponent<Player_Interact_Hitbox>();
        }
    }
    #endregion

    #region Input Handling
    private void HandleInteractInput()
    {
        if (sm.NearbyInteractables.Count > 0)
        {
            sm.IsNearInteractable = true;
        }
        else
        {
            sm.IsNearInteractable = false;
        }

        if (Input.GetKeyDown(KeyCode.F) && sm.IsNearInteractable && !sm.IsGunDrawing && !sm.IsKnifeUsing)
        {
            sm.NearbyInteractables[0].PlayerInteract();
        }
    }

    private void HandleAttackInput()
    {
        if (sm.CurrentEquipment == Player_StateManager.PlayerEquipment.Knife)
        {
            if (Input.GetKeyDown(KeyCode.X) && !sm.IsKnifeUsing && stamina >= AttackStaminaCost)
            {
                // ✅ NEW: Set attack locked state
                SetAttackLocked("Knife Attack");
                sm.IsKnifeUsing = true;
                stamina -= AttackStaminaCost;
            }
        }

        if (sm.CurrentEquipment == Player_StateManager.PlayerEquipment.Gun)
        {
            if (Input.GetKeyDown(KeyCode.X) && !sm.IsGunDrawing && playerInventory.HasCombatItem(CombatItemType.Ammo) && stamina >= AttackStaminaCost)
            {
                playerInventory.ModifyItemCount(CombatItemType.Ammo, -1);
                ProfileManager.Instance.UpdateGunHolder(playerInventory.GetCombatItemCount(CombatItemType.Ammo));

                // ✅ NEW: Set attack locked state
                SetAttackLocked("Gun Attack");
                sm.IsGunDrawing = true;
                stamina -= AttackStaminaCost;
            }
        }
    }

    private void HandleConsumeItemInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha3) && playerInventory.HasCombatItem(CombatItemType.Bandage))
        {
            playerInventory.ModifyItemCount(CombatItemType.Bandage, -1);
            ProfileManager.Instance.UpdateBandageHolder(playerInventory.GetCombatItemCount(CombatItemType.Bandage));
            playerHealth.Heal(20f);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4) && playerInventory.HasCombatItem(CombatItemType.Medkit))
        {
            playerInventory.ModifyItemCount(CombatItemType.Medkit, -1);
            ProfileManager.Instance.UpdateMedkitHolder(playerInventory.GetCombatItemCount(CombatItemType.Medkit));
            playerHealth.Heal(50f);
        }
    }

    private void HandleEquipWeaponInput()
    {
        // Unequip current weapon
        if (Input.GetKeyDown(KeyCode.Alpha1) && sm.CurrentEquipment == Player_StateManager.PlayerEquipment.Knife && !sm.IsKnifeUsing)
        {
            sm.CurrentEquipment = Player_StateManager.PlayerEquipment.None;
            ProfileManager.Instance.UpdateWeaponBoard(ProfileManager.Weapon.None);
            return;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) && sm.CurrentEquipment == Player_StateManager.PlayerEquipment.Gun && !sm.IsGunDrawing)
        {
            sm.CurrentEquipment = Player_StateManager.PlayerEquipment.None;
            ProfileManager.Instance.UpdateWeaponBoard(ProfileManager.Weapon.None);
            return;
        }

        // Equip weapons
        if (Input.GetKeyDown(KeyCode.Alpha1) && playerInventory.HasCombatItem(CombatItemType.Knife) && !sm.IsKnifeUsing)
        {
            sm.CurrentEquipment = Player_StateManager.PlayerEquipment.Knife;
            ProfileManager.Instance.UpdateWeaponBoard(ProfileManager.Weapon.Knife);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) && playerInventory.HasCombatItem(CombatItemType.Gun) && !sm.IsGunDrawing)
        {
            sm.CurrentEquipment = Player_StateManager.PlayerEquipment.Gun;
            ProfileManager.Instance.UpdateWeaponBoard(ProfileManager.Weapon.Gun);
            ProfileManager.Instance.UpdateGunHolder(playerInventory.GetCombatItemCount(CombatItemType.Ammo));
        }
    }

    private void HandleInputAndStamina()
    {
        if (sm.IsKnifeUsing)
        {
            rb.linearVelocity = Vector2.zero;
            sm.IsWalking = false;
            sm.IsRunLooping = false;
            sm.IsRunStarting = false;
            return;
        }

        moveInput = Vector2.zero;

        // Only arrow keys allowed
        if (Input.GetKey(KeyCode.LeftArrow))
            moveInput.x = -1f;
        else if (Input.GetKey(KeyCode.RightArrow))
            moveInput.x = 1f;

        if (Input.GetKey(KeyCode.UpArrow))
            moveInput.y = 1f;
        else if (Input.GetKey(KeyCode.DownArrow))
            moveInput.y = -1f;

        // Rest of your existing code stays the same...
        currentSpeed = Movespeed;

        // Handle running
        if (Input.GetKey(KeyCode.LeftShift) && moveInput.magnitude > 0)
        {
            stamina -= StaminaDrainRate * Time.deltaTime;
            stamina = Mathf.Clamp(stamina, 0f, MaxStamina);
            ProfileManager.Instance.UpdateStaminaSliderValue(stamina);

            if (stamina > 0)
            {
                currentSpeed = RunSpeed;
                if (!sm.IsRunStarting && !sm.IsRunLooping)
                {
                    sm.IsRunStarting = true;
                }
            }
            else
            {
                sm.IsRunStarting = false;
                sm.IsRunLooping = false;
                currentSpeed = Movespeed;
            }
        }
        else
        {
            stamina += StaminaRegenRate * Time.deltaTime;
            stamina = Mathf.Clamp(stamina, 0f, MaxStamina);
            ProfileManager.Instance.UpdateStaminaSliderValue(stamina);
            sm.IsRunStarting = false;
            sm.IsRunLooping = false;
        }

        // Handle facing direction
        if (moveInput.x != 0)
        {
            if (moveInput.x > 0 && tf.localScale.x > 0)
                FlipToOppositeDirection();
            if (moveInput.x < 0 && tf.localScale.x < 0)
                FlipToOppositeDirection();
        }
    }
    #endregion

    #region Movement & Physics
    // ✅ NEW: Separate normal movement logic
    private void HandleNormalMovement()
    {
        if (sm.IsKnifeUsing || sm.IsGunDrawing)
        {
            rb.linearVelocity = Vector2.zero;
            sm.IsWalking = false;
            sm.IsRunLooping = false;
            sm.IsRunStarting = false;
            return;
        }

        if (moveInput.sqrMagnitude > 0.01f)
        {
            Vector2 targetPos = rb.position + currentSpeed * Time.fixedDeltaTime * moveInput.normalized;
            rb.MovePosition(targetPos);
            sm.IsWalking = true;
        }
        else
        {
            sm.IsWalking = false;
        }
    }

    private void FlipToOppositeDirection()
    {
        transform.localScale = new Vector3(tf.localScale.x * -1, tf.localScale.y, tf.localScale.z);
    }

    // ✅ NEW: Force idle animation state
    private void ForceIdleState()
    {
        rb.linearVelocity = Vector2.zero;
        moveInput = Vector2.zero;
        sm.IsWalking = false;
        sm.IsRunLooping = false;
        sm.IsRunStarting = false;
    }
    #endregion

    #region Player State Control - UPDATED
    // ✅ NEW: Modern state transition methods
    public void SetNormalControl(string source = "Unknown")
    {
        sm.ValidateStateTransition(Player_StateManager.PlayerControlState.Normal, source);
        sm.ControlState = Player_StateManager.PlayerControlState.Normal;
        Debug.Log($"🎮 Player: Normal control restored (by {source})");
    }

    public void SetUIPaused(string source = "UI System")
    {
        sm.ValidateStateTransition(Player_StateManager.PlayerControlState.UIPaused, source);
        sm.ControlState = Player_StateManager.PlayerControlState.UIPaused;

        // Force idle state immediately
        ForceIdleState();

        Debug.Log($"⏸️ Player: UI paused - forced to idle (by {source})");
    }

    public void SetCutsceneControlled(string source = "Cutscene")
    {
        sm.ValidateStateTransition(Player_StateManager.PlayerControlState.CutsceneControlled, source);
        sm.ControlState = Player_StateManager.PlayerControlState.CutsceneControlled;

        // ✅ FIXED: Clear input and force idle on cutscene start
        moveInput = Vector2.zero;
        ForceIdleState(); // Clear any previous walking state

        Debug.Log($"🎬 Player: Cutscene controlled - starting in idle (by {source})");
    }

    public void SetSceneTransition(string source = "Scene System")
    {
        sm.ValidateStateTransition(Player_StateManager.PlayerControlState.SceneTransition, source);
        sm.ControlState = Player_StateManager.PlayerControlState.SceneTransition;

        // Force idle state immediately (same as UI pause)
        ForceIdleState();

        Debug.Log($"🚪 Player: Scene transition - frozen in idle (by {source})");
    }

    public void SetAttackLocked(string source = "Attack System")
    {
        sm.ValidateStateTransition(Player_StateManager.PlayerControlState.AttackLocked, source);
        sm.ControlState = Player_StateManager.PlayerControlState.AttackLocked;
        Debug.Log($"⚔️ Player: Attack locked - movement disabled (by {source})");
    }

    // ✅ BACKWARD COMPATIBILITY: Keep old methods but mark as deprecated
    [System.Obsolete("Use SetUIPaused() instead")]
    public void SetPlayerPause(bool isPaused)
    {
        if (isPaused)
            SetUIPaused("Legacy SetPlayerPause");
        else
            SetNormalControl("Legacy SetPlayerPause");
    }

    [System.Obsolete("Use SetCutsceneControlled() instead")]
    public void SetPlayerIsCutsceneControlled(bool isCutsceneControlled)
    {
        if (isCutsceneControlled)
            SetCutsceneControlled("Legacy SetPlayerIsCutsceneControlled");
        else
            SetNormalControl("Legacy SetPlayerIsCutsceneControlled");
    }

    // ✅ EXISTING: Keep these
    public void SetPlayerIsCombattingState(bool isCombatting)
    {
        sm.IsCombatting = isCombatting;
    }

    public bool IsPlayerCombatting()
    {
        return sm.IsCombatting;
    }

    public void RestoreToFullStamina()
    {
        stamina = MaxStamina;
        ProfileManager.Instance.UpdateStaminaSliderValue(stamina);
    }
    #endregion

    #region Cutscene Movement - ENHANCED
    // ✅ NEW: Cutscene animation control
    public void CutsceneForceIdle()
    {
        if (sm.ControlState != Player_StateManager.PlayerControlState.CutsceneControlled)
        {
            Debug.LogWarning("⚠️ Player is not in cutscene mode!");
            return;
        }

        ForceIdleState();
        Debug.Log("🎬 Cutscene: Forced player to idle");
    }

    public void CutsceneStartWalking()
    {
        if (sm.ControlState != Player_StateManager.PlayerControlState.CutsceneControlled)
        {
            Debug.LogWarning("⚠️ Player is not in cutscene mode!");
            return;
        }

        sm.IsWalking = true;
        Debug.Log("🎬 Cutscene: Started walking animation");
    }

    public void CutsceneStopWalking()
    {
        if (sm.ControlState != Player_StateManager.PlayerControlState.CutsceneControlled)
        {
            Debug.LogWarning("⚠️ Player is not in cutscene mode!");
            return;
        }

        sm.IsWalking = false;
        Debug.Log("🎬 Cutscene: Stopped walking animation");
    }

    public void MoveToCutscenePosition(Vector3 targetPosition)
    {
        if (currentCutsceneMovement != null)
        {
            StopCoroutine(currentCutsceneMovement);
        }

        currentCutsceneMovement = StartCoroutine(CutsceneMovementCoroutine(targetPosition));
    }

    public void StopCutsceneMovement()
    {
        if (currentCutsceneMovement != null)
        {
            StopCoroutine(currentCutsceneMovement);
            currentCutsceneMovement = null;
        }

        cutsceneTargetPosition = Vector3.zero;

        sm.IsWalking = false;

        Debug.Log("🎬 Cutscene movement stopped");
    }

    public void CutsceneFlipToDirection(bool faceRight)
    {
        if (sm.ControlState != Player_StateManager.PlayerControlState.CutsceneControlled)
        {
            Debug.LogWarning("⚠️ Player is not in cutscene mode! Use SetCutsceneControlled() first.");
            return;
        }

        bool currentlyFacingRight = tf.localScale.x < 0;

        if (faceRight && !currentlyFacingRight)
        {
            FlipToOppositeDirection();
            Debug.Log("🎬 Cutscene: Player flipped to face RIGHT");
        }
        else if (!faceRight && currentlyFacingRight)
        {
            FlipToOppositeDirection();
            Debug.Log("🎬 Cutscene: Player flipped to face LEFT");
        }
    }

    public void CutsceneFlipToLeft()
    {
        CutsceneFlipToDirection(false);
    }

    public void CutsceneFlipToRight()
    {
        CutsceneFlipToDirection(true);
    }

    // ✅ UPDATED: Cutscene movement coroutine
    private IEnumerator CutsceneMovementCoroutine(Vector3 targetPosition)
    {
        SetCutsceneControlled("Cutscene Movement");
        cutsceneTargetPosition = targetPosition;

        // ✅ NEW: Start walking animation when movement begins
        CutsceneStartWalking();

        Debug.Log($"🎬 Starting cutscene movement to: {targetPosition}");

        while (Vector3.Distance(tf.position, targetPosition) > 0.1f)
        {
            yield return null;
        }

        rb.MovePosition(targetPosition);
        cutsceneTargetPosition = Vector3.zero;

        // ✅ NEW: Stop walking animation when movement ends
        CutsceneStopWalking();

        Debug.Log($"🎬 Reached cutscene position: {targetPosition}");
    }

    // ✅ UPDATED: HandleCutsceneMovement
    private void HandleCutsceneMovement()
    {
        if (cutsceneTargetPosition == Vector3.zero)
        {
            return; // No target set
        }

        // ✅ FIXED: Check if we're already close enough
        float distanceToTarget = Vector3.Distance(tf.position, cutsceneTargetPosition);
        if (distanceToTarget <= 0.1f)
        {
            // We're close enough - snap to target and stop
            rb.MovePosition(cutsceneTargetPosition);
            cutsceneTargetPosition = Vector3.zero; // Clear target
            Debug.Log("🎬 Player reached target via HandleCutsceneMovement");
            return;
        }

        Vector3 direction = (cutsceneTargetPosition - tf.position).normalized;
        Vector2 targetPos = rb.position + Movespeed * Time.fixedDeltaTime * (Vector2)direction;

        rb.MovePosition(targetPos);
    }
    #endregion

    #region Door System
    public void SetLastUsedDoorID(string doorID)
    {
        lastUsedDoorID = doorID;
        Debug.Log($"Last used door ID set to: {doorID}");
    }

    public string GetLastUsedDoorID()
    {
        return lastUsedDoorID;
    }
    #endregion

    #region Public Getters
    public Transform GetCameraTarget()
    {
        return cameraTarget;
    }

    public Transform GetPlayerReference()
    {
        return tf;
    }
    #endregion

    #region Animation Events
    private void StartRunLooping()
    {
        sm.IsRunStarting = false;
        sm.IsRunLooping = true;
    }

    private void StartKnifeHitbox()
    {
        knifeHitbox.gameObject.SetActive(true);
        playerAnimate.PlayKnifeSound();
    }

    private void EndKnifeHitbox()
    {
        knifeHitbox.gameObject.SetActive(false);
    }

    private void StopKnifeUse()
    {
        sm.IsKnifeUsing = false;
        // ✅ NEW: Return to normal state after attack
        SetNormalControl("Knife Attack Complete");
    }

    private void StopGunDraw()
    {
        sm.IsGunDrawing = false;
        // ✅ NEW: Return to normal state after attack
        SetNormalControl("Gun Attack Complete");
    }

    private void GunFire()
    {
        playerAnimate.PlayGunShotSound();

        Transform bulletInstance = Instantiate(bullet, bulletSpawnPoint.position, bulletSpawnPoint.rotation);
        Bullet_Controller bulletController = bulletInstance.GetComponent<Bullet_Controller>();

        float bulletSpeed = 10f;
        Vector3 bulletVelocity = tf.localScale.x > 0
            ? new Vector3(-bulletSpeed, 0, 0)
            : new Vector3(bulletSpeed, 0, 0);

        bulletController.velocity = bulletVelocity;
    }
    #endregion
}