namespace Unity.Hare.Enemies
{
    /// <summary>
    /// The view on <see cref="FlyBody"/> (obj_fly, ai_fly). Create's image_index = choose(spr_fly, spr_fly2) assigns a
    /// sprite id to the frame index of the one-frame spr_fly, so every fly shows spr_fly; spr_fly2 is never seen.
    /// </summary>
    public class Fly : Insect
    {
        public const float MaxSpeedXPxPerStep = FlyBody.MaxSpeedXPxPerStep;
        public const float MaxSpeedYPxPerStep = FlyBody.MaxSpeedYPxPerStep;
        public const float SinkSpeedPxPerStep = FlyBody.SinkSpeedPxPerStep;
        public const int ThinkMaxStepsValue = FlyBody.ThinkMaxStepsValue;

        protected override InsectKind Kind => InsectKind.Fly;
        protected override string SpriteName => "spr_fly";
    }
}
