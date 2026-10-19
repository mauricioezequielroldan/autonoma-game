// AUTONOMA – Núcleo de simulación v0.8 (Godot 4.1+ .NET / C#)
// Necesidades, rasgos, presets, estado, Fe, lluvia, progreso del Hogar
// y el MODELO DE DATOS del aprendizaje: SkillTree, LearningAgent, Knowledge, MemoryEpisodic.
// La lógica de enseñanza/olvido/ejecución vive en AutonomaLearning.cs.
// Todo acá es C# plano: los agentes NO son nodos.

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Autonoma;

public static class GameTime
{
    public const float RealSecondsPerGameDay = 1200f; // 20 min reales = 1 día de juego
    public const float RealSecondsPerGameHour = RealSecondsPerGameDay / 24f;
    public static float ToGameHours(double realDelta) => (float)realDelta / RealSecondsPerGameHour;
}

public enum Biome { BosqueAncestral, BaldioDeCeniza, CumbresHeladas }
public enum Weather { Despejado, Lluvia, Tormenta, TormentaCeniza, Ventisca, Niebla }
public enum ReactionProfile { Neutral, Valiente, Miedoso, Curioso, Leal, Empatico, Resiliente }
public enum SusurroIntent { Explorar, Comer, Dormir, Refugiarse, Acercarse, Evitar, Practicar }
public enum MoralChoice { Atacar, Curar, Huir }
public enum Goal { Comer, Dormir, BuscarLuz, Explorar, Refugio }
public enum InfluenceType { Susurro, ObjetoDivino, AlterarMundo, Recuerdo, DecisionMoral, Posesion }

// Aprendizaje
public enum SkillCategory { Supervivencia, Recoleccion, Emocional }
public enum SkillState { NoAprendido, Imitando, Practicando, Dominado } // el orden importa (se compara con >=)
public enum DemoActionType { Recolectar, Colocar, Martillar, Encender, Agacharse, Sentarse, Senalar, Respirar, Inspeccionar, Revolver, Descartar }

// ───────────────────────── Rasgos ─────────────────────────
public sealed class Traits
{
    public float Valentia, Curiosidad, Lealtad, Empatia, Resiliencia; // 0-100
    public const float High = 65f, Low = 35f;

    public Traits(float valentia, float curiosidad, float lealtad, float empatia, float resiliencia)
    {
        Valentia = valentia; Curiosidad = curiosidad; Lealtad = lealtad;
        Empatia = empatia; Resiliencia = resiliencia;
    }

    public float Strength(ReactionProfile p) => p switch
    {
        ReactionProfile.Valiente   => Valentia    >= High ? Valentia - 50f    : 0f,
        ReactionProfile.Miedoso    => Valentia    <= Low  ? 50f - Valentia    : 0f,
        ReactionProfile.Curioso    => Curiosidad  >= High ? Curiosidad - 50f  : 0f,
        ReactionProfile.Leal       => Lealtad     >= High ? Lealtad - 50f     : 0f,
        ReactionProfile.Empatico   => Empatia     >= High ? Empatia - 50f     : 0f,
        ReactionProfile.Resiliente => Resiliencia >= High ? Resiliencia - 50f : 0f,
        _ => 0f
    };

    public bool Is(ReactionProfile p) => Strength(p) > 0f;

    public ReactionProfile Resolve(IEnumerable<ReactionProfile> available)
    {
        var best = ReactionProfile.Neutral;
        float bestStrength = 0f;
        foreach (var p in available)
        {
            float s = Strength(p);
            if (s > bestStrength) { best = p; bestStrength = s; }
        }
        return best;
    }
}

public static class PersonalityPresets
{
    //                                       Val  Cur  Leal Emp  Res
    public static Traits Centinela()     => new(85, 35, 70, 45, 75);
    public static Traits Errante()       => new(55, 90, 30, 50, 55);
    public static Traits Devoto()        => new(30, 40, 95, 70, 35);
    public static Traits Cuidadora()     => new(45, 50, 55, 95, 60);
    public static Traits Superviviente() => new(40, 20, 20, 25, 95);

    public static readonly Dictionary<string, Func<Traits>> All = new()
    {
        ["centinela"] = Centinela, ["errante"] = Errante, ["devoto"] = Devoto,
        ["cuidadora"] = Cuidadora, ["superviviente"] = Superviviente,
    };

    public static Traits RandomVariant(string id, RandomNumberGenerator rng, float spread = 10f)
    {
        var b = All[id]();
        float J(float v) => Mathf.Clamp(v + rng.RandfRange(-spread, spread), 0f, 100f);
        return new Traits(J(b.Valentia), J(b.Curiosidad), J(b.Lealtad), J(b.Empatia), J(b.Resiliencia));
    }
}

// ───────────────────────── Necesidades ─────────────────────────
public sealed class Needs
{
    // Saciedad/Energía/Vínculo: 100 = bien. Miedo/Humedad: 100 = mal. Curiosidad: impulso neutral.
    public float Saciedad = 80f, Energia = 80f, Miedo = 20f, Curiosidad = 40f, Vinculo = 50f, Humedad = 0f;
    public float Animo = 65f;
    public float AnimoPenalty; // enfermedad, etc.

    public bool Cojea => Saciedad < 20f;
    public float SpeedMultiplier => (Cojea ? 0.5f : 1f) * (1f - 0.15f * Humedad / 100f);
    public bool Rendido => Animo < 15f;

    public float AnimoObjetivo => Mathf.Clamp(
        0.30f * Saciedad + 0.25f * Energia + 0.30f * (100f - Miedo) + 0.15f * Vinculo - AnimoPenalty, 0f, 100f);

