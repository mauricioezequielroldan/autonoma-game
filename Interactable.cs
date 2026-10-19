// AUTONOMA v0.8 – Interactable: algo que se recolecta o una estación donde se hace un paso.

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Autonoma.DemoActionType;
using static Autonoma.SkillState;

namespace Autonoma;

/// Algo que se recolecta (GivesItem) y/o una "estación" donde se hacen pasos con ese TargetGroup.
/// Ejemplos: palo suelto (GivesItem=palo), fogata (StationGroup=fuego), arbusto (ambos), escondite.
public partial class Interactable : Node3D
{
    [Export] public string StationGroup = "";
    [Export] public string GivesItem = "";
    [Export] public int GiveAmount = 1;
    [Export] public int MaxPickups = 1;
    [Export] public bool FreeWhenExhausted = true;

    private int _pickups;

    public override void _Ready()
    {
        AddToGroup("interactable");
        if (StationGroup != "") AddToGroup(StationGroup);
        if (GivesItem != "") AddToGroup("item_" + GivesItem);
    }

    public virtual bool Handles(RecipeStep s, string skillId) =>
        (s.Action == Recolectar && GivesItem != "" && s.ItemId == GivesItem && _pickups < MaxPickups)
        || (s.Action != Recolectar && StationGroup != "" && s.TargetGroup == StationGroup);

    public virtual bool Perform(RecipeStep s, CharacterState c, float quality)
    {
        if (s.Action == Recolectar)
        {
            if (GivesItem == "" || _pickups >= MaxPickups) return false;
            c.AddItem(GivesItem, GiveAmount);
            _pickups++;
            if (_pickups >= MaxPickups)
            {
                RemoveFromGroup("item_" + GivesItem);
                if (FreeWhenExhausted) QueueFree();
            }
            return true;
        }
        if (s.Action == Colocar) return c.TryConsume(s.ItemId, s.ConsumePerAction);
        return true;
    }
}
