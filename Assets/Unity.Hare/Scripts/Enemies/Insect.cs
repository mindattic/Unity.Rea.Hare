using UnityEngine;
using Unity.Hare.Weapons;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// What obj_butterfly, obj_firefly and obj_fly (gmlHARE) share. None of the three has a parent
    /// or any collision event, so they are ambient swarm life rather than par_enemy: weapons pass
    /// through them, rockets and mines never home on them, and they never touch the ship. Create
    /// drops each one at a random point in the lower half of the room with dX = dY = 1 and sets
    /// Alarm 0 to irandom_range(5, 30); Alarm 0 runs the insect's ai_* script (<see cref="Think"/>),
    /// which picks a new random velocity and applies the bounds rules, then re-arms itself. Step
    /// adds dX/dY to x/y every step. dX/dY are kept in GML units here (px/step, y down) so the ai_*
    /// numbers read the same as the source; movement flips y for Unity.
    /// </summary>
    public abstract class Insect : MonoBehaviour
    {
        /// <summary>Create's alarm[0] = irandom_range(5, 30).</summary>
        public const int FirstThinkMinSteps = 5;
        public const int FirstThinkMaxSteps = 30;
        /// <summary>ai_* bounds: within 10 px of the left/right walls the insect turns back.</summary>
        public const float SideMarginPx = 10f;

        Rect room;

        /// <summary>The room (world units) the ai_* bounds refer to.</summary>
        public Rect Room => room;
        /// <summary>GML dX: px/step, positive right.</summary>
        public float DX { get; protected set; }
        /// <summary>GML dY: px/step, positive down (GML's y axis).</summary>
        public float DY { get; protected set; }
        /// <summary>Steps until Alarm 0 next runs <see cref="Think"/>.</summary>
        public float ThinkCountdownSteps { get; private set; }

        /// <summary>The alarm[0] = irandom_range(5, N) that the Alarm 0 event re-arms with.</summary>
        public abstract int ThinkMaxSteps { get; }

        /// <summary>
        /// GML Create. <paramref name="position"/> overrides the random spot in the room's lower half
        /// (tests pin it); <paramref name="room"/> defaults to <see cref="Gml.Room"/>.
        /// </summary>
        public void Init(Rect? room = null, Vector2? position = null)
        {
            this.room = room ?? Gml.Room;
            // x = random_range(0, room_width); y = random_range(room_height/2, room_height)
            Vector2 p = position ?? new Vector2(
                Random.Range(this.room.xMin, this.room.xMax),
                Random.Range(this.room.yMin, this.room.center.y));
            transform.position = new Vector3(p.x, p.y, transform.position.z);
            DX = 1f;
            DY = 1f;
            ThinkCountdownSteps = Random.Range(FirstThinkMinSteps, FirstThinkMaxSteps + 1);
            OnInit();
        }

        protected virtual void OnInit() { }

        void FixedUpdate() => Tick(Time.fixedDeltaTime);

        /// <summary>One GML step (Alarm 0, then Step), scaled to <paramref name="dt"/> seconds.</summary>
        public void Tick(float dt)
        {
            float steps = dt * Gml.StepsPerSecond;
            ThinkCountdownSteps -= steps;
            // Alarms fire on whole steps; the slack absorbs float drift.
            while (ThinkCountdownSteps <= 1e-3f)
            {
                Think();
                ThinkCountdownSteps += Random.Range(FirstThinkMinSteps, ThinkMaxSteps + 1);
            }

            // Step: x += dX; y += dY (GML y down, Unity y up).
            Vector3 p = transform.position;
            p.x += Gml.Px(DX * steps);
            p.y -= Gml.Px(DY * steps);
            transform.position = p;
        }

        /// <summary>The insect's ai_* script: a new random velocity, then the bounds rules.</summary>
        public abstract void Think();

        /// <summary>GML y &lt; room_height/2.</summary>
        protected bool InUpperHalf => transform.position.y > room.center.y;
        /// <summary>GML y &gt; room_height - <paramref name="px"/>.</summary>
        protected bool NearBottom(float px) => transform.position.y < room.yMin + Gml.Px(px);
        /// <summary>GML x &lt; 10.</summary>
        protected bool NearLeft => transform.position.x < room.xMin + Gml.Px(SideMarginPx);
        /// <summary>GML x &gt; room_width - 10.</summary>
        protected bool NearRight => transform.position.x > room.xMax - Gml.Px(SideMarginPx);
    }
}