    public void ClampAll()
    {
        Saciedad = Mathf.Clamp(Saciedad, 0f, 100f);
        Energia = Mathf.Clamp(Energia, 0f, 100f);
        Miedo = Mathf.Clamp(Miedo, 0f, 100f);
        Curiosidad = Mathf.Clamp(Curiosidad, 0f, 100f);
        Vinculo = Mathf.Clamp(Vinculo, 0f, 100f);
        Humedad = Mathf.Clamp(Humedad, 0f, 100f);
        Animo = Mathf.Clamp(Animo, 0f, 100f);
    }
}

// ───────────────────────── Mundo ─────────────────────────
public sealed class WorldState
{
    public Biome Biome = Biome.BosqueAncestral;
    public Weather Weather = Weather.Despejado;
    public float TotalHours = 8f;
    public float HourOfDay => TotalHours % 24f;
    public int Day => (int)(TotalHours / 24f) + 1;
    public bool IsNight => HourOfDay < 6f || HourOfDay >= 20f;
    public bool IsDusk => HourOfDay >= 17f && HourOfDay < 20f;

    // Lluvia (la maneja RainController)
    public float RainIntensity;                              // 0 = seco, 1 = diluvio
    public float RainForecastHours = float.PositiveInfinity; // horas hasta que empiece
    public bool IsRaining => RainIntensity > 0.01f;

    // Consultas espaciales (las llena el mundo con Area3D / grupos)
    public float HostileDistance = float.PositiveInfinity;
    public float ShelterDistance = float.PositiveInfinity;
    public float FireDistance = float.PositiveInfinity;
    public bool FireHasRoof;
    public bool KnownBerriesNearby, UnknownBerriesNearby, OnFrozenLake, RuinNearby,
                OrphanCubNearby, StrangerSpawnNearby, AtMountainPass;
    public readonly HashSet<string> ItemsNearby = new(); // ids de ítems/grupos a < 20 m
    public bool Near(string id) => ItemsNearby.Contains(id);
}

// ───────────────────────── Fe (recurso del jugador) ─────────────────────────
public sealed class FaithPool
{
    public float Fe = 50f;
    public const float Max = 100f;
    public const float CostAntorcha = 20f, CostCalmar = 10f, CostAlterar = 15f, CostPosesion = 30f;

    public static float Cost(InfluenceType t, bool answeringHelpRequest = false) => (t switch
    {
        InfluenceType.ObjetoDivino => CostAntorcha,
        InfluenceType.Recuerdo     => CostCalmar,
        InfluenceType.AlterarMundo => CostAlterar,
        InfluenceType.Posesion     => CostPosesion,
        _ => 0f // susurros y decisiones morales son gratis
    }) * (answeringHelpRequest ? 0.5f : 1f);

    public void Tick(float h, CharacterState c) => Fe = Mathf.Min(Max, Fe + (1f + c.Needs.Vinculo / 50f) * h);
    public bool CanAfford(float cost) => Fe >= cost;
    public bool TrySpend(float cost) { if (Fe < cost) return false; Fe -= cost; return true; }
    public void Offering(float amount = 5f) => Fe = Mathf.Min(Max, Fe + amount);
}

// ───────────────────────── Aprendizaje: datos ─────────────────────────

/// Un paso de la receta de una habilidad. Count = cuántas veces se repite la acción.
public sealed class RecipeStep
{
    public DemoActionType Action;
    public string ItemId = "";        // ítem que se recolecta / coloca / sobre el que se actúa
    public int Count = 1;
    public string Anim = "";          // clave de animación en CharacterMover
    public float Duration = 1f;       // segundos reales por repetición
    public string TargetGroup;        // dónde se hace si NO es en el BuildSite (null = BuildSite o el ítem)
    public int ConsumePerAction = 1;  // para Colocar: cuánto inventario gasta cada repetición
}

public sealed class SkillDef
{
    public string Id = "", Name = "", MasteryEffect = "";
    public SkillCategory Category;
    public int Tier;
    public string[] Prereqs = Array.Empty<string>();          // deben estar en Practicando o más
    public Dictionary<string, int> Materials = new();          // consumibles
    public string[] Tools = Array.Empty<string>();             // se usan pero no se gastan
    public RecipeStep[] Steps = Array.Empty<RecipeStep>();
    public bool IsBuild;              // deja una estructura (BuildSite)
    public string AnchorGroup;        // si se construye sobre algo existente (fuego, refugio, sendero)
    public float DemoGain = 30f;      // % que da una demostración perfecta (antes de modificadores)
    public float PracticeSuccessGain = 8f, PracticeFailGain = 3f;
    public float Difficulty = 1f;     // divide todas las ganancias
    public float IdealDemoSeconds = 30f;
    public Func<CharacterState, WorldState, float> Utility = (_, _) => 0f; // 0-100: ganas de hacerlo solo
    public int TotalUnits => Steps.Sum(s => s.Count);
}

public struct PathPoint
{
    public Vector3 LocalPos; // relativo al Anchor de la demo
    public float Time;
    public PathPoint(Vector3 p, float t) { LocalPos = p; Time = t; }
}

public sealed class DemoStep
{
    public DemoActionType Action;
    public string ItemId = "";
    public Vector3 LocalPos;   // relativo al Anchor
    public float Time;         // segundos desde el inicio de la posesión
    public bool InRecipe;      // false = gesto de más (quirk)
}

