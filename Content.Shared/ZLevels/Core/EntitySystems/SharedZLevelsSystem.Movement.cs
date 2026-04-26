using System.Numerics;
using Content.Shared.ZLevels.Core.Components;
using Content.Shared.Chasm;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Throwing;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

namespace Content.Shared.ZLevels.Core.EntitySystems;

public abstract partial class SharedZLevelsSystem
{
    public const int MaxZLevelsBelowRendering = 3;

    private const float ZGravityForce = 9.8f;
    private const float ZVelocityLimit = 20.0f;

    /// <summary>
    /// The minimum speed required to trigger LandEvent events.
    /// </summary>
    private const float ImpactVelocityLimit = 3f;

    private EntityQuery<ZLevelHighGroundComponent> _highgroundQuery;

    private void InitMovement()
    {
        _highgroundQuery = GetEntityQuery<ZLevelHighGroundComponent>();

        SubscribeLocalEvent<ZPhysicsComponent, GetZVelocityEvent>(OnGetVelocity);
        SubscribeLocalEvent<ZPhysicsComponent, ZLevelMapMoveEvent>(OnZLevelMapMove);
        SubscribeLocalEvent<ActiveZPhysicsComponent, ComponentInit>(OnActiveInit);

        SubscribeLocalEvent<ZPhysicsComponent, MoveEvent>(OnMoveEvent);
        SubscribeLocalEvent<ZLevelMapComponent, TileChangedEvent>(OnTileChanged);

        SubscribeLocalEvent<DamageableComponent, ZLevelHitEvent>(OnFallDamage);
        SubscribeLocalEvent<PhysicsComponent, ZLevelHitEvent>(OnFallAreaImpact);
    }

    private void OnActiveInit(Entity<ActiveZPhysicsComponent> ent, ref ComponentInit args)
    {
        if (!ZPhyzQuery.TryComp(ent, out var zComp))
            return;
        CacheMovement((ent, zComp));
    }

    private void OnTileChanged(Entity<ZLevelMapComponent> ent, ref TileChangedEvent args)
    {
        if (!TryComp<MapGridComponent>(args.Entity, out var grid))
            return;

        // For each changed tile compute its world AABB and query all entities intersecting it
        foreach (var change in args.Changes)
        {
            var mapCoords = _map.GridTileToWorld(args.Entity, grid, change.GridIndices);

            var half = grid.TileSizeHalfVector;
            var min = mapCoords.Position - half;
            var max = mapCoords.Position + half;
            var aabb = new Box2(min, max);

            var ents = _lookup.GetEntitiesIntersecting(mapCoords.MapId, aabb);
            foreach (var uid in ents)
            {
                if (!ZPhyzQuery.TryComp(uid, out var zComp))
                    continue;

                CacheMovement((uid, zComp));
            }
        }
    }

    private void CacheMovement(Entity<ZPhysicsComponent> ent)
    {
        ent.Comp.CurrentGroundHeight = ComputeGroundHeightInternal((ent, ent), out var sticky);
        ent.Comp.CurrentStickyGround = sticky;
    }

    private void OnMoveEvent(Entity<ZPhysicsComponent> ent, ref MoveEvent args)
    {
        CacheMovement(ent);
    }

    private void OnZLevelMapMove(Entity<ZPhysicsComponent> ent, ref ZLevelMapMoveEvent args)
    {
        ent.Comp.CurrentZLevel = args.CurrentZLevel;
        DirtyField(ent, ent.Comp, nameof(ZPhysicsComponent.CurrentZLevel));
        // Update cached ground height when entity moves between Z-level maps
        CacheMovement(ent);
    }

    private void OnGetVelocity(Entity<ZPhysicsComponent> ent, ref GetZVelocityEvent args)
    {
        args.VelocityDelta -= ZGravityForce * ent.Comp.GravityMultiplier;
    }

    private void OnFallDamage(Entity<DamageableComponent> ent, ref ZLevelHitEvent args) //TODO unhardcode
    {
        var knockdownTime = MathF.Min(args.ImpactPower * 0.25f, 5f);
        _stun.TryKnockdown(ent.Owner, TimeSpan.FromSeconds(knockdownTime));

        var damageType = _proto.Index<DamageTypePrototype>("Blunt");
        var damageAmount = args.ImpactPower * 6f;

        _damage.TryChangeDamage(ent.Owner, new DamageSpecifier(damageType, damageAmount));
    }

