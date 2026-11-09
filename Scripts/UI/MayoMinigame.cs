using System;
using System.Collections.Generic;
using Godot;

namespace TrashPcTycoon.UI;

/// <summary>
/// Интерактивная мини-игра «Намазать майонез» (Обновление 2.0): вид процессора сверху.
/// Зажимаешь и водишь — майонез размазывается по сетке 14x14. Цель — покрыть 70–90% крышки.
/// Заехал за край на материнку — потёк; потёков больше 8% или покрытие 97%+ — при тесте будет
/// короткое замыкание (см. BuildController.SetPasteResult).
/// </summary>
public partial class MayoMinigame : Control
{
    public event Action<float, float>? Finished;
    public event Action? Cancelled;

    private readonly HashSet<Vector2I> _covered = new();
    private readonly HashSet<Vector2I> _leaked = new();
    private bool _painting;
    private Label _statusLabel = null!;
    private ProgressBar _coverageBar = null!;

    private const int GridSize = 14;
    private const float BoardSize = 460f;

    public float CoveragePercent => _covered.Count * 100f / (GridSize * GridSize);
    public float LeakPercent => _leaked.Count * 100f / (GridSize * GridSize);

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        BuildChildren();
    }

    public void Open(float currentPaste01)
    {
        _covered.Clear();
        _leaked.Clear();
        _painting = false;
        // Показываем уже имеющийся слой, чтобы можно было домешать, а не начинать с нуля.
        int prefill = (int)(currentPaste01 * GridSize * GridSize);
        var rng = new Random(7);
        while (_covered.Count < prefill)
        {
            _covered.Add(new Vector2I(rng.Next(GridSize), rng.Next(GridSize)));
        }

        Visible = true;
        UpdateStatus();
        QueueRedraw();
    }

    public void Close()
    {
        Visible = false;
        _painting = false;
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
                _painting = button.Pressed;
                if (button.Pressed)
                {
                    PaintAt(button.Position);
                }

                AcceptEvent();
                break;
            case InputEventMouseMotion motion when _painting:
                PaintAt(motion.Position);
                AcceptEvent();
                break;
            case InputEventScreenTouch touch:
                _painting = touch.Pressed;
                if (touch.Pressed)
                {
                    PaintAt(touch.Position);
                }

                AcceptEvent();
                break;
            case InputEventScreenDrag drag when _painting:
                PaintAt(drag.Position);
                AcceptEvent();
                break;
        }
    }

    public override void _Draw()
    {
        Rect2 board = GetBoardRect();
        // Материнка вокруг процессора: грязно-зелёная плата с дорожками.
        DrawRect(board, new Color(0.20f, 0.30f, 0.18f));
        for (int i = 0; i < 7; i++)
        {
            float y = board.Position.Y + 30f + i * 62f;
            DrawLine(new Vector2(board.Position.X + 14f, y), new Vector2(board.End.X - 14f, y),
                new Color(0.28f, 0.40f, 0.24f), 5f);
        }

        Rect2 cpu = GetCpuRect(board);
        DrawRect(cpu, new Color(0.55f, 0.56f, 0.52f));
        DrawRect(cpu, new Color(0.16f, 0.15f, 0.14f), false, 4f);

        float cellW = cpu.Size.X / GridSize;
        float cellH = cpu.Size.Y / GridSize;
        var mayo = new Color(0.96f, 0.90f, 0.68f);
        foreach (Vector2I cell in _covered)
        {
            DrawRect(new Rect2(cpu.Position + new Vector2(cell.X * cellW, cell.Y * cellH),
                new Vector2(cellW - 1f, cellH - 1f)), mayo);
        }

        float leakCell = board.Size.X / GridSize;
        var leakColor = new Color(0.85f, 0.78f, 0.55f, 0.85f);
        foreach (Vector2I cell in _leaked)
        {
            var rect = new Rect2(board.Position + new Vector2(cell.X * leakCell, cell.Y * leakCell),
                new Vector2(leakCell - 1f, leakCell - 1f));
            if (!cpu.Intersects(rect))
            {
                DrawRect(rect, leakColor);
            }
        }

    }

    private void PaintAt(Vector2 position)
    {
        Rect2 board = GetBoardRect();
        Rect2 cpu = GetCpuRect(board);

        if (cpu.HasPoint(position))
        {
            float cellW = cpu.Size.X / GridSize;
            float cellH = cpu.Size.Y / GridSize;
            var center = new Vector2I(
                (int)((position.X - cpu.Position.X) / cellW),
                (int)((position.Y - cpu.Position.Y) / cellH));
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    var cell = new Vector2I(center.X + dx, center.Y + dy);
                    if (cell.X >= 0 && cell.X < GridSize && cell.Y >= 0 && cell.Y < GridSize
                        && (dx == 0 || dy == 0 || (center.X + center.Y) % 2 == 0))
                    {
                        _covered.Add(cell);
                    }
                }
            }
        }
        else if (board.HasPoint(position))
        {
            float leakCell = board.Size.X / GridSize;
            var cell = new Vector2I(
                (int)((position.X - board.Position.X) / leakCell),
                (int)((position.Y - board.Position.Y) / leakCell));
            _leaked.Add(cell);
        }

        UpdateStatus();
        QueueRedraw();
    }

    private void UpdateStatus()
    {
        float coverage = CoveragePercent;
        float leak = LeakPercent;
        _coverageBar.Value = coverage;
        string verdict = coverage < 70f
            ? "Мало. Процессор будет печкой."
            : coverage <= 90f
                ? "Идеально. Так держать."
                : "Много! Ещё чуть — и потечёт на дорожки.";
        if (leak > 8f)
        {
            verdict = "ПОТЁК НА МАТЕРИНКУ. При тесте будет короткое замыкание!";
        }

        _statusLabel.Text = $"Покрытие: {coverage:0}% (цель 70–90%) • Потёки: {leak:0}% • {verdict}";
    }

    private Rect2 GetBoardRect()
    {
        Vector2 center = Size * 0.5f + new Vector2(0, 20f);
        return new Rect2(center - new Vector2(BoardSize, BoardSize) * 0.5f, new Vector2(BoardSize, BoardSize));
    }

    private static Rect2 GetCpuRect(Rect2 board)
    {
        Vector2 cpuSize = board.Size * 0.62f;
        return new Rect2(board.GetCenter() - cpuSize * 0.5f, cpuSize);
    }

    private void BuildChildren()
    {
        var dim = new ColorRect { Color = new Color(0.05f, 0.04f, 0.035f, 0.82f) };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        dim.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(dim);

        _statusLabel = new Label
        {
            Text = "Води зажатым пальцем по процессору. Цель — 70–90%, за край не выезжай.",
            Position = new Vector2(24, 18)
        };
        _statusLabel.AddThemeFontSizeOverride("font_size", 24);
        _statusLabel.AddThemeColorOverride("font_color", new Color(0.98f, 0.99f, 1.0f));
        AddChild(_statusLabel);

        _coverageBar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            Value = 0,
            ShowPercentage = false,
            Position = new Vector2(24, 56),
            Size = new Vector2(520, 22)
        };
        AddChild(_coverageBar);

        var done = MakeGlassButton("Готово, ставь кулер");
        done.Size = new Vector2(280, 56);
        done.SetAnchorsPreset(LayoutPreset.BottomLeft);
        done.Position = new Vector2(24, -80);
        done.AddThemeFontSizeOverride("font_size", 22);
        done.Pressed += () =>
        {
            float coverage = CoveragePercent;
            float leak = LeakPercent;
            Close();
            Finished?.Invoke(coverage, leak);
        };
        AddChild(done);

        var cancel = MakeGlassButton("Отмена");
        cancel.Size = new Vector2(180, 56);
        cancel.SetAnchorsPreset(LayoutPreset.BottomLeft);
        cancel.Position = new Vector2(324, -80);
        cancel.AddThemeFontSizeOverride("font_size", 22);
        cancel.Pressed += () =>
        {
            Close();
            Cancelled?.Invoke();
        };
        AddChild(cancel);
    }

    /// <summary>Стеклянная кнопка под glassmorphism-стиль (2.0/Модуль 3).</summary>
    private static Button MakeGlassButton(string text)
    {
        var button = new Button { Text = text };
        button.AddThemeColorOverride("font_color", new Color(0.97f, 0.98f, 1.0f));
        button.AddThemeFontSizeOverride("font_size", 22);
        var normal = new StyleBoxFlat
        {
            BgColor = new Color(0.16f, 0.19f, 0.24f, 0.80f),
            BorderColor = new Color(1f, 1f, 1f, 0.35f),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 9, CornerRadiusTopRight = 9, CornerRadiusBottomLeft = 9, CornerRadiusBottomRight = 9,
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 8, ContentMarginBottom = 8
        };
        var hover = (StyleBoxFlat)normal.Duplicate();
        hover.BgColor = new Color(0.28f, 0.33f, 0.40f, 0.88f);
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", hover);
        return button;
    }
}
