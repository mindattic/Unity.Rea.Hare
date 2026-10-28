using UnityEngine;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// The view on an <see cref="InsectBody"/> (obj_butterfly, obj_firefly, obj_fly). None of the three has a parent or
    /// any collision event, so they are ambient swarm life rather than par_enemy: weapons pass through them, rockets and
    /// mines never home on them, and they never touch the ship. The ai_* scripts, alarms and bounds rules live in
    /// Core; this sends each step to the world and shows where the insect is.
    /// </summary>
    public abstract class Insect : MonoBehaviour, IPresentable
    {
        public const int FirstThinkMinSteps = InsectBody.FirstThinkMinSteps;
        public const int FirstThinkMaxSteps = InsectBody.FirstThinkMaxSteps;

        /// <summary>The Core state this view shows (read-only for presentation).</summary>
        public InsectBody Core { get; private set; }

        /// <summary>The room (world units) the ai_* bounds refer to.</summary>
        public Rect Room => Core.Room.ToRect();
        /// <summary>GML dX: px/step, positive right.</summary>
        public float DX => Core.DX;
        /// <summary>GML dY: px/step, positive down (GML's y axis).</summary>
        public float DY => Core.DY;
        /// <summary>Steps until Alarm 0 next runs the ai_* script.</summary>
        public float ThinkCountdownSteps => Core.ThinkCountdownSteps;
        /// <summary>The alarm[0] = irandom_range(5, N) that the Alarm 0 event re-arms with.</summary>
        public int ThinkMaxSteps => Core.ThinkMaxSteps;

        protected abstract InsectKind Kind { get; }

        /// <summary>
        /// GML Create. <paramref name="position"/> overrides the random spot in the room's lower half (tests pin it);
        /// <paramref name="room"/> defaults to room_gamescreen.
        /// </summary>
        public void Init(Rect? room = null, Vector2? position = null)
        {
            var result = EnemyDirector.Execute(new SpawnInsect(Kind, room?.ToBounds(), position?.ToVec2()), this);
            Core = result.Insect;
            EnemyDirector.Register(Core.Id, this);
            Show();
            var renderer = Effects.HareArt.Show(gameObject, SpriteName, 0); // the object's sprite_index at depth 0 (U.HARE-T95)
            if (AnimationFrames > 1) anim = new Effects.SpriteAnimation(SpriteName, AnimationFrames, AnimationSpeed, renderer);
            OnShown();
        }

        /// <summary>The GML sprite the object starts with (spr_butterfly, spr_firefly, spr_fly).</summary>
        protected abstract string SpriteName { get; }

        protected virtual void OnShown() { }

        /// <summary>Frames to loop while unpaused (0 or 1: the sprite stays on frame 0).</summary>
        protected virtual int AnimationFrames => 0;
        /// <summary>GML image_speed while unpaused.</summary>
        protected virtual float AnimationSpeed => 1f;
        Effects.SpriteAnimation anim;

        void OnDestroy() => EnemyDirector.Dispose(Core, this);

        void IPresentable.Present()
        {
            Show();
            OnShown();
        }

        /// <summary>One GML step (Alarm 0, then Step), scaled to <paramref name="dt"/> seconds.</summary>
        public void Tick(float dt)
        {
            EnemyDirector.Execute(new TickInsect(Core.Id, dt));
            anim?.AdvanceLooping(Effects.SpriteAnimation.StepsIn(dt));
            Show();
            OnShown();
        }

        /// <summary>Runs the insect's ai_* script now: a new random velocity, then the bounds rules.</summary>
        public void Think()
        {
            EnemyDirector.Execute(new ThinkInsect(Core.Id));
            OnShown();
        }

        /// <summary>Puts the insect somewhere (GML x/y = ...).</summary>
        public void Place(Vector2 position)
        {
            EnemyDirector.Execute(new PlaceBody(Core.Id, position.ToVec2()));
            Show();
        }

        void Show()
        {
            Vector2 p = Core.Position.ToVector2();
            transform.position = new Vector3(p.x, p.y, transform.position.z);
        }
    }
}
