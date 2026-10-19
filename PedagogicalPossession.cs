// AUTONOMA v0.8 – Posesión Pedagógica: 30-45 s de control directo (WASD + E) para enseñar.
// Input Map: move_left, move_right, move_forward, move_back, interact.

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Autonoma.DemoActionType;
using static Autonoma.SkillState;

namespace Autonoma;

public partial class PedagogicalPossession : Node
{
    [Export] public AutonomaSim Sim;
    [Export] public LearningSystem Learning;
    [Export] public SkillExecutor Executor;
    [Export] public CharacterMover Mover;
    [Export] public Camera3D Camera;
    [Export] public float InteractRadius = 2.2f;
    [Export] public float GhostSiteDistance = 2.5f;
    [Export] public float CuriousLureRadius = 6f;   // nodos en grupo "curiosidad" tiran del Curioso
    [Export] public string ActLeft = "move_left", ActRight = "move_right",
                           ActForward = "move_forward", ActBack = "move_back", ActInteract = "interact";

    public bool Active { get; private set; }
    public float Remaining { get; private set; }
    public float HoldProgress01 { get; private set; }
    public SkillDef Teaching { get; private set; }
    public DemoRecorder Recorder => _rec;

    public event Action<SkillDef, float> Started;
    public event Action<RecipeStep, Interactable> HintChanged;  // próximo paso y dónde (null = falta material)
    public event Action<string> Refused;                         // sin_fe, no_te_deja_entrar, bloqueada…
    public event Action<MemoryEpisodic> Ended;

    private DemoRecorder _rec;
    private BuildSite _site;
    private Interactable _holdTarget;
    private RecipeStep _holdStep;
    private float _elapsed, _hold, _driftTimer, _endGrace = -1f;
    private Vector3 _drift;
    private readonly RandomNumberGenerator _rng = new();
    CharacterState C => Sim.Character;

    public override void _Ready() => _rng.Randomize();

    public bool CanStart(string skillId, out string reason)
    {
        reason = "";
        if (Active) reason = "ya_poseyendo";
        else if (!SkillTree.All.ContainsKey(skillId)) reason = "habilidad_desconocida";
        else if (!C.Skills.IsUnlocked(skillId)) reason = "bloqueada";
        else if (C.Skills.IsAtLeast(skillId, Dominado)) reason = "ya_dominada";
        else if (!Sim.Faith.CanAfford(FaithPool.CostPosesion)) reason = "sin_fe";
        else if (C.Needs.Vinculo < 25f) reason = "no_te_deja_entrar";
        else if (C.IsSleeping) reason = "durmiendo";
        return reason == "";
    }

    public bool Start(string skillId)
    {
        if (!CanStart(skillId, out var why)) { Refused?.Invoke(why); return false; }
        Sim.Faith.TrySpend(FaithPool.CostPosesion);
        Teaching = SkillTree.All[skillId];

        Executor.Cancel();
        Sim.EventsPaused = true;
        Mover.Stop();
        Mover.Mode = MoveMode.Direct;
        Remaining = LearningRules.PossessionSeconds(C);
        _elapsed = 0f; _endGrace = -1f; ClearHold();

        Vector3 anchor = Mover.GlobalPosition;
        _site = null;
        if (Teaching.IsBuild)
        {
            Vector3 fwd = Mover.GlobalTransform.Basis.Z * (Mover.ModelFacesPositiveZ ? 1f : -1f);
            Vector3 pos = anchor + fwd * GhostSiteDistance;
            if (!string.IsNullOrEmpty(Teaching.AnchorGroup))
                foreach (Node n in GetTree().GetNodesInGroup(Teaching.AnchorGroup))
                    if (n is Node3D n3 && n3.GlobalPosition.DistanceTo(anchor) < 15f) { pos = n3.GlobalPosition; break; }
            _site = Executor.FindIncompleteSite(skillId, pos, 6f) ?? Executor.SpawnSite(Teaching, pos, ghost: true);
            if (_site != null) { _site.SetGhost(true); anchor = _site.GlobalPosition; }
        }

        _rec = new DemoRecorder(Teaching, anchor, Mover.GlobalPosition, C, Sim.World.Day, Sim.World.HourOfDay);

        C.PossessionsToday++;
        C.HoursSincePossession = 0f;
        C.HoursSinceInfluence = 0f;
        if (C.PossessionsToday > LearningRules.FreePossessionsPerDay)
        { C.Needs.Vinculo -= 5f; C.Needs.Miedo += 10f; C.Needs.ClampAll(); } // se siente usado

        Active = true;
        Started?.Invoke(Teaching, Remaining);
        EmitHint();
        return true;
    }

    public override void _Process(double delta)
    {
        if (!Active) return;
        float dt = (float)delta;
        Remaining -= dt; _elapsed += dt;

        // Movimiento relativo a la cámara isométrica + "deriva" (se resiste un poco)
        Vector2 inp = Input.GetVector(ActLeft, ActRight, ActForward, ActBack);
        Vector3 dir = CameraRelative(inp);
        UpdateDrift(dt);
        Mover.DirectInput = _holdTarget != null || dir.LengthSquared() < 0.01f
            ? Vector3.Zero
            : dir + _drift * dir.Length();

        _rec.RecordPosition(Mover.GlobalPosition, _elapsed);
        HandleInteract(dt);

        if (_rec.RecipeComplete && _endGrace < 0f) _endGrace = 1f; // terminó: un segundo para respirar
        if (_endGrace >= 0f) { _endGrace -= dt; if (_endGrace <= 0f) { End(); return; } }
        if (Remaining <= 0f) End();
    }

    private void HandleInteract(float dt)
    {
        if (Mover.IsActing) return;

        if (_holdTarget != null)
        {
            if (Input.IsActionPressed(ActInteract))
            {
                _hold += dt;
                HoldProgress01 = Mathf.Clamp(_hold / Mathf.Max(0.1f, _holdStep.Duration), 0f, 1f);
                if (_hold >= _holdStep.Duration) { var t = _holdTarget; var s = _holdStep; ClearHold(); Commit(t, s); }
            }
            else ClearHold(); // soltó antes: no cuenta
            return;
        }

        if (!Input.IsActionJustPressed(ActInteract)) return;

        var (target, step) = Resolve();
        if (target == null)
        {
            Mover.PlayAction("inspeccionar", 0.6f);
            _rec.RecordAction(Inspeccionar, "nada", Mover.GlobalPosition, _elapsed); // gesto al aire: baja la claridad
            return;
        }
        if (LearningRules.IsHoldAction(step.Action))
        {
            _holdTarget = target; _holdStep = step; _hold = 0f;
            Mover.FaceTowards(target.GlobalPosition);
            Mover.PlayAction(step.Anim, step.Duration, lockMovement: false);
            return;
        }
        Commit(target, step);
    }

    private void Commit(Interactable target, RecipeStep step)
    {
        Mover.FaceTowards(target.GlobalPosition);
        if (!LearningRules.IsHoldAction(step.Action)) Mover.PlayAction(step.Anim, step.Duration * 0.8f);

        bool synthetic = Array.IndexOf(Teaching.Steps, step) < 0;
        bool performed = synthetic && step.Action == Martillar || target.Perform(step, C, _rng.RandfRange(0.85f, 1f));
        if (!performed) { HintChanged?.Invoke(step, null); return; } // le falta material

        _rec.RecordAction(step.Action, step.ItemId, target.GlobalPosition, _elapsed);
        EmitHint();
    }

    private (Interactable, RecipeStep) Resolve()
    {
        Interactable best = null; RecipeStep bestStep = null;
        int bestPri = int.MaxValue; float bestD = float.MaxValue;
        foreach (Node n in GetTree().GetNodesInGroup("interactable"))
        {
            if (n is not Interactable it || !it.IsInsideTree() || it.IsQueuedForDeletion()) continue;
            float dist = it.GlobalPosition.DistanceTo(Mover.GlobalPosition);
            if (dist > InteractRadius) continue;
            var st = PickStep(it, out int pri);
            if (st == null) continue;
            if (pri < bestPri || (pri == bestPri && dist < bestD)) { best = it; bestStep = st; bestPri = pri; bestD = dist; }
        }
        return (best, bestStep);
    }

    /// 0 = paso actual · 1 = paso posterior (fuera de orden) · 2 = recolectar de más · 3 = golpe de más en la obra
    private RecipeStep PickStep(Interactable it, out int pri)
    {
        var steps = Teaching.Steps; int cur = _rec.Cursor;
        if (cur < steps.Length && it.Handles(steps[cur], Teaching.Id)) { pri = 0; return steps[cur]; }
        for (int k = cur + 1; k < steps.Length; k++)
            if (it.Handles(steps[k], Teaching.Id)) { pri = 1; return steps[k]; }
        if (it.GivesItem != "")
        { pri = 2; return new RecipeStep { Action = Recolectar, ItemId = it.GivesItem, Anim = "recoger", Duration = 0.8f }; }
        if (it is BuildSite)
        { pri = 3; return new RecipeStep { Action = Martillar, ItemId = "golpe", Anim = "martillar", Duration = 0.7f }; }
        pri = int.MaxValue; return null;
    }

    public void End()
    {
        if (!Active) return;
        Active = false;
        ClearHold();
        Mover.DirectInput = Vector3.Zero;
        Mover.Mode = MoveMode.Navigation;
        Mover.Stop();
        Sim.EventsPaused = false;

        if (_site != null)
        {
            if (!_site.AnyPlaced) _site.QueueFree(); // no hiciste nada: no queda obra
            else _site.SetGhost(false);              // lo construido queda; él puede terminarlo después
        }

        var mem = _rec.Finish(_elapsed);
        Learning.RegisterDemonstration(mem);
        Mover.PlayAction("mirar_manos", 2.5f); // "despierta" y se mira las manos
        Ended?.Invoke(mem);
        _rec = null; _site = null; Teaching = null;
    }

    private void EmitHint()
    {
        var step = _rec?.CurrentStep;
        if (step == null) { HintChanged?.Invoke(null, null); return; }
        Interactable best = null; float bestD = float.MaxValue;
        foreach (Node n in GetTree().GetNodesInGroup("interactable"))
            if (n is Interactable it && !it.IsQueuedForDeletion() && it.Handles(step, Teaching.Id))
            {
                float dist = it.GlobalPosition.DistanceTo(Mover.GlobalPosition);
                if (dist < bestD) { best = it; bestD = dist; }
            }
        HintChanged?.Invoke(step, best);
    }

    private void UpdateDrift(float dt)
    {
        _driftTimer -= dt;
        if (_driftTimer > 0f) return;
        _driftTimer = _rng.RandfRange(1.5f, 3f);
        float s = LearningRules.DriftStrength(C);
        _drift = new Vector3(_rng.RandfRange(-1f, 1f), 0f, _rng.RandfRange(-1f, 1f)).Normalized() * s;

        if (C.Traits.Is(ReactionProfile.Curioso)) // la Errante se te va hacia lo que le llama la atención
            foreach (Node n in GetTree().GetNodesInGroup("curiosidad"))
                if (n is Node3D n3 && n3.GlobalPosition.DistanceTo(Mover.GlobalPosition) < CuriousLureRadius)
                {
                    Vector3 pull = n3.GlobalPosition - Mover.GlobalPosition; pull.Y = 0f;
                    if (pull.LengthSquared() > 0.01f) _drift += pull.Normalized() * 0.25f;
                    break;
                }
    }

    private Vector3 CameraRelative(Vector2 inp)
    {
        if (Camera == null) return new Vector3(inp.X, 0f, inp.Y);
        var b = Camera.GlobalTransform.Basis;
        Vector3 fwd = -b.Z; fwd.Y = 0f; fwd = fwd.Normalized();
        Vector3 right = b.X; right.Y = 0f; right = right.Normalized();
        return right * inp.X - fwd * inp.Y;
    }

    private void ClearHold() { _holdTarget = null; _holdStep = null; _hold = 0f; HoldProgress01 = 0f; }
}
