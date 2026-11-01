using System;

namespace Unity.Rea.Hare
{
    /// <summary>
    /// A seat at the game. GML's global "player" becomes this: every command carries the id of the player it
    /// acts for and nothing looks up "the" current player, so more than one seat can share a world.
    /// </summary>
    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        public PlayerId(int value) => Value = value;

        public int Value { get; }

        public bool Equals(PlayerId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PlayerId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => "P" + Value;

        public static bool operator ==(PlayerId a, PlayerId b) => a.Value == b.Value;
        public static bool operator !=(PlayerId a, PlayerId b) => a.Value != b.Value;
    }
}