/// Memoria episódica: una demostración tuya, comprimida (pasos simbólicos + waypoints).
public sealed class MemoryEpisodic
{
    public string SkillId = "";
    public int Day;
    public float Hour;
    public Vector3 Anchor;                       // posición mundial de referencia
    public readonly List<DemoStep> Steps = new();
    public readonly List<PathPoint> Path = new(); // máx. 64 puntos
    public readonly Dictionary<string, int> MaterialsUsed = new();
    public readonly List<string> Quirks = new(); // "rodeo", "orden_invertido", "golpes_de_mas", "pausa_pensativa"
    public float DurationSeconds;
    public float Completeness, Efficiency, Clarity, Quality; // 0-1
    public bool Completed => Completeness >= 0.999f;
}

/// Un "agente" por habilidad: datos + reglas. Cero nodos, ~1-2 KB cada uno.
public sealed class LearningAgent
{
    public readonly SkillDef Def;
    public float Knowledge { get; private set; } // 0-100
    public bool EverReachedImitando { get; private set; }
    private bool _dominatedLatch;

    public float FirstLearnedHour = -1f, LastPracticeHour = -1f, LastDecayHour = -1f;
    public int Demonstrations, PracticeSuccesses, PracticeFailures, DemosToday;
    public bool FirstAutonomousDone, FirstQuirkShown;
    public string SignatureQuirk; // la "firma del maestro": un gesto tuyo que conserva para siempre

    public MemoryEpisodic First { get; private set; }
    public readonly List<MemoryEpisodic> Episodes = new(); // las 3 mejores

    internal Action<LearningAgent, SkillState, SkillState> OnStateChanged;

    public LearningAgent(SkillDef def) => Def = def;

    public SkillState State
    {
        get
        {
            if (_dominatedLatch) return SkillState.Dominado; // histéresis: sigue Dominado hasta < 85
            if (Knowledge >= 60f) return SkillState.Practicando;
            if (Knowledge >= 30f) return SkillState.Imitando;
            return SkillState.NoAprendido;
        }
    }

    public bool WasTaught => Demonstrations > 0;
    public MemoryEpisodic Template => Episodes.OrderByDescending(e => e.Quality).FirstOrDefault() ?? First;

    public void SetKnowledge(float value)
    {
        var old = State;
        float floor = EverReachedImitando ? 15f : 0f; // residuo: nunca lo olvida del todo
        Knowledge = Mathf.Clamp(value, floor, 100f);
        if (Knowledge >= 30f) EverReachedImitando = true;
        if (Knowledge >= 100f) _dominatedLatch = true;
        else if (Knowledge < 85f) _dominatedLatch = false;
        var now = State;
        if (now != old) OnStateChanged?.Invoke(this, old, now);
    }

    public void AddEpisode(MemoryEpisodic m)
    {
        First ??= m;
        Episodes.Add(m);
        if (Episodes.Count > 3)
            Episodes.Remove(Episodes.OrderBy(e => e.Quality).First());
    }
}

/// El "libro" de habilidades de un personaje: un LearningAgent por cada SkillDef.
public sealed class SkillBook
{
    private readonly Dictionary<string, LearningAgent> _agents = new();
    public event Action<LearningAgent, SkillState, SkillState> StateChanged;

    public SkillBook()
    {
        foreach (var d in SkillTree.All.Values)
        {
            var a = new LearningAgent(d);
            a.OnStateChanged = (ag, o, n) => StateChanged?.Invoke(ag, o, n);
            _agents[d.Id] = a;
        }
    }

    public IEnumerable<LearningAgent> Agents => _agents.Values;
    public LearningAgent Get(string id) => _agents[id];
    public SkillState StateOf(string id) => _agents.TryGetValue(id, out var a) ? a.State : SkillState.NoAprendido;
    public bool IsAtLeast(string id, SkillState s) => StateOf(id) >= s;
    public int CountAtLeast(SkillState s) => _agents.Values.Count(a => a.State >= s);

    /// "Cada habilidad desbloquea la siguiente": se puede enseñar si los requisitos están en Practicando+.
    public bool IsUnlocked(string id) =>
        SkillTree.All[id].Prereqs.All(p => IsAtLeast(p, SkillState.Practicando));
}

// ───────────────────────── Árbol de habilidades (15) ─────────────────────────
public static class SkillTree
{
    static RecipeStep S(DemoActionType a, string item, int count, string anim, float dur,
                        string target = null, int consume = 1) =>
        new() { Action = a, ItemId = item, Count = count, Anim = anim, Duration = dur,
                TargetGroup = target, ConsumePerAction = consume };

    const DemoActionType Rec = DemoActionType.Recolectar, Col = DemoActionType.Colocar,
        Mar = DemoActionType.Martillar, Enc = DemoActionType.Encender, Aga = DemoActionType.Agacharse,
        Sen = DemoActionType.Sentarse, Ana = DemoActionType.Senalar, Res = DemoActionType.Respirar,
        Ins = DemoActionType.Inspeccionar, Rev = DemoActionType.Revolver, Des = DemoActionType.Descartar;

