using System.Threading.Tasks;
using Unity.Hare.Player;
using Unity.Multiplayer; // IBotDriver, NetBot, [NetBotDriver]
using UnityEngine;
using UnityEngine.Scripting;

namespace Unity.Hare.Online
{
    /// <summary>
    /// Hare's bot (U.HARE-T35): launched with <c>-bot A</c> (mp-host game smoke, NetBotTest) the kit joins the game by
    /// code with no menus and hands the session to <see cref="RunAsync"/>. It flies like a player would, through the
    /// same [ToServer] calls: it turns counter-clockwise until the server has turned its ship, then boosts until the
    /// server reports the boost, and passes.
    /// </summary>
    [NetBotDriver, Preserve]
    public sealed class HareBot : IBotDriver
    {
        /// <summary>Degrees the server must have turned the ship before the bot counts the turn.</summary>
        public const float TurnDegrees = 30f;

        public async Task RunAsync(NetBot bot)
        {
            HareNetPlayer me = await bot.LocalPlayer<HareNetPlayer>();
            await bot.Until(() => me.Seat >= 0, "the server to seat this bot");
            float start = me.Direction;

            me.Turn(1f); // A: counter-clockwise
            await bot.Until(() => Mathf.Abs(Mathf.DeltaAngle(start, me.Direction)) >= TurnDegrees,
                "the server to turn the ship " + TurnDegrees + " degrees", 20f);
            me.Turn(0f);

            me.PressBoost();
            await bot.Until(() => me.IsBoosting, "the server to boost the ship", 20f);
            bot.Pass("seat " + me.Seat + " (" + me.PlayerId + ") turned from " + start.ToString("0") + " to "
                + me.Direction.ToString("0") + " degrees and boosted, as " + me.DisplayName);
        }
    }
}
