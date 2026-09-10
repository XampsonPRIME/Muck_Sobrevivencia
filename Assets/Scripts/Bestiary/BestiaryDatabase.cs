using System.Collections.Generic;
using UnityEngine;

public static class BestiaryDatabase
{
    const string CowIllustrationPath = "Bestiary/Vaca";
    const string WildChickenIllustrationPath = "Bestiary/GalinhaSelvagem";
    const string WildBoarIllustrationPath = "Bestiary/JavaliSelvagem";
    const string EarthGolemIllustrationPath = "Bestiary/GolemDeTerra";
    const string KaelTorGuardianIllustrationPath = "Bestiary/KaelTorGuardian";

    public const string CowId = "cow";
    public const string WildChickenId = "wild_chicken";
    public const string WildBoarId = "wild_boar";
    public const string EarthGolemId = "earth_golem";
    public const string MiniKrugId = "mini_krug";
    public const string BossEnemyId = "boss_enemy";
    public const string KaelTorGuardianId = "kaeltor_guardian";

    static readonly List<BestiaryCreatureData> creatures = new List<BestiaryCreatureData>();
    static readonly Dictionary<string, BestiaryCreatureData> byId = new Dictionary<string, BestiaryCreatureData>();
    static bool initialized;

    public static IReadOnlyList<BestiaryCreatureData> Creatures
    {
        get
        {
            EnsureInitialized();
            return creatures;
        }
    }

    public static void EnsureInitialized()
    {
        if (initialized)
            return;

        initialized = true;
        creatures.Clear();
        byId.Clear();

        BestiaryCreatureData[] resourceData = Resources.LoadAll<BestiaryCreatureData>("Bestiary");
        if (resourceData != null)
        {
            for (int i = 0; i < resourceData.Length; i++)
                Register(resourceData[i]);
        }

        RegisterDefaultIfMissing(BuildCow());
        RegisterDefaultIfMissing(BuildWildChicken());
        RegisterDefaultIfMissing(BuildWildBoar());
        RegisterDefaultIfMissing(BuildEarthGolem());
        RegisterDefaultIfMissing(BuildMiniKrug());
        RegisterDefaultIfMissing(BuildBossEnemy());
        RegisterDefaultIfMissing(BuildKaelTorGuardian());
    }

    public static BestiaryCreatureData Get(string creatureId)
    {
        EnsureInitialized();

        if (string.IsNullOrWhiteSpace(creatureId))
            return null;

        return byId.TryGetValue(creatureId, out BestiaryCreatureData data) ? data : null;
    }

    public static bool TryResolveCreature(Component component, out string creatureId)
    {
        creatureId = null;

        if (component == null)
            return false;

        BestiaryCreatureIdentity identity = component.GetComponentInParent<BestiaryCreatureIdentity>();
        if (identity != null)
        {
            string identityId = identity.ResolveCreatureId();
            if (!string.IsNullOrWhiteSpace(identityId))
            {
                creatureId = identityId;
                return Get(creatureId) != null;
            }
        }

        if (component.GetComponentInParent<WildChicken>() != null)
        {
            creatureId = WildChickenId;
            return true;
        }

        if (component.GetComponentInParent<WildBoar>() != null)
        {
            creatureId = WildBoarId;
            return true;
        }

        if (component.GetComponentInParent<EarthGolem>() != null)
        {
            creatureId = EarthGolemId;
            return true;
        }

        if (component.GetComponentInParent<Cow>() != null)
        {
            creatureId = CowId;
            return true;
        }

        if (component.GetComponentInParent<MiniKrug>() != null)
        {
            creatureId = MiniKrugId;
            return true;
        }

        if (component.GetComponentInParent<BossEnemy>() != null)
        {
            creatureId = BossEnemyId;
            return true;
        }

        if (component.GetComponentInParent<KaelTorGuardian>() != null)
        {
            creatureId = KaelTorGuardianId;
            return true;
        }

        return false;
    }

