using System;
using Godot;

namespace TrashPcTycoon.UI;

/// <summary>Минимальный интерфейс локации «Свалка» (2.0): касса, инвентарь, уехать/вернуться.</summary>
public partial class DumpUI : CanvasLayer
{
    /// <summary>3.0/Модуль 2: клик любой кнопки свалки (для звука).</summary>
    public event Action? ButtonClicked;

    public event Action? BackToGaragePressed;
    public event Action? RefreshDumpPressed;
    public event Action? DigPressed;

    private GameState _state = null!;
    private Label _moneyLabel = null!;
    private Label _countLabel = null!;
    private Label _messageLabel = null!;
    private Label _heapsLabel = null!;

    // 2.0/Модуль 3: glassmorphism, как в гараже.
    private static readonly Color PanelColor = new(0.075f, 0.085f, 0.11f, 0.68f);
    private static readonly Color TextColor = new(0.97f, 0.98f, 1.0f, 1f);

    public override void _Ready()
    {
        var top = new PanelContainer();
        top.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        top.AddThemeStyleboxOverride("panel", MakeStyle());
        AddChild(top);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);
        top.AddChild(row);

        _moneyLabel = MakeLabel("Деньги: 0 ₽", 25);
        row.AddChild(_moneyLabel);
        _countLabel = MakeLabel("Деталей: 0", 25);
        row.AddChild(_countLabel);
        _heapsLabel = MakeLabel("Кучи: ...", 25);
        row.AddChild(_heapsLabel);

        var spacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddChild(spacer);

        Button refresh = MakeButton("Уехать и вернуться (новый мусор)");
        refresh.Pressed += () => RefreshDumpPressed?.Invoke();
        row.AddChild(refresh);

        Button back = MakeButton("Вернуться в гараж");
        back.Pressed += () => BackToGaragePressed?.Invoke();
        row.AddChild(back);

        var messagePanel = new PanelContainer();
        messagePanel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        messagePanel.OffsetTop = -96;
        messagePanel.OffsetBottom = -52;
        messagePanel.OffsetLeft = 16;
        messagePanel.OffsetRight = -340;
        messagePanel.AddThemeStyleboxOverride("panel", MakeStyle());
        AddChild(messagePanel);
        _messageLabel = MakeLabel("Свалка. Пахнет возможностями и немного — бедой.", 21);
        _messageLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        messagePanel.AddChild(_messageLabel);

        var dig = MakeButton("КОПАТЬ");
        dig.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        dig.Position = new Vector2(-300, -108);
        dig.Size = new Vector2(280, 76);
        dig.AddThemeFontSizeOverride("font_size", 26);
        dig.Pressed += () => DigPressed?.Invoke();
        AddChild(dig);

        WireButtonClicks();
        var hint = MakeLabel("Тапай по кучам — копай • Куча пустеет • Тяни экран — камера", 18);
        hint.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        hint.Position = new Vector2(18, -40);
        AddChild(hint);
    }

    private void WireButtonClicks()
    {
        WireClicksRecursive(this);
    }

    private void WireClicksRecursive(Node node)
    {
        if (node is Button button)
        {
            button.Pressed += () => ButtonClicked?.Invoke();
        }

        foreach (Node child in node.GetChildren())
        {
            WireClicksRecursive(child);
        }
    }

    public void Bind(GameState state)
    {
        _state = state;
        _state.MoneyChanged += _ => Refresh();
        _state.InventoryChanged += _ => Refresh();
        Refresh();
    }

    public void SetMessage(string text) => _messageLabel.Text = text;

    public void SetHeapsStatus(string text) => _heapsLabel.Text = text;

    private void Refresh()
    {
        _moneyLabel.Text = $"Деньги: {_state.Money} ₽";
        _countLabel.Text = $"Деталей: {_state.Inventory.Count}/{_state.InventoryCapacity}";
    }

    private static StyleBoxFlat MakeStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = PanelColor,
            BorderColor = new Color(1f, 1f, 1f, 0.30f),
            BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12,
            ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 12, ContentMarginBottom = 12,
            ShadowColor = new Color(0f, 0f, 0f, 0.35f),
            ShadowSize = 6,
            ShadowOffset = new Vector2(0, 3)
        };
    }

    private static Button MakeButton(string text)
    {
        var button = new Button { Text = text };
        button.AddThemeColorOverride("font_color", TextColor);
        button.AddThemeFontSizeOverride("font_size", 21);
        var normal = new StyleBoxFlat
        {
            BgColor = new Color(0.16f, 0.19f, 0.24f, 0.78f),
            BorderColor = new Color(1f, 1f, 1f, 0.35f),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 9, CornerRadiusTopRight = 9, CornerRadiusBottomLeft = 9, CornerRadiusBottomRight = 9,
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 8, ContentMarginBottom = 8
        };
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", normal);
        button.AddThemeStyleboxOverride("pressed", normal);
        return button;
    }

    private static Label MakeLabel(string text, int fontSize)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", TextColor);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        return label;
    }
}
