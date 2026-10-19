// AUTONOMA v0.8 – SkillExecutor: el cuerpo ejecuta UNA habilidad (imitando, practicando o dominada).

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Autonoma.DemoActionType;
using static Autonoma.SkillState;

namespace Autonoma;

public partial class SkillExecutor : Node
{
    [Export] public AutonomaSim Sim;
    [Export] public LearningSystem Learning;
    [Export] public CharacterMover Mover;
    [Export] public Node3D Camp;                                   // centro del campamento
    [Export] public Godot.Collections.Array<string> BuildSkillIds = new();      // "refugio_palos", "hacer_fuego"…
    [Export] public Godot.Collections.Array<PackedScene> BuildScenes = new();    // misma posición que el id
    [Export] public float RadiusImitando = 15f, RadiusPracticando = 30f, RadiusDominado = 50f;

    public bool Busy { get; private set; }
    public LearningAgent Current { get; private set; }
    public event Action<LearningAgent, bool, float> Finished;   // agente, éxito, calidad
    public event Action<LearningAgent, int, int> StepProgress;  // paso, total
    public event Action<CharacterState> HelpRequested;          // "Pedir Ayuda": marcar en el mapa

    private int _token;
    private readonly RandomNumberGenerator _rng = new();
    CharacterState C => Sim.Character;

    private sealed class PlanStep
    {
        public RecipeStep Step;
        public bool IsQuirk;
        public Vector3? LiteralPos;
        public readonly List<Vector3> PathBefore = new();
    }

    public override void _Ready() => _rng.Randomize();

    public void Cancel()
    {
        _token++;
        Busy = false; Current = null;
        if (Mover != null) Mover.ExtraSpeedFactor = 1f;
    }

    public async void Run(LearningAgent a, bool autonomous)
    {
        if (Busy) return;
        int token = ++_token;
        Busy = true; Current = a;
        try { await Execute(a, autonomous, token); }
        catch (Exception e) { GD.PushError($"[AUTONOMA] SkillExecutor: {e}"); }
        finally
        {
            if (token == _token) { Busy = false; Current = null; Mover.ExtraSpeedFactor = 1f; }
        }
    }

    private async Task<bool> Execute(LearningAgent a, bool autonomous, int token)
    {
        var d = a.Def; var state = a.State; var tpl = a.Template;
        float P = LearningRules.SuccessChance(a, C);
        Mover.ExtraSpeedFactor = state == Imitando ? 0.8f : state == Dominado ? 1.1f : 1f;

        Vector3 anchor = ResolveAnchor(a, state, tpl);
        var plan = BuildPlan(a, state, tpl, anchor);
        if (plan.Count == 0) return false;
        float pf = 1f - Mathf.Pow(P, 1f / plan.Count); // falla por paso, así el total da P

        BuildSite site = null;
        if (d.IsBuild)
        {
            site = FindIncompleteSite(d.Id, anchor, 6f) ?? SpawnSite(d, anchor, ghost: false);
            if (site == null) return false;
        }

        float startHours = Sim.World.TotalHours;
        float qSum = 0f; int qN = 0;
        bool ok = true, quirkShown = false;

        for (int i = 0; i < plan.Count; i++)
        {
            var ps = plan[i];
            foreach (var wp in ps.PathBefore)
            {
                await MoveTo(wp, token, 6f); // copia tu recorrido (rodeos incluidos)
                if (token != _token) return false;
            }

            var target = ResolveTarget(ps, d.Id, site, state, out Vector3 pos);
            if (target == null && !ps.IsQuirk)
            {
                if (ps.Step.Action == Recolectar && C.Count(ps.Step.ItemId) > 0) continue; // ya lo tenía
                ok = false; await Confused(token); break;
            }

            if (!await MoveTo(Approach(pos), token, 20f))
            {
                if (token != _token) return false;
                ok = false; await Confused(token); break;
            }
            Mover.FaceTowards(pos);

            bool stepOk = _rng.Randf() >= pf;
            if (!stepOk)
            {
                await Perform(ps.Step, token, state); // el intento fallido se ve
                if (token != _token) return false;
                if (state == Imitando) { ok = false; await Confused(token); break; } // imitando no sabe corregir
                stepOk = _rng.Randf() >= pf; // reintento
            }
            await Perform(ps.Step, token, state);
            if (token != _token) return false;

            // "La mejor ayuda es no ayudar": una antorcha divina durante su primer fuego lo interrumpe
            if (d.Id == "hacer_fuego" && !a.FirstAutonomousDone && C.HoursSinceObjetoDivino < Sim.World.TotalHours - startHours)
                return false;

            if (ps.IsQuirk) { quirkShown = true; continue; } // gesto copiado: sin efecto

            float q = stepOk ? _rng.RandfRange(0.65f, 1f) : _rng.RandfRange(0.3f, 0.55f);
            bool applied = target == null || target.Perform(ps.Step, C, q);
            if (!applied) { ok = false; await Confused(token); break; }
            qSum += q; qN++;
            StepProgress?.Invoke(a, i + 1, plan.Count);
        }

        float quality = qN > 0 ? qSum / qN : 0f;
        bool success = ok && (site == null || site.IsComplete);
        Learning.RegisterPractice(a, success, quality, autonomous);
        if (success) ApplyCompletionEffects(d, site);
        if (state == Imitando && quirkShown && !a.FirstQuirkShown)
        {
            a.FirstQuirkShown = true;
            Sim.FireExternal("te_imita");
        }
        Finished?.Invoke(a, success, quality);
        return success;
    }

