using System.Numerics;
using Content.Shared.ZLevels.Core.Components;
using Content.Shared.ZLevels.Core.EntitySystems;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Console;
using Robust.Shared.Enums;

namespace Content.Client.ZLevels.Core;

public sealed class ZLevelDebugOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    private readonly SharedZLevelsSystem _zLevels = default!;
    private readonly SharedTransformSystem _transform = default!;
    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private readonly Font _font;

    public ZLevelDebugOverlay()
    {
        IoCManager.InjectDependencies(this);

        _zLevels = _entityManager.System<SharedZLevelsSystem>();
        _transform = _entityManager.System<SharedTransformSystem>();

        _font = new VectorFont(_cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 8);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var query = _entityManager.EntityQueryEnumerator<ZPhysicsComponent, ActiveZPhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var zPhys, out _, out var xform))
        {
            if (xform.MapUid != xform.ParentUid)
                continue;

            var worldPos = _transform.GetWorldPosition(uid);
            var screenPos = args.ViewportControl?.WorldToScreen(worldPos) ?? Vector2.Zero;

            var localPos = MathF.Round(zPhys.LocalPosition, 2);
            var groundDis = MathF.Round(zPhys.LocalPosition - zPhys.CurrentGroundHeight, 2);
            var velocity = MathF.Round(zPhys.Velocity, 2);
            var sticky = zPhys.CurrentStickyGround;

            var depthText = $"ZLocalHeight: {localPos}\nDistance to ground: {groundDis}\nVelocity: {velocity}\nSticky: {sticky}";

            args.ScreenHandle.DrawString(_font, screenPos, depthText, Color.White);
        }
    }
}

public sealed class ShowZLevelDebugCommand : LocalizedCommands
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    public override string Command => "showzleveldebug";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_overlayManager.HasOverlay<ZLevelDebugOverlay>())
            _overlayManager.RemoveOverlay<ZLevelDebugOverlay>();
        else
            _overlayManager.AddOverlay(new ZLevelDebugOverlay());
    }
}