    public static readonly Dictionary<string, SkillDef> All = new SkillDef[]
    {
        // ══════ RECOLECCIÓN ══════
        new() {
            Id = "juntar_palos", Name = "Juntar Palos", Category = SkillCategory.Recoleccion, Tier = 0,
            DemoGain = 40f, Difficulty = 0.8f, IdealDemoSeconds = 15f,
            Steps = new[] { S(Rec, "palo", 3, "recoger", 0.8f), S(Col, "palo", 1, "colocar_palo", 0.8f, "pila_palos", 3) },
            MasteryEffect = "Mantiene sola una pila de 6 palos en el campamento.",
            Utility = (c, w) => c.Count("palo") < 3 && w.Near("palo") ? 50f : 0f,
        },
        new() {
            Id = "identificar_bayas", Name = "Identificar Bayas Buenas", Category = SkillCategory.Recoleccion, Tier = 0,
            DemoGain = 30f, Difficulty = 1.2f, IdealDemoSeconds = 20f,
            Steps = new[] { S(Ins, "arbusto_bayas", 2, "inspeccionar", 2f, "arbusto_bayas"),
                            S(Rec, "baya_buena", 2, "recoger", 0.7f),
                            S(Des, "baya_desconocida", 1, "descartar", 1f, "arbusto_desconocido") },
            MasteryEffect = "Inmune a 'Bayas de Ceniza'. Encuentra comida nueva en cada bioma.",
            Utility = (c, w) => c.Needs.Saciedad < 50f && (w.Near("arbusto_bayas") || w.UnknownBerriesNearby)
                                ? 100f - c.Needs.Saciedad : 0f,
        },
        new() {
            Id = "talar", Name = "Talar", Category = SkillCategory.Recoleccion, Tier = 1,
            Prereqs = new[] { "juntar_palos" }, Tools = new[] { "piedra_afilada" },
            DemoGain = 30f, Difficulty = 1.1f, IdealDemoSeconds = 25f,
            Steps = new[] { S(Rec, "piedra_afilada", 1, "recoger", 0.8f),
                            S(Mar, "arbol_joven", 6, "martillar", 0.5f, "arbol_joven"),
                            S(Rec, "tronco", 1, "recoger_pesado", 1.2f) },
            MasteryEffect = "Junta troncos solo cuando el campamento los necesita para subir de nivel.",
            Utility = (c, w) => c.LogsNeeded > 0 && w.Near("arbol_joven") && c.Needs.Energia > 50f ? 45f : 0f,
        },
        new() {
            Id = "rastrear", Name = "Rastrear", Category = SkillCategory.Recoleccion, Tier = 1,
            Prereqs = new[] { "identificar_bayas" },
            DemoGain = 25f, Difficulty = 1.3f, IdealDemoSeconds = 25f,
            Steps = new[] { S(Ins, "huella", 3, "inspeccionar", 1.5f, "huella"),
                            S(Ana, "huella", 1, "senalar", 1f, "huella") },
            MasteryEffect = "Sigue huellas solo: encuentra agua, crías y senderos de animales.",
            Utility = (c, w) => c.Needs.Curiosidad > 50f && w.Near("huella") ? c.Needs.Curiosidad * 0.8f : 0f,
        },
        new() {
            Id = "trampa_simple", Name = "Trampa Simple", Category = SkillCategory.Recoleccion, Tier = 2,
            Prereqs = new[] { "rastrear", "juntar_palos" }, IsBuild = true, AnchorGroup = "sendero_animal",
            Materials = new() { ["palo"] = 2, ["liana"] = 1, ["baya_buena"] = 1 },
            DemoGain = 25f, Difficulty = 1.4f, IdealDemoSeconds = 30f,
            Steps = new[] { S(Rec, "palo", 2, "recoger", 0.8f), S(Rec, "liana", 1, "recoger", 1f),
                            S(Col, "palo", 2, "colocar_palo", 1f), S(Col, "baya_buena", 1, "agacharse", 1f),
                            S(Mar, "trampa", 2, "martillar", 0.5f) },
            MasteryEffect = "Arma y revisa trampas al amanecer: comida sin depender de bayas.",
            Utility = (c, w) => c.Needs.Saciedad < 60f && w.Near("sendero_animal") ? 55f : 0f,
        },

        // ══════ SUPERVIVENCIA ══════
        new() {
            Id = "hacer_fuego", Name = "Hacer Fuego", Category = SkillCategory.Supervivencia, Tier = 1,
            Prereqs = new[] { "juntar_palos" }, IsBuild = true, Tools = new[] { "piedra" },
            Materials = new() { ["palo"] = 3, ["hojas_secas"] = 1 },
            DemoGain = 30f, Difficulty = 1f, IdealDemoSeconds = 25f,
            Steps = new[] { S(Rec, "palo", 3, "recoger", 0.8f), S(Rec, "hojas_secas", 1, "recoger", 0.7f),
                            S(Rec, "piedra", 1, "recoger", 0.7f),
                            S(Col, "palo", 3, "colocar_palo", 0.8f), S(Col, "hojas_secas", 1, "agacharse", 0.8f),
                            S(Enc, "piedra", 4, "encender", 0.6f) },
            MasteryEffect = "Prende fuego solo al atardecer. Ya no depende de tus antorchas.",
            Utility = (c, w) => (w.IsNight || w.IsDusk || c.Needs.Miedo > 50f || c.Needs.Humedad > 50f)
                                && w.FireDistance > 8f ? 60f + c.Needs.Miedo * 0.3f : 0f,
        },
        new() {
            Id = "refugio_palos", Name = "Refugio de Palos", Category = SkillCategory.Supervivencia, Tier = 1,
            Prereqs = new[] { "juntar_palos" }, IsBuild = true,
            Materials = new() { ["palo_largo"] = 4, ["hojas_grandes"] = 6 },
            DemoGain = 30f, Difficulty = 1f, IdealDemoSeconds = 40f,
            Steps = new[] { S(Rec, "palo_largo", 4, "recoger", 0.9f),
                            S(Col, "palo_largo", 4, "colocar_palo", 1.2f),
                            S(Mar, "refugio", 4, "martillar", 0.9f),
                            S(Rec, "hojas_grandes", 6, "recoger", 0.6f),
                            S(Col, "hojas_grandes", 2, "colocar_techo", 1.4f, null, 3) },
            MasteryEffect = "Con lluvia en el pronóstico, construye o va a su refugio antes de mojarse.",
            Utility = (c, w) => c.InShelter || w.ShelterDistance <= 30f ? 0f
                                : w.IsRaining ? 90f : w.RainForecastHours <= 2f ? 60f : w.IsNight ? 30f : 0f,
        },
        new() {
            Id = "cama_paja", Name = "Cama de Paja", Category = SkillCategory.Supervivencia, Tier = 2,
            Prereqs = new[] { "refugio_palos" }, IsBuild = true, AnchorGroup = "refugio",
            Materials = new() { ["paja"] = 4 },
            DemoGain = 35f, Difficulty = 0.8f, IdealDemoSeconds = 25f,
            Steps = new[] { S(Rec, "paja", 4, "recoger", 0.6f), S(Col, "paja", 2, "agacharse", 1f, null, 2),
                            S(Aga, "cama", 1, "alisar", 2f) },
            MasteryEffect = "Dormir recupera +30% de Energía y el Miedo nocturno sube menos.",
            Utility = (c, w) => c.HasBuiltShelter && !c.HasBed && w.Near("paja") ? 40f : 0f,
        },
        new() {
            Id = "olla_cocinar", Name = "Cocinar en Olla", Category = SkillCategory.Supervivencia, Tier = 2,
            Prereqs = new[] { "hacer_fuego" }, Tools = new[] { "olla" },
            Materials = new() { ["ingrediente"] = 2 },
            DemoGain = 30f, Difficulty = 1.1f, IdealDemoSeconds = 25f,
            Steps = new[] { S(Rec, "olla", 1, "recoger", 0.8f), S(Col, "olla", 1, "colocar", 1f, "fuego", 0),
                            S(Col, "ingrediente", 2, "colocar", 0.8f, "fuego"),
                            S(Rev, "olla", 1, "revolver", 3f, "fuego") },
            MasteryEffect = "Cocina sola: +50% Saciedad por comida y el caldo cura la enfermedad.",
            Utility = (c, w) => w.FireDistance < 8f && c.Count("ingrediente") >= 2 && (c.Needs.Saciedad < 60f || c.IsSick)
                                ? (c.IsSick ? 80f : 100f - c.Needs.Saciedad) : 0f,
        },
        new() {
            Id = "antorcha", Name = "Hacer Antorcha", Category = SkillCategory.Supervivencia, Tier = 2,
            Prereqs = new[] { "hacer_fuego" },
            Materials = new() { ["palo"] = 1, ["resina"] = 1 },
            DemoGain = 35f, Difficulty = 0.9f, IdealDemoSeconds = 20f,
            Steps = new[] { S(Rec, "palo", 1, "recoger", 0.8f), S(Rec, "resina", 1, "inspeccionar", 1.5f),
                            S(Mar, "palo", 1, "envolver", 1.2f, "fuego"), S(Enc, "fuego", 1, "encender", 1f, "fuego") },
            MasteryEffect = "Se hace su propia antorcha al atardecer. Las antorchas divinas ya casi no hacen falta.",
            Utility = (c, w) => !c.HasTorch && (w.IsDusk || w.IsNight) && w.FireDistance < 8f && w.Near("resina") ? 55f : 0f,
        },
        new() {
            Id = "techo_fogata", Name = "Techo para la Fogata", Category = SkillCategory.Supervivencia, Tier = 3,
            Prereqs = new[] { "refugio_palos", "hacer_fuego" }, IsBuild = true, AnchorGroup = "fuego",
            Materials = new() { ["palo_largo"] = 3, ["hojas_grandes"] = 4 },
            DemoGain = 25f, Difficulty = 1.3f, IdealDemoSeconds = 35f,
            Steps = new[] { S(Rec, "palo_largo", 3, "recoger", 0.9f), S(Col, "palo_largo", 3, "colocar_palo", 1.2f),
                            S(Rec, "hojas_grandes", 4, "recoger", 0.6f),
                            S(Col, "hojas_grandes", 1, "colocar_techo", 1.6f, null, 4),
                            S(Mar, "techo_fogata", 1, "martillar", 0.9f) },
            MasteryEffect = "La fogata ya no se apaga con la lluvia.",
            Utility = (c, w) => w.FireDistance < 10f && !w.FireHasRoof && (w.IsRaining || w.RainForecastHours <= 3f) ? 75f : 0f,
        },
        new() {
            Id = "agua_lluvia", Name = "Juntar Agua de Lluvia", Category = SkillCategory.Supervivencia, Tier = 3,
            Prereqs = new[] { "olla_cocinar", "refugio_palos" }, IsBuild = true, Tools = new[] { "olla" },
            Materials = new() { ["hojas_grandes"] = 1 },
            DemoGain = 30f, Difficulty = 0.9f, IdealDemoSeconds = 25f,
            Steps = new[] { S(Rec, "olla", 1, "recoger", 0.8f), S(Col, "olla", 1, "colocar", 1f, null, 0),
                            S(Rec, "hojas_grandes", 1, "recoger", 0.6f), S(Col, "hojas_grandes", 1, "colocar_techo", 1.2f),
                            S(Sen, "colector", 1, "sentarse", 3f) },
            MasteryEffect = "La lluvia deja de ser solo amenaza: cuando empieza, Miedo +10 en vez de +30.",
            Utility = (c, w) => w.RainIntensity > 0.2f && !c.HasWater && (c.Count("olla") > 0 || w.Near("olla")) ? 45f : 0f,
        },

        // ══════ EMOCIONAL ══════
        new() {
            Id = "esconderse", Name = "Esconderse", Category = SkillCategory.Emocional, Tier = 0,
            DemoGain = 40f, Difficulty = 0.7f, IdealDemoSeconds = 15f,
            Steps = new[] { S(Aga, "escondite", 1, "esconderse", 5f, "escondite") },
            MasteryEffect = "Ante amenazas, se esconde solo: Miedo sube 50% menos y las bestias pierden el rastro.",
            Utility = (c, w) => w.HostileDistance < 25f ? 85f
                                : (w.Weather == Weather.Tormenta && !c.InShelter) ? 60f : 0f,
        },
        new() {
            Id = "pedir_ayuda", Name = "Pedir Ayuda", Category = SkillCategory.Emocional, Tier = 1,
            Prereqs = new[] { "esconderse" },
            DemoGain = 30f, Difficulty = 1f, IdealDemoSeconds = 25f,
            Steps = new[] { S(Rec, "piedra", 3, "recoger", 0.7f), S(Col, "piedra", 3, "colocar", 0.8f, "lugar_alto"),
                            S(Ana, "cielo", 1, "senalar_cielo", 2f, "lugar_alto") },
            MasteryEffect = "En crisis te llama con una señal visible. Responderle cuesta la mitad de Fe.",
            Utility = (c, w) => (c.Needs.Miedo > 70f || c.Needs.Saciedad < 20f) && c.Needs.Vinculo > 30f
                                && c.HoursSinceInfluence > 2f ? 70f : 0f,
        },
        new() {
            Id = "calmarse_solo", Name = "Calmarse Solo", Category = SkillCategory.Emocional, Tier = 2,
            Prereqs = new[] { "pedir_ayuda" },
            DemoGain = 25f, Difficulty = 1.5f, IdealDemoSeconds = 25f,
            Steps = new[] { S(Sen, "fuego", 1, "sentarse", 1.5f, "fuego"), S(Res, "aire", 4, "respirar", 1.5f, "fuego"),
                            S(Ins, "recuerdo_personal", 1, "mirar_recuerdo", 2.5f, "recuerdo_personal") },
            MasteryEffect = "Con Miedo > 60 se calma solo (−30 Miedo, cada 6 h). Requisito del Hogar Permanente.",
            Utility = (c, w) => c.Needs.Miedo > 60f && c.HoursSinceSelfCalm > 6f ? c.Needs.Miedo : 0f,
        },
    }.ToDictionary(d => d.Id);

