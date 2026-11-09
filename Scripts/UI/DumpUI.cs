using System;
using Godot;

namespace TrashPcTycoon.UI;

/// <summary>Минимальный интерфейс локации «Свалка» (2.0): касса, инвентарь, уехать/вернуться.</summary>
public partial class DumpUI : CanvasLayer
{
    public event Action? BackToGaragePressed;
    public event Action? RefreshDumpPressed;
    public event Action? DigPressed;

    private GameState _state = null!;
    private Label _moneyLabel = null!;
    private Label _countLabel = null!;
    private Label _messageLabel = null!;
    private Label _heapsLabel = null!;

    private static readonly Color PanelColor = new(0.13f, 0.11f, 0.085f, 0.96f);
    private static readonly Color TextColor = new(0.92f, 0.87f, 0.74f, 1f);

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

        var hint = MakeLabel("Тапай по кучам — копай • Куча пустеет • Тяни экран — камера", 18);
        hint.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        hint.Position = new Vector2(18, -40);
        AddChild(hint);
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
        _countLabel.Text = $"Деталей: {_state.Inventory.Count}";
    }

    private static StyleBoxFlat MakeStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = PanelColor,
            BorderColor = new Color(0.45f, 0.34f, 0.20f),
            BorderWidthLeft = 3, BorderWidthRight = 3, BorderWidthTop = 3, BorderWidthBottom = 3,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 10, ContentMarginBottom = 10
        };
    }

    private static Button MakeButton(string text)
    {
        var button = new Button { Text = text };
        button.AddThemeColorOverride("font_color", TextColor);
        button.AddThemeFontSizeOverride("font_size", 21);
        var normal = new StyleBoxFlat
        {
            BgColor = new Color(0.38f, 0.27f, 0.16f),
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
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