    public static Sprite ResolveItemSprite(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            return null;

        if (string.Equals(itemName, "Gold", System.StringComparison.OrdinalIgnoreCase))
            return GoldItemRegistry.GetGoldSprite();

        if (string.Equals(itemName, FeatherItemRegistry.ItemName, System.StringComparison.OrdinalIgnoreCase))
            return FeatherItemRegistry.GetSprite();

        if (string.Equals(itemName, RawChickenMeatItemRegistry.ItemName, System.StringComparison.OrdinalIgnoreCase))
            return RawChickenMeatItemRegistry.GetSprite();

        if (string.Equals(itemName, CowMeatItemRegistry.ItemName, System.StringComparison.OrdinalIgnoreCase))
            return CowMeatItemRegistry.GetSprite();

        if (string.Equals(itemName, CowLeatherItemRegistry.ItemName, System.StringComparison.OrdinalIgnoreCase))
            return CowLeatherItemRegistry.GetSprite();

        if (string.Equals(itemName, ThickLeatherItemRegistry.ItemName, System.StringComparison.OrdinalIgnoreCase))
            return ThickLeatherItemRegistry.GetSprite();

        if (string.Equals(itemName, SharpTuskItemRegistry.ItemName, System.StringComparison.OrdinalIgnoreCase))
            return SharpTuskItemRegistry.GetSprite();

        if (string.Equals(itemName, BoarMeatItemRegistry.ItemName, System.StringComparison.OrdinalIgnoreCase))
            return BoarMeatItemRegistry.GetSprite();

        if (string.Equals(itemName, BoarTrophyItemRegistry.ItemName, System.StringComparison.OrdinalIgnoreCase))
            return BoarTrophyItemRegistry.GetSprite();

        if (string.Equals(itemName, StoneFragmentItemRegistry.ItemName, System.StringComparison.OrdinalIgnoreCase))
            return StoneFragmentItemRegistry.GetSprite();

        if (string.Equals(itemName, ResilientMossItemRegistry.ItemName, System.StringComparison.OrdinalIgnoreCase))
            return ResilientMossItemRegistry.GetSprite();

        if (string.Equals(itemName, IronItemRegistry.ItemName, System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(itemName, "Minerio de Ferro", System.StringComparison.OrdinalIgnoreCase))
            return IronItemRegistry.GetSprite();

        if (string.Equals(itemName, EarthCoreItemRegistry.ItemName, System.StringComparison.OrdinalIgnoreCase))
            return EarthCoreItemRegistry.GetSprite();

        if (string.Equals(itemName, AncestralCoreItemRegistry.ItemName, System.StringComparison.OrdinalIgnoreCase))
            return AncestralCoreItemRegistry.GetSprite();

        if (string.Equals(itemName, FirstArtifactFragmentItemRegistry.ItemName, System.StringComparison.OrdinalIgnoreCase))
            return FirstArtifactFragmentItemRegistry.GetSprite();

        if (string.Equals(itemName, KaelTorTrophyItemRegistry.ItemName, System.StringComparison.OrdinalIgnoreCase))
            return KaelTorTrophyItemRegistry.GetSprite();

        if (string.Equals(itemName, FirstArtifactItemRegistry.ItemName, System.StringComparison.OrdinalIgnoreCase))
            return FirstArtifactItemRegistry.GetSprite();

        if (string.Equals(itemName, MushroomTyrantDungeonKeyRegistry.ItemName, System.StringComparison.OrdinalIgnoreCase))
            return MushroomTyrantDungeonKeyRegistry.GetOrCreate()?.icon;

        if (string.Equals(itemName, "Magia Ancestral", System.StringComparison.OrdinalIgnoreCase))
            return MagicSpellItemRegistry.GetOrCreate()?.icon;

        return null;
    }

    static void RegisterDefaultIfMissing(BestiaryCreatureData data)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.creatureId) || byId.ContainsKey(data.creatureId))
            return;

        Register(data);
    }

    static void Register(BestiaryCreatureData data)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.creatureId))
            return;

        if (byId.ContainsKey(data.creatureId))
            return;

        byId[data.creatureId] = data;
        creatures.Add(data);
    }

    static BestiaryCreatureData BuildCow()
    {
        BestiaryCreatureData data = CreateRuntimeData(CowId, "Vaca");
        data.description = "Animal pacifico encontrado em campos abertos. E uma fonte confiavel de carne para sobrevivencia.";
        data.creatureType = BestiaryCreatureType.Animal;
        data.element = BestiaryElement.Nature;
        data.hostility = BestiaryHostility.Passive;
        data.rarity = BestiaryRarity.Common;
        data.biomes.Add(BestiaryBiome.Plains);
        data.biomes.Add(BestiaryBiome.Forest);
        data.maxHealth = 4;
        data.damage = 0f;
        data.speed = 1.8f;
        data.behavior = "Anda aleatoriamente e nao ataca o jogador.";
        data.illustration = Resources.Load<Sprite>(CowIllustrationPath)
            ?? BestiaryIconFactory.CreateCreatureSprite("VacaBestiarySprite", new Color(0.92f, 0.86f, 0.72f), new Color(0.17f, 0.12f, 0.1f), BestiaryCreatureType.Animal);
        data.drops.Add(CreateDrop(CowMeatItemRegistry.ItemName, "1-3", BestiaryRarity.Common, 1f));
        data.drops.Add(CreateDrop(CowLeatherItemRegistry.ItemName, "1-2", BestiaryRarity.Common, 1f));
        return data;
    }

    static BestiaryCreatureData BuildWildChicken()
    {
        BestiaryCreatureData data = CreateRuntimeData(WildChickenId, "Galinha Selvagem");
        data.description = "Ave pequena e arisca. Foge rapidamente quando percebe o jogador por perto.";
        data.creatureType = BestiaryCreatureType.Animal;
        data.element = BestiaryElement.Nature;
        data.hostility = BestiaryHostility.Passive;
        data.rarity = BestiaryRarity.Common;
        data.biomes.Add(BestiaryBiome.Plains);
        data.biomes.Add(BestiaryBiome.Forest);
        data.maxHealth = 20;
        data.damage = 0f;
        data.speed = 4f;
        data.behavior = "Patrulha curta, detecta ameacas e foge por alguns segundos.";
        data.illustration = Resources.Load<Sprite>(WildChickenIllustrationPath)
            ?? BestiaryIconFactory.CreateCreatureSprite("GalinhaBestiarySprite", new Color(0.95f, 0.88f, 0.62f), new Color(0.84f, 0.18f, 0.12f), BestiaryCreatureType.Animal);
        data.drops.Add(CreateDrop(FeatherItemRegistry.ItemName, "1-3", BestiaryRarity.Common, 1f));
        data.drops.Add(CreateDrop(RawChickenMeatItemRegistry.ItemName, "1-2", BestiaryRarity.Common, 1f));
        return data;
    }

    static BestiaryCreatureData BuildWildBoar()
    {
        BestiaryCreatureData data = CreateRuntimeData(WildBoarId, "Javali Selvagem");
        data.description = "Animal hostil e veloz encontrado em florestas e campos abertos. Usa investidas para causar dano e empurrar o jogador.";
        data.creatureType = BestiaryCreatureType.Animal;
        data.element = BestiaryElement.Earth;
        data.hostility = BestiaryHostility.Hostile;
        data.rarity = BestiaryRarity.Uncommon;
        data.biomes.Add(BestiaryBiome.Forest);
        data.biomes.Add(BestiaryBiome.Plains);
        data.maxHealth = 120;
        data.damage = 38f;
        data.speed = 5.8f;
        data.behavior = "Patrulha, detecta o jogador em media distancia, persegue e executa uma investida furiosa com knockback.";
        data.illustration = Resources.Load<Sprite>(WildBoarIllustrationPath)
            ?? BestiaryIconFactory.CreateCreatureSprite("JavaliBestiarySprite", new Color(0.36f, 0.2f, 0.1f), new Color(0.9f, 0.78f, 0.52f), BestiaryCreatureType.Animal);
        data.drops.Add(CreateDrop(ThickLeatherItemRegistry.ItemName, "1-3", BestiaryRarity.Common, 0.6f));
        data.drops.Add(CreateDrop(SharpTuskItemRegistry.ItemName, "1", BestiaryRarity.Uncommon, 0.25f));
        data.drops.Add(CreateDrop(BoarMeatItemRegistry.ItemName, "1-2", BestiaryRarity.Uncommon, 0.1f));
        data.drops.Add(CreateDrop(BoarTrophyItemRegistry.ItemName, "1", BestiaryRarity.Rare, 0.05f));
        return data;
    }

    static BestiaryCreatureData BuildEarthGolem()
    {
        BestiaryCreatureData data = CreateRuntimeData(EarthGolemId, "Golem de Terra");
        data.description = "Elemental massivo formado por rochas antigas, musgo e energia de terra. E resistente, pesado e perigoso quando provocado.";
        data.creatureType = BestiaryCreatureType.Elemental;
        data.element = BestiaryElement.Earth;
        data.hostility = BestiaryHostility.Defensive;
        data.rarity = BestiaryRarity.Rare;
        data.biomes.Add(BestiaryBiome.Desert);
        data.biomes.Add(BestiaryBiome.Mountain);
        data.biomes.Add(BestiaryBiome.Cave);
        data.maxHealth = 1200;
        data.damage = 85f;
        data.speed = 2.15f;
        data.behavior = "Permanece neutro ate ser provocado, avanca com passos pesados e usa socos rapidos e golpes no chao em area.";
        data.illustration = Resources.Load<Sprite>(EarthGolemIllustrationPath)
            ?? BestiaryIconFactory.CreateCreatureSprite("GolemTerraBestiarySprite", new Color(0.44f, 0.35f, 0.22f), new Color(0.72f, 0.58f, 0.18f), BestiaryCreatureType.Elemental);
        data.drops.Add(CreateDrop(StoneFragmentItemRegistry.ItemName, "3-8", BestiaryRarity.Common, 0.7f));
        data.drops.Add(CreateDrop(ResilientMossItemRegistry.ItemName, "1-3", BestiaryRarity.Common, 0.4f));
        data.drops.Add(CreateDrop(IronItemRegistry.ItemName, "1-2", BestiaryRarity.Uncommon, 0.1f));
        data.drops.Add(CreateDrop(EarthCoreItemRegistry.ItemName, "1", BestiaryRarity.Rare, 0.2f));
        return data;
    }

    static BestiaryCreatureData BuildMiniKrug()
    {
        BestiaryCreatureData data = CreateRuntimeData(MiniKrugId, "MiniKrug");
        data.description = "Criatura hostil que persegue aventureiros, especialmente quando a noite toma conta da floresta.";
        data.creatureType = BestiaryCreatureType.Monster;
        data.element = BestiaryElement.Shadow;
        data.hostility = BestiaryHostility.Hostile;
        data.rarity = BestiaryRarity.Uncommon;
        data.biomes.Add(BestiaryBiome.Forest);
        data.biomes.Add(BestiaryBiome.Cave);
        data.maxHealth = 2;
        data.damage = 15f;
        data.speed = 4f;
        data.behavior = "Persegue o jogador, causa dano por contato e pode guardar chaves raras.";
        data.illustration = BestiaryIconFactory.CreateCreatureSprite("MiniKrugBestiarySprite", new Color(0.28f, 0.62f, 0.24f), new Color(0.12f, 0.16f, 0.12f), BestiaryCreatureType.Monster);
        data.drops.Add(CreateDrop("Gold", "5-10", BestiaryRarity.Common, 1f));
        data.drops.Add(CreateDrop(MushroomTyrantDungeonKeyRegistry.ItemName, "1", BestiaryRarity.Rare, 0.28f));
        return data;
    }

    static BestiaryCreatureData BuildBossEnemy()
    {
        BestiaryCreatureData data = CreateRuntimeData(BossEnemyId, "Guardiao Ancestral");
        data.description = "Chefe territorial de grande porte. Guarda recompensas importantes e exige preparo antes do combate.";
        data.creatureType = BestiaryCreatureType.Boss;
        data.element = BestiaryElement.Earth;
        data.hostility = BestiaryHostility.Hostile;
        data.rarity = BestiaryRarity.Legendary;
        data.biomes.Add(BestiaryBiome.Cave);
        data.biomes.Add(BestiaryBiome.Forest);
        data.maxHealth = 150;
        data.damage = 86f;
        data.speed = 2.6f;
        data.behavior = "Patrulha seu territorio, persegue alvos proximos e pode usar ataques em area.";
        data.illustration = BestiaryIconFactory.CreateCreatureSprite("BossBestiarySprite", new Color(0.48f, 0.12f, 0.1f), new Color(0.86f, 0.68f, 0.18f), BestiaryCreatureType.Boss);
        data.drops.Add(CreateDrop("Gold", "25-45", BestiaryRarity.Uncommon, 1f));
        data.drops.Add(CreateDrop("Magia Ancestral", "1", BestiaryRarity.Legendary, 1f));
        return data;
    }

    static BestiaryCreatureData BuildKaelTorGuardian()
    {
        BestiaryCreatureData data = CreateRuntimeData(KaelTorGuardianId, "Kael'Tor, o Vigia dos Artefatos");
        data.description = "Primeiro dos Sete Guardioes. Um protetor antigo corrompido pela quebra do equilibrio dos artefatos, atacando qualquer um que se aproxime das ruinas.";
        data.creatureType = BestiaryCreatureType.Boss;
        data.element = BestiaryElement.Earth;
        data.hostility = BestiaryHostility.Defensive;
        data.rarity = BestiaryRarity.Legendary;
        data.biomes.Add(BestiaryBiome.Mountain);
        data.biomes.Add(BestiaryBiome.Cave);
        data.biomes.Add(BestiaryBiome.Forest);
        data.maxHealth = 4200;
        data.damage = 165f;
        data.speed = 3.35f;
        data.behavior = "Usa socos pesados, pisao, arremesso de rochas, ondas sismicas, invocacoes, chuva de pedras e uma investida final telegrafada.";
        data.illustration = Resources.Load<Sprite>(KaelTorGuardianIllustrationPath)
            ?? BestiaryIconFactory.CreateCreatureSprite("KaelTorBestiarySprite", new Color(0.42f, 0.32f, 0.2f), new Color(1f, 0.68f, 0.12f), BestiaryCreatureType.Boss);
        data.drops.Add(CreateDrop(AncestralCoreItemRegistry.ItemName, "1", BestiaryRarity.Legendary, 1f));
        data.drops.Add(CreateDrop(FirstArtifactFragmentItemRegistry.ItemName, "1", BestiaryRarity.Legendary, 1f));
        data.drops.Add(CreateDrop(KaelTorTrophyItemRegistry.ItemName, "1", BestiaryRarity.Legendary, 1f));
        return data;
    }

    static BestiaryCreatureData CreateRuntimeData(string id, string displayName)
    {
        BestiaryCreatureData data = ScriptableObject.CreateInstance<BestiaryCreatureData>();
        data.hideFlags = HideFlags.HideAndDontSave;
        data.creatureId = id;
        data.displayName = displayName;
        data.name = $"Bestiary_{id}";
        return data;
    }

    static BestiaryDropData CreateDrop(string itemName, string amountLabel, BestiaryRarity rarity, float chance)
    {
        return new BestiaryDropData
        {
            itemName = itemName,
            amountLabel = amountLabel,
            rarity = rarity,
            dropChance = Mathf.Clamp01(chance),
            icon = ResolveItemSprite(itemName)
        };
    }
}

