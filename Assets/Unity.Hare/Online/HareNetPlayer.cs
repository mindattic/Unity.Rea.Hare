using Unity.Hare.Art;
using Unity.Hare.Content;
using Unity.Hare.Input;
using Unity.Hare.Player;
using Unity.Multiplayer;      // NetEntity, NetBots, NetPlayer
using Unity.Multiplayer.Core; // [ToServer], [Synced]
using UnityEngine;
using UnityEngine.InputSystem;

namespace Unity.Hare.Online
{
    /// <summary>
    /// An online player's ship (U.HARE-T35): the NetworkManager's Player Prefab, one per connection, carrying its
    /// seat. Its owner turns the same Input System actions local play reads into [ToServer] calls, which arrive at
    /// the server as the same Core commands <see cref="PlayerShipController"/> sends locally (SetTurn, SetAim,
    /// PressBoostKey, PressBrakeKey, ReleaseShieldKey); the server's <see cref="HareAuthority"/> runs them on the one
    /// <see cref="SharedWorld"/>, steps it, and writes each ship's state into the [Synced] fields below, which every
    /// peer (late joiners too) draws. Nothing on a client ever changes the game. The name and colour are not copied
    /// here: they come from the session's roster.
    /// </summary>
    public partial class HareNetPlayer : NetEntity
    {
        public const string PrefabResource = "Hare/Online/HareNetPlayer";

        // ---- client -> server: the owner's input ----

        /// <summary>A/D (or the left stick): -1 clockwise .. +1 counter-clockwise. Sent when it changes.</summary>
        [ToServer(RateLimit = 30)]
        public partial void Turn(float value);

        /// <summary>GML mouse_angle, degrees. Sent when it moves; a newer aim supersedes a lost one.</summary>
        [ToServer(Delivery = Delivery.Unreliable, RateLimit = 30)]
        public partial void Aim(float degrees);

        /// <summary>The boost key went down (boosts in flight, raises the shield while perched).</summary>
        [ToServer(RateLimit = 10)]
        public partial void PressBoost();

        /// <summary>The brake key went down (brakes in flight, raises the shield while perched).</summary>
        [ToServer(RateLimit = 10)]
        public partial void PressBrake();

        /// <summary>The boost or brake key came up (drops a perched ship's shield).</summary>
        [ToServer(RateLimit = 10)]
        public partial void ReleaseShield();

        // ---- server -> everyone: the state the authority owns ----

        [Synced] private int _seat = -1;
        [Synced] private Vector2 _position;
        [Synced] private float _direction = 270f;
        [Synced] private float _aimDirection = 270f;
        [Synced] private float _energy;
        [Synced] private int _flightStateValue;
        [Synced] private bool _boosting;
        [Synced] private bool _braking;
        [Synced] private bool _shielding;

        HareAuthority authority; // server only
        PlayerId id;             // server only

        HareControls controls;   // owner only
        float sentTurn;
        float sentAim = float.NaN;
        SpriteRenderer body;
        Sprite flying, perched;

        /// <summary>The seat's player id (seat 0 is P1), as every peer sees it; default until the server seats it.</summary>
        public PlayerId PlayerId => Seat >= 0 ? SharedWorld.PlayerFor(Seat) : default;

        public PlayerFlightState FlightState => (PlayerFlightState)FlightStateValue;
        public bool IsBoosting => Boosting;
        public bool IsBraking => Braking;
        public bool IsShielding => Shielding;

        /// <summary>The owner's name, from the roster ("Player" until the roster has it).</summary>
        public string DisplayName => Roster(out NetPlayer player) ? player.DisplayName : "Player";

        /// <summary>The owner's colour, from the roster (their pick, or their seat's palette colour).</summary>
        public Color32 Color => Roster(out NetPlayer player) ? player.UnityColor() : new Color32(255, 255, 255, 255);

        protected override void OnSpawned()
        {
            if (IsServer)
            {
                authority = HareAuthority.For(Session);
                int seat = Owner.HasSeat ? Owner.SeatIndex : 0;
                id = authority.Join(this, seat);
            }
            BuildView();
            Draw();
        }

        protected override void OnDespawned()
        {
            if (authority != null) authority.Leave(this);
            authority = null;
        }

        /// <summary>Server: writes <paramref name="playerId"/>'s state from the world into the replicated fields.</summary>
        internal void Publish(SharedWorld world, PlayerId playerId)
        {
            if (!IsServer) return;
            var flight = world.Ship(playerId).Flight;
            var at = world.PositionOf(playerId);
            Seat = SharedWorld.SeatOf(playerId);
            Position = new Vector2(at.X, at.Y);
            Direction = flight.Direction;
            AimDirection = flight.AimDirection;
            Energy = flight.Energy;
            FlightStateValue = (int)flight.State;
            Boosting = flight.IsBoosting;
            Braking = flight.IsBraking;
            Shielding = flight.IsShielding;
        }

