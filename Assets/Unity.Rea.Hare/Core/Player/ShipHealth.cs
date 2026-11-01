using System;
using Unity.Rea.Hare.Content;

namespace Unity.Rea.Hare.Player
{
    /// <summary>
    /// obj_player's health and lives rules (gmlHARE: player_hurt, reset_player, the hit collisions and Alarms
    /// 1/4/5/6/7). HP = MAXHP = 6; every hit costs exactly 1 HP, makes the ship invulnerable for flash_time and starts
    /// Alarm 5's colour cycle; a respawn is protected (justspawned) for flash_time too, and only rocket explosions ignore
    /// that protection. At 0 HP a life goes (LIVES -= 1), the ship stops, and after alarm_death_wait either reset_player
    /// runs (lives left) or the game restarts. LIVES is not the ship's: it is counted by an <see cref="ILivesLedger"/>, the
    /// ship's own until the room hands it the session's (U.HARE-T54). The original only counted a hit while the source
    /// was on screen (in_view); a damage source that needs that check applies it before sending the command.
    /// </summary>
    public sealed class ShipHealth
    {
        const float Epsilon = 1e-5f;

        readonly EventSink sink;
        readonly ShipFlight flight;
        readonly Func<float, float, float> random;
        readonly LivesCounter ownLives = new LivesCounter();
        ILivesLedger lives;
        HealthTuning tuning = GameContent.Current.Health;
        float invulnerableTimer;
        float spawnProtectionTimer;
        float hurtTimer;
        float deathTimer;
        float flashTimer;
        bool flashing;
        int colorCycle;

        internal ShipHealth(EventSink sink, ShipFlight flight, Func<float, float, float> random)
        {
            this.sink = sink;
            this.flight = flight;
            this.random = random;
            lives = ownLives;
            Reset(tuning);
        }

        public HealthTuning Tuning => tuning;
        public int MaxHealth => tuning.MaxHealth;
        public int Health { get; private set; }
        /// <summary>GML: LIVES, as the ship's ledger counts it.</summary>
        public int Lives => lives.Lives;
        /// <summary>Whether LIVES is someone else's (the session's) rather than the ship's own.</summary>
        public bool UsesSharedLives => !ReferenceEquals(lives, ownLives);
        public bool IsDead { get; private set; }
        public bool IsGameOver { get; private set; }

        /// <summary>GML: invulnerable. Set by a hit, cleared when flash_time runs out.</summary>
        public bool IsInvulnerable => leaving || invulnerableTimer > Epsilon;

        /// <summary>
        /// obj_level_results' Enter: <c>player.invulnerable = true</c> with no end_flash alarm, so the leaving ship cannot
        /// be hurt until setup_level replaces it (U.HARE-T22). Reset, a respawn and setup_level's ship clear it. A ship sent off
        /// while dead no longer waits out its death: in the original alarm_death_wait's reset_player put it back to
        /// state_moving, so it never left and the level never finished (docs/BIBLE.md D21).
        /// </summary>
        internal void MakeInvulnerableToLeave()
        {
            leaving = true;
            IsDead = false;
            deathTimer = 0f;
        }

        bool leaving;
        /// <summary>GML: justspawned. Set by a (re)spawn, cleared when flash_time runs out or the ship perches.</summary>
        public bool IsSpawnProtected => spawnProtectionTimer > Epsilon;
        /// <summary>GML: hurting. Drives the *_hurt sprite pose (<see cref="ShipPose"/>).</summary>
        public bool IsHurting => hurtTimer > Epsilon;
        /// <summary>GML: color, the blend Alarm 5 cycles while invulnerable; white otherwise.</summary>
        public ShipColor Color { get; private set; }

        /// <summary>
        /// A fresh ship: full HP, spawn-protected like the original's Create event. The ship's own LIVES go back to the
        /// start; a shared ledger's are left alone (a room reload is not game_restart).
        /// </summary>
        public void Reset(HealthTuning newTuning)
        {
            tuning = newTuning ?? GameContent.Current.Health;
            Health = tuning.MaxHealth;
            ownLives.Set(tuning.StartingLives);
            IsDead = false;
            IsGameOver = false;
            invulnerableTimer = 0f;
            leaving = false;
            hurtTimer = 0f;
            deathTimer = 0f;
            StopFlashing();
            spawnProtectionTimer = GameClock.Seconds(tuning.SpawnProtectionSteps);
        }

        /// <summary>Counts LIVES with <paramref name="ledger"/> from now on; null goes back to the ship's own.</summary>
        public void UseLives(ILivesLedger ledger) => lives = ledger ?? ownLives;

        /// <summary>
        /// The player's hit collisions followed by player_hurt. False when the hit didn't land: dead, still
        /// invulnerable from the last hit, or spawn-protected (unless the source ignores that, which only
        /// obj_rocket_explosion does in the original).
        /// </summary>
        public bool TryTakeHit(bool ignoresSpawnProtection)
        {
            if (IsDead || IsGameOver) return false;
            if (IsInvulnerable) return false;
            if (IsSpawnProtected && !ignoresSpawnProtection) return false;

            // The collision: invulnerable = true; alarm[alarm_flashing] = 1; alarm[alarm_end_flash] = flash_time.
            invulnerableTimer = GameClock.Seconds(tuning.InvulnerableSteps);
            flashing = true;
            flashTimer = GameClock.Seconds(tuning.FlashFirstSteps);

            // player_hurt: HP -= 1, the hurt pose, alarm_hurting.
            hurtTimer = GameClock.Seconds(tuning.HurtPoseSteps);
            Health = Math.Max(0, Health - 1);

            if (Health <= 0)
                Die();
            else
                sink.Emit(new PlayerDamaged { Health = Health, HurtSound = PickWhole(1, tuning.HurtSounds) });
            return true;
        }

