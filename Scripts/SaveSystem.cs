using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;

namespace TrashPcTycoon;

/// <summary>
/// Сохранение прогресса в user://trashpc_save.json: деньги, инвентарь,
/// установленные на столе детали с их состоянием (пыль/майонез/молоток),
/// купленные инструменты и статистика продаж. Формат — обычный JSON (System.Text.Json).
/// </summary>
public static class SaveSystem
{
    public sealed class PartDto
    {
        public string Name { get; set; } = "";
        public int Type { get; set; }
        public int Quality { get; set; }
        public int ValueRub { get; set; }
        public float TintR { get; set; }
        public float TintG { get; set; }
        public float TintB { get; set; }
        public string Source { get; set; } = "";
        public string Description { get; set; } = "";
        public bool Iranian { get; set; }
    }

    public sealed class InstalledDto
    {
        public PartDto Part { get; set; } = new();
        public bool NeedsHammer { get; set; }
        public bool Hammered { get; set; }
        public int HammerHits { get; set; }
        public float Dust01 { get; set; }
        public float Paste01 { get; set; }
    }

    public sealed class SaveData
    {
        public int Version { get; set; } = 1;
        public int Money { get; set; }
        public List<PartDto> Inventory { get; set; } = new();
        public List<InstalledDto> Installed { get; set; } = new();
        public List<string> Tools { get; set; } = new();
        public int ComputersSold { get; set; }
        public int TotalEarnedRub { get; set; }
        public int Reputation { get; set; } = GameState.StartReputation;
        public int GarageLevel { get; set; } = 1;
        /// <summary>3.0/Модуль 3: ступень разгона рычага на столе (старые сейвы = 0).</summary>
        public int Overclock { get; set; }
    }

    private static string SavePath => ProjectSettings.GlobalizePath("user://trashpc_save.json");

    public static bool HasSave => File.Exists(SavePath);

    public static void Save(GameState state, BuildController? build)
    {
        var data = new SaveData
        {
            Money = state.Money,
            ComputersSold = state.ComputersSold,
            TotalEarnedRub = state.TotalEarnedRub,
            Reputation = state.Reputation,
            GarageLevel = state.GarageLevel,
            Tools = new List<string>(state.OwnedTools),
            Overclock = build?.OverclockLevel ?? 0
        };

        foreach (PcPart part in state.Inventory)
        {
            data.Inventory.Add(ToDto(part));
        }

        if (build == null)
        {
            // Сцена свалки: стола сборки здесь нет — сохраняем установленный ПК из прошлого сейва как есть.
            SaveData? previous = Load();
            if (previous != null)
            {
                data.Installed = previous.Installed;
                data.Overclock = previous.Overclock;
            }
        }

        foreach (KeyValuePair<PartType, BuildController.InstalledPart> pair in build?.Installed
                     ?? new Dictionary<PartType, BuildController.InstalledPart>())
        {
            BuildController.InstalledPart entry = pair.Value;
            data.Installed.Add(new InstalledDto
            {
                Part = ToDto(entry.Part),
                NeedsHammer = entry.NeedsHammer,
                Hammered = entry.Hammered,
                HammerHits = entry.HammerHits,
                Dust01 = entry.Dust01,
                Paste01 = entry.Paste01
            });
        }

        string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SavePath, json);
        GD.Print($"Trash-PC Tycoon: прогресс сохранён ({SavePath}).");
    }

    public static SaveData? Load()
    {
        if (!HasSave)
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            return JsonSerializer.Deserialize<SaveData>(json);
        }
        catch (System.Exception ex)
        {
            GD.PushWarning($"SaveSystem: сейв не читается ({ex.Message}), начинаем с чистого гаража.");
            return null;
        }
    }

    public static PartDto ToDto(PcPart part)
    {
        return new PartDto
        {
            Name = part.Name,
            Type = (int)part.Type,
            Quality = (int)part.Quality,
            ValueRub = part.ValueRub,
            TintR = part.Tint.R,
            TintG = part.Tint.G,
            TintB = part.Tint.B,
            Source = part.Source,
            Description = part.Description,
            Iranian = part.Iranian
        };
    }

    public static PcPart FromDto(PartDto dto)
    {
        return new PcPart(dto.Name, (PartType)dto.Type, (PartQuality)dto.Quality, dto.ValueRub,
            new Color(dto.TintR, dto.TintG, dto.TintB), dto.Source, dto.Description, dto.Iranian);
    }
}
