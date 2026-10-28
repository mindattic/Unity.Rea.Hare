using System;
using UnityEngine;
using Unity.Hare.Player;
using Unity.Hare.Weapons;
using Object = UnityEngine.Object;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// The view on a Core <see cref="EnemyBody"/>: it sends the body's time and contacts to the world as commands
    /// (<see cref="EnemyDirector"/>), shows the body where Core says it is, and plays the events it raises. It holds
    /// no rules and never changes enemy state itself. The weapons reach it through <see cref="IProjectileTarget"/>
    /// and <see cref="IRocketTarget"/>; each turns the contact into the matching strike command.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public abstract class EnemyBodyView : MonoBehaviour, IProjectileTarget, IRocketTarget, IEnemyEventSink, IPresentable
    {
        Rigidbody2D body;
        PlayerShipController player;

        /// <summary>The Core state this view shows (read-only for presentation).</summary>
        public EnemyBody Core { get; private set; }

        public bool IsDead => Core == null || Core.IsDead;
        public Vector2 Position => Core != null ? Core.Position.ToVector2() : (Vector2)transform.position;
        public Rect Room => Core.Room.ToRect();
        /// <summary>GML direction, degrees in [0, 360).</summary>
        public float Direction => Core.Direction;
        /// <summary>GML speed, px/step; negative moves backwards like GML.</summary>
        public float SpeedPxPerStep => Core.SpeedPxPerStep;
        /// <summary>GML image_angle, degrees in [0, 360); turns the hitbox with it.</summary>
        public float ImageAngle => Core.ImageAngle;
        public float ImageAlpha => Core.ImageAlpha;

        /// <summary>GML player. Found in the scene when not given.</summary>
        public PlayerShipController Player
        {
            get => player;
            set => player = value;
        }

        /// <summary>Shapes this view's colliders for its body (the hitbox is presentation; the rules read the bounding box from Core).</summary>
        protected abstract void Configure();

        /// <summary>Plays an event this body raised (the effects every enemy shares are played by the director).</summary>
        public virtual void OnEnemyEvent(EnemyEvent e) { }

        /// <summary>An obj_bullet this body fired has been made.</summary>
        public virtual void OnRound(RoundFired fired, Bullet bullet) { }

        /// <summary>Called once, when the body has died, before the object is removed.</summary>
        protected abstract void OnKilled(EnemyKill kill);

        /// <summary>Shows <paramref name="coreBody"/>: GML Create has run in Core already.</summary>
        public void Attach(EnemyBody coreBody, PlayerShipController ship)
        {
            Core = coreBody;
            if (ship != null) player = ship;
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            EnemyDirector.Register(coreBody.Id, this);
            Configure();
            Show(false);
        }

        void OnDestroy() => EnemyDirector.Dispose(Core, this);

        /// <summary>Shows where Core says the body is after the director stepped the world.</summary>
        void IPresentable.Present()
        {
            if (!IsDead) Show(true);
        }

        /// <summary>One GML step (alarms, Step, then built-in motion), scaled to <paramref name="dt"/> seconds.</summary>
        public void Tick(float dt)
        {
            if (IsDead) return;
            TrackPlayer();
            var result = EnemyDirector.Execute(new TickEnemy(Core.Id, dt));
            if (this != null && result.Body != null && !Core.IsDead) Show(true);
            if (this != null && !IsDead) OnTicked(dt);
        }

        /// <summary>After each unpaused step: a view animates its sprite here (GML image_index += image_speed).</summary>
        protected virtual void OnTicked(float dt) { }

        /// <summary>Sends the nearest ship's position to the world now (GML's global player), finding the ship when none was
        /// given. For driving a body directly, one step at a time, outside the director's own loop (EditMode tests).</summary>
        internal void TrackPlayer()
        {
            RegisterPlayer();
            EnemyDirector.SyncTargets();
        }

        /// <summary>Finds this view's ship (GML's global player) and tells the director to track it, without syncing: the
        /// director's own Step does that once for every tracked ship, not once per view.</summary>
        internal void RegisterPlayer()
        {
            if (player == null) player = Object.FindAnyObjectByType<PlayerShipController>();
            if (player != null) EnemyDirector.Track(player);
        }

        /// <summary>Puts the object where Core says the body is. <paramref name="moving"/> moves the kinematic body (so triggers fire); a jump sets the transform.</summary>
        protected void Show(bool moving)
        {
            Vector2 p = Core.Position.ToVector2();
            if (moving && Application.isPlaying && body != null) body.MovePosition(p);
            else transform.position = new Vector3(p.x, p.y, transform.position.z);
            transform.rotation = Quaternion.Euler(0f, 0f, Core.ImageAngle);
            Draw();
        }

        /// <summary>Draws the body's own sprite (its draw_self); a view with no sprite of its own draws nothing here.</summary>
        protected virtual void Draw() { }

        /// <summary>Runs a command on this body's world and plays its events; shows the body afterwards (a push, a jump).</summary>
        protected EnemyResult Send(EnemyCommand command)
        {
            var result = EnemyDirector.Execute(command);
            if (this != null && Core != null && !Core.IsDead) Show(false);
            return result;
        }

        // ---- Weapon contacts ----

        /// <summary>The ship's own rounds (GML other.owner == player.id): the ones that score.</summary>
        public static bool IsPlayerRound(Bullet bullet) =>
            bullet != null && !bullet.FromEnemy && bullet.Owner != null && bullet.Owner.GetComponentInParent<PlayerShipController>() != null;

        BulletShot ShotOf(Bullet bullet) => bullet == null
            ? new BulletShot(false, false, Core.Position, 0f, 0f)
            : new BulletShot(bullet.FromEnemy, IsPlayerRound(bullet), ((Vector2)bullet.transform.position).ToVec2(), bullet.Direction, bullet.Speed, bullet.GetInstanceID());

        public bool OnProjectileHit(Bullet bullet)
        {
            if (IsDead) return false;
            var result = Send(new StrikeBullet(Core.Id, ShotOf(bullet)));
            if (result.Deflection.HasValue && bullet != null) bullet.SetDirection(result.Deflection.Value);
            return result.Accepted;
        }

        public virtual bool OnRocketHit(Rocket rocket)
        {
            if (IsDead) return false;
            var velocity = rocket != null ? (Gml.Dir(rocket.Direction) * rocket.SpeedPxPerStep).ToVec2() : default;
            var result = Send(new StrikeRocket(Core.Id, rocket != null ? rocket.Position.ToVec2() : Core.Position, velocity));
            if (result.Deflection.HasValue && rocket != null) rocket.Deflect(this, GmlMath.Dir(result.Deflection.Value).ToVector2());
            return result.Accepted;
        }

        /// <summary>Removes the object (the body is already gone from the world).</summary>
        protected void RemoveObject()
        {
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }

        protected void WatchKill(EnemyEvent e)
        {
            if (e is EnemyKilled killed)
            {
                OnKilled(killed.Kill);
                RemoveObject();
            }
        }
    }

    /// <summary>
    /// The view on the par_enemy bodies (the set-piece enemies, their shield, illusion and segments, the dagger). It
    /// routes every weapon to the enemy's own collision event in Core: bullets, rockets and rocket explosions to the
    /// body's own rules. The laser, railgun and flame destroy any par_enemy outright from their side (with(inst)
    /// instance_destroy()), so those kills give no points, pickup or gibs. Touching the ship hurts it (obj_player's
    /// par_enemy collision), through <see cref="HurtPlayer"/>.
    /// </summary>
    public abstract class SetPieceEnemy : EnemyBodyView, IEnemyTarget
    {
        /// <summary>Raised once, just before the enemy is removed.</summary>
        public event Action<SetPieceEnemy, EnemyKill> Killed;

        /// <summary>Starts the enemy: GML instance_create + Create, then shows it. <paramref name="room"/> defaults to room_gamescreen.</summary>
        protected void Begin(EnemyKind kind, Vector2 position, Rect? room, PlayerShipController ship, EnemyOptions options)
        {
            var result = EnemyDirector.Execute(new SpawnEnemy(kind, position.ToVec2(), room?.ToBounds(), options), this);
            Attach(result.Body, ship);
        }

        /// <summary>Hurtful contact with the ship (obj_player's par_enemy collision), routed through Core.</summary>
        protected void AddDamageSource()
        {
            var source = GetComponent<PlayerDamageSource>();
            if (source == null) source = gameObject.AddComponent<PlayerDamageSource>();
            source.HitOverride = health => EnemyDirector.Hurt(health, Core.Id);
        }

        protected CircleCollider2D UseCircle(float radiusPx, Vector2 offsetPx = default)
        {
            var c = GetComponent<CircleCollider2D>();
            if (c == null) c = gameObject.AddComponent<CircleCollider2D>();
            c.isTrigger = true;
            c.radius = Gml.Px(radiusPx);
            c.offset = new Vector2(Gml.Px(offsetPx.x), Gml.Px(offsetPx.y));
            return c;
        }

        protected BoxCollider2D UseBox(Vector2 sizePx, Vector2 offsetPx = default)
        {
            var c = GetComponent<BoxCollider2D>();
            if (c == null) c = gameObject.AddComponent<BoxCollider2D>();
            c.isTrigger = true;
            c.size = new Vector2(Gml.Px(sizePx.x), Gml.Px(sizePx.y));
            c.offset = new Vector2(Gml.Px(offsetPx.x), Gml.Px(offsetPx.y));
            return c;
        }

        /// <summary>An ellipse mask (GML colkind 2) as a capsule.</summary>
        protected CapsuleCollider2D UseEllipse(Vector2 sizePx, Vector2 offsetPx = default)
        {
            var c = GetComponent<CapsuleCollider2D>();
            if (c == null) c = gameObject.AddComponent<CapsuleCollider2D>();
            c.isTrigger = true;
            c.direction = sizePx.x > sizePx.y ? CapsuleDirection2D.Horizontal : CapsuleDirection2D.Vertical;
            c.size = new Vector2(Gml.Px(sizePx.x), Gml.Px(sizePx.y));
            c.offset = new Vector2(Gml.Px(offsetPx.x), Gml.Px(offsetPx.y));
            return c;
        }

        /// <summary>Beams and flame destroy the instance; rockets and explosions run its own events.</summary>
        public void OnWeaponHit(in WeaponHit hit)
        {
            if (IsDead) return;
            switch (hit.Kind)
            {
                case WeaponHitKind.Laser: Send(new StrikeBeam(Core.Id, BeamKind.Laser)); break;
                case WeaponHitKind.Railgun: Send(new StrikeBeam(Core.Id, BeamKind.Railgun)); break;
                case WeaponHitKind.Flame: Send(new StrikeBeam(Core.Id, BeamKind.Flame)); break;
                case WeaponHitKind.Rocket: Send(new StrikeRocket(Core.Id, Core.Position)); break;
                case WeaponHitKind.RocketExplosion: Send(new StrikeExplosion(Core.Id)); break;
            }
        }

        void OnTriggerStay2D(Collider2D other)
        {
            if (IsDead || other == null) return;
            OnTouch(other);
            if (IsDead || !Core.PushesAway) return;
            var enemy = other.GetComponentInParent<IEnemyTarget>() as Component;
            if (enemy == null || enemy.gameObject == gameObject) return;
            PushAwayFrom(enemy.transform.position, Time.fixedDeltaTime * Gml.StepsPerSecond);
        }

        /// <summary>Something overlaps this enemy this step (its own collision events with other objects, e.g. a harpoon's par_rock).</summary>
        protected virtual void OnTouch(Collider2D other) { }

        /// <summary>GML collision with par_enemy: x/y += lengthdir(2, point_direction(other, self)).</summary>
        public void PushAwayFrom(Vector2 other, float steps = 1f) => Send(new Separate(Core.Id, other.ToVec2(), steps));

        /// <summary>GML distance_to_point from this enemy's bounding box, px.</summary>
        public float DistanceToPointPx(Vector2 point) => Core.DistanceToPointPx(point.ToVec2());

        /// <summary>GML distance_to_object(player), the ship taken as a point; infinite without a ship.</summary>
        public float DistanceToPlayerPx() => Core.DistanceToPlayerPx();

        public override void OnEnemyEvent(EnemyEvent e) => WatchKill(e);

        protected override void OnKilled(EnemyKill kill) => Killed?.Invoke(this, kill);

        /// <summary>The GML sprite_index this object draws with draw_self, found through HareArt (U.HARE-T98); null when the view draws it another way.</summary>
        protected virtual string BodySprite => null;
        /// <summary>The sprite's image_index.</summary>
        protected virtual int BodyFrame => 0;
        /// <summary>The object's GML depth (a lower depth draws on top).</summary>
        protected virtual int BodyDepth => -3;

        /// <summary>The body's image_blend as a grey level in [0, 1] (the worm and meatball dim it with their RGB).</summary>
        protected virtual float BodyBlend => 1f;

        System.Collections.Generic.Dictionary<string, SpriteRenderer> layers;

        protected override void Draw()
        {
            string name = BodySprite;
            if (name == null) return;
            var renderer = Unity.Hare.Effects.HareArt.Show(gameObject, name, BodyDepth, Core.ImageAlpha);
            renderer.sprite = Unity.Hare.Effects.HareArt.Frame(name, BodyFrame);
            float g = Mathf.Clamp01(BodyBlend);
            renderer.color = new Color(g, g, g, Core.ImageAlpha);
            DrawLayers(renderer.sortingOrder);
        }

        /// <summary>The draw_sprite_ext calls a Draw event makes before its draw_self: a view shows each with <see cref="DrawLayer"/>. <paramref name="bodyOrder"/> is the body's sorting order.</summary>
        protected virtual void DrawLayers(int bodyOrder) { }

        /// <summary>
        /// One draw_sprite_ext(sprite, 0, x, y, 1, 1, angle, blend, 1) under the body (drawn before draw_self, so beneath it): a child
        /// SpriteRenderer, kept by <paramref name="key"/>, at world <paramref name="position"/> turned to <paramref name="angle"/>.
        /// <paramref name="beneath"/> is how many sorting steps under the body it sits (a later call in the Draw event is a smaller number).
        /// </summary>
        protected SpriteRenderer DrawLayer(string key, string sprite, int bodyOrder, int beneath, Vector2 position, float angle, float blend = 1f, bool visible = true)
        {
            if (layers == null) layers = new System.Collections.Generic.Dictionary<string, SpriteRenderer>();
            if (!layers.TryGetValue(key, out var renderer) || renderer == null)
            {
                var go = new GameObject("Layer_" + key);
                go.transform.SetParent(transform, false);
                renderer = go.AddComponent<SpriteRenderer>();
                layers[key] = renderer;
            }
            var t = renderer.transform;
            t.SetPositionAndRotation(new Vector3(position.x, position.y, transform.position.z), Quaternion.Euler(0f, 0f, angle));
            renderer.sprite = Unity.Hare.Effects.HareArt.Frame(sprite);
            renderer.sortingOrder = bodyOrder - beneath;
            float g = Mathf.Clamp01(blend);
            renderer.color = new Color(g, g, g, 1f);
            renderer.gameObject.SetActive(visible);
            return renderer;
        }
    }

    /// <summary>obj_bullet as the enemies fire it: owner = ID, hurting the ship on contact.</summary>
    public static class EnemyRounds
    {
        /// <summary>spr_bullet's half width.</summary>
        public const float RadiusPx = 4f;
        /// <summary>GML globals buffer_zone: bullets die this far outside the room.</summary>
        public const float BufferZonePx = 400f;

        /// <summary>Every enemy round, as it is fired (sounds and sprites hang off this).</summary>
        public static event Action<Bullet> Fired;

        public static Bullet Spawn(GameObject owner, Vector2 origin, float direction, float speedPxPerStep)
        {
            var go = new GameObject("EnemyBullet");
            go.transform.position = new Vector3(origin.x, origin.y, 0f);
            var bullet = go.AddComponent<Bullet>();
            // In a play room the bullet goes past that room's buffer_zone (U.HARE-T21); with none running, a lifetime long
            // enough to cross room_gamescreen plus buffer_zone stands in for it.
            float reachPx = new Vector2(Gml.RoomWidthPx, Gml.RoomHeightPx).magnitude + 2f * BufferZonePx;
            float speed = Mathf.Max(0.01f, Mathf.Abs(speedPxPerStep));
            bullet.Init(owner, direction, Gml.Px(speedPxPerStep) * Gml.StepsPerSecond, 1, Gml.Steps(reachPx / speed), Gml.Px(RadiusPx));
            bullet.MarkFromEnemy();
            var source = go.AddComponent<PlayerDamageSource>();
            source.DestroyOnHit = true;
            source.HitOverride = health => EnemyDirector.Hurt(health, null);
            Fired?.Invoke(bullet);
            return bullet;
        }
    }
}
