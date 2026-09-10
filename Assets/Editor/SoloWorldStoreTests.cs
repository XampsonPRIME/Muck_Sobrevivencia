using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class SoloWorldStoreTests
{
    string root;
    SoloWorldStore store;
    [SetUp] public void SetUp()
    {
        root = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "SoloWorldTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        store = new SoloWorldStore(root);
    }
    [TearDown] public void TearDown()
    {
        string allowed = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Temp", "SoloWorldTests")) + Path.DirectorySeparatorChar;
        if (root != null && Path.GetFullPath(root).StartsWith(allowed, StringComparison.OrdinalIgnoreCase) && Directory.Exists(root))
            Directory.Delete(root, true);
    }
    SaveGameData Progress(int seed, int day, int xp) => new SaveGameData
    {
        sceneName = "Main", worldSeed = seed, currentDay = day, currentXp = xp,
        health = 75, inventory = new System.Collections.Generic.List<SaveInventoryItemData>
        { new SaveInventoryItemData { itemName = "Madeira", quantity = 19 } }
    };
    string PathFor(string id) => Path.Combine(root, "solo_worlds", id + ".json");

    [Test] public void ThreeWorldsKeepIndependentProgressAfterRestart()
    {
        var a = store.Create("Vale das Cinzas", 111); var b = store.Create("Bosque", 222); var c = store.Create("Ruínas", 333);
        store.SaveProgress(a.id, Progress(111, 8, 900)); store.SaveProgress(b.id, Progress(222, 2, 20));
        var reload = new SoloWorldStore(root);
        Assert.AreEqual(3, reload.ListWorlds().Count); Assert.AreEqual(900, reload.Load(a.id).progress.currentXp);
        Assert.AreEqual(2, reload.Load(b.id).progress.currentDay); Assert.IsNull(reload.Load(c.id).progress);
        Assert.AreEqual(333, reload.Load(c.id).seed); Assert.AreEqual("Ruínas", reload.Load(c.id).name);
    }
    [Test] public void CapacityRejectsFourthAndDeletionFreesExactlyOneSlot()
    {
        var a = store.Create("A", 1); var b = store.Create("B", 2); var c = store.Create("C", 3);
        Assert.Throws<InvalidOperationException>(() => store.Create("D", 4));
        store.Delete(b.id); var d = store.Create("D", 4);
        CollectionAssert.AreEquivalent(new[] { a.id, c.id, d.id }, store.ListWorlds().Select(w => w.id).ToArray());
    }
    [Test] public void RenamePreservesSeedProgressAndLastSaveTime()
    {
        var a = store.Create("A", 42); store.SaveProgress(a.id, Progress(42, 7, 125));
        string savedAt = store.Load(a.id).updatedUtc; store.Rename(a.id, "  Novo nome  ");
        var updated = new SoloWorldStore(root).Load(a.id);
        Assert.AreEqual("Novo nome", updated.name); Assert.AreEqual(savedAt, updated.updatedUtc);
        Assert.AreEqual(42, updated.seed); Assert.AreEqual(125, updated.progress.currentXp);
        Assert.AreEqual(19, updated.progress.inventory[0].quantity);
    }
    [Test] public void NamesRejectBlankLongDuplicateAndControlCharacters()
    {
        store.Create("  Vale  ", 1);
        Assert.Throws<ArgumentException>(() => store.Create("vale", 2));
        Assert.Throws<ArgumentException>(() => store.Create("   ", 2));
        Assert.Throws<ArgumentException>(() => store.Create(new string('a', 33), 2));
        Assert.Throws<ArgumentException>(() => store.Create("A\nB", 2));
        var b = store.Create("B", 2); Assert.Throws<ArgumentException>(() => store.Rename(b.id, "VALE"));
        Assert.AreEqual("B", store.Load(b.id).name);
    }
    [Test] public void DisplayNamesCannotEscapeStorageDirectory()
    {
        var a = store.Create("../Meu <mundo>", 1);
        Assert.IsTrue(File.Exists(PathFor(a.id))); Assert.AreEqual("../Meu <mundo>", store.Load(a.id).name);
        Assert.Throws<ArgumentException>(() => store.Delete("../../savegame"));
        Assert.Throws<ArgumentException>(() => store.Load("../../savegame"));
    }
    [Test] public void LegacyImportIsLosslessIdempotentAndNeverResurrectsDeletedWorld()
    {
        string legacy = Path.Combine(root, "savegame.json");
        string json = JsonUtility.ToJson(Progress(1234, 15, 333)); File.WriteAllText(legacy, json);
        store = new SoloWorldStore(root); var imported = store.ListWorlds().Single();
        Assert.AreEqual("Meu primeiro mundo", imported.name); Assert.AreEqual(333, imported.progress.currentXp);
        Assert.AreEqual(json, File.ReadAllText(legacy)); Assert.AreEqual(1, new SoloWorldStore(root).ListWorlds().Count);
        store.Delete(imported.id); Assert.AreEqual(0, new SoloWorldStore(root).ListWorlds().Count);
        Assert.AreEqual(json, File.ReadAllText(legacy));
    }
    [Test] public void CorruptPrimaryRecoversPreviousAtomicSave()
    {
        var a = store.Create("A", 1); store.SaveProgress(a.id, Progress(1, 2, 20)); store.SaveProgress(a.id, Progress(1, 3, 30));
        File.WriteAllText(PathFor(a.id), "{ broken");
        var recovered = store.Load(a.id); Assert.IsTrue(recovered.recoveredFromBackup);
        Assert.AreEqual(20, recovered.progress.currentXp);
        store.SaveProgress(a.id, Progress(1, 4, 40)); Assert.AreEqual(40, store.Load(a.id).progress.currentXp);
    }
    [Test] public void UnreadableSlotCanBeDeletedAndStillCountsTowardCapacity()
    {
        var a = store.Create("A", 1); store.Create("B", 2); store.Create("C", 3);
        File.WriteAllText(PathFor(a.id), "{}");
        Assert.IsTrue(store.ListWorlds().Single(w => w.id == a.id).unreadable);
        Assert.Throws<InvalidOperationException>(() => store.Create("D", 4));
        store.Delete(a.id); Assert.AreEqual(2, store.ListWorlds().Count);
    }
    [Test] public void FailedWriteLeavesOriginalSaveIntact()
    {
        var a = store.Create("A", 1); store.SaveProgress(a.id, Progress(1, 5, 77));
        string before = File.ReadAllText(PathFor(a.id));
        Directory.CreateDirectory(PathFor(a.id) + ".tmp");
        Assert.Catch(() => store.Rename(a.id, "Changed"));
        Assert.AreEqual(before, File.ReadAllText(PathFor(a.id))); Assert.AreEqual(77, store.Load(a.id).progress.currentXp);
    }
    [Test] public void MultiplayerSaveIsUntouchedBySoloOperations()
    {
        string multiplayer = Path.Combine(root, "multiplayer_session.json"); File.WriteAllText(multiplayer, "multiplayer fixture");
        var a = store.Create("A", 1); store.Rename(a.id, "B"); store.Delete(a.id);
        Assert.AreEqual("multiplayer fixture", File.ReadAllText(multiplayer));
    }

    [MenuItem("Tools/Elarion/Validate Solo World Saves")]
    public static void RunValidation()
    {
        var report = new StringBuilder(); int failed = 0;
        foreach (var method in typeof(SoloWorldStoreTests).GetMethods().Where(m => m.GetCustomAttribute<TestAttribute>() != null))
        {
            var test = new SoloWorldStoreTests();
            try { test.SetUp(); method.Invoke(test, null); report.AppendLine("PASS " + method.Name); }
            catch (Exception e) { failed++; report.AppendLine("FAIL " + method.Name + ": " + (e.InnerException ?? e)); }
            finally { test.TearDown(); }
        }
        report.AppendLine("Failures: " + failed);
        Directory.CreateDirectory("Temp/SoloWorldTests"); File.WriteAllText("Temp/SoloWorldTests/results.txt", report.ToString());
        if (failed == 0) Debug.Log(report.ToString()); else Debug.LogError(report.ToString());
    }
}
