using UnityEngine;

namespace Unity.Hare.Weapons
{
    /// <summary>
    /// Ports obj_laser (gmlHARE): a continuous beam that lives while the trigger is held. Every step
    /// it re-anchors at the muzzle (player gun + 34 px along mouse_angle) and runs 2000 px along the
    /// aim, kills one par_enemy on that line (collision_line), and is drawn by draw_beam in
    /// cornflower blue up to the first rock/structure. As in the original, the kill check uses the
    /// full line; only the drawing stops at blockers. The loadout creates and destroys it.
    /// </summary>
    public class LaserBeam : MonoBehaviour
    {
        static readonly Color Cornflower = new Color(100f / 255f, 149f / 255f, 237f / 255f);

        AirWeaponLoadout owner;
        LineRenderer glow;
        LineRenderer core;

        public Vector2 Start { get; private set; }
        public Vector2 End { get; private set; }
        /// <summary>Where the drawn beam stops: <see cref="End"/>, or the first <see cref="IBeamBlocker"/>.</summary>
        public Vector2 VisibleEnd { get; private set; }

        public void Init(AirWeaponLoadout loadout)
        {
            owner = loadout;
            // draw_beam's stacked widths (8/6/3 cornflower, 1 white) reduced to a glow and a core.
            glow = Gml.AddLine(gameObject, new Color(Cornflower.r, Cornflower.g, Cornflower.b, 0.4f), Gml.Px(6f));
            var coreGo = new GameObject("Core");
            coreGo.transform.SetParent(transform, false);
            core = Gml.AddLine(coreGo, new Color(1f, 1f, 1f, 0.8f), Gml.Px(1f));
            Tick();
        }

        void Update()
        {
            if (owner == null) { Destroy(gameObject); return; }
            Tick();
        }

        /// <summary>One GML Step: follow the aim, kill the enemy on the line, update the drawing.</summary>
        public void Tick()
        {
            Start = owner.MuzzlePosition;
            End = owner.BeamEndPosition;
            transform.position = Start;

            var enemy = Gml.FirstAlong<IEnemyTarget>(Start, End, out var hitPoint);
            enemy?.OnWeaponHit(new WeaponHit(WeaponHitKind.Laser, hitPoint, (End - Start).normalized));

            Gml.FirstAlong<IBeamBlocker>(Start, End, out var blockedAt);
            VisibleEnd = blockedAt;

            SetLine(glow);
            SetLine(core);
        }

        void SetLine(LineRenderer line)
        {
            if (line == null) return;
            line.SetPosition(0, Start);
            line.SetPosition(1, VisibleEnd);
        }
    }
}