    /// <summary>
    /// Cause AoE damage in impact point
    /// </summary>
    private void OnFallAreaImpact(Entity<PhysicsComponent> ent, ref ZLevelHitEvent args)
    {
        var entitiesAround = _lookup.GetEntitiesInRange(ent, 0.25f, LookupFlags.Uncontained);

        foreach (var victim in entitiesAround)
        {
            if (victim == ent.Owner)
                continue;

            var knockdownTime = MathF.Min(args.ImpactPower * ent.Comp.Mass * 0.1f, 10f);
            _stun.TryKnockdown(victim, TimeSpan.FromSeconds(knockdownTime));

            var damageType = _proto.Index<DamageTypePrototype>("Blunt");
            var damageAmount = args.ImpactPower * ent.Comp.Mass * 0.15f;

            _damage.TryChangeDamage(victim, new DamageSpecifier(damageType, damageAmount));
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ZPhysicsComponent, ActiveZPhysicsComponent, TransformComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var zPhys, out _, out var xform, out var physics))
        {
            if (!_zMapQuery.HasComp(xform.MapUid))
                continue;

            var oldVelocity = zPhys.Velocity;
            var oldHeight = zPhys.LocalPosition;

            if (physics.BodyStatus == BodyStatus.OnGround)
            {
                //Velocity application
                var velocityEv = new GetZVelocityEvent((uid, zPhys));
                RaiseLocalEvent(uid, velocityEv);

                zPhys.Velocity += velocityEv.VelocityDelta * frameTime;
            }

            //Movement application
            zPhys.LocalPosition += zPhys.Velocity * frameTime;

            var distanceToGround = zPhys.LocalPosition - zPhys.CurrentGroundHeight;

            // AutoStep: lift entity up if floor is higher
            if (zPhys.AutoStep && distanceToGround < 0)
                zPhys.LocalPosition -= distanceToGround; //Lift up

            // Sticky ground: only pull down when slowly falling on sticky surfaces (ladders)
            if (zPhys.CurrentStickyGround)
                zPhys.LocalPosition -= distanceToGround; //Sticky move down

            if (zPhys.Velocity < 0) //Falling down
            {
                if (distanceToGround <= 0.05f) //There`s a ground
                {
                    if (MathF.Abs(zPhys.Velocity) >= ImpactVelocityLimit)
                    {
                        RaiseLocalEvent(uid, new ZLevelHitEvent(-zPhys.Velocity));
                        var land = new LandEvent(null, true);
                        RaiseLocalEvent(uid, ref land);
                    }

                    zPhys.Velocity = -zPhys.Velocity * zPhys.Bounciness;
                }
            }

            if (zPhys.LocalPosition < 0) //Need teleport to ZLevel down
            {
                var nearestFloor = GetNearestFloorBelow(uid, xform.MapUid, 3);
                if (nearestFloor == null)
                {
                    zPhys.LocalPosition = 0;
                    zPhys.Velocity = 0;
                    DirtyField(uid, zPhys, nameof(ZPhysicsComponent.Velocity));
                    DirtyField(uid, zPhys, nameof(ZPhysicsComponent.LocalPosition));
                    continue;
                }

                if (TryMoveDownOrChasm(uid, nearestFloor.Value))
                {
                    zPhys.LocalPosition += Math.Abs(nearestFloor.Value);

                    if (!zPhys.CurrentStickyGround)
                    {
                        var fallEv = new ZLevelFallMapEvent();
                        RaiseLocalEvent(uid, fallEv);
                    }
                }
            }

            if (zPhys.LocalPosition >= 1) //Need teleport to ZLevel up
            {
                if (HasTileAbove(uid)) //Hit roof
                {
                    if (MathF.Abs(zPhys.Velocity) >= ImpactVelocityLimit)
                    {
                        RaiseLocalEvent(uid, new ZLevelHitEvent(zPhys.Velocity));
                        var land = new LandEvent(null, true);
                        RaiseLocalEvent(uid, ref land);
                    }

                    zPhys.LocalPosition = 1;
                    zPhys.Velocity = -zPhys.Velocity * zPhys.Bounciness;
                }
                else //Move up
                {
                    if (TryMoveUp(uid))
                        zPhys.LocalPosition -= 1;
                }
            }

            if (Math.Abs(zPhys.Velocity) > ZVelocityLimit)
                zPhys.Velocity = MathF.Sign(zPhys.Velocity) * ZVelocityLimit;

            if (Math.Abs(oldVelocity - zPhys.Velocity) > 0.01f)
                DirtyField(uid, zPhys, nameof(ZPhysicsComponent.Velocity));

            if (Math.Abs(oldHeight - zPhys.LocalPosition) > 0.01f)
                DirtyField(uid, zPhys, nameof(ZPhysicsComponent.LocalPosition));
        }
    }

    /// <summary>
    /// Returns the last cached distance to the floor.
    /// </summary>
    /// <param name="target">The entity, the distance to the floor which we calculate</param>
    /// <returns></returns>
    public float DistanceToGround(Entity<ZPhysicsComponent?> target)
    {
        if (!Resolve(target, ref target.Comp, false))
            return 0;

        return target.Comp.LocalPosition - target.Comp.CurrentGroundHeight;
    }

    /// <summary>
    /// Computes the "ground height" relative to the entity's current Z-level.
    /// Returns values where 0 means ground on the same level, -1 means ground one level below,
    /// and intermediate values are possible for high ground entities (stairs).
    /// </summary>
    private float ComputeGroundHeightInternal(Entity<ZPhysicsComponent?> target, out bool stickyGround, int maxFloors = 1)
    {
        stickyGround = false;
        if (!Resolve(target, ref target.Comp, false))
            return 0;

        var xform = Transform(target);
        if (!_zMapQuery.TryComp(xform.MapUid, out var zMapComp))
            return 0;
        if (!_gridQuery.TryComp(xform.MapUid, out var mapGrid))
            return 0;

        var worldPosI = _transform.GetGridOrMapTilePosition(target);
        var worldPos = _transform.GetWorldPosition(target);

        //Select current map by default
        Entity<ZLevelMapComponent> checkingMap = (xform.MapUid.Value, zMapComp);
        var checkingGrid = mapGrid;

        for (var floor = 0; floor <= maxFloors; floor++)
        {
            if (floor != 0) //Select map below
            {
                if (!TryMapOffset((checkingMap.Owner, checkingMap.Comp), -floor, out var tempCheckingMap))
                    continue;
                if (!_gridQuery.TryComp(tempCheckingMap, out var tempCheckingGrid))
                    continue;

                checkingMap = tempCheckingMap.Value;
                checkingGrid = tempCheckingGrid;
            }

            //Check all types of ZHeight entities
            var query = _map.GetAnchoredEntitiesEnumerator(checkingMap, checkingGrid, worldPosI);
            while (query.MoveNext(out var uid))
            {
                if (!_highgroundQuery.TryComp(uid, out var heightComp))
                    continue;

                var dir = _transform.GetWorldRotation(uid.Value).GetCardinalDir();

                var local = new Vector2((worldPos.X % 1 + 1) % 1, (worldPos.Y % 1 + 1) % 1);

                var t = dir switch
                {
                    Direction.East => heightComp.Corner ? (local.X + 1f - local.Y) / 2f : local.X,
                    Direction.West => heightComp.Corner ? (1f - local.X + local.Y) / 2f : 1f - local.X,
                    Direction.North => heightComp.Corner ? (local.X + local.Y) / 2f : local.Y,
                    Direction.South => heightComp.Corner ? (1f - local.X + 1f - local.Y) / 2f : 1f - local.Y,
                    _ => 0.5f,
                };

                t = Math.Clamp(t, 0f, 1f);

                var curve = heightComp.HeightCurve;
                if (curve.Count == 0)
                    continue;

                if (curve.Count == 1)
                {
                    var groundY = curve[0];
                    // groundHeight is negative downwards: -floor + groundY
                    return -floor + groundY;
                }

                var step = 1f / (curve.Count - 1);
                var index = (int)(t / step);
                var frac = (t - index * step) / step;

                var y0 = curve[Math.Clamp(index, 0, curve.Count - 1)];
                var y1 = curve[Math.Clamp(index + 1, 0, curve.Count - 1)];

                var groundYInterp = MathHelper.Lerp(y0, y1, frac);

                if (target.Comp.Velocity < 0 && target.Comp.Velocity > -2f && heightComp.Stick)
                    stickyGround = true;

                return -floor + groundYInterp;
            }

            //No ZEntities found, check floor tiles
            if (_map.TryGetTileRef(checkingMap, checkingGrid, worldPosI, out var tileRef) &&
                !tileRef.Tile.IsEmpty)
                return -floor; // tile ground has groundY == 0 -> -floor
        }

        return -maxFloors;
    }

    /// <summary>
    /// Checks whether there is a ceiling above the specified entity (tiles on the layer above).
    /// If there are no Z-levels above, false will be returned.
    /// </summary>
    [PublicAPI]
    public bool HasTileAbove(EntityUid ent, Entity<ZLevelMapComponent?>? currentMapUid = null)
    {
        currentMapUid ??= Transform(ent).MapUid;

        if (currentMapUid is null)
            return false;

        if (!TryMapUp(currentMapUid.Value, out var mapAboveUid))
            return false;

        if (!_gridQuery.TryComp(mapAboveUid.Value, out var mapAboveGrid))
            return false;

        if (_map.TryGetTileRef(mapAboveUid.Value, mapAboveGrid, _transform.GetWorldPosition(ent), out var tileRef) &&
            !tileRef.Tile.IsEmpty)
            return true;

        return false;
    }

    /// <summary>
    /// Checks whether there is a floor below the specified entity within maxDistance levels.
    /// Returns the offset to the nearest floor (e.g., -1 for immediate level below, -2 for two levels below).
    /// If no floor found within maxDistance, returns null.
    /// </summary>
    [PublicAPI]
    public int? GetNearestFloorBelow(EntityUid ent, Entity<ZLevelMapComponent?>? currentMapUid = null, int maxDistance = 3)
    {
        currentMapUid ??= Transform(ent).MapUid;

        if (currentMapUid is null)
            return null;

        var worldPos = _transform.GetWorldPosition(ent);

        for (int offset = 1; offset <= maxDistance; offset++)
        {
            if (!TryMapOffset(currentMapUid.Value, -offset, out var mapBelowUid))
                break;

            if (!_gridQuery.TryComp(mapBelowUid.Value, out var mapBelowGrid))
                continue;

            if (_map.TryGetTileRef(mapBelowUid.Value, mapBelowGrid, worldPos, out var tileRef) &&
                !tileRef.Tile.IsEmpty)
            {
                return -offset;
            }
        }

        return null;
    }

    /// <summary>
    /// Checks whether there is a tile (floor) exactly one level below the entity.
    /// </summary>
    [PublicAPI]
    public bool HasTileBelow(EntityUid ent, Entity<ZLevelMapComponent?>? currentMapUid = null)
    {
        return GetNearestFloorBelow(ent, currentMapUid, 1) == -1;
    }

    /// <summary>
    /// Checks whether there is a ceiling above the specified entity (tiles on the layer above).
    /// If there are no Z-levels above, false will be returned.
    /// </summary>
    [PublicAPI]
    public bool HasTileAbove(Vector2i indices, Entity<ZLevelMapComponent?> map)
    {
        if (!Resolve(map, ref map.Comp, false))
            return false;

        if (!TryMapUp(map, out var mapAboveUid))
            return false;

        if (!_gridQuery.TryComp(mapAboveUid.Value, out var mapAboveGrid))
            return false;

        if (_map.TryGetTileRef(mapAboveUid.Value, mapAboveGrid, indices, out var tileRef) &&
            !tileRef.Tile.IsEmpty)
            return true;

        return false;
    }

    [PublicAPI]
    public void SetZPosition(Entity<ZPhysicsComponent?> ent, float newPosition)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        ent.Comp.LocalPosition = newPosition;
        DirtyField(ent, ent.Comp, nameof(ZPhysicsComponent.LocalPosition));
    }

    [PublicAPI]
    public void UpdateGravityState(Entity<ZPhysicsComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        var ev = new CheckGravityEvent();
        RaiseLocalEvent(ent.Owner, ev);

        SetZGravity(ent, ev.Gravity);
    }

    private void SetZGravity(Entity<ZPhysicsComponent?> ent, float newGravityMultiplier)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        ent.Comp.GravityMultiplier = newGravityMultiplier;
        DirtyField(ent, ent.Comp, nameof(ZPhysicsComponent.GravityMultiplier));
    }

    /// <summary>
    /// Sets the vertical velocity for the entity. Positive values make the entity fly upward. Negative values make it fly downward.
    /// </summary>
    [PublicAPI]
    public void SetZVelocity(Entity<ZPhysicsComponent?> ent, float newVelocity)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        ent.Comp.Velocity = newVelocity;
        DirtyField(ent, ent.Comp, nameof(ZPhysicsComponent.Velocity));
    }

    /// <summary>
    /// Add the vertical velocity for the entity. Positive values make the entity fly upward. Negative values make it fly downward.
    /// </summary>
    [PublicAPI]
    public void AddZVelocity(Entity<ZPhysicsComponent?> ent, float newVelocity)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        ent.Comp.Velocity += newVelocity;
        DirtyField(ent, ent.Comp, nameof(ZPhysicsComponent.Velocity));
    }

    [PublicAPI]
    public bool TryMove(EntityUid ent, int offset, Entity<ZLevelMapComponent?>? map = null)
    {
        map ??= Transform(ent).MapUid;

        if (map is null)
            return false;

        if (!TryMapOffset(map.Value, offset, out var targetMap))
            return false;

        if (!_mapQuery.TryComp(targetMap, out var targetMapComp))
            return false;


        _transform.SetMapCoordinates(ent, new MapCoordinates(_transform.GetWorldPosition(ent), targetMapComp.MapId));

        var ev = new ZLevelMapMoveEvent(offset, targetMap.Value.Comp.Depth);
        RaiseLocalEvent(ent, ev);

        return true;
    }

    [PublicAPI]
    public bool TryMoveUp(EntityUid ent, int offset = 1)
    {
        return TryMove(ent, offset);
    }

    [PublicAPI]
    public bool TryMoveDown(EntityUid ent, int offset = -1)
    {
        return TryMove(ent, offset);
    }

    [PublicAPI]
    public bool TryMoveDownOrChasm(EntityUid ent, int offset = -1)
    {
        if (TryMoveDown(ent, offset))
            return true;

        //welp, that default Chasm behavior. Not really good, but ok for now.
        if (HasComp<ChasmFallingComponent>(ent))
            return false; //Already falling

        return false;
    }
}

