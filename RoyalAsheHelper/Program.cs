using System;
using LeagueSharp;
using LeagueSharp.Common;

namespace RoyalAsheHelper
{
    class Program
    {
        private static readonly Obj_AI_Hero player = ObjectManager.Player;
        private static readonly string champName = "Ashe";
        private static Spell Q, W;
        private static bool hasQ = false;
        static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }
        private static void Game_OnGameLoad(EventArgs args)
        {
            if (player.ChampionName != champName) return;
            Q = new Spell(SpellSlot.Q, 0);
            Game.OnGameSendPacket += OnSendPacket;
            Game.PrintChat("RoyalAsheHelper loaded!");
        }
        private static void OnSendPacket(GamePacketEventArgs args)
        {
            if (args.PacketData[0] == Packet.C2S.Move.Header && Packet.C2S.Move.Decoded(args.PacketData).SourceNetworkId == player.NetworkId && Packet.C2S.Move.Decoded(args.PacketData).MoveType == 3)
            {
                foreach (BuffInstance buff in player.Buffs)
                    if (buff.Name == "FrostShot") hasQ = true; else hasQ = false;
                foreach (Obj_AI_Hero hero in ObjectManager.Get<Obj_AI_Hero>())
                    if (hero.NetworkId == Packet.C2S.Move.Decoded(args.PacketData).TargetNetworkId)
                    {
                        if (!hasQ) Q.Cast();
                        hasQ = true;
                        Game.PrintChat("Attacking enemy!" + hasQ.ToString() + " " + Packet.C2S.Move.Decoded(args.PacketData).TargetNetworkId);
                    }
                    else
                    {
                        if (hasQ) Q.Cast();
                        hasQ = false;
                        Game.PrintChat("Not attacking enemy!" + hasQ.ToString() + " " + Packet.C2S.Move.Decoded(args.PacketData).TargetNetworkId);
                    }
            }
        }
    }
}
