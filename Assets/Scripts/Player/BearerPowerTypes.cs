using System;
using System.Collections.Generic;
using UnityEngine;

public enum BearerPowerId
{
    None,
    FlameHeir,
    VoidWalker,
    WildSummoner,
    EarthTitan
}

public enum BearerAbilitySlot
{
    Primary,
    Secondary,
    Utility,
    Transformation,
    Passive
}

[Serializable]
public class BearerPowerAbilityDefinition
{
    public string stableId;
    public string displayName;
    public string description;
    public int unlockLevel;
    public bool isTransformation;
    public BearerAbilitySlot slot;
    public float cooldown;
    public float staminaCost;

    public BearerPowerAbilityDefinition(
        string stableId,
        string displayName,
        string description,
        int unlockLevel,
        BearerAbilitySlot slot,
        float cooldown,
        float staminaCost,
        bool isTransformation = false)
    {
        this.stableId = stableId;
        this.displayName = displayName;
        this.description = description;
        this.unlockLevel = Mathf.Max(1, unlockLevel);
        this.slot = slot;
        this.cooldown = Mathf.Max(0f, cooldown);
        this.staminaCost = Mathf.Max(0f, staminaCost);
        this.isTransformation = isTransformation;
    }
}

[Serializable]
public class BearerPowerDefinition
{
    public BearerPowerId id;
    public string stableId;
    public string displayName;
    public string bearerTitle;
    public string shortDescription;
    public string firstAbilityName;
    public string firstAbilityDescription;
    public Color primaryColor;
    public Color secondaryColor;
    public List<BearerPowerAbilityDefinition> abilities = new List<BearerPowerAbilityDefinition>();

    public BearerPowerDefinition(
        BearerPowerId id,
        string stableId,
        string displayName,
        string bearerTitle,
        string shortDescription,
        string firstAbilityName,
        string firstAbilityDescription,
        Color primaryColor,
        Color secondaryColor)
    {
        this.id = id;
        this.stableId = stableId;
        this.displayName = displayName;
        this.bearerTitle = bearerTitle;
        this.shortDescription = shortDescription;
        this.firstAbilityName = firstAbilityName;
        this.firstAbilityDescription = firstAbilityDescription;
        this.primaryColor = primaryColor;
        this.secondaryColor = secondaryColor;
    }

    public BearerPowerDefinition WithAbilities(params BearerPowerAbilityDefinition[] configuredAbilities)
    {
        abilities.Clear();
        if (configuredAbilities != null)
            abilities.AddRange(configuredAbilities);
        return this;
    }
}