    public static IEnumerable<SkillDef> ByCategory(SkillCategory cat) => All.Values.Where(d => d.Category == cat);
}

// ───────────────────────── Estado del personaje ─────────────────────────
public sealed class CharacterState
{
    public Traits Traits;
    public Needs Needs = new();
    public SkillBook Skills = new();

    // Flags que llena el controlador / el mundo
    public bool HasTorch, NearFire, InShelter, ShelterLeaks, IsSleeping, IsMoving, IsFleeing, IsHidden;
    public bool HasBuiltShelter, HasBed, HasWater, HasFireMaterials;
    public int ShelterLevel;   // nivel del campamento 0-3
    public int LogsNeeded;     // troncos que faltan para el próximo nivel
    public int RecuerdosAceptados;
    public float FearAtSleepStart;

    // Salud
    public float SickHours;    // > 0 = enfermo (resfrío)
    public bool IsSick => SickHours > 0f;
    public float HoursSoaked;
    private float _sickRisk;

    // Contadores (horas de juego)
    public float HoursStarving, HoursMoraleCritical, HoursBondAbove80;
    public float HoursSinceInfluence, HoursSinceObjetoDivino = 999f, HoursSinceRecuerdo = 999f;
    public float HoursSincePossession = 999f, HoursSinceSelfCalm = 999f;
    public int PossessionsToday;
    public int ConsecutiveFearNights;
    public float AvgSaciedad3d = 80f, AvgEnergia3d = 80f;

    /// Para el Hogar Permanente: cualquier ayuda directa (objeto, recuerdo o posesión) reinicia la autonomía.
    public float HoursSinceDivineHelp => Mathf.Min(HoursSinceObjetoDivino, Mathf.Min(HoursSinceRecuerdo, HoursSincePossession));

    private float _nightMaxFear;
    private bool _wasSleeping;

    // Memoria semántica (tags) y de objetos
    private readonly Dictionary<string, int> _memories = new();
    public IReadOnlyDictionary<string, int> Memories => _memories;
    private readonly Dictionary<string, int> _inventory = new();
    public IReadOnlyDictionary<string, int> Inventory => _inventory;

    public static readonly string[] TraumaTags =
        { "abandonado", "miedo_hielo", "cazado", "bayas_ceniza_venenosas", "robo_extrano" };

    public CharacterState(Traits traits) => Traits = traits;

    public void Remember(string tag, int day) => _memories[tag] = day;
    public bool Has(string tag) => _memories.ContainsKey(tag);
    public int DaysSince(string tag, int today) =>
        _memories.TryGetValue(tag, out var d) ? today - d : int.MaxValue;
    public bool HasAnyTrauma => TraumaTags.Any(Has);
    public int MapFragments => _memories.Keys.Count(k => k.StartsWith("fragmento_mapa_"));

    public int Count(string item) => _inventory.TryGetValue(item, out var n) ? n : 0;
    public void AddItem(string item, int n = 1) => _inventory[item] = Count(item) + n;
    public bool TryConsume(string item, int n)
    {
        if (n <= 0) return true;
        if (Count(item) < n) return false;
        _inventory[item] = Count(item) - n;
        return true;
    }

    public Goal MostUrgentGoal(WorldState w)
    {
        var n = Needs;
        var scores = new (Goal g, float u)[]
        {
            (Goal.Comer,     100f - n.Saciedad),
            (Goal.Dormir,    100f - n.Energia + (w.IsNight ? 20f : 0f)),
            (Goal.BuscarLuz, n.Miedo),
            (Goal.Explorar,  n.Curiosidad * (0.5f + Traits.Curiosidad / 100f)),
            (Goal.Refugio,   w.IsRaining && !InShelter ? 50f + n.Humedad * 0.5f : 0f),
        };
        return scores.OrderByDescending(s => s.u).First().g;
    }

    public void Tick(float h, WorldState w)
    {
        var n = Needs;

        if (IsSleeping && !_wasSleeping) FearAtSleepStart = n.Miedo;
        _wasSleeping = IsSleeping;

        // Saciedad
        n.Saciedad -= 3f * h * (Has("cria_adoptada") ? 1.25f : 1f) * (IsSick ? 1.25f : 1f);

        // Energía
        float sleepMul = (IsSick ? 0.5f : 1f) * (HasBed && InShelter ? 1.3f : 1f);
        if (IsSleeping) n.Energia += (NearFire ? 12f : 6f) * sleepMul * h;
        else n.Energia -= (IsMoving ? 5f : 1.5f) * h;

        // Lluvia y humedad
        float exposure = !w.IsRaining ? 0f : InShelter ? (ShelterLeaks ? 0.3f : 0f) : 1f;
        if (exposure > 0f) n.Humedad += 30f * w.RainIntensity * exposure * h;
        else n.Humedad -= (NearFire ? 40f : InShelter ? 10f : 5f) * h;
        if (n.Humedad > 50f) n.Energia -= 3f * h; // frío

        // Miedo
        float fear = 0f;
        if (w.IsNight && !HasTorch && !NearFire) fear += 6f;
        if (w.Weather is Weather.Tormenta or Weather.TormentaCeniza or Weather.Ventisca) fear += 8f;
        if (exposure > 0.5f) fear += 4f * w.RainIntensity;
        if (w.HostileDistance < 25f) fear += IsHidden ? 7.5f : 15f;
        if (HasTorch || NearFire) fear -= 8f;
        if (InShelter) fear -= 5f;
        if (fear == 0f) fear = -2f;
        if (fear > 0f) fear *= Mathf.Lerp(1.4f, 0.6f, Traits.Valentia / 100f);
        if (fear > 0f && Has("cria_adoptada") && w.IsNight) fear *= 0.8f;
        if (fear > 0f && w.IsNight && HasBed && InShelter) fear *= 0.8f;
        n.Miedo += fear * h;

        // Curiosidad
        n.Curiosidad += (n.Miedo < 50f ? 3f * (0.5f + Traits.Curiosidad / 100f) : -5f) * h;

        // Vínculo
        if (HoursSinceInfluence > 12f) n.Vinculo -= 0.5f * (1.5f - Traits.Lealtad / 100f) * h;

        // Enfermedad: empapado (>70) durante 4 h acumula riesgo; la Resiliencia lo reduce
        HoursSoaked = n.Humedad > 70f ? HoursSoaked + h : 0f;
        if (!IsSick && HoursSoaked >= 4f)
        {
            _sickRisk += 0.25f * (1.3f - Traits.Resiliencia / 100f) * h;
            if (_sickRisk >= 1f) { SickHours = 36f; _sickRisk = 0f; }
        }
        if (IsSick) SickHours = Mathf.Max(0f, SickHours - h);
        n.AnimoPenalty = IsSick ? 10f : 0f;

        n.ClampAll();

        float target = n.AnimoObjetivo;
        float rate = target > n.Animo ? 0.5f : Mathf.Lerp(0.35f, 0.08f, Traits.Resiliencia / 100f);
        n.Animo = Mathf.Lerp(n.Animo, target, 1f - Mathf.Exp(-rate * h));

        HoursStarving       = (n.Saciedad <= 0f || n.Energia <= 0f) ? HoursStarving + h : 0f;
        HoursMoraleCritical = n.Rendido ? HoursMoraleCritical + h : 0f;
        HoursBondAbove80    = n.Vinculo >= 80f ? HoursBondAbove80 + h : 0f;
        HoursSinceInfluence += h; HoursSinceObjetoDivino += h; HoursSinceRecuerdo += h;
        HoursSincePossession += h; HoursSinceSelfCalm += h;
        if (w.IsNight) _nightMaxFear = Mathf.Max(_nightMaxFear, n.Miedo);

        float k = 1f - Mathf.Exp(-h / 72f);
        AvgSaciedad3d = Mathf.Lerp(AvgSaciedad3d, n.Saciedad, k);
        AvgEnergia3d  = Mathf.Lerp(AvgEnergia3d,  n.Energia,  k);
    }

    public void OnDawn()
    {
        ConsecutiveFearNights = _nightMaxFear > 70f ? ConsecutiveFearNights + 1 : 0;
        _nightMaxFear = 0f;
        PossessionsToday = 0;
    }

    /// Llamar cuando empieza a llover (transición seco → lluvia).
    public void OnRainStarted()
    {
        if (InShelter && !ShelterLeaks) return;
        if (Skills.IsAtLeast("agua_lluvia", SkillState.Dominado)) { Needs.Miedo += 10f; Needs.Animo += 5f; }
        else Needs.Miedo += 30f;
        Needs.ClampAll();
    }
}

// ───────────────────────── Progreso del Hogar (0-100%) ─────────────────────────
public static class HomeProgress
{
    public static readonly string[] CoreSurvival = { "hacer_fuego", "refugio_palos", "cama_paja", "olla_cocinar" };

    public static float Compute(CharacterState c)
    {
        float skills = CoreSurvival.Count(id => c.Skills.IsAtLeast(id, SkillState.Dominado)) / (float)CoreSurvival.Length;
        float camp = Mathf.Clamp(c.ShelterLevel / 3f, 0f, 1f);
        float emo = c.Skills.IsAtLeast("calmarse_solo", SkillState.Dominado) ? 1f
                  : c.Skills.IsAtLeast("pedir_ayuda", SkillState.Practicando) ? 0.5f : 0f;
        float autonomy = Mathf.Clamp(c.HoursSinceDivineHelp / 72f, 0f, 1f);
        return 100f * (0.35f * skills + 0.25f * camp + 0.20f * emo + 0.20f * autonomy);
    }

    /// Requisitos de habilidad para subir el campamento de nivel (los troncos los cuenta el mundo).
    public static bool CanReachCampLevel(CharacterState c, int level) => level switch
    {
        1 => c.Skills.IsAtLeast("refugio_palos", SkillState.Practicando) && c.Skills.IsAtLeast("hacer_fuego", SkillState.Practicando),
        2 => CanReachCampLevel(c, 1) && c.Skills.IsAtLeast("cama_paja", SkillState.Practicando)
             && c.Skills.IsAtLeast("olla_cocinar", SkillState.Practicando) && c.Skills.IsAtLeast("talar", SkillState.Practicando),
        3 => CanReachCampLevel(c, 2) && c.Skills.IsAtLeast("techo_fogata", SkillState.Practicando)
             && c.Skills.IsAtLeast("agua_lluvia", SkillState.Practicando) && c.Skills.IsAtLeast("talar", SkillState.Dominado),
        _ => level <= 0
    };
}

// ───────────────────────── Influencias ─────────────────────────
public static class Influence
{
    public static float SusurroAcceptance(SusurroIntent intent, CharacterState c, string skillId = null)
    {
        var n = c.Needs; var t = c.Traits;

        if (intent == SusurroIntent.Practicar &&
            (skillId == null || c.Skills.StateOf(skillId) == SkillState.NoAprendido))
            return 0.05f; // no puede practicar lo que no vio

        float afinidad = intent switch
        {
            SusurroIntent.Explorar   => n.Curiosidad * 0.5f * (0.5f + t.Curiosidad / 100f),
            SusurroIntent.Comer      => (100f - n.Saciedad) * 0.5f,
            SusurroIntent.Dormir     => (100f - n.Energia) * 0.5f,
            SusurroIntent.Refugiarse => n.Miedo * 0.5f * (1.5f - t.Valentia / 100f) + n.Humedad * 0.2f,
            SusurroIntent.Acercarse  => t.Empatia * 0.3f + n.Curiosidad * 0.2f,
            SusurroIntent.Evitar     => n.Miedo * 0.3f + (100f - t.Valentia) * 0.2f,
            SusurroIntent.Practicar  => 10f + c.Skills.Get(skillId).Knowledge * 0.3f,
            _ => 0f
        };

        bool proteccion = intent is SusurroIntent.Refugiarse or SusurroIntent.Evitar;
        float miedo = proteccion ? 0f : n.Miedo * 0.4f;
        float vinculo = n.Vinculo * 0.3f * (0.5f + t.Lealtad / 100f);
        float nombre = c.Has("el_nombre") ? 15f : 0f;

        float raw = 20f + afinidad + vinculo - miedo + nombre;
        if (n.Vinculo < 20f) raw *= 0.25f;
        return Mathf.Clamp(raw, 5f, 95f) / 100f;
    }

    /// Si c.Needs.Rendido, el controlador NO ejecuta la acción aunque se acepte.
    public static bool TrySusurro(SusurroIntent intent, CharacterState c, RandomNumberGenerator rng,
                                  out float chance, string skillId = null)
    {
        chance = SusurroAcceptance(intent, c, skillId);
        bool ok = rng.Randf() < chance;
        if (ok)
        {
            c.HoursSinceInfluence = 0f;
            c.Needs.Vinculo = Mathf.Min(100f, c.Needs.Vinculo + 1f);
        }
        return ok;
    }

    public static void ApplyRecuerdo(CharacterState c)
    {
        var n = c.Needs;
        float eff = n.Vinculo < 20f ? 0.5f : 1f;
        n.Miedo -= 30f * eff;
        n.Energia += 10f * eff;
        if (n.Rendido) n.Animo += 15f * eff;
        n.Vinculo += 3f;
        c.RecuerdosAceptados++;
        c.HoursSinceInfluence = 0f;
        c.HoursSinceRecuerdo = 0f;
        n.ClampAll();
    }

    public static void OnObjetoDivinoRecogido(CharacterState c, int day, bool esAntorcha)
    {
        c.HoursSinceInfluence = 0f;
        c.HoursSinceObjetoDivino = 0f;
        if (esAntorcha) { c.HasTorch = true; c.Remember("antorcha_regalada", day); }
    }

    public static void OnAlterarMundo(CharacterState c) => c.HoursSinceInfluence = 0f;

    public static bool RefusesMoralChoice(MoralChoice choice, Traits t) =>
        choice == MoralChoice.Atacar && t.Empatia >= 85f;

    public static float MoralDissonance(MoralChoice choice, Traits t) => choice switch
    {
        MoralChoice.Huir   => t.Valentia > 65f ? -(t.Valentia - 65f) * 0.3f : 0f,
        MoralChoice.Atacar => t.Empatia > 65f ? -(t.Empatia - 65f) * 0.4f : (t.Valentia < 35f ? -5f : 0f),
        MoralChoice.Curar  => t.Empatia < 30f ? -5f : 0f,
        _ => 0f
    };

    public static void ApplyMoralChoice(MoralChoice choice, CharacterState c)
    {
        c.Needs.Vinculo += MoralDissonance(choice, c.Traits);
        c.HoursSinceInfluence = 0f;
        c.Needs.ClampAll();
    }
}
