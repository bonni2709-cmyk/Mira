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

    public const int StartMoneyRub = 500;

    public int Money { get; private set; } = StartMoneyRub;
    public List<PcPart> Inventory { get; } = new();
    public HashSet<string> OwnedTools { get; } = new();
    public int ComputersSold { get; private set; }
    public int TotalEarnedRub { get; private set; }

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
        int computersSold, int totalEarnedRub)
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
        EmitSignal(SignalName.MoneyChanged, Money);
        EmitSignal(SignalName.InventoryChanged, Inventory.Count);
    }
}
