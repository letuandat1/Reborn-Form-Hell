using System.Collections.Generic;
using UnityEngine;

public class Player_Inventory : MonoBehaviour
{
    public static Player_Inventory Instance { get; private set; }

    [GetComponent] private Player_StateManager sm;

    [RuntimeCalculated] private bool isBandageDiscovered = false;
    public bool IsBandageDiscovered => isBandageDiscovered;
    [RuntimeCalculated] private bool isMedkitDiscovered = false;
    public bool IsMedkitDiscovered => isMedkitDiscovered;

    [Header("Ammo Settings")]
    [SerializeField] private int ammoPerPickup = 7;  // ✅ Designer-configurable ammo amount

    // === COMBAT ITEMS (Legacy System) ===
    [RuntimeCalculated]
    private readonly Dictionary<CombatItemType, Item> combatItems = new Dictionary<CombatItemType, Item>()
    {
        { CombatItemType.Knife, new Item("Knife", "A sharp blade for close combat") },
        { CombatItemType.Gun, new Item("Gun", "A firearm for ranged combat") },
        { CombatItemType.Ammo, new Item("Ammo", "Ammunition for the gun") },
        { CombatItemType.Bandage, new Item("Bandage", "A bandage for treating minor wounds") },
        { CombatItemType.Medkit, new Item("Medkit", "A medical kit for treating serious injuries") },
    };

    // === UNIFIED ITEM SYSTEM (New) ===
    [RuntimeCalculated] 
    private readonly Dictionary<string, UnifiedItemData> unifiedInventory = new Dictionary<string, UnifiedItemData>();

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

    void Start()
    {
        sm = GetComponent<Player_StateManager>();
    }

    // === COMBAT ITEM METHODS (Updated for CombatItemType) ===
    public void ModifyItemCount(CombatItemType item, int count)
    {
        if (combatItems.ContainsKey(item))
        {
            combatItems[item].Count += count;
            Debug.Log($"🗡️ Combat: Added {count} {item}. New count: {combatItems[item].Count}");
        }
        else
        {
            Debug.LogWarning($"Combat item {item} not found in inventory.");
        }

        // Update discovery flags
        if (item == CombatItemType.Bandage) isBandageDiscovered = true;
        else if (item == CombatItemType.Medkit) isMedkitDiscovered = true;

        // Update UI
        UpdateCombatItemUI(item);
    }

    public bool HasCombatItem(CombatItemType item)
    {
        return combatItems.ContainsKey(item) && combatItems[item].Count > 0;
    }

    public int GetCombatItemCount(CombatItemType item)
    {
        return combatItems.ContainsKey(item) ? combatItems[item].Count : 0;
    }

    // ✅ FIXED: AddCombatItem method that CombatItem.cs is calling
    public void AddCombatItem(CombatItemType itemType)
    {
        int amountToAdd = 1;  // Default for most items
        
        if (itemType == CombatItemType.Ammo)
        {
            amountToAdd = ammoPerPickup;  // ✅ Use configurable amount for ammo
        }
        
        ModifyItemCount(itemType, amountToAdd);
        Debug.Log($"🗡️ Added combat item: {itemType} x{amountToAdd}");
    }

    // === NEW UNIFIED ITEM METHODS ===
    public void AddUnifiedItem(
        string itemID, 
        InventoryItemSO displayInfo,
        CombatItemType? combatType = null,
        ProgressionFlagSO progressionFlag = null,
        int milestoneCount = 0,
        string loreID = null)
    {
        if (string.IsNullOrEmpty(itemID))
        {
            Debug.LogWarning("⚠️ Attempted to add item with empty ID!");
            return;
        }

        // ✅ HARDCODED: Always add 1 item to unified inventory
        if (!unifiedInventory.ContainsKey(itemID))
        {
            unifiedInventory[itemID] = new UnifiedItemData(itemID, displayInfo, 0);
        }

        var itemData = unifiedInventory[itemID];
        itemData.count += 1;  // ✅ Always add 1

        // Set usage flags
        if (combatType.HasValue)
        {
            itemData.hasCombatUsage = true;
            itemData.combatType = combatType.Value;
        }

        if (progressionFlag != null)
        {
            itemData.hasKeyUsage = true;
            itemData.progressionFlag = progressionFlag;
            itemData.requiredMilestone = milestoneCount;
        }

        if (!string.IsNullOrEmpty(loreID))
        {
            itemData.hasLoreUsage = true;
            itemData.loreID = loreID;
        }

        Debug.Log($"📦 Added to unified inventory: {itemData.GetDisplayName()} x1");
    }