    // ── Plan: Imitando copia tu demo; Practicando sigue la receta; Dominado la optimiza ──
    private List<PlanStep> BuildPlan(LearningAgent a, SkillState state, MemoryEpisodic tpl, Vector3 anchor)
    {
        var d = a.Def;
        var plan = new List<PlanStep>();

        if (state == Imitando && tpl != null && tpl.Steps.Count > 0)
        {
            float prevT = 0f;
            foreach (var ds in tpl.Steps)
            {
                var rs = ds.InRecipe ? d.Steps.FirstOrDefault(r => r.Action == ds.Action && r.ItemId == ds.ItemId) : null;
                var ps = new PlanStep
                {
                    Step = rs ?? QuirkStep(ds.Action, ds.ItemId),
                    IsQuirk = rs == null,
                    LiteralPos = anchor + ds.LocalPos, // donde VOS lo hiciste
                };
                foreach (var pp in tpl.Path)
                    if (pp.Time > prevT && pp.Time < ds.Time) ps.PathBefore.Add(anchor + pp.LocalPos);
                prevT = ds.Time;
                plan.Add(ps);
            }
            return plan;
        }

        var inv = C.Inventory.ToDictionary(kv => kv.Key, kv => kv.Value);
        foreach (var rs in d.Steps)
            for (int k = 0; k < rs.Count; k++)
            {
                if (rs.Action == Recolectar && inv.TryGetValue(rs.ItemId, out int have) && have > 0)
                { inv[rs.ItemId] = have - 1; continue; }
                plan.Add(new PlanStep { Step = rs });
            }

        if (state == Dominado) plan = plan.OrderBy(p => p.Step.Action == Recolectar ? 0 : 1).ToList(); // junta todo primero

        // La "firma del maestro": un gesto tuyo que conserva siempre
        switch (a.SignatureQuirk)
        {
            case "golpes_de_mas":
                int last = plan.FindLastIndex(p => p.Step.Action == Martillar);
                if (last >= 0) plan.Insert(last + 1, new PlanStep { Step = QuirkStep(Martillar, "firma"), IsQuirk = true });
                break;
            case "pausa_pensativa":
                plan.Insert(plan.Count / 2, new PlanStep { Step = QuirkStep(Inspeccionar, "firma", 2f), IsQuirk = true });
                break;
            case "rodeo" when state == Practicando && tpl != null && plan.Count > 0:
                foreach (var pp in tpl.Path.Take(tpl.Path.Count / 2)) plan[0].PathBefore.Add(tpl.Anchor + pp.LocalPos);
                break;
        }
        return plan;
    }

    private static RecipeStep QuirkStep(DemoActionType a, string item, float dur = 0.8f) =>
        new() { Action = a, ItemId = item, Anim = LearningRules.DefaultAnim(a), Duration = dur };

    private Interactable ResolveTarget(PlanStep ps, string skillId, BuildSite site, SkillState state, out Vector3 pos)
    {
        var s = ps.Step;
        Vector3 center = state == Imitando && ps.LiteralPos.HasValue ? ps.LiteralPos.Value : Mover.GlobalPosition;
        float r = state == Imitando ? RadiusImitando : state == Dominado ? RadiusDominado : RadiusPracticando;

        if (ps.IsQuirk) { pos = ps.LiteralPos ?? (site?.GlobalPosition ?? Mover.GlobalPosition); return null; }

        Interactable it;
        if (s.Action == Recolectar) it = FindNearest("item_" + s.ItemId, center, r, x => x.Handles(s, skillId));
        else if (s.TargetGroup == null) it = site;
        else it = FindNearest(s.TargetGroup, center, r, x => x.Handles(s, skillId));

        pos = it?.GlobalPosition ?? center;
        return it;
    }

    private Vector3 ResolveAnchor(LearningAgent a, SkillState state, MemoryEpisodic tpl)
    {
        var d = a.Def;
        Vector3 self = Mover.GlobalPosition;

        if (!string.IsNullOrEmpty(d.AnchorGroup))
        {
            var n = FindNearest(d.AnchorGroup, self, 60f, _ => true);
            if (n != null) return n.GlobalPosition;
        }
        if (state == Imitando && tpl != null) return tpl.Anchor; // construye exactamente donde vos
        if (state == Practicando && tpl != null)
        {
            if (Camp != null && Camp.GlobalPosition.DistanceTo(self) < tpl.Anchor.DistanceTo(self))
                return SnapToNav(Camp.GlobalPosition + Ring(3f, 6f));
            return tpl.Anchor;
        }
        // Dominado: elige su propio lugar
        Vector3 fwd = Mover.GlobalTransform.Basis.Z * (Mover.ModelFacesPositiveZ ? 1f : -1f);
        Vector3 p = Camp != null && Camp.GlobalPosition.DistanceTo(self) < 25f
            ? Camp.GlobalPosition + Ring(3f, 7f)
            : self + fwd * 2.5f;
        return SnapToNav(p);
    }

