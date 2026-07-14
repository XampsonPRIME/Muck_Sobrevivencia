using System;
using System.Collections.Generic;
using UnityEngine;

public enum BestiaryBiome
{
    All,
    Forest,
    Plains,
    Swamp,
    Mountain,
    Cave,
    Desert
}

public enum BestiaryCreatureType
{
    Animal,
    Elemental,
    Monster,
    Boss
}

public enum BestiaryElement
{
    None,
    Nature,
    Earth,
    Shadow,
    Fire,
    Poison
}

public enum BestiaryHostility
{
    Passive,
    Defensive,
    Hostile
}

public enum BestiaryRarity
{
    All,
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

public enum BestiaryDiscoveryState
{
    NotDiscovered,
    Discovered,
    Defeated
}

public enum BestiarySortMode
{
    Recent,
    Alphabetical,
    MostDefeated
}

[Serializable]
public class BestiaryDropData
{
    public string itemName;
    public Sprite icon;
    public BestiaryRarity rarity = BestiaryRarity.Common;
    [Range(0f, 1f)] public float dropChance = 1f;
    public string amountLabel;
}

[Serializable]
public class SaveBestiaryEntryData
{
    public string creatureId;
    public bool discovered;
    public bool defeated;
    public int defeatCount;
    public string firstDiscoveredAt;
    public string firstDefeatedAt;
}

[CreateAssetMenu(menuName = "Elarion/Bestiary/Creature Data")]
public class BestiaryCreatureData : ScriptableObject
{
    public string creatureId;
    public string displayName;
    public string description;
    public BestiaryCreatureType creatureType = BestiaryCreatureType.Animal;
    public BestiaryElement element = BestiaryElement.None;
    public BestiaryHostility hostility = BestiaryHostility.Passive;
    public BestiaryRarity rarity = BestiaryRarity.Common;
    public List<BestiaryBiome> biomes = new List<BestiaryBiome>();
    public int maxHealth;
    public float damage;
    public float speed;
    public string behavior;
    public Sprite illustration;
    public List<BestiaryDropData> drops = new List<BestiaryDropData>();

    public bool HasBiome(BestiaryBiome biome)
    {
        return biome == BestiaryBiome.All || biomes != null && biomes.Contains(biome);
    }
}
