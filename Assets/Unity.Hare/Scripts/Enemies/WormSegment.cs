namespace Unity.Hare.Enemies
{
    /// <summary>The view on <see cref="WormSegmentBody"/> (obj_worm_segment): one link of a <see cref="Worm"/>.</summary>
    public class WormSegment : SetPieceEnemy
    {
        public const int Points = 10;

        WormSegmentBody Rules => (WormSegmentBody)Core;

        protected override string BodySprite => "spr_worm_segment";

        public Worm Head => EnemyDirector.ViewOf<Worm>(Rules.Head.Id);
        /// <summary>GML parent: the part this one follows.</summary>
        public SetPieceEnemy Parent => EnemyDirector.ViewOf<SetPieceEnemy>(Rules.Parent.Id);
        /// <summary>GML child: the next segment back, or null for the tail.</summary>
        public WormSegment Child => Rules.Child == null ? null : EnemyDirector.ViewOf<WormSegment>(Rules.Child.Id);
        public bool IsExposed => Rules.IsExposed;
        public float RadiusPx => Rules.RadiusPx;
        public int Rgb => Rules.Rgb;
        public float Spin => Rules.Spin;
        public float BackAngle => Rules.BackAngle;

        protected override void Configure()
        {
            UseCircle(56.5f);
            AddDamageSource();
        }
    }
}