static class BestiaryIconFactory
{
    public static Sprite CreateCreatureSprite(string name, Color main, Color accent, BestiaryCreatureType type)
    {
        Texture2D texture = new Texture2D(96, 96, TextureFormat.RGBA32, false);
        texture.name = name;
        texture.filterMode = FilterMode.Point;

        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
                texture.SetPixel(x, y, clear);
        }

        DrawEllipse(texture, 48, type == BestiaryCreatureType.Boss ? 45 : 50, type == BestiaryCreatureType.Boss ? 25 : 22, type == BestiaryCreatureType.Boss ? 30 : 20, main);
        DrawEllipse(texture, 58, type == BestiaryCreatureType.Boss ? 27 : 32, type == BestiaryCreatureType.Boss ? 16 : 13, type == BestiaryCreatureType.Boss ? 15 : 12, main);

        if (type == BestiaryCreatureType.Animal)
        {
            DrawEllipse(texture, 66, 26, 5, 9, accent);
            DrawEllipse(texture, 44, 26, 5, 9, accent);
            DrawEllipse(texture, 68, 34, 3, 3, Color.black);
        }
        else
        {
            DrawEllipse(texture, 38, 18, 5, 14, accent);
            DrawEllipse(texture, 66, 18, 5, 14, accent);
            DrawEllipse(texture, 62, 30, 3, 3, Color.black);
            DrawEllipse(texture, 50, 30, 3, 3, Color.black);
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 96f);
    }

    static void DrawEllipse(Texture2D texture, int centerX, int centerY, int radiusX, int radiusY, Color color)
    {
        for (int y = centerY - radiusY; y <= centerY + radiusY; y++)
        {
            for (int x = centerX - radiusX; x <= centerX + radiusX; x++)
            {
                if (x < 0 || y < 0 || x >= texture.width || y >= texture.height)
                    continue;

                float dx = (x - centerX) / Mathf.Max(1f, radiusX);
                float dy = (y - centerY) / Mathf.Max(1f, radiusY);
                if (dx * dx + dy * dy <= 1f)
                    texture.SetPixel(x, y, color);
            }
        }
    }
}