public static class BearerPowerCatalog
{
    static readonly List<BearerPowerDefinition> Definitions = new List<BearerPowerDefinition>
    {
        new BearerPowerDefinition(
            BearerPowerId.FlameHeir,
            "flame_heir",
            "Herdeiro das Chamas",
            "Portador da Chama",
            "Um legado instavel que transforma dor, furia e coragem em fogo vivo.",
            "Bola de Fogo",
            "Projétil flamejante que explode ao atingir o alvo.",
            new Color(1f, 0.19f, 0.04f, 1f),
            new Color(1f, 0.66f, 0.08f, 1f))
            .WithAbilities(
                new BearerPowerAbilityDefinition("fireball", "Bola de Fogo", "Projetil explosivo de fogo.", 1, BearerAbilitySlot.Primary, 3f, 18f),
                new BearerPowerAbilityDefinition("flame_burst", "Explosao Flamejante", "Explosao que afasta inimigos proximos.", 5, BearerAbilitySlot.Secondary, 9f, 28f),
                new BearerPowerAbilityDefinition("growing_flame", "Chama Crescente", "Aprimora o dano e a explosao das chamas.", 10, BearerAbilitySlot.Passive, 0f, 0f),
                new BearerPowerAbilityDefinition("igneous_form", "Forma Ignea", "Transformacao temporaria do portador.", 15, BearerAbilitySlot.Transformation, 55f, 45f, true)),
        new BearerPowerDefinition(
            BearerPowerId.VoidWalker,
            "void_walker",
            "Andarilho do Vazio",
            "Portador do Vazio",
            "O espaço se dobra ao redor de quem aceita ouvir aquilo que existe entre os mundos.",
            "Teleporte",
            "Avanço instantâneo através de uma pequena fenda.",
            new Color(0.45f, 0.08f, 0.82f, 1f),
            new Color(0.83f, 0.24f, 1f, 1f))
            .WithAbilities(
                new BearerPowerAbilityDefinition("void_step", "Teleporte", "Avanco instantaneo atraves do vazio.", 1, BearerAbilitySlot.Primary, 5f, 16f),
                new BearerPowerAbilityDefinition("void_rift", "Fenda do Vazio", "Fenda que puxa inimigos.", 5, BearerAbilitySlot.Secondary, 11f, 26f),
                new BearerPowerAbilityDefinition("shadow_clone", "Clone Sombrio", "Cria uma copia temporaria.", 10, BearerAbilitySlot.Utility, 22f, 36f),
                new BearerPowerAbilityDefinition("void_form", "Forma do Vazio", "Transformacao completa do portador.", 15, BearerAbilitySlot.Transformation, 55f, 45f, true)),
        new BearerPowerDefinition(
            BearerPowerId.WildSummoner,
            "wild_summoner",
            "Invocador Selvagem",
            "Portador da Matilha",
            "A floresta reconhece um novo guardião e envia suas criaturas para acompanhá-lo.",
            "Invocar Lobo",
            "Invoca um companheiro selvagem permanente.",
            new Color(0.18f, 0.68f, 0.16f, 1f),
            new Color(0.66f, 0.94f, 0.22f, 1f))
            .WithAbilities(
                new BearerPowerAbilityDefinition("summon_wolf", "Invocar Lobo", "Invoca um companheiro selvagem.", 1, BearerAbilitySlot.Primary, 8f, 18f),
                new BearerPowerAbilityDefinition("entangling_roots", "Raizes", "Prende inimigos ao solo.", 5, BearerAbilitySlot.Secondary, 12f, 25f),
                new BearerPowerAbilityDefinition("forest_spirit", "Espirito da Floresta", "Fortalece o companheiro invocado.", 10, BearerAbilitySlot.Utility, 24f, 32f),
                new BearerPowerAbilityDefinition("wild_form", "Forma Selvagem", "Transformacao completa do portador.", 15, BearerAbilitySlot.Transformation, 55f, 45f, true)),
        new BearerPowerDefinition(
            BearerPowerId.EarthTitan,
            "earth_titan",
            "Titã da Terra",
            "Portador do Monólito",
            "Pedra ancestral cobre o corpo e concede a força das montanhas esquecidas.",
            "Soco Sísmico",
            "Golpe pesado que espalha uma onda de impacto pelo chão.",
            new Color(0.47f, 0.29f, 0.13f, 1f),
            new Color(0.17f, 0.84f, 0.92f, 1f))
            .WithAbilities(
                new BearerPowerAbilityDefinition("seismic_punch", "Soco Sismico", "Golpe que causa dano em area.", 1, BearerAbilitySlot.Primary, 4f, 20f),
                new BearerPowerAbilityDefinition("stone_wall", "Muralha de Pedra", "Ergue uma cobertura de pedra.", 5, BearerAbilitySlot.Secondary, 14f, 30f),
                new BearerPowerAbilityDefinition("titanic_leap", "Salto Titanico", "Salto que causa impacto ao aterrissar.", 10, BearerAbilitySlot.Utility, 16f, 35f),
                new BearerPowerAbilityDefinition("titan_form", "Forma de Tita", "Transformacao completa do portador.", 15, BearerAbilitySlot.Transformation, 55f, 45f, true))
    };

    public static IReadOnlyList<BearerPowerDefinition> All => Definitions;

    public static BearerPowerDefinition Find(BearerPowerId id)
    {
        for (int i = 0; i < Definitions.Count; i++)
        {
            if (Definitions[i].id == id)
                return Definitions[i];
        }

        return null;
    }

    public static BearerPowerDefinition Find(string stableId)
    {
        if (string.IsNullOrWhiteSpace(stableId))
            return null;

        for (int i = 0; i < Definitions.Count; i++)
        {
            if (string.Equals(Definitions[i].stableId, stableId, StringComparison.OrdinalIgnoreCase))
                return Definitions[i];
        }

        return null;
    }
}
