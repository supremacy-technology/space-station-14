using Content.Shared.ZLevels.Core.Components;
using Content.Shared.Actions;
using Content.Shared.Maps;
using Robust.Shared.Map;

namespace Content.Shared.ZLevels.Core.EntitySystems;

public abstract partial class SharedZLevelsSystem
{
    [Dependency] protected readonly ITileDefinitionManager TilDefMan = default!;
    private void InitView()
    {
        SubscribeLocalEvent<ZLevelViewerComponent, MoveEvent>(OnViewerMove);
        SubscribeLocalEvent<ZLevelViewerComponent, ToggleZLevelLookUpAction>(OnToggleLookUp);
    }

    protected virtual void OnViewerMove(Entity<ZLevelViewerComponent> ent, ref MoveEvent args)
    {
        if (!ent.Comp.LookUp)
            return;

        if (!HasOpaqueAbove(ent))
            return;

        ent.Comp.LookUp = false;
        DirtyField(ent, ent.Comp, nameof(ZLevelViewerComponent.LookUp));
    }

    private void OnToggleLookUp(Entity<ZLevelViewerComponent> ent, ref ToggleZLevelLookUpAction args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (HasOpaqueAbove(ent))
        {
            _popup.PopupClient(Loc.GetString("zlevel-look-up-fail"), ent, ent);
            return;
        }

        ent.Comp.LookUp = !ent.Comp.LookUp;
        DirtyField(ent, ent.Comp, nameof(ZLevelViewerComponent.LookUp));
    }

    public bool HasOpaqueAbove(EntityUid ent, Entity<ZLevelMapComponent?>? currentMapUid = null)
    {
        currentMapUid ??= Transform(ent).MapUid;

        if (currentMapUid is null)
            return false;

        if (!TryMapUp(currentMapUid.Value, out var mapAboveUid))
            return false;

        if (!_gridQuery.TryComp(mapAboveUid.Value, out var mapAboveGrid))
            return false;

        if (!_map.TryGetTileRef(mapAboveUid.Value, mapAboveGrid, _transform.GetWorldPosition(ent), out var tileRef))
            return false;

        var tileDef = (ContentTileDefinition)TilDefMan[tileRef.Tile.TypeId];

        return !tileDef.Transparent;
    }
}

public sealed partial class ToggleZLevelLookUpAction : InstantActionEvent
{
}
