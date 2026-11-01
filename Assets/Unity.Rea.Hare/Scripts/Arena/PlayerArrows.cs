using System.Collections.Generic;
using UnityEngine;
using Unity.Rea.Hare.Art;
using Unity.Rea.Hare.Effects;
using Unity.Rea.Hare.Player;
using Unity.Rea.Hare.Rendering;

namespace Unity.Rea.Hare.Arena
{
    /// <summary>
    /// obj_player's Draw event's arrows (U.HARE-T52), drawn for the arena's ship while it is not dead:
    /// <list type="bullet">
    /// <item>spr_direction_arrow on the ship, turned to its direction at alpha 0.5, while it spins (<c>spin != 0</c>);</item>
    /// <item>spr_big_arrow 64 px inside the edge the ship has flown out of the room by, level with it and turned to its
    /// direction at alpha 0.5, unless it has just spawned (<c>!justspawned</c>);</item>
    /// <item>the leap line: while perched (<c>state_idle</c>) with show_leap_arrow and can_jump, a cornflower-blue dotted
    /// line (4 px dashes, 1 px wide, alpha 0.3) from the gun (<c>lengthdir(-13, image_angle)</c> from the ship) to the
    /// mouse.</item>
    /// </list>
    /// It reads the ship's state and the arena's numbers and never changes either. Every sprite is a world object, so the
    /// minimap draws them too, as GameMaker ran the Draw event once per view.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerArrows : MonoBehaviour
    {
        /// <summary>obj_player's depth 0: the arrows draw with the ship, over the rocks and backgrounds.</summary>
        const int Depth = 0;

        static Sprite whitePixel;

        ArenaRoom arena;
        SpriteRenderer directionArrow, offscreenArrow;
        Transform lineRoot;
        readonly List<SpriteRenderer> dashes = new List<SpriteRenderer>();

        /// <summary>spr_direction_arrow, while the ship spins.</summary>
        public SpriteRenderer DirectionArrow => directionArrow;
        /// <summary>spr_big_arrow, while the ship is out of the room.</summary>
        public SpriteRenderer OffscreenArrow => offscreenArrow;
        /// <summary>How many of the leap line's dashes are drawn now (0 when it is not).</summary>
        public int LeapDashes { get; private set; }
        /// <summary>The leap line's dashes (the first <see cref="LeapDashes"/> are drawn).</summary>
        public IReadOnlyList<SpriteRenderer> Dashes => dashes;

        /// <summary>The arrows of <paramref name="room"/>'s ship.</summary>
        public static PlayerArrows Create(ArenaRoom room)
        {
            var go = new GameObject("obj_player arrows");
            go.transform.SetParent(room.transform, false);
            var arrows = go.AddComponent<PlayerArrows>();
            arrows.arena = room;
            arrows.Build();
            return arrows;
        }

        void Build()
        {
            var tuning = arena.State.Tuning;
            var manifest = HareArtManifest.Load();
            directionArrow = MakeSprite("spr_direction_arrow", manifest != null ? manifest.Frame(tuning.DirectionArrowSprite) : null, tuning.ArrowAlpha, -2); // under the gun (-1) and the ship (0, PlayerLook), as Draw draws it first
            offscreenArrow = MakeSprite("spr_big_arrow", manifest != null ? manifest.Frame(tuning.OffscreenArrowSprite) : null, tuning.ArrowAlpha, 1);
            lineRoot = new GameObject("leap line").transform;
            lineRoot.SetParent(transform, false);
        }

        SpriteRenderer MakeSprite(string name, Sprite sprite, float alpha, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(1f, 1f, 1f, alpha);
            renderer.sortingLayerName = HareSortingLayers.Default;
            renderer.sortingOrder = HareArt.SortingOrderForDepth(Depth) + order;
            renderer.enabled = false;
            return renderer;
        }

        void LateUpdate() => Draw();

        /// <summary>Draws what obj_player's Draw would this frame. Runs every LateUpdate.</summary>
        public void Draw()
        {
            var ship = arena != null ? arena.Player : null;
            var health = ship != null ? ship.GetComponent<PlayerHealth>() : null;
            bool alive = ship != null && (health == null || !health.IsDead);
            var tuning = arena != null && arena.State != null ? arena.State.Tuning : null;
            if (!alive || tuning == null)
            {
                directionArrow.enabled = false;
                offscreenArrow.enabled = false;
                ShowDashes(0);
                return;
            }

            Vector2 position = ship.transform.position;
            var facing = Quaternion.Euler(0f, 0f, ship.Direction);

            directionArrow.enabled = ship.Spin != 0f;
            directionArrow.transform.SetPositionAndRotation(position, facing);

            var room = arena.ToRoom(position);
            bool justSpawned = health != null && health.IsSpawnProtected;
            if (!justSpawned && ArenaWorld.OffscreenArrow(room.x, room.y, arena.State.Width, arena.State.Height, tuning.OffscreenArrowInset, out var at))
            {
                offscreenArrow.enabled = true;
                offscreenArrow.transform.SetPositionAndRotation(arena.ToWorld(at.X, at.Y), facing);
            }
            else offscreenArrow.enabled = false;

            if (ship.State == PlayerFlightState.Idle && tuning.ShowLeapArrow && ship.CanLeap && ship.AimPoint.HasValue)
                DrawLeapLine(ship, tuning);
            else ShowDashes(0);
        }

        void DrawLeapLine(PlayerShipController ship, ArenaTuning tuning)
        {
            float imageAngle = ship.transform.eulerAngles.z * Mathf.Deg2Rad;
            Vector2 gun = (Vector2)ship.transform.position + new Vector2(Mathf.Cos(imageAngle), Mathf.Sin(imageAngle)) * GameClock.Px(tuning.GunOffset);
            var from = arena.ToRoom(gun);
            var to = arena.ToRoom(ship.AimPoint.Value);
            var line = ArenaWorld.DottedLine(new Vec2(from.x, from.y), new Vec2(to.x, to.y), tuning.LeapLineDash);

            ColorUtility.TryParseHtmlString(tuning.LeapLineColor, out var color);
            color.a = tuning.LeapLineAlpha;
            while (dashes.Count < line.Count) dashes.Add(MakeDash());
            for (int i = 0; i < line.Count; i++)
            {
                Vector2 a = arena.ToWorld(line[i].From.X, line[i].From.Y);
                Vector2 b = arena.ToWorld(line[i].To.X, line[i].To.Y);
                var dash = dashes[i];
                var d = b - a;
                dash.transform.SetPositionAndRotation((a + b) / 2f, Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg));
                dash.transform.localScale = new Vector3(d.magnitude, GameClock.Px(tuning.LeapLineWidth), 1f);
                dash.color = color;
            }
            ShowDashes(line.Count);
        }

        void ShowDashes(int count)
        {
            LeapDashes = count;
            for (int i = 0; i < dashes.Count; i++) dashes[i].enabled = i < count;
        }

        SpriteRenderer MakeDash()
        {
            var go = new GameObject("dash");
            go.transform.SetParent(lineRoot, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = WhitePixel();
            renderer.sortingLayerName = HareSortingLayers.Default;
            renderer.sortingOrder = HareArt.SortingOrderForDepth(Depth) + 2;
            return renderer;
        }

        /// <summary>A 1x1 white sprite one world unit across, scaled to each dash.</summary>
        static Sprite WhitePixel()
        {
            if (whitePixel != null) return whitePixel;
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "HareLeapLinePixel", hideFlags = HideFlags.DontSave };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            whitePixel = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            whitePixel.name = "HareLeapLinePixel";
            whitePixel.hideFlags = HideFlags.DontSave;
            return whitePixel;
        }
    }
}