/// <summary>
/// Is called on an entity when it moves between z-levels.
/// </summary>
/// <param name="offset">How many levels were crossed. If negative, it means there was a downward movement. If positive, it means an upward movement.</param>
public sealed class ZLevelMapMoveEvent(int offset, int level) : EntityEventArgs
{
    /// <summary>
    /// How many levels were crossed. If negative, it means there was a downward movement. If positive, it means an upward movement.
    /// </summary>
    public int Offset = offset;

    public int CurrentZLevel = level;
}

/// <summary>
/// Is triggered when an entity falls to the lower z-levels under the force of gravity
/// </summary>
public sealed class ZLevelFallMapEvent : EntityEventArgs;

/// <summary>
/// It is called on an entity when it hits the floor or ceiling with force.
/// </summary>
/// <param name="impactPower">The speed at the moment of impact. Always positive</param>
public sealed class ZLevelHitEvent(float impactPower) : EntityEventArgs
{
    /// <summary>
    /// The speed at the moment of impact. Always positive
    /// </summary>
    public float ImpactPower = impactPower;
}

/// <summary>
/// Is called every frame to calculate the current vertical velocity of the object with CEActiveZPhysicsComponent.
/// </summary>
public sealed class GetZVelocityEvent(Entity<ZPhysicsComponent> target) : EntityEventArgs
{
    public Entity<ZPhysicsComponent> Target = target;
    public float VelocityDelta = 0;
}

/// <summary>
/// Called when UpdateGravityState is used to update the current strength of the active z-level gravity. Various systems can subscribe to this to disable gravity.
/// </summary>
public sealed class CheckGravityEvent : EntityEventArgs
{
    public float Gravity = 1f;
}
