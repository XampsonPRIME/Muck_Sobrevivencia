using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class SoloWorldSave
{
    public int version = 1;
    public string id;
    public string name;
    public string createdUtc;
    public string updatedUtc;
    public int seed;
    // Unity's inline serializer reconstructs null classes; persist readiness explicitly.
    public bool hasProgress;
    public SaveGameData progress;
    [NonSerialized] public bool unreadable;
    [NonSerialized] public bool recoveredFromBackup;
}

/// <summary>Independent, atomic solo saves. Display names are never used as paths.</summary>
public sealed class SoloWorldStore
{
    public const int Capacity = 3;
    public const int MaxNameLength = 32;
    const string LegacyId = "00000000000000000000000000000001";
    readonly string directory;
    readonly string legacyPath;

    public SoloWorldStore(string saveDirectory)
    {
        directory = Path.Combine(saveDirectory, "solo_worlds");
        legacyPath = Path.Combine(saveDirectory, "savegame.json");
        Directory.CreateDirectory(directory);
        MigrateLegacy();
    }

    public List<SoloWorldSave> ListWorlds()
    {
        var worlds = new List<SoloWorldSave>();
        foreach (string path in Directory.GetFiles(directory, "*.json"))
        {
            string id = Path.GetFileNameWithoutExtension(path);
            if (!Guid.TryParseExact(id, "N", out _)) continue;
            try { worlds.Add(Load(id)); }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException)
            {
                // A damaged file still occupies a slot and can be explicitly deleted in the UI.
                worlds.Add(new SoloWorldSave { id = id, name = "Mundo indisponível", unreadable = true });
            }
        }
        return worlds.OrderByDescending(w => w.updatedUtc, StringComparer.Ordinal).ThenBy(w => w.id).ToList();
    }

    public SoloWorldSave Create(string name, int seed)
    {
        var worlds = ListWorlds();
        if (worlds.Count >= Capacity)
            throw new InvalidOperationException("Você já tem 3 mundos. Exclua um deles para iniciar uma nova jornada.");
        string cleanName = ValidateName(name, worlds, null);
        string now = DateTime.UtcNow.ToString("o");
        var world = new SoloWorldSave { id = Guid.NewGuid().ToString("N"), name = cleanName,
            createdUtc = now, updatedUtc = now, seed = seed == 0 ? 1 : seed };
        Write(world);
        return world;
    }

    public SoloWorldSave Load(string id)
    {
        string path = WorldPath(id);
        try { return Read(path, id); }
        catch (Exception e) when (e is IOException || e is ArgumentException)
        {
            if (!File.Exists(path + ".bak")) throw;
            var backup = Read(path + ".bak", id);
            backup.recoveredFromBackup = true;
            return backup;
        }
    }

    public void SaveProgress(string id, SaveGameData progress)
    {
        if (progress == null) throw new ArgumentNullException(nameof(progress));
        var world = Load(id);
        world.progress = progress;
        world.hasProgress = true;
        world.seed = progress.worldSeed;
        world.updatedUtc = DateTime.UtcNow.ToString("o");
        Write(world);
    }

    public void Rename(string id, string name)
    {
        var world = Load(id);
        world.name = ValidateName(name, ListWorlds(), id);
        Write(world); // Preserve the date of the last gameplay save.
    }

    public void Delete(string id)
    {
        string path = WorldPath(id);
        // Remove recovery copies first, so a deleted world cannot reappear.
        if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
        if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
        File.Delete(path);
    }

    public static string ValidateName(string value, List<SoloWorldSave> worlds, string exceptId)
    {
        string name = (value ?? "").Trim().Normalize(NormalizationForm.FormC);
        if (name.Length == 0) throw new ArgumentException("Dê um nome ao seu mundo para continuar.");
        if (name.Length > MaxNameLength) throw new ArgumentException("Use um nome com até 32 caracteres.");
        if (name.Any(char.IsControl)) throw new ArgumentException("Use apenas caracteres visíveis no nome.");
        if (worlds.Exists(w => w.id != exceptId && string.Equals(w.name, name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Você já tem um mundo com esse nome. Escolha outro.");
        return name;
    }

    string WorldPath(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("Identificador de mundo inválido.");
        return Path.Combine(directory, id + ".json");
    }

    static SoloWorldSave Read(string path, string id)
    {
        var world = JsonUtility.FromJson<SoloWorldSave>(File.ReadAllText(path));
        if (world == null || world.version != 1 || world.id != id || string.IsNullOrWhiteSpace(world.name))
            throw new IOException("Não foi possível ler este mundo. O arquivo salvo está incompleto ou incompatível.");
        if (!world.hasProgress) world.progress = null;
        else if (world.progress == null || string.IsNullOrWhiteSpace(world.progress.sceneName))
            throw new IOException("O progresso deste mundo está incompleto.");
        return world;
    }

    void Write(SoloWorldSave world)
    {
        string path = WorldPath(world.id);
        string temp = path + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonUtility.ToJson(world, true), Encoding.UTF8);
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
            else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    void MigrateLegacy()
    {
        string marker = Path.Combine(directory, "legacy-imported.marker");
        if (File.Exists(marker) || !File.Exists(legacyPath)) return;
        // Deterministic destination makes an interrupted import safe to retry.
        if (!File.Exists(WorldPath(LegacyId)))
        {
            if (ListWorlds().Count >= Capacity)
                throw new InvalidOperationException("Há um save antigo para importar. Libere um espaço antes de continuar.");
            var progress = JsonUtility.FromJson<SaveGameData>(File.ReadAllText(legacyPath));
            if (progress == null || string.IsNullOrWhiteSpace(progress.sceneName))
                throw new IOException("O save antigo não pôde ser importado. O arquivo original foi preservado.");
            Write(new SoloWorldSave { id = LegacyId, name = "Meu primeiro mundo", seed = progress.worldSeed,
                createdUtc = File.GetCreationTimeUtc(legacyPath).ToString("o"),
                updatedUtc = File.GetLastWriteTimeUtc(legacyPath).ToString("o"), hasProgress = true, progress = progress });
        }
        File.WriteAllText(marker, "Imported; original savegame.json preserved.");
    }
}