    private void ApplyCompletionEffects(SkillDef d, BuildSite site)
    {
        var n = C.Needs;
        switch (d.Id)
        {
            case "refugio_palos": C.HasBuiltShelter = true; break;
            case "cama_paja": C.HasBed = true; break;
            case "olla_cocinar": n.Saciedad += 35f; if (C.IsSick) C.SickHours = Mathf.Max(0f, C.SickHours - 12f); break;
            case "identificar_bayas": n.Saciedad += 10f; break;
            case "antorcha": C.HasTorch = true; break;
            case "agua_lluvia": C.HasWater = true; break;
            case "calmarse_solo": n.Miedo -= 30f; C.HoursSinceSelfCalm = 0f; break;
            case "esconderse": C.IsHidden = true; break; // el mundo lo apaga al moverse
            case "pedir_ayuda": C.Remember("pidio_ayuda", Sim.World.Day); HelpRequested?.Invoke(C); break;
            case "techo_fogata":
                if (site != null)
                    foreach (Node node in GetTree().GetNodesInGroup("campfire"))
                        if (node is Campfire f && f.GlobalPosition.DistanceTo(site.GlobalPosition) < 3f) f.HasRoof = true;
                break;
        }
        n.ClampAll();
    }

    // ── Utilidades de mundo ──
    public BuildSite SpawnSite(SkillDef d, Vector3 pos, bool ghost)
    {
        int idx = BuildSkillIds.IndexOf(d.Id);
        if (idx < 0 || idx >= BuildScenes.Count) { GD.PushWarning($"[AUTONOMA] Falta escena para {d.Id}"); return null; }
        var site = BuildScenes[idx].Instantiate<BuildSite>();
        GetTree().CurrentScene.AddChild(site);
        site.GlobalPosition = pos;
        site.SetGhost(ghost);
        return site;
    }

    public BuildSite FindIncompleteSite(string skillId, Vector3 near, float radius)
    {
        foreach (Node n in GetTree().GetNodesInGroup("buildsite_" + skillId))
            if (n is BuildSite b && !b.IsComplete && b.GlobalPosition.DistanceTo(near) <= radius) return b;
        return null;
    }

    private Interactable FindNearest(string group, Vector3 from, float radius, Func<Interactable, bool> filter)
    {
        Interactable best = null; float bestD = radius;
        foreach (Node n in GetTree().GetNodesInGroup(group))
        {
            if (n is not Interactable it || !it.IsInsideTree() || it.IsQueuedForDeletion() || !filter(it)) continue;
            float dist = it.GlobalPosition.DistanceTo(from);
            if (dist <= bestD) { best = it; bestD = dist; }
        }
        return best;
    }

    private Vector3 Approach(Vector3 target)
    {
        Vector3 dir = Mover.GlobalPosition - target; dir.Y = 0f;
        return dir.LengthSquared() < 0.01f ? target : target + dir.Normalized() * 0.9f;
    }

    private Vector3 SnapToNav(Vector3 p) => NavigationServer3D.MapGetClosestPoint(Mover.GetWorld3D().NavigationMap, p);

    private Vector3 Ring(float rMin, float rMax)
    {
        float ang = _rng.RandfRange(0f, Mathf.Tau), r = _rng.RandfRange(rMin, rMax);
        return new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
    }

    private async Task<bool> MoveTo(Vector3 p, int token, float timeout)
    {
        Mover.GoTo(p);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        float t = 0f;
        while (!Mover.Arrived)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (token != _token) return false;
            t += (float)GetPhysicsProcessDeltaTime();
            if (t > timeout) { Mover.Stop(); return false; }
        }
        return true;
    }

    private async Task Perform(RecipeStep s, int token, SkillState state)
    {
        float dur = s.Duration * (state == Imitando ? 1.3f : state == Dominado ? 0.8f : 1f);
        Mover.PlayAction(s.Anim, dur);
        await ToSignal(GetTree().CreateTimer(dur), SceneTreeTimer.SignalName.Timeout);
    }

    private async Task Confused(int token)
    {
        if (token != _token) return;
        Mover.PlayAction("confundido", 1.5f);
        C.Needs.Miedo = Mathf.Min(100f, C.Needs.Miedo + 3f);
        await ToSignal(GetTree().CreateTimer(1.5f), SceneTreeTimer.SignalName.Timeout);
    }
}
