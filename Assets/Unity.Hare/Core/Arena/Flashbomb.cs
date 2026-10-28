using System;
using System.Collections.Generic;
using Unity.Hare.Content;

namespace Unity.Hare.Arena
{
    /// <summary>
    /// obj_flashbomb's numbers (U.HARE-T67), the <c>arena.Flashbomb</c> section. obj_player's key press F (70) makes one with
    /// <c>instance_create(0, 0, obj_flashbomb)</c>: no ammo, no cooldown and no paused or dead check, so every press is
    /// another bomb.
    /// </summary>
    [Serializable]
    public sealed class FlashbombDefinition
    {
        /// <summary>obj_flashbomb's sprite: spr_white (32x32, origin 0,0), scaled by view_wview and view_hview at view 0's corner.</summary>
        public string Sprite = "spr_white";
        /// <summary>obj_flashbomb's depth -1000: over everything drawn in the room.</summary>
        public int Depth = -1000;
        /// <summary>Create: <c>alpha = 0.8; image_alpha = alpha</c>, the flash while it is lit.</summary>
        public float FlashAlpha = 0.8f;
        /// <summary>Create: <c>flash_count = 12</c>, the dark phases Alarm 0 counts before it stops re-arming.</summary>
        public int FlashCount = 12;
        /// <summary>Create: <c>rate = 6; alarm[0] = rate</c>, the steps between Alarm 0's toggles.</summary>
        public int FlashRateSteps = 6;
        /// <summary>Step: <c>radius += 10</c>, the circle that sweeps the enemies and shots.</summary>
        public float RadiusGrowth = 10f;
        /// <summary>Step: <c>radius2 += 8</c>, drawn only.</summary>
        public float Radius2Growth = 8f;
        /// <summary>Step: <c>radius3 += 6</c>, drawn only.</summary>
        public float Radius3Growth = 6f;
        /// <summary>Step: <c>radius4 = 4</c> (set, not added), drawn only.</summary>
        public float Radius4 = 4f;
        /// <summary>Create: <c>burst_alpha = 1</c>, the circles' alpha.</summary>
        public float BurstAlpha = 1f;
        /// <summary>Step: <c>if(burst_alpha &gt; 0) burst_alpha -= 0.01; else instance_destroy()</c>.</summary>
        public float BurstFadePerStep = 0.01f;
        /// <summary>Create: <c>audio_play_sound(sfx_flashbomb, 1, 0)</c>.</summary>
        public string Sound = "sfx_flashbomb";

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Required(errors, path + ".Sprite", Sprite);
            ValidationHelpers.Required(errors, path + ".Sound", Sound);
            if (FlashAlpha <= 0f || FlashAlpha > 1f) errors.Add($"{path}.FlashAlpha: must be above 0 and at most 1 (was {FlashAlpha}).");
            if (BurstAlpha <= 0f || BurstAlpha > 1f) errors.Add($"{path}.BurstAlpha: must be above 0 and at most 1 (was {BurstAlpha}).");
            ValidationHelpers.Positive(errors, path + ".FlashCount", FlashCount);
            ValidationHelpers.Positive(errors, path + ".FlashRateSteps", FlashRateSteps);
            ValidationHelpers.Positive(errors, path + ".RadiusGrowth", RadiusGrowth);
            ValidationHelpers.NonNegative(errors, path + ".Radius2Growth", Radius2Growth);
            ValidationHelpers.NonNegative(errors, path + ".Radius3Growth", Radius3Growth);
            ValidationHelpers.NonNegative(errors, path + ".Radius4", Radius4);
            ValidationHelpers.Positive(errors, path + ".BurstFadePerStep", BurstFadePerStep);
        }
    }

    /// <summary>
    /// One obj_flashbomb (U.HARE-T67). Create: <c>on = true; count = 0</c>, the flash at <c>alpha</c> over view 0, the four
    /// circles at 0, <c>burst_alpha = 1</c>, <c>alarm[0] = rate</c>, obj_rabbit_schematic and sfx_flashbomb. Alarm 0 blinks
    /// it: lit to lit, then dark (<c>count += 1</c>), re-armed while <c>count &lt; flash_count</c>. Each Step grows the
    /// circles round <c>player</c>, sweeps them, and fades <c>burst_alpha</c> by 0.01, destroying it the step after it is
    /// no longer above 0 (about 100 steps, so the blinking ends with the bomb after 8 of its 12 dark phases).
    /// </summary>
    public sealed class Flashbomb
    {
        internal Flashbomb(int id, PlayerId owner, FlashbombDefinition d)
        {
            Id = id;
            Owner = owner;
            On = true;
            Count = 0;
            ImageAlpha = d.FlashAlpha;
            BurstAlpha = d.BurstAlpha;
            Alarm = d.FlashRateSteps;
        }

        public int Id { get; }
        /// <summary>The ship whose F made it (GML: the one <c>player</c>), round which its circles grow.</summary>
        public PlayerId Owner { get; }
        /// <summary>GML on: the state Alarm 0 toggles.</summary>
        public bool On { get; internal set; }
        /// <summary>GML count: the dark phases so far.</summary>
        public int Count { get; internal set; }
        /// <summary>alarm[0]: steps until it fires; 0 once it is no longer armed.</summary>
        public int Alarm { get; internal set; }
        /// <summary>image_alpha, the white flash over view 0 (doubles, as GameMaker's reals are).</summary>
        public double ImageAlpha { get; internal set; }
        /// <summary>burst_alpha, the circles' alpha.</summary>
        public double BurstAlpha { get; internal set; }
        /// <summary>radius, the circle that sweeps (px).</summary>
        public double Radius { get; internal set; }
        public double Radius2 { get; internal set; }
        public double Radius3 { get; internal set; }
        public double Radius4 { get; internal set; }
        /// <summary>instance_destroy() ran.</summary>
        public bool IsGone { get; internal set; }

        /// <summary>The four circles, outermost first, in the order its Draw draws them.</summary>
        public double[] Radii => new[] { Radius, Radius2, Radius3, Radius4 };
    }

    /// <summary>Something the flashbombs did; presentation plays these and never writes back.</summary>
    public abstract class FlashbombEvent
    {
        public Flashbomb Bomb;
    }

    /// <summary>obj_flashbomb's Create: <c>instance_create(0, 0, obj_rabbit_schematic)</c> and <c>audio_play_sound(sfx_flashbomb, 1, 0)</c>.</summary>
    public sealed class FlashbombSetOff : FlashbombEvent
    {
        public string Sound;
    }

    /// <summary>Alarm 0 toggled the flash: <see cref="Flashbomb.ImageAlpha"/> is the new one.</summary>
    public sealed class FlashbombBlinked : FlashbombEvent { }

    /// <summary>
    /// The Step's sweep, after the circles grew: <c>with(par_enemy)</c> each enemy in view inside the circle of
    /// <see cref="Radius"/> round the ship becomes an obj_rocket_explosion, then <c>with(par_projectile)</c> each shot an
    /// obj_tinyexplosion (BIBLE D26: each by its own mask, not the original's any-instance test).
    /// </summary>
    public sealed class FlashbombSwept : FlashbombEvent
    {
        /// <summary>The sweeping circle's radius this step, px.</summary>
        public double Radius;
    }

    /// <summary>burst_alpha ran out: <c>instance_destroy()</c>.</summary>
    public sealed class FlashbombGone : FlashbombEvent { }

    /// <summary>What a <see cref="FlashbombCommand"/> did: whether it took effect and the events it raised, in order.</summary>
    public readonly struct FlashbombResult
    {
        public FlashbombResult(bool accepted, IReadOnlyList<FlashbombEvent> events)
        {
            Accepted = accepted;
            Events = events ?? Array.Empty<FlashbombEvent>();
        }

        public bool Accepted { get; }
        public IReadOnlyList<FlashbombEvent> Events { get; }
    }

    /// <summary>The only way the flashbombs change. Runs in <see cref="FlashbombField.Execute"/>.</summary>
    public abstract class FlashbombCommand
    {
        internal abstract bool Apply(FlashbombField field, List<FlashbombEvent> events);
    }

    /// <summary>obj_player's key press F (70): <c>flashbomb = instance_create(0, 0, obj_flashbomb)</c>. Never refused.</summary>
    public sealed class SetOffFlashbomb : FlashbombCommand
    {
        readonly PlayerId owner;
        public SetOffFlashbomb(PlayerId owner) => this.owner = owner;
        internal override bool Apply(FlashbombField field, List<FlashbombEvent> events) { field.SetOff(owner, events); return true; }
    }

    /// <summary>One room step of every flashbomb (Alarm 0, then Step), paused or not: obj_flashbomb has no paused check.</summary>
    public sealed class StepFlashbombs : FlashbombCommand
    {
        internal override bool Apply(FlashbombField field, List<FlashbombEvent> events) => field.Step(events);
    }

    /// <summary>
    /// Every obj_flashbomb in the room (U.HARE-T67), engine-free. It keeps each bomb's flash, circles and lifetime; what the
    /// sweep hits (enemy and shot masks, view 0) is presentation's, told by <see cref="FlashbombSwept"/>.
    /// </summary>
    public sealed class FlashbombField
    {
        readonly List<Flashbomb> bombs = new List<Flashbomb>();
        int nextId = 1;

        public FlashbombField(FlashbombDefinition definition) => Definition = definition ?? new FlashbombDefinition();

        public FlashbombDefinition Definition { get; }

        /// <summary>The live bombs, oldest first.</summary>
        public IReadOnlyList<Flashbomb> Bombs => bombs;

        public FlashbombResult Execute(FlashbombCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            var events = new List<FlashbombEvent>();
            bool accepted = command.Apply(this, events);
            return new FlashbombResult(accepted, events);
        }

        internal void SetOff(PlayerId owner, List<FlashbombEvent> events)
        {
            var bomb = new Flashbomb(nextId++, owner, Definition);
            bombs.Add(bomb);
            events.Add(new FlashbombSetOff { Bomb = bomb, Sound = Definition.Sound });
        }

        internal bool Step(List<FlashbombEvent> events)
        {
            if (bombs.Count == 0) return false;
            var d = Definition;
            foreach (var bomb in bombs.ToArray())
            {
                // Alarm 0 runs before the Step event.
                if (bomb.Alarm > 0 && --bomb.Alarm == 0) Blink(bomb, d, events);

                bomb.Radius += d.RadiusGrowth;
                bomb.Radius2 += d.Radius2Growth;
                bomb.Radius3 += d.Radius3Growth;
                bomb.Radius4 = d.Radius4;
                events.Add(new FlashbombSwept { Bomb = bomb, Radius = bomb.Radius });

                if (bomb.BurstAlpha > 0) bomb.BurstAlpha -= d.BurstFadePerStep;
                else
                {
                    bomb.IsGone = true;
                    bombs.Remove(bomb);
                    events.Add(new FlashbombGone { Bomb = bomb });
                }
            }
            return true;
        }

        /// <summary>Alarm 0: <c>if(on) { on = false; image_alpha = alpha } else { on = true; image_alpha = 0; count += 1 }</c>, re-armed while <c>count &lt; flash_count</c>.</summary>
        static void Blink(Flashbomb bomb, FlashbombDefinition d, List<FlashbombEvent> events)
        {
            if (bomb.On)
            {
                bomb.On = false;
                bomb.ImageAlpha = d.FlashAlpha;
            }
            else
            {
                bomb.On = true;
                bomb.ImageAlpha = 0;
                bomb.Count += 1;
            }
            if (bomb.Count < d.FlashCount) bomb.Alarm = d.FlashRateSteps;
            events.Add(new FlashbombBlinked { Bomb = bomb });
        }

        /// <summary>The room went away: every bomb with it (obj_flashbomb is not persistent).</summary>
        public void Clear() => bombs.Clear();
    }
}
