using System.Numerics;
using Content.Client.ZLevels.Core;
using Content.Shared.ZLevels.Core.Components;
using Content.Shared.ZLevels.Flight;
using Content.Shared.ZLevels.Flight.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client.ZLevels.Flight;

public sealed class ClientZFlightSystem : SharedZFlightSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ZFlyerComponent, ZPhysicsComponent, TransformComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var flyer, out var zPhys, out var xform, out var sprite))
        {
            if (!flyer.Active)
                continue;
            if (_timing.CurTime < flyer.NextVfx)
                continue;
            flyer.NextVfx = _timing.CurTime + TimeSpan.FromSeconds(0.2f);

            if (flyer.FlightVfx is not null)
            {
                var vfx = SpawnAtPosition(flyer.FlightVfx, xform.Coordinates);
                _sprite.SetOffset(vfx, new Vector2(0, zPhys.LocalPosition * ClientZLevelsSystem.ZLevelOffset) + zPhys.SpriteOffsetDefault);
            }
        }
    }
}