        /// <summary>
        /// obj_carrot's collision with obj_player: <c>if (player.HP &lt; player.MAXHP) { player.HP += 1; }</c>, by
        /// <paramref name="amount"/>, never past MAXHP. False when it was already full. Like the original it does not ask
        /// whether the ship is dead: a carrot caught during the death wait only raises the HP reset_player refills anyway.
        /// </summary>
        public bool Heal(int amount)
        {
            if (amount <= 0 || Health >= tuning.MaxHealth) return false;
            Health = Math.Min(tuning.MaxHealth, Health + amount);
            sink.Emit(new PlayerHealed { Health = Health });
            return true;
        }

        /// <summary>The landing collisions' <c>justspawned = false</c>: a ship that perches is no longer spawn-protected.</summary>
        public void EndSpawnProtection() => spawnProtectionTimer = 0f;

        /// <summary>Advances the original's alarms by <paramref name="deltaTime"/> seconds.</summary>
        public void Tick(float deltaTime)
        {
            if (IsGameOver) return;

            bool wasInvulnerable = IsInvulnerable;
            invulnerableTimer = Math.Max(0f, invulnerableTimer - deltaTime);
            spawnProtectionTimer = Math.Max(0f, spawnProtectionTimer - deltaTime);
            hurtTimer = Math.Max(0f, hurtTimer - deltaTime);

            // Alarm 1 (end_flash): invulnerable = false; color = c_white.
            if (wasInvulnerable && !IsInvulnerable) Color = ShipColor.White;
            // Alarm 5 (flashing): while invulnerable, the next colour; it re-arms every 3 steps.
            if (flashing && invulnerableTimer > Epsilon) TickFlash(deltaTime);

            if (!IsDead) return;
            deathTimer -= deltaTime;
            if (deathTimer > Epsilon) return;
            if (lives.Lives > 0) Respawn();
            else EndGame();
        }

        void TickFlash(float deltaTime)
        {
            flashTimer -= deltaTime;
            float every = GameClock.Seconds(tuning.FlashSteps);
            while (flashTimer <= Epsilon)
            {
                colorCycle = colorCycle <= 3 ? colorCycle + 1 : 0;
                switch (colorCycle)
                {
                    case 0: Color = ShipColor.White; break;
                    case 1: Color = ShipColor.Pink; break;
                    case 2: Color = ShipColor.Amber; break;
                    case 3: Color = ShipColor.CornflowerBlue; break;
                    // case 4: no colour of its own, so cornflower blue holds.
                }
                flashTimer += every;
            }
        }

        void StopFlashing()
        {
            flashing = false;
            flashTimer = 0f;
            Color = ShipColor.White;
        }

        /// <summary>player_hurt's HP-reached-0 branch: LIVES -= 1, the ship stops dead, alarm_death_wait = 320.</summary>
        void Die()
        {
            lives.Spend();
            IsDead = true;
            hurtTimer = 0f; // player_hurt clears hurting on death
            deathTimer = GameClock.Seconds(tuning.DeathWaitSteps);
            flight.Die();
            sink.Emit(new PlayerDied { Lives = lives.Lives });
        }

        /// <summary>
        /// obj_player's Step past the buffer zone of an edge that does not loop (U.HARE-T52):
        /// <c>if(LIVES &gt; 0) { LIVES -= 1; } else { game_restart(); } reset_player();</c>. With a life to spare it goes
        /// (<see cref="PlayerLeftArena"/>) and the ship is reset to fly back in (<see cref="PlayerRespawned"/>); with none
        /// the game restarts (<see cref="GameEnded"/>). Refused while dead or once the game is over.
        /// </summary>
        public bool LeaveArena()
        {
            if (IsDead || IsGameOver) return false;
            if (lives.Lives > 0)
            {
                lives.Spend();
                sink.Emit(new PlayerLeftArena { Lives = lives.Lives });
                Respawn();
            }
            else EndGame();
            return true;
        }

        /// <summary>
        /// GML: reset_player. Full HP, justspawned for flash_time, no longer invulnerable or hurt, colour back to white
        /// with the flashing alarm stopped, and flying in heading <c>270 + irandom_range(-30, 30)</c> at speed 5.
        /// </summary>
        void Respawn()
        {
            IsDead = false;
            Health = tuning.MaxHealth;
            invulnerableTimer = 0f;
            leaving = false;
            hurtTimer = 0f;
            StopFlashing();
            spawnProtectionTimer = GameClock.Seconds(tuning.SpawnProtectionSteps);

            float direction = tuning.RespawnDirection + PickWhole(-tuning.RespawnDirectionJitter, tuning.RespawnDirectionJitter);
            flight.Respawn(direction);
            sink.Emit(new PlayerRespawned { Direction = direction });
        }

        /// <summary>
        /// setup_level's ship (U.HARE-T57): <c>with(obj_player) instance_destroy(); player = instance_create(0,0, obj_player);
        /// reset_player();</c>. A new obj_player is alive and not over whatever the old one was doing (dead, waiting out its
        /// death, game over), then reset_player flies it in (<see cref="PlayerRespawned"/>). LIVES are not touched: they are
        /// a global the new instance shares.
        /// </summary>
        internal void SetUp()
        {
            IsGameOver = false;
            deathTimer = 0f;
            Respawn();
        }

        /// <summary>GML: game_restart, reached once the death wait runs out with no lives left.</summary>
        void EndGame()
        {
            IsGameOver = true;
            sink.Emit(new GameEnded());
        }

        /// <summary>GML irandom_range(min, max): a whole number from min to max inclusive.</summary>
        int PickWhole(int min, int max)
        {
            if (max <= min) return min;
            int value = (int)Math.Floor(random(min, max + 1));
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
