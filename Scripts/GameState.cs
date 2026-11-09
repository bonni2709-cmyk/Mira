using System.Collections.Generic;
using Godot;

namespace TrashPcTycoon;

/// <summary>
/// Автозагружаемый синглтон состояния игры (см. [autoload] в project.godot):
/// деньги, инвентарь, купленные инструменты и статистика. На диск это всё
/// пишет SaveSystem (Модуль 3), здесь — только память и сигналы для UI.
/// </summary>
public partial class GameState : Node
{
    [Signal] public delegate void MoneyChangedEventHandler(int money);
    [Signal] public delegate void InventoryChangedEventHandler(int itemCount);
    [Signal] public delegate void ReputationChangedEventHandler(int reputation);
    [Signal] public delegate void GarageLevelChangedEventHandler(int level);

    public const int StartMoneyRub = 500;

    public int Money { get; private set; } = StartMoneyRub;
    public List<PcPart> Inventory { get; } = new();

    // 2.0/Модуль 2: слоты инвентаря. Ящик «Кладовка» добавляет ещё пять.
    public const int BaseInventorySlots = 15;
    public const int CrateBonusSlots = 5;
    public int InventoryCapacity => BaseInventorySlots + (OwnsTool(ToolsCatalog.Crate) ? CrateBonusSlots : 0);
    public bool InventoryFull => Inventory.Count >= InventoryCapacity;
    public HashSet<string> OwnedTools { get; } = new();
    public int ComputersSold { get; private set; }
    public int TotalEarnedRub { get; private set; }

    // Модуль 4: репутация (0..100) и уровень гаража (1..3).
    public const int StartReputation = 40;
    public int Reputation { get; private set; } = StartReputation;
    public int GarageLevel { get; private set; } = 1;

    /// <summary>Звёздный рейтинг для HUD: 0.0 .. 5.0.</summary>
    public float StarRating => Reputation / 20.0f;

    public void AddReputation(int delta)
    {
        int next = Mathf.Clamp(Reputation + delta, 0, 100);
        if (next == Reputation)
        {
            return;
        }

        Reputation = next;
        EmitSignal(SignalName.ReputationChanged, Reputation);
    }

    public bool BuyGarageUpgrade(int targetLevel, int priceRub)
    {
        if (GarageLevel >= targetLevel || !SpendMoney(priceRub))
        {
            return false;
        }

        GarageLevel = targetLevel;
        EmitSignal(SignalName.GarageLevelChanged, GarageLevel);
        EmitSignal(SignalName.MoneyChanged, Money);
        return true;
    }

    public bool CanAfford(int amountRub) => Money >= amountRub;
    public bool OwnsTool(string toolId) => OwnedTools.Contains(toolId);

    public bool SpendMoney(int amountRub)
    {
        if (amountRub <= 0 || Money < amountRub)
        {
            return false;
        }

        Money -= amountRub;
        EmitSignal(SignalName.MoneyChanged, Money);
        return true;
    }

    public void AddMoney(int amountRub)
    {
        if (amountRub == 0)
        {
            return;
        }

        Money = Mathf.Max(0, Money + amountRub);
        if (amountRub > 0)
        {
            TotalEarnedRub += amountRub;
        }

        EmitSignal(SignalName.MoneyChanged, Money);
    }

    public void AddPart(PcPart part)
    {
        Inventory.Add(part);
        EmitSignal(SignalName.InventoryChanged, Inventory.Count);
        EmitSignal(SignalName.MoneyChanged, Money);
    }

    public bool RemovePart(PcPart part)
    {
        if (!Inventory.Remove(part))
        {
            return false;
        }

        EmitSignal(SignalName.InventoryChanged, Inventory.Count);
        return true;
    }

    public bool BuyTool(string toolId, int priceRub)
    {
        if (OwnedTools.Contains(toolId) || !SpendMoney(priceRub))
        {
            return false;
        }

        OwnedTools.Add(toolId);
        EmitSignal(SignalName.MoneyChanged, Money);
        return true;
    }

    public void RegisterSale()
    {
        ComputersSold++;
    }

    /// <summary>Восстановление из сейва SaveSystem. Сигналы дёргаем один раз в конце.</summary>
    public void Restore(int money, IEnumerable<PcPart> inventory, IEnumerable<string> tools,
        int computersSold, int totalEarnedRub, int reputation, int garageLevel)
    {
        Money = Mathf.Max(0, money);
        Inventory.Clear();
        Inventory.AddRange(inventory);
        OwnedTools.Clear();
        foreach (string tool in tools)
        {
            OwnedTools.Add(tool);
        }

        ComputersSold = computersSold;
        TotalEarnedRub = totalEarnedRub;
        Reputation = Mathf.Clamp(reputation, 0, 100);
        GarageLevel = Mathf.Clamp(garageLevel, 1, 3);
        EmitSignal(SignalName.MoneyChanged, Money);
        EmitSignal(SignalName.InventoryChanged, Inventory.Count);
        EmitSignal(SignalName.ReputationChanged, Reputation);
        EmitSignal(SignalName.GarageLevelChanged, GarageLevel);
    }
}