        // ---- server: the handlers turn each call into the same Core command local play sends ----

        void OnTurn(float value) => Run(new SetTurn(id, Mathf.Clamp(value, -1f, 1f)));
        void OnAim(float degrees) { if (!float.IsNaN(degrees) && !float.IsInfinity(degrees)) Run(new SetAim(id, degrees)); }
        void OnPressBoost() => Run(new PressBoostKey(id));
        void OnPressBrake() => Run(new PressBrakeKey(id));
        void OnReleaseShield() => Run(new ReleaseShieldKey(id));

        void Run(PlayerCommand command)
        {
            if (authority == null || !authority.World.IsSeated(id)) return;
            authority.Execute(command);
            Publish(authority.World, id);
        }

        void Update()
        {
            if (!IsSpawned) return;
            if (IsOwner && !NetBots.IsBot) ReadInput(); // a bot's driver makes the calls itself (HareBot)
            Draw();
        }

        /// <summary>Owner: the local controls, sent only when they change (GML's key events and mouse_angle).</summary>
        void ReadInput()
        {
            if (controls == null)
            {
                controls = new HareControls();
                controls.Player.Enable();
            }
            var player = controls.Player;

            float turn = Mathf.Clamp(player.Rotate.ReadValue<float>(), -1f, 1f);
            if (!Mathf.Approximately(turn, sentTurn))
            {
                sentTurn = turn;
                Turn(turn);
            }

            // Space moved from Boost to Leap (U.HARE-T53); the online arena has no rocks to perch on, so its press is
            // always the in-flight boost here.
            if (player.Boost.WasPressedThisFrame() || (player.Leap.WasPressedThisFrame() && player.Leap.activeControl?.device is Keyboard)) PressBoost();
            if (player.Brake.WasPressedThisFrame()) PressBrake();
            if (player.Boost.WasReleasedThisFrame() || player.Brake.WasReleasedThisFrame()) ReleaseShield();

            if (TryReadAim(player, out float aim)
                && (float.IsNaN(sentAim) || Mathf.Abs(Mathf.DeltaAngle(aim, sentAim)) >= GameContent.Current.Online.AimResendDegrees))
            {
                sentAim = aim;
                Aim(aim);
            }
        }

        /// <summary>The right stick past its deadzone, else the mouse relative to this ship (as local play aims).</summary>
        bool TryReadAim(HareControls.PlayerActions player, out float degrees)
        {
            degrees = 0f;
            Vector2 stick = player.AimStick.ReadValue<Vector2>();
            if (stick.sqrMagnitude > 0.25f)
            {
                degrees = Mathf.Atan2(stick.y, stick.x) * Mathf.Rad2Deg;
                return true;
            }

            var cam = Camera.main;
            if (cam == null || Mouse.current == null) return false;
            Vector3 screen = player.Aim.ReadValue<Vector2>();
            screen.z = -cam.transform.position.z;
            Vector2 offset = (Vector2)cam.ScreenToWorldPoint(screen) - Position;
            if (offset.sqrMagnitude <= Mathf.Epsilon) return false;
            degrees = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
            return true;
        }

        // ---- presentation: reads the replicated state, never writes it ----

        void BuildView()
        {
            if (body != null) return;
            body = GetComponent<SpriteRenderer>();
            if (body == null) body = gameObject.AddComponent<SpriteRenderer>();
            var manifest = HareArtManifest.Load();
            flying = FirstFrame(manifest, "spr_rabbit_leap");
            perched = FirstFrame(manifest, "spr_rabbit_idle");
        }

        static Sprite FirstFrame(HareArtManifest manifest, string name)
        {
            var sprite = manifest != null ? manifest.FindSprite(name) : null;
            return sprite != null && sprite.frames != null && sprite.frames.Length > 0 ? sprite.frames[0] : null;
        }

        void Draw()
        {
            transform.SetPositionAndRotation(new Vector3(Position.x, Position.y, 0f), Quaternion.Euler(0f, 0f, Direction));
            if (body == null) return;
            body.sprite = FlightState == PlayerFlightState.Moving ? flying : perched;
            body.color = Color;
        }

        bool Roster(out NetPlayer player)
        {
            player = null;
            return Session != null && Session.Roster != null && Session.Roster.TryGet(OwnerClientId, out player);
        }

        public override void OnDestroy()
        {
            if (controls != null)
            {
                controls.Player.Disable();
                controls.Dispose();
                controls = null;
            }
            base.OnDestroy();
        }
    }
}
