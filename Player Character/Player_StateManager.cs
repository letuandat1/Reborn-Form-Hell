using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class Player_StateManager : MonoBehaviour
{
    public static Player_StateManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    #region Enums
    public enum PlayerEquipment { None, Knife, Gun }

    public enum PlayerControlState
    {
        Normal,              // Full player control (walking, attacking, interacting)
        UIPaused,           // UI opened (dialogue, lore) - freeze everything, force idle
        CutsceneControlled, // Cutscene active - no input, script controls movement/flip
        SceneTransition,    // Door transition - freeze movement, maintain current animation
        AttackLocked        // During attack animations - movement blocked, no new input
    }
    #endregion

    #region Core State
    [Header("Core Player State")]
    [RuntimeCalculated][SerializeField] private PlayerControlState _controlState = PlayerControlState.Normal;
    public PlayerControlState ControlState
    {
        get => _controlState;
        set => _controlState = value; // Only Player_Core should set this
    }

    [RuntimeCalculated][SerializeField] private PlayerEquipment _currentEquipment = PlayerEquipment.None;
    public PlayerEquipment CurrentEquipment
    {
        get => _currentEquipment;
        set => _currentEquipment = value;
    }
    #endregion

    #region Animation States
    [Header("Animation States")]
    [RuntimeCalculated][SerializeField] private bool _isKnifeUsing = false;
    public bool IsKnifeUsing
    {
        get => _isKnifeUsing;
        set => _isKnifeUsing = value;
    }

    [RuntimeCalculated][SerializeField] private bool _isWalking = false;
    public bool IsWalking
    {
        get => _isWalking;
        set => _isWalking = value;
    }

    [RuntimeCalculated][SerializeField] private bool _isRunStarting = false;
    public bool IsRunStarting
    {
        get => _isRunStarting;
        set => _isRunStarting = value;
    }

    [RuntimeCalculated][SerializeField] private bool _isRunLooping = false;
    public bool IsRunLooping
    {
        get => _isRunLooping;
        set => _isRunLooping = value;
    }

    [RuntimeCalculated][SerializeField] private bool _isGunDrawing = false;
    public bool IsGunDrawing
    {
        get => _isGunDrawing;
        set => _isGunDrawing = value;
    }
    #endregion

    #region Game States
    [Header("Game States")]
    [RuntimeCalculated][SerializeField] private bool _isStealth = false;
    public bool IsStealth
    {
        get => _isStealth;
        set => _isStealth = value;
    }

    [RuntimeCalculated][SerializeField] private bool _isDead = false;
    public bool IsDead
    {
        get => _isDead;
        set => _isDead = value;
    }

    [RuntimeCalculated][SerializeField] private bool _isNearInteractable = false;
    public bool IsNearInteractable
    {
        get => _isNearInteractable;
        set => _isNearInteractable = value;
    }

    [RuntimeCalculated][SerializeField] private bool _isCombatting = false;
    public bool IsCombatting
    {
        get => _isCombatting;
        set => _isCombatting = value;
    }
    #endregion

    #region Interactable Management
    [Header("Interaction System")]
    [RuntimeCalculated] public List<Interactable> NearbyInteractables = new List<Interactable>();

    public void AddInteractable(Interactable interactable)
    {
        if (!NearbyInteractables.Contains(interactable))
        {
            NearbyInteractables.Add(interactable);
            Debug.Log($"🎯 Added interactable: {interactable.name}");
        }
    }

    public void RemoveInteractable(Interactable interactable)
    {
        if (NearbyInteractables.Contains(interactable))
        {
            NearbyInteractables.Remove(interactable);
            Debug.Log($"🎯 Removed interactable: {interactable.name}");
        }
    }

    public void ClearAllInteractables()
    {
        NearbyInteractables.Clear();
        IsNearInteractable = false;
        Debug.Log("🎯 Cleared all interactables");
    }
    #endregion

    #region Derived Properties - Backward Compatibility
    // ✅ BACKWARD COMPATIBILITY: Old boolean properties computed from new state
    public bool IsPaused => ControlState == PlayerControlState.UIPaused;
    public bool IsCutsceneControlled => ControlState == PlayerControlState.CutsceneControlled;
    public bool IsSceneTransitioning => ControlState == PlayerControlState.SceneTransition;
    public bool IsAttackLocked => ControlState == PlayerControlState.AttackLocked;
    #endregion

    #region Input Permission Checks
    // ✅ NEW: Clean permission checking for input systems
    public bool CanAcceptMovementInput => ControlState == PlayerControlState.Normal;

    public bool CanAcceptInteractionInput =>
        ControlState == PlayerControlState.Normal &&
        !IsKnifeUsing &&
        !IsGunDrawing;

    public bool CanAcceptAttackInput =>
        ControlState == PlayerControlState.Normal &&
        !IsKnifeUsing &&
        !IsGunDrawing;

    public bool CanAcceptEquipInput =>
        ControlState == PlayerControlState.Normal &&
        !IsKnifeUsing &&
        !IsGunDrawing;

    public bool CanAcceptConsumeInput =>
        ControlState == PlayerControlState.Normal &&
        !IsKnifeUsing &&
        !IsGunDrawing;
    #endregion

    #region Movement Permission Checks
    // ✅ NEW: Clean permission checking for movement systems
    public bool CanMove =>
        ControlState == PlayerControlState.Normal ||
        ControlState == PlayerControlState.CutsceneControlled;

    public bool CanFlip =>
        ControlState == PlayerControlState.Normal ||
        ControlState == PlayerControlState.CutsceneControlled;

    public bool ShouldFreezeInPlace =>
        ControlState == PlayerControlState.UIPaused ||
        ControlState == PlayerControlState.SceneTransition ||
        ControlState == PlayerControlState.AttackLocked;

    public bool ShouldForceIdleAnimation =>
        ControlState == PlayerControlState.UIPaused ||
        ControlState == PlayerControlState.SceneTransition;

    #endregion

    #region Animation Permission Checks
    // ✅ NEW: Animation control based on state
    public bool CanPlayWalkAnimation =>
        ControlState == PlayerControlState.Normal ||
        ControlState == PlayerControlState.CutsceneControlled;

    public bool CanPlayRunAnimation =>
        ControlState == PlayerControlState.Normal;

    public bool CanPlayAttackAnimation =>
        ControlState == PlayerControlState.Normal ||
        ControlState == PlayerControlState.AttackLocked;

    public bool ShouldResetAnimationsToIdle =>
        ControlState == PlayerControlState.UIPaused;
    #endregion

    #region State Validation & Debugging
    [ContextMenu("Debug Current State")]
    private void DebugCurrentState()
    {
        Debug.Log("=== PLAYER STATE DEBUG ===");
        Debug.Log($"🎮 Control State: {ControlState}");
        Debug.Log($"⚔️ Equipment: {CurrentEquipment}");
        Debug.Log("");

        Debug.Log("INPUT PERMISSIONS:");
        Debug.Log($"   Movement: {CanAcceptMovementInput}");
        Debug.Log($"   Interaction: {CanAcceptInteractionInput}");
        Debug.Log($"   Attack: {CanAcceptAttackInput}");
        Debug.Log($"   Equip: {CanAcceptEquipInput}");
        Debug.Log("");

        Debug.Log("MOVEMENT PERMISSIONS:");
        Debug.Log($"   Can Move: {CanMove}");
        Debug.Log($"   Can Flip: {CanFlip}");
        Debug.Log($"   Should Freeze: {ShouldFreezeInPlace}");
        Debug.Log("");

        Debug.Log("ANIMATION STATES:");
        Debug.Log($"   Walking: {IsWalking}");
        Debug.Log($"   Running: Start={IsRunStarting}, Loop={IsRunLooping}");
        Debug.Log($"   Knife: {IsKnifeUsing}");
        Debug.Log($"   Gun: {IsGunDrawing}");
        Debug.Log("");

        Debug.Log("GAME STATES:");
        Debug.Log($"   Stealth: {IsStealth}");
        Debug.Log($"   Combatting: {IsCombatting}");
        Debug.Log($"   Dead: {IsDead}");
        Debug.Log($"   Near Interactable: {IsNearInteractable} (Count: {NearbyInteractables.Count})");
    }

    [ContextMenu("List Nearby Interactables")]
    private void DebugNearbyInteractables()
    {
        if (NearbyInteractables.Count == 0)
        {
            Debug.Log("🎯 No nearby interactables");
            return;
        }

        Debug.Log($"🎯 Nearby Interactables ({NearbyInteractables.Count}):");
        for (int i = 0; i < NearbyInteractables.Count; i++)
        {
            var interactable = NearbyInteractables[i];
            if (interactable != null)
            {
                Debug.Log($"   [{i}] {interactable.name} ({interactable.GetType().Name})");
            }
            else
            {
                Debug.LogWarning($"   [{i}] NULL INTERACTABLE!");
            }
        }
    }

    [ContextMenu("Force Normal State")]
    private void ForceNormalState()
    {
        ControlState = PlayerControlState.Normal;
        Debug.Log("🔧 Forced player to Normal state");
    }

    [ContextMenu("Test All States")]
    private void TestAllStates()
    {
        Debug.Log("🧪 Testing all player states:");

        foreach (PlayerControlState state in System.Enum.GetValues(typeof(PlayerControlState)))
        {
            ControlState = state;
            Debug.Log($"   {state}: Input={CanAcceptMovementInput}, Move={CanMove}, Freeze={ShouldFreezeInPlace}");
        }

        ControlState = PlayerControlState.Normal; // Reset to normal
        Debug.Log("🔧 Reset to Normal state");
    }
    #endregion

    #region State Transition Validation
    // ✅ NEW: Validate state transitions (called by Player_Core)
    public bool CanTransitionTo(PlayerControlState newState)
    {
        // Define valid state transitions
        switch (ControlState)
        {
            case PlayerControlState.Normal:
                return true; // Can transition to any state from Normal

            case PlayerControlState.UIPaused:
                return newState == PlayerControlState.Normal; // Can only return to Normal

            case PlayerControlState.CutsceneControlled:
                return newState == PlayerControlState.Normal; // Can only return to Normal

            case PlayerControlState.SceneTransition:
                return newState == PlayerControlState.Normal; // Can only return to Normal (after scene load)

            case PlayerControlState.AttackLocked:
                return newState == PlayerControlState.Normal; // Can only return to Normal (after attack)

            default:
                return false;
        }
    }

    public void ValidateStateTransition(PlayerControlState newState, string source = "Unknown")
    {
        if (!CanTransitionTo(newState))
        {
            Debug.LogWarning($"⚠️ Invalid state transition: {ControlState} → {newState} (requested by {source})");
        }
        else
        {
            Debug.Log($"✅ Valid state transition: {ControlState} → {newState} (requested by {source})");
        }
    }
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        // Initialize to normal state
        ControlState = PlayerControlState.Normal;
        Debug.Log("🎮 Player_StateManager initialized to Normal state");
    }

    private void Update()
    {
        // Update derived states
        IsNearInteractable = NearbyInteractables.Count > 0;
    }
    #endregion
}