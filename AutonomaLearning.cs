// AUTONOMA – Sistema de Aprendizaje por Agentes v0.8 (Godot 4.1+ .NET / C#)
// Este archivo: LearningRules (todas las fórmulas) y DemoRecorder (graba tu demo como MemoryEpisodic).
// Los nodos están en su propio archivo (Godot lo exige para poder adjuntarlos):
//   LearningSystem.cs · SkillExecutor.cs · PedagogicalPossession.cs · Interactable.cs · BuildSite.cs
// Rendimiento: los 15 "agentes" son objetos C# (LearningAgent), no nodos. Hay 3 nodos por personaje
// (LearningSystem, SkillExecutor, PedagogicalPossession) sin importar cuántas habilidades existan.
//
// Input Map necesario: move_left, move_right, move_forward, move_back, interact (E).

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Autonoma.DemoActionType;
using static Autonoma.SkillState;

namespace Autonoma;

// ═══════════════════════════ Reglas ═══════════════════════════
public static class LearningRules
{
    public const float DemoCap = 59f;               // mostrando sólo llega a Imitando: dominar lo tiene que hacer él
    public const float DecayEveryHours = 48f, DecayAmount = 10f;
    public const float PanicThreshold = 80f, PanicLoss = 20f, PanicCooldownHours = 12f;
    public const float AutonomyThresholdPracticando = 70f, AutonomyThresholdDominado = 40f;
    public const int FreePossessionsPerDay = 2;

    public static float RateMultiplier(SkillDef d, CharacterState c, bool fromDemo)
    {
        var t = c.Traits; var n = c.Needs; float r = 1f;
        if (t.Is(ReactionProfile.Curioso)) r *= 1.2f;
        if (fromDemo)
        {
            if (t.Is(ReactionProfile.Leal)) r *= 1.15f;
            if (t.Lealtad <= 30f) r *= 0.85f;
            r *= 0.6f + n.Vinculo / 250f;            // confía más en lo que le muestra alguien querido
        }
        else if (t.Lealtad <= 30f) r *= 1.25f;      // aprende haciendo
        if (d.Category == SkillCategory.Emocional && t.Is(ReactionProfile.Empatico)) r *= 1.15f;
        if (d.Category == SkillCategory.Supervivencia && t.Is(ReactionProfile.Valiente)) r *= 1.1f;
        if (d.Id == "esconderse")
        {
            if (t.Is(ReactionProfile.Miedoso)) r *= 1.25f;
            if (t.Is(ReactionProfile.Valiente)) r *= 0.85f;
        }
        else if (t.Is(ReactionProfile.Miedoso) && n.Miedo > 50f) r *= 0.85f;
        if (n.Miedo > 60f) r *= 0.7f;
        if (c.IsSick) r *= 0.8f;
        if (n.Energia < 20f) r *= 0.8f;
        return r;
    }

    public static float DemoGain(LearningAgent a, MemoryEpisodic m, CharacterState c)
    {
        float g = a.Def.DemoGain * m.Quality * RateMultiplier(a.Def, c, true) / a.Def.Difficulty;
        g *= Mathf.Pow(0.8f, a.DemosToday);                     // repetir el mismo día rinde menos
        if (a.EverReachedImitando && a.Knowledge < 30f) g *= 1.3f; // lo recuerda más rápido
        if (m.Completeness < 0.5f) g *= 0.5f;                    // demo confusa
        return g;
    }

    public static float PracticeGain(LearningAgent a, CharacterState c, bool success, float quality)
    {
        float g = success
            ? a.Def.PracticeSuccessGain * (0.5f + 0.5f * quality)
            : a.Def.PracticeFailGain * (c.Traits.Is(ReactionProfile.Resiliente) ? 1.5f : 1f);
        return g * RateMultiplier(a.Def, c, false) / a.Def.Difficulty;
    }

    public static float SuccessChance(LearningAgent a, CharacterState c)
    {
        var n = c.Needs;
        float p = 0.35f + 0.6f * a.Knowledge / 100f
                - 0.004f * Mathf.Max(0f, n.Miedo - 40f)
                - (n.Cojea ? 0.1f : 0f) - (c.IsSick ? 0.1f : 0f) - (n.Humedad > 70f ? 0.05f : 0f);
        if (a.State == Dominado) p = Mathf.Max(p, 0.92f);
        return Mathf.Clamp(p, 0.05f, 0.98f);
    }

    public static float PossessionSeconds(CharacterState c)
    {
        float s = 30f + 15f * c.Needs.Vinculo / 100f;
        if (c.Traits.Lealtad <= 30f) s -= 8f;
        if (c.Needs.Miedo > 80f) s *= 0.5f;
        return Mathf.Max(15f, s);
    }

    /// Cuánto "se resiste" a tu control: el personaje tira hacia otro lado.
    public static float DriftStrength(CharacterState c) => (1f - c.Traits.Lealtad / 100f) * 0.3f;

    public static bool StressImmune(CharacterState c) => c.Traits.Is(ReactionProfile.Resiliente);

    public static bool IsHoldAction(DemoActionType a) =>
        a is Inspeccionar or Agacharse or Sentarse or Respirar or Revolver or Senalar;

    public static string DefaultAnim(DemoActionType a) => a switch
    {
        Recolectar => "recoger", Colocar => "colocar", Martillar => "martillar", Encender => "encender",
        Agacharse => "agacharse", Sentarse => "sentarse", Senalar => "senalar", Respirar => "respirar",
        Inspeccionar => "inspeccionar", Revolver => "revolver", Descartar => "descartar", _ => "inspeccionar"
    };
}

// ═══════════════════════════ Grabador de demostraciones ═══════════════════════════
public sealed class DemoRecorder
{
    const float MinSegment = 1.5f, TurnDegrees = 35f;
    const int MaxPath = 64;

    readonly SkillDef _d;
    readonly MemoryEpisodic _m;
    readonly float[] _credit;
    readonly Dictionary<DemoActionType, int> _extras = new();
    readonly Vector3 _start;
    int _cursor;
    Vector3 _prev, _lastStored, _lastDir;
    float _pathLen, _lastT, _still, _maxIdle;
    bool _disorder;

    public SkillDef Skill => _d;
    public int Cursor => _cursor;
    public RecipeStep CurrentStep => _cursor < _d.Steps.Length ? _d.Steps[_cursor] : null;
    public bool RecipeComplete => _cursor >= _d.Steps.Length;
    public float Progress01 => TotalCredit / Mathf.Max(1, _d.TotalUnits);

    float TotalCredit
    {
        get { float s = 0f; for (int i = 0; i < _credit.Length; i++) s += Mathf.Min(_credit[i], _d.Steps[i].Count); return s; }
    }

    public DemoRecorder(SkillDef d, Vector3 anchor, Vector3 start, CharacterState c, int day, float hour)
    {
        _d = d;
        _m = new MemoryEpisodic { SkillId = d.Id, Day = day, Hour = hour, Anchor = anchor };
        _credit = new float[d.Steps.Length];
        _start = _prev = _lastStored = start;
        _m.Path.Add(new PathPoint(start - anchor, 0f));

        // Lo que ya tiene en el inventario cuenta como "recolectado"
        var inv = c.Inventory.ToDictionary(kv => kv.Key, kv => kv.Value);
        for (int i = 0; i < d.Steps.Length; i++)
        {
            var st = d.Steps[i];
            if (st.Action != Recolectar) continue;
            inv.TryGetValue(st.ItemId, out int have);
            int cov = Math.Min(have, st.Count);
            _credit[i] = cov;
            inv[st.ItemId] = have - cov;
        }
        SkipCredited();
    }

