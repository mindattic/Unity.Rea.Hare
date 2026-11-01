// Hare's online scaffolding (U.HARE-T35) over a real connection, on Unity.Multiplayer's test kit: every test gets a
// real host or dedicated server and real clients over Unity Transport on a free loopback port.
//   Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -assemblyNames Unity.Rea.Hare.Tests.Online
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.Hare.Online;
using Unity.Rea.Hare.Player;
using Unity.Multiplayer;
using Unity.Multiplayer.Core;
using Unity.Multiplayer.Testing;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Rea.Hare.Tests.Online
{
    internal static class HarePrefabs
    {
        public static GameObject Player
        {
            get
            {
                var prefab = Resources.Load<GameObject>(HareNetPlayer.PrefabResource);
                Assert.That(prefab, Is.Not.Null, "Resources/" + HareNetPlayer.PrefabResource + ".prefab is missing; run Unity.Rea.Hare > Rebuild Online Scene");
                return prefab;
            }
        }
    }

    /// <summary>
    /// A host (which is also a player) and one client: each ship carries its seat, the client's input goes up as
    /// [ToServer] calls and runs on the host's one world, and every peer draws the state that comes back.
    /// </summary>
    public class HareNetPlayerTests : NetSessionTest
    {
        public HareNetPlayerTests() : base(Topology.Host, clients: 1) { }

        protected override void OnSessionsCreated()
        {
            SetPlayerPrefab(HarePrefabs.Player);
            Client.Identity = new PlayerIdentity("ann-token", "Ann", new PlayerColor(230, 80, 60));
        }

        [UnityTest]
        public IEnumerator EachPlayerIsSeated_AndOnlyTheServerHoldsTheWorld()
        {
            yield return StartAndJoinAll();
            var hostOnHost = WaitForSpawn<HareNetPlayer>(Server, owner: Server.LocalClientId);
            yield return hostOnHost;
            var annOnAnn = WaitForSpawn<HareNetPlayer>(Client, owner: Client.LocalClientId);
            yield return annOnAnn;

            yield return WaitForSynced(() => hostOnHost.Result.Seat, 0, "the host's seat");
            yield return WaitForSynced(() => annOnAnn.Result.Seat, 1, "Ann's seat, on her own client");
            Assert.That(annOnAnn.Result.PlayerId, Is.EqualTo(new PlayerId(2)));

            var authority = HareAuthority.Find(Server);
            Assert.That(authority, Is.Not.Null, "the server holds the authority");
            Assert.That(HareAuthority.Find(Client), Is.Null, "a client never does");
            Assert.That(authority.World.Seated, Is.EquivalentTo(new[] { new PlayerId(1), new PlayerId(2) }));

            // Every ship starts flying at its seat's spawn point, and the client sees it move.
            Vector2 start = annOnAnn.Result.Position;
            Assert.That(annOnAnn.Result.FlightState, Is.EqualTo(PlayerFlightState.Moving));
            yield return WaitUntil(() => annOnAnn.Result.Position.y < start.y - 0.1f,
                () => "Ann's ship never flew on her own client; it is at " + annOnAnn.Result.Position);

            yield return WaitUntil(() => hostOnHost.Result.Seat >= 0 && annOnAnn.Result.DisplayName == "Ann",
                () => "Ann's client shows her as " + annOnAnn.Result.DisplayName);
            Assert.That(annOnAnn.Result.Color, Is.EqualTo(new Color32(230, 80, 60, 255)));
        }

        [UnityTest]
        public IEnumerator AClientsInputTurnsAndBoostsItsOwnShipOnTheServer_AndNoOneElses()
        {
            yield return StartAndJoinAll();
            var annOnAnn = WaitForSpawn<HareNetPlayer>(Client, owner: Client.LocalClientId);
            yield return annOnAnn;
            var annOnHost = WaitForSpawn<HareNetPlayer>(Server, owner: Client.LocalClientId);
            yield return annOnHost;
            var hostOnHost = WaitForSpawn<HareNetPlayer>(Server, owner: Server.LocalClientId);
            yield return hostOnHost;
            yield return WaitForSynced(() => annOnAnn.Result.Seat, 1, "Ann's seat");

            float annStart = annOnHost.Result.Direction;
            float hostStart = hostOnHost.Result.Direction;
            annOnAnn.Result.Turn(1f); // what her A key does
            yield return WaitUntil(() => Mathf.DeltaAngle(annStart, annOnAnn.Result.Direction) > 20f,
                () => "Ann's own client never saw her ship turn; it heads " + annOnAnn.Result.Direction);
            annOnAnn.Result.Turn(0f);
            Assert.That(hostOnHost.Result.Direction, Is.EqualTo(hostStart), "the host's ship did not turn");

            annOnAnn.Result.PressBoost(); // her W key
            yield return WaitUntil(() => annOnAnn.Result.IsBoosting, "Ann's client never saw her boost");
            var world = HareAuthority.Find(Server).World;
            Assert.That(world.Ship(new PlayerId(2)).Flight.IsBoosting, Is.True, "the boost ran on the server's world");
            Assert.That(world.Ship(new PlayerId(1)).Flight.IsBoosting, Is.False);
            Assert.That(annOnAnn.Result.Energy, Is.LessThanOrEqualTo(100f));
        }

        [UnityTest]
        public IEnumerator TheServerClampsInput_SoACheatingClientTurnsNoFasterThanTheRules()
        {
            yield return StartAndJoinAll();
            var annOnAnn = WaitForSpawn<HareNetPlayer>(Client, owner: Client.LocalClientId);
            yield return annOnAnn;
            var annOnHost = WaitForSpawn<HareNetPlayer>(Server, owner: Client.LocalClientId);
            yield return annOnHost;
            yield return WaitForSynced(() => annOnAnn.Result.Seat, 1, "Ann's seat");

            float start = annOnHost.Result.Direction;
            float began = Time.time;
            annOnAnn.Result.Turn(50f); // fifty times the stick's travel
            yield return WaitUntil(() => Mathf.Abs(Mathf.DeltaAngle(start, annOnHost.Result.Direction)) > 10f, "Ann's ship never turned");
            annOnAnn.Result.Turn(0f);
            float seconds = Time.time - began;
            float turnRate = Unity.Rea.Hare.Content.GameContent.Current.Flight.TurnSpeedDegreesPerSecond;
            Assert.That(annOnHost.Result.Direction - start, Is.LessThanOrEqualTo(turnRate * (seconds + 0.25f)));
        }
    }

    /// <summary>The nightly smoke without Docker: a dedicated server and two bots (HareBot) join by code and fly.</summary>
    public class HareBotTests : NetBotTest
    {
        public HareBotTests() : base(bots: 2) { }

        protected override float BotTimeoutSeconds => 60f;

        protected override void OnSessionsCreated() => SetPlayerPrefab(HarePrefabs.Player);

        [UnityTest, Timeout(120000)]
        public IEnumerator TwoBotsJoinByCode_EachInItsOwnSeat_AndFly()
        {
            yield return RunBots(role => new HareBot(), "A", "B");

            foreach (NetBotRunner bot in Bots)
                Assert.That(bot.PassMessage, Does.Contain("boosted"), bot.Role);

            var world = HareAuthority.Find(Server).World;
            Assert.That(world.Seated.Count, Is.EqualTo(2), "a dedicated server seats only the bots");
            Assert.That(world.Seated.Select(id => world.Ship(id).Flight.Direction).All(d => !Mathf.Approximately(d, 270f)),
                "the server turned both ships");
        }
    }
}
