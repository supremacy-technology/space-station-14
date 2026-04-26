using System.Linq;
using Content.Server.ZLevels.Core;
using Content.Shared.ZLevels.Core.Components;
using Content.Shared.ZLevels.Roof;
using Content.Shared.Light.Components;
using Content.Shared.Maps;

namespace Content.Server.ZLevels.Roof;

/// <inheritdoc/>
public sealed class RoofSystem : ZSharedRoofSystem
{
    private readonly HashSet<Vector2i> _roofMap = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZLevelsNetworkComponent, ZLevelNetworkUpdatedEvent>(OnNetworkUpdated);
    }

    private void OnNetworkUpdated(Entity<ZLevelsNetworkComponent> ent, ref ZLevelNetworkUpdatedEvent args)
    {
        RecalculateNetworkRoofs(ent);
    }

    public void RecalculateNetworkRoofs(Entity<ZLevelsNetworkComponent> network)
    {
        _roofMap.Clear();

        List<EntityUid> sortedMaps = new();
        foreach (var mapUid in network.Comp.ZLevels
                     .OrderByDescending(kv => kv.Key) // depth sorting
                     .Select(kv => kv.Value)
                     .Where(uid => uid.HasValue)
                     .Select(uid => uid!.Value))
        {
            sortedMaps.Add(mapUid);
        }

        foreach (var map in sortedMaps)
        {
            if (!GridQuery.TryComp(map, out var mapGrid))
                continue;

            var enumerator = Map.GetAllTilesEnumerator(map, mapGrid);
            var roofComp = EnsureComp<RoofComponent>(map);

            while (enumerator.MoveNext(out var tileRef))
            {
                Roof.SetRoof((map, mapGrid, roofComp), tileRef.Value.GridIndices, _roofMap.Contains(tileRef.Value.GridIndices));

                var tileDef = (ContentTileDefinition)TilDefMan[tileRef.Value.Tile.TypeId];

                if (!tileDef.Transparent)
                    _roofMap.Add(tileRef.Value.GridIndices);
            }
        }
    }
}
