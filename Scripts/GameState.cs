using System.Collections.Generic;
using Godot;

namespace TrashPcTycoon;

/// <summary>
/// Автозагружаемый синглтон состояния игры (см. [autoload] в project.godot):
/// деньги и инвентарь. Сохранение на диск появится в Модуле 3, здесь только память и сигналы для UI.
/// </summary>
public partial class GameState : Node
{
    [Signal] public delegate void MoneyChangedEventHandler(int money);
    [Signal] public delegate void InventoryChangedEventHandler(int itemCount);

    public const int StartMoneyRub = 500;

    public int Money { get; private set; } = StartMoneyRub;
    public List<PcPart> Inventory { get; } = new();

    public bool CanAfford(int amountRub) => Money >= amountRub;

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
}
