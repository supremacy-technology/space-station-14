using Content.Server.Actions;
using Content.Shared.ZLevels.Flight;
using Content.Shared.ZLevels.Flight.Components;

namespace Content.Server.ZLevels.Flight;

public sealed class ZFlightSystem : SharedZFlightSystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ControllableFlightComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ControllableFlightComponent, ComponentRemove>(OnRemove);
    }

    private void OnRemove(Entity<ControllableFlightComponent> ent, ref ComponentRemove args)
    {
        _actions.RemoveAction(ent.Comp.ZLevelUpActionEntity);
        _actions.RemoveAction(ent.Comp.ZLevelDownActionEntity);
        _actions.RemoveAction(ent.Comp.ZLevelToggleActionEntity);
    }

    private void OnMapInit(Entity<ControllableFlightComponent> ent, ref MapInitEvent args)
    {
        if (!ZPhyzQuery.TryComp(ent, out var zPhys))
            return;

        if (!TryComp<ZFlyerComponent>(ent.Owner, out var flyerComp))
            return;

        SetTargetHeight(ent.Owner, zPhys.CurrentZLevel);

        _actions.AddAction(ent, ref ent.Comp.ZLevelUpActionEntity, ent.Comp.UpActionProto);
        _actions.AddAction(ent, ref ent.Comp.ZLevelDownActionEntity, ent.Comp.DownActionProto);
        _actions.AddAction(ent, ref ent.Comp.ZLevelToggleActionEntity, ent.Comp.ToggleActionProto);

        _actions.SetEnabled(ent.Comp.ZLevelDownActionEntity, flyerComp.Active);
        _actions.SetEnabled(ent.Comp.ZLevelUpActionEntity, flyerComp.Active);
    }
}
