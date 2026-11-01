using System.Collections.Generic;
using UnityEngine;
using Unity.Rea.Hare.Weapons;

namespace Unity.Rea.Hare.Enemies
{
    /// <summary>
    /// Ports obj_butterfly_emitter, obj_firefly_emitter and obj_fly_emitter (gmlHARE): on Create each one creates its
    /// whole swarm at once (repeat(100) butterflies, repeat(28) fireflies, repeat(32) flies) and then does nothing
    /// more; every insect places itself in Core. The emitter draws nothing.
    /// </summary>
    public class InsectEmitter : MonoBehaviour
    {
        public const int ButterflyCount = 100;
        public const int FireflyCount = 28;
        public const int FlyCount = 32;

        [SerializeField] InsectKind kind;
        [SerializeField] bool emitOnStart = true;

        public InsectKind Kind
        {
            get => kind;
            set => kind = value;
        }

        public static int SwarmSize(InsectKind kind)
        {
            switch (kind)
            {
                case InsectKind.Butterfly: return ButterflyCount;
                case InsectKind.Firefly: return FireflyCount;
                default: return FlyCount;
            }
        }

        void Start()
        {
            if (emitOnStart) Emit();
        }

        /// <summary>GML Create: the full swarm for <see cref="Kind"/> in <paramref name="room"/> (default room_gamescreen).</summary>
        public List<Insect> Emit(Rect? room = null)
        {
            int count = SwarmSize(kind);
            var swarm = new List<Insect>(count);
            for (int i = 0; i < count; i++) swarm.Add(Spawn(kind, room ?? Gml.Room));
            return swarm;
        }

        /// <summary>GML instance_create(0, 0, obj_*): one insect, which then places itself.</summary>
        public static Insect Spawn(InsectKind kind, Rect room, Vector2? position = null)
        {
            var go = new GameObject(kind.ToString());
            Insect insect;
            switch (kind)
            {
                case InsectKind.Butterfly: insect = go.AddComponent<Butterfly>(); break;
                case InsectKind.Firefly: insect = go.AddComponent<Firefly>(); break;
                default: insect = go.AddComponent<Fly>(); break;
            }
            insect.Init(room, position);
            return insect;
        }
    }
}
