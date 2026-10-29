using UnityEngine;
using Unity.Hare.Effects;

namespace Unity.Hare.Weapons
{
    /// <summary>
    /// Ports obj_laser (gmlHARE): a continuous beam that lives while the trigger is held. Every step
    /// it re-anchors at the muzzle (player gun + 34 px along mouse_angle) and runs 2000 px along the
    /// aim, kills one par_enemy on that line (collision_line), and is drawn by draw_beam in
    /// cornflower blue up to the first rock/structure. As in the original, the kill check uses the
    /// full line; only the drawing stops at blockers. Every step it also puffs a small cornflower
    /// ef_flare under everything at the muzzle. The laser gun creates and destroys it.
    /// </summary>
    public class LaserBeam : MonoBehaviour
    {
        static readonly Color Cornflower = new Color(100f / 255f, 149f / 255f, 237f / 255f);

        LaserGun owner;
        LineRenderer glow;
        LineRenderer core;
        float flareSteps;

        public Vector2 Start { get; private set; }
        public Vector2 End { get; private set; }
        /// <summary>Where the drawn beam stops: <see cref="End"/>, or the first <see cref="IBeamBlocker"/>.</summary>
        public Vector2 VisibleEnd { get; private set; }

        public void Init(LaserGun gun)
        {
            owner = gun;
            // draw_beam's stacked widths (8/6/3 cornflower, 1 white) reduced to a glow and a core.
            glow = Gml.AddLine(gameObject, new Color(Cornflower.r, Cornflower.g, Cornflower.b, 0.4f), Gml.Px(6f));
            var coreGo = new GameObject("Core");
            coreGo.transform.SetParent(transform, false);
            core = Gml.AddLine(coreGo, new Color(1f, 1f, 1f, 0.8f), Gml.Px(1f));
            Tick();
            Flare();
        }

        void Update()
        {
            if (owner == null) { Destroy(gameObject); return; }
            Tick();
            Glow(Time.deltaTime);
            Hum();
        }

        /// <summary>
        /// obj_laser's Step: <c>if(!audio_is_playing(sfx_fire_laser)) if(!audio_is_playing(sfx_loop_laser))
        /// audio_play_sound(sfx_loop_laser, 1, 0)</c>, so the hum follows the firing sound and starts over each time it ends.
        /// </summary>
        static void Hum()
        {
            if (Audio.HareAudio.IsPlaying(LaserGun.FireSound) || Audio.HareAudio.IsPlaying(LaserGun.LoopSound)) return;
            Audio.HareAudio.PlayVoice(LaserGun.LoopSound);
        }

        /// <summary>
        /// GML Step's effect_create_below(ef_flare, x1, y1, 0, c_cornflowerblue): one small cornflower flare at the
        /// muzzle every step the beam is on, for the steps <paramref name="dt"/> seconds hold. Returns how many it made.
        /// </summary>
        public int Glow(float dt)
        {
            flareSteps += dt * Gml.StepsPerSecond;
            int made = 0;
            while (flareSteps >= 1f - 1e-4f)
            {
                flareSteps -= 1f;
                Flare();
                made++;
            }
            return made;
        }

        void Flare() => GmlEffects.CreateBelow(GmlEffectKind.Flare, Start, 0, Cornflower);

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