    public void RecordPosition(Vector3 p, float t)
    {
        float dt = t - _lastT; _lastT = t;
        float moved = p.DistanceTo(_prev);
        _pathLen += moved; _prev = p;
        _still = moved < 0.02f ? _still + dt : 0f;
        _maxIdle = Mathf.Max(_maxIdle, _still);

        Vector3 seg = p - _lastStored; seg.Y = 0f;
        float len = seg.Length();
        if (len < 0.5f) return;
        bool turned = _lastDir != Vector3.Zero && Mathf.RadToDeg(_lastDir.AngleTo(seg / len)) > TurnDegrees;
        if (len < MinSegment && !turned) return;

        _m.Path.Add(new PathPoint(p - _m.Anchor, t));
        _lastDir = seg / len; _lastStored = p;
        if (_m.Path.Count > MaxPath) Decimate();
    }

    void Decimate()
    {
        var keep = new List<PathPoint>();
        for (int i = 0; i < _m.Path.Count; i++)
            if (i % 2 == 0 || i == _m.Path.Count - 1) keep.Add(_m.Path[i]);
        _m.Path.Clear(); _m.Path.AddRange(keep);
    }

    /// Devuelve true si la acción era parte de la receta.
    public bool RecordAction(DemoActionType a, string item, Vector3 worldPos, float t)
    {
        _still = 0f;
        bool inRecipe = false;
        if (_cursor < _d.Steps.Length && Matches(_d.Steps[_cursor], a, item))
        {
            _credit[_cursor] += 1f; inRecipe = true;
            SkipCredited();
        }
        else
        {
            for (int k = _cursor + 1; k < _d.Steps.Length; k++)
                if (Matches(_d.Steps[k], a, item) && _credit[k] < _d.Steps[k].Count)
                { _credit[k] += 0.5f; _disorder = true; inRecipe = true; break; }
            if (!inRecipe) _extras[a] = (_extras.TryGetValue(a, out var e) ? e : 0) + 1;
        }

        if (inRecipe && a == Colocar)
            _m.MaterialsUsed[item] = (_m.MaterialsUsed.TryGetValue(item, out var u) ? u : 0) + 1;

        _m.Steps.Add(new DemoStep { Action = a, ItemId = item, LocalPos = worldPos - _m.Anchor, Time = t, InRecipe = inRecipe });
        return inRecipe;
    }

    static bool Matches(RecipeStep s, DemoActionType a, string item) =>
        s.Action == a && (string.IsNullOrEmpty(s.ItemId) || s.ItemId == item);

    void SkipCredited()
    {
        while (_cursor < _d.Steps.Length && _credit[_cursor] >= _d.Steps[_cursor].Count) _cursor++;
    }

    public MemoryEpisodic Finish(float duration)
    {
        _m.DurationSeconds = duration;
        _m.Path.Add(new PathPoint(_prev - _m.Anchor, duration));

        int extras = _extras.Values.Sum();
        _m.Completeness = TotalCredit / Mathf.Max(1, _d.TotalUnits);
        _m.Efficiency = Mathf.Clamp(_d.IdealDemoSeconds / Mathf.Max(duration, 1f), 0f, 1f);
        _m.Clarity = Mathf.Clamp(1f - extras / (float)(_d.TotalUnits + extras) - (_disorder ? 0.15f : 0f), 0f, 1f);
        _m.Quality = 0.6f * _m.Completeness + 0.25f * _m.Efficiency + 0.15f * _m.Clarity;

        // Quirks: tus "manías" que él va a copiar
        float straight = 0f; Vector3 prevP = _start;
        foreach (var s in _m.Steps) { var wp = _m.Anchor + s.LocalPos; straight += prevP.DistanceTo(wp); prevP = wp; }
        if (_pathLen > 8f && _pathLen > 1.8f * Mathf.Max(straight, 1f)) _m.Quirks.Add("rodeo");
        if (_disorder) _m.Quirks.Add("orden_invertido");
        if (_extras.TryGetValue(Martillar, out var hits) && hits >= 2) _m.Quirks.Add("golpes_de_mas");
        if (_maxIdle > 4f) _m.Quirks.Add("pausa_pensativa");
        return _m;
    }
}