    public UnifiedItemData GetUnifiedItem(string itemID)
    {
        return unifiedInventory.ContainsKey(itemID) ? unifiedInventory[itemID] : null;
    }

    public Dictionary<string, UnifiedItemData> GetAllUnifiedItems()
    {
        return new Dictionary<string, UnifiedItemData>(unifiedInventory);
    }

    // ✅ FIXED: RestoreUnifiedInventory method that GameManager is calling
    public void RestoreUnifiedInventory(Dictionary<string, UnifiedItemData> savedItems)
    {
        unifiedInventory.Clear();
        foreach (var kvp in savedItems)
        {
            unifiedInventory[kvp.Key] = kvp.Value;
        }
        Debug.Log($"🔄 Restored {unifiedInventory.Count} unified items");
    }

    // === UI UPDATE METHODS ===
    private void UpdateCombatItemUI(CombatItemType item)
    {
        switch (item)
        {
            case CombatItemType.Ammo:
                ProfileManager.Instance.UpdateGunHolder(combatItems[item].Count);
                break;
            case CombatItemType.Bandage:
                ProfileManager.Instance.UpdateBandageHolder(combatItems[item].Count);
                break;
            case CombatItemType.Medkit:
                ProfileManager.Instance.UpdateMedkitHolder(combatItems[item].Count);
                break;
        }
    }

    public void UpdateGameplayUI()
    {
        ProfileManager.Instance.UpdateBandageHolder(combatItems[CombatItemType.Bandage].Count);
        ProfileManager.Instance.UpdateMedkitHolder(combatItems[CombatItemType.Medkit].Count);
        ProfileManager.Instance.UpdateBloodAmount(GameManager.Instance.ActiveGameData.mutantBloodAmount);
        if (combatItems[CombatItemType.Knife].Count > 0 || combatItems[CombatItemType.Gun].Count > 0)
            ProfileManager.Instance.UpdateWeaponBoard(ProfileManager.Weapon.None);
    }

    // === SAVE/LOAD METHODS (Updated) ===
    public void RestoreInventory(Dictionary<CombatItemType, int> combatInventory, bool bandageDiscovered, bool medkitDiscovered)
    {
        foreach (var item in combatInventory)
        {
            if (combatItems.ContainsKey(item.Key))
            {
                combatItems[item.Key].Count = item.Value;
            }
        }
        this.isBandageDiscovered = bandageDiscovered;
        this.isMedkitDiscovered = medkitDiscovered;

        Debug.Log("Player inventory restored.");
    }

    public Dictionary<CombatItemType, int> GetCombatInventory()
    {
        Dictionary<CombatItemType, int> inventory = new Dictionary<CombatItemType, int>();
        foreach (var item in combatItems)
        {
            inventory[item.Key] = item.Value.Count;
        }
        return inventory;
    }

    // === DEBUG METHODS ===
    [ContextMenu("Debug All Items")]
    public void DebugAllItems()
    {
        Debug.Log("🔍 COMBAT ITEMS:");
        foreach (var kvp in combatItems)
        {
            Debug.Log($"  - {kvp.Key}: {kvp.Value.Count}x");
        }

        Debug.Log("🔍 UNIFIED ITEMS:");
        foreach (var kvp in unifiedInventory)
        {
            var item = kvp.Value;
            string usages = "";
            if (item.hasCombatUsage) usages += "Combat ";
            if (item.hasKeyUsage) usages += "Key ";
            if (item.hasLoreUsage) usages += "Lore ";
            Debug.Log($"  - {kvp.Key}: {item.count}x ({item.GetDisplayName()}) [{usages.Trim()}]");
        }
    }
}

// Keep existing Item class unchanged
public class Item
{
    public string Name { get; set; }
    public string Description { get; set; }
    public int Count { get; set; }
    public Item(string name, string description, int count = 0)
    {
        Name = name;
        Description = description;
        Count = count;
    }
}