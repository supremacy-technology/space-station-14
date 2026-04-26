using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.ZLevels.Core.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Popups;
using JetBrains.Annotations;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Shared.ZLevels.Core.EntitySystems;

public abstract partial class SharedZLevelsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    //[Dependency] private readonly SharedPopupSystem _popup = default!;

    private EntityQuery<MapComponent> _mapQuery;
    private EntityQuery<ZLevelMapComponent> _zMapQuery;
    private EntityQuery<MapGridComponent> _gridQuery;

    protected EntityQuery<ZPhysicsComponent> ZPhyzQuery;

    public override void Initialize()
    {
        base.Initialize();

        _mapQuery = GetEntityQuery<MapComponent>();
        _zMapQuery = GetEntityQuery<ZLevelMapComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        ZPhyzQuery = GetEntityQuery<ZPhysicsComponent>();

        InitMovement();
        InitView();
        InitializeActivation();
    }

    /// <summary>
    /// Checks whether the map is in the zLevels network. If so, returns true and the current depth + Entity of the current zLevels network.
    /// </summary>
    [PublicAPI]
    public bool TryGetZNetwork(EntityUid mapUid, [NotNullWhen(true)] out Entity<ZLevelsNetworkComponent>? zLevel)
    {
        zLevel = null;
        var query = EntityQueryEnumerator<ZLevelsNetworkComponent>();
        while (query.MoveNext(out var uid, out var zLevelComp))
        {
            if (!zLevelComp.ZLevels.ContainsValue(mapUid))
                continue;

            zLevel = (uid, zLevelComp);
            return true;
        }

        return false;
    }

    [PublicAPI]
    public bool TryMapOffset(Entity<ZLevelMapComponent?> inputMapUid,
        int offset,
        [NotNullWhen(true)] out Entity<ZLevelMapComponent>? outputMapUid)
    {
        outputMapUid = null;
        if (!Resolve(inputMapUid, ref inputMapUid.Comp, false))
            return false;

        var query = EntityQueryEnumerator<ZLevelsNetworkComponent>();
        while (query.MoveNext(out var network))
        {
            if (!network.ZLevels.ContainsValue(inputMapUid))
                continue;

            if (!network.ZLevels.TryGetValue(inputMapUid.Comp.Depth + offset, out var targetMapUid))
                continue;

            if (!_zMapQuery.TryComp(targetMapUid, out var targetZLevelComp))
                continue;

            outputMapUid = (targetMapUid.Value, targetZLevelComp);
            return true;
        }

        return false;
    }

    [PublicAPI]
    public bool TryZNetwork(Entity<ZLevelMapComponent?> inputMapUid,
        [NotNullWhen(true)] out Entity<ZLevelsNetworkComponent>? zNetwork)
    {
        zNetwork = null;
        if (!Resolve(inputMapUid, ref inputMapUid.Comp, false))
            return false;

        var query = EntityQueryEnumerator<ZLevelsNetworkComponent>();
        while (query.MoveNext(out var uid, out var network))
        {
            if (!network.ZLevels.ContainsValue(inputMapUid))
                continue;

            zNetwork = (uid, network);
            return true;
        }

        return false;
    }

    [PublicAPI]
    public bool TryMapUp(Entity<ZLevelMapComponent?> inputMapUid,
        [NotNullWhen(true)] out Entity<ZLevelMapComponent>? aboveMapUid)
    {
        return TryMapOffset(inputMapUid, 1, out aboveMapUid);
    }

    [PublicAPI]
    public bool TryMapDown(Entity<ZLevelMapComponent?> inputMapUid,
        [NotNullWhen(true)] out Entity<ZLevelMapComponent>? belowMapUid)
    {
        return TryMapOffset(inputMapUid, -1, out belowMapUid);
    }

    /// <summary>
    /// Returns a list of all maps above the specified map. The closest map at the top is returned first.
    /// </summary>
    [PublicAPI]
    public List<EntityUid> GetAllMapsAbove(Entity<ZLevelMapComponent> inputMapUid)
    {
        var result = new List<EntityUid>();

        var inputDepth = inputMapUid.Comp.Depth;
        var query = EntityQueryEnumerator<ZLevelsNetworkComponent>();
        while (query.MoveNext(out var network))
        {
            if (!network.ZLevels.ContainsValue(inputMapUid))
                continue;

            result.AddRange(
                network.ZLevels
                    .Where(kv => kv.Value.HasValue && kv.Key > inputDepth)
                    .OrderBy(kv => kv.Key)
                    .Select(kv => kv.Value!.Value)
            );
        }
        return result;
    }

    /// <summary>
    /// Returns a list of all maps below the specified map. The closest map at the bottom is returned first.
    /// </summary>
    [PublicAPI]
    public List<EntityUid> GetAllMapsBelow(Entity<ZLevelMapComponent> inputMapUid)
    {
        var result = new List<EntityUid>();

        var inputDepth = inputMapUid.Comp.Depth;
        var query = EntityQueryEnumerator<ZLevelsNetworkComponent>();
        while (query.MoveNext(out var network))
        {
            if (!network.ZLevels.ContainsValue(inputMapUid))
                continue;

            foreach (var zLevelEnt in network.ZLevels
                         .Where(kv => kv.Value.HasValue && kv.Key < inputDepth)
                         .OrderByDescending(kv => kv.Key)
                         .Select(kv => kv.Value!.Value))
            {
                result.Add(zLevelEnt);
            }
        }

        return result;
    }
}
