﻿using System;
using System.Collections.Generic;
using System.Net;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using Color = System.Drawing.Color;

namespace RoyalAkali
{
    //TODO
    /*
     * Use W if < % HP and № enemies around //Panic mode - CHECK
     * Use W for bush vision(configure ward\W) //WIP
     * Smart R killsteal
     *      To remove - Dont dive with ulti under towers unless you can kill enemy with R so you could get out with the stack you gain
    */
    class Program
    {
        //////////////////////////////
        static readonly Obj_AI_Hero player = ObjectManager.Player;
        static readonly string localVersion = "1.03";

        static Menu menu = new Menu("Royal Rapist Akali", "Akali", true);
        static Orbwalking.Orbwalker orbwalker;

        static Spell E;
        static Spell Q;
        static Spell R;
        static Spell W;
        static SpellSlot IgniteSlot = player.GetSpellSlot("SummonerDot");

        static Obj_AI_Hero rektmate = default(Obj_AI_Hero);
        static float assignTime = 0f;

        static List<Spell> SpellList;
        //////////////////////////////

        static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += OnGameLoad;
        }

        static void OnGameLoad(EventArgs args)
        {
            if (player.ChampionName != "Akali")
                return;

            UpdateChecks();

            LoadMenu();

            Q = new Spell(SpellSlot.Q, 600);
            W = new Spell(SpellSlot.W, 700);
            E = new Spell(SpellSlot.E, 325);
            R = new Spell(SpellSlot.R, 800);

            SpellList = new List<Spell>() { Q, W, E, R };

            Drawing.OnDraw += OnDraw;
            Game.OnGameUpdate += OnUpdate;
            //Obj_AI_Hero.OnProcessSpellCast += OnCast;

            Game.PrintChat("Royal Rapist Akali by princer007 Loaded. More rape for the god of rape! ( ^_^)");
            Game.PrintChat("--------------------------------------------------------------------------------");
            Console.WriteLine("\a \a \a");
        }

        //TODO: Remove
        static void OnCast(LeagueSharp.Obj_AI_Base sender, LeagueSharp.GameObjectProcessSpellCastEventArgs args)
        {
            if (!sender.IsMe) return;
            //Console.WriteLine(args.SData.Name + " was sent on " + args.Target.Name+" in "+Game.Time);
        }
        //

        static void OnUpdate(EventArgs args)
        {
            orbwalker.SetAttacks(true);
            if(menu.Item("RKillsteal").GetValue<bool>())
                foreach (Obj_AI_Hero enemy in ObjectManager.Get<Obj_AI_Hero>())
                    if (enemy.IsEnemy && enemy.Distance(player) <= R.Range && player.GetSpellDamage(enemy, SpellSlot.R) > enemy.Health && ultiCount() > 0 && R.IsReady())
                        R.CastOnUnit(enemy);

            switch (orbwalker.ActiveMode)
            {
                case Orbwalking.OrbwalkingMode.Combo:
                    RapeTime();
                    break;

                case Orbwalking.OrbwalkingMode.Mixed:
                    if (menu.SubMenu("harass").Item("useQ").GetValue<bool>())
                        CastQ(true);
                    if (menu.SubMenu("harass").Item("useE").GetValue<bool>())
                        CastE(true);
                    break;

                case Orbwalking.OrbwalkingMode.LaneClear:
                    if (menu.SubMenu("laneclear").Item("useQ").GetValue<bool>())
                        CastQ(false);
                    if (menu.SubMenu("laneclear").Item("useE").GetValue<bool>())
                        CastE(false);
                    break;
            }
            if (menu.SubMenu("misc").Item("escape").GetValue<KeyBind>().Active) Escape();
        }

        private static void OnDraw(EventArgs args)
        {
            if (menu.SubMenu("misc").Item("escape").GetValue<KeyBind>().Active)
            {
                Utility.DrawCircle(Game.CursorPos, 200, W.IsReady() ? Color.Blue : Color.Red, 3);
                Utility.DrawCircle(player.Position, R.Range, menu.Item("Rrange").GetValue<Circle>().Color, 13);
            }
            foreach (var spell in SpellList)
            {
                var menuItem = menu.Item(spell.Slot + "range").GetValue<Circle>();
                if (menuItem.Active)
                    Utility.DrawCircle(player.Position, spell.Range, menuItem.Color);
            }
            if (menu.SubMenu("drawings").Item("RAPE").GetValue<bool>() && rektmate != default(Obj_AI_Hero))
                Utility.DrawCircle(rektmate.Position, 50, Color.ForestGreen);
            /*
            Drawing.DrawLine(Drawing.WorldToScreen(debugTarget), Drawing.WorldToScreen(debugJump), 3, Color.AliceBlue);
            Drawing.DrawLine(Drawing.WorldToScreen(debugTarget), Drawing.WorldToScreen(debugPlayer), 3, Color.Aquamarine);
            Drawing.DrawText(Drawing.WorldToScreen(debugTarget).X, Drawing.WorldToScreen(debugTarget).Y, Color.PowderBlue, debugTargetDist.ToString());
            Drawing.DrawText(Drawing.WorldToScreen(debugJump).X, Drawing.WorldToScreen(debugJump).Y, Color.PowderBlue, debugJumpDist.ToString());
            */
        }

        private static void CastQ(bool mode)
        {
            if (!Q.IsReady()) return;
            if (mode)
            {
                Obj_AI_Hero target = SimpleTs.GetTarget(Q.Range, SimpleTs.DamageType.Magical);
                if (!target.IsValidTarget(Q.Range)) return;
                Q.Cast(target);
            }
            else
            {
                foreach (Obj_AI_Base minion in MinionManager.GetMinions(player.Position, Q.Range, MinionTypes.All, MinionTeam.Enemy, MinionOrderTypes.Health))
                    if (hasBuff(minion, "AkaliMota") && Orbwalking.GetRealAutoAttackRange(player) >= player.Distance(minion)) orbwalker.ForceTarget(minion);

                foreach (Obj_AI_Base minion in MinionManager.GetMinions(player.Position, Q.Range, MinionTypes.All, MinionTeam.Enemy, MinionOrderTypes.Health))
                    if (HealthPrediction.GetHealthPrediction(minion, (int)(E.Delay + (minion.Distance(player) / E.Speed)) * 1000) < player.GetSpellDamage(minion, SpellSlot.Q) &&
                        HealthPrediction.GetHealthPrediction(minion, (int)(E.Delay + (minion.Distance(player) / E.Speed)) * 1000) > 0 &&
                        player.Distance(minion) > Orbwalking.GetRealAutoAttackRange(player))
                        Q.Cast(minion);
            }
        }

        private static void CastE(bool mode)
        {
            if (!E.IsReady()) return;
            if (mode)
            {
                Obj_AI_Hero target = SimpleTs.GetTarget(E.Range, SimpleTs.DamageType.Magical);
                if (target == null || !target.IsValidTarget(E.Range)) return;
                if (hasBuff(target, "AkaliMota") && !E.IsReady() && Orbwalking.GetRealAutoAttackRange(player) >= player.Distance(target))
                    orbwalker.ForceTarget(target);
                else
                    E.Cast(target);
            }
            else
            {   //Minions in E range                                                                            >= Value in menu
                if (MinionManager.GetMinions(player.Position, E.Range, MinionTypes.All, MinionTeam.Enemy).Count >= menu.SubMenu("laneclear").Item("hitCounter").GetValue<Slider>().Value) E.Cast();
            }
        }

        private static void RapeTime()
        {
            Obj_AI_Hero possibleVictim = SimpleTs.GetTarget(R.Range * 2 + Orbwalking.GetRealAutoAttackRange(player), SimpleTs.DamageType.Magical);
            try
            {
                if (rektmate.IsDead || Game.Time - assignTime > 1.5)
                {
                    //Console.WriteLine("Unassign - " + rektmate.ChampionName + " dead: " + rektmate.IsDead + "\n\n");
                    rektmate = default(Obj_AI_Hero);
                }
            }
            catch (Exception ex) { }
            try
            {
                if (rektmate == default(Obj_AI_Hero) && IsRapeble(possibleVictim) > possibleVictim.Health)
                {
                    rektmate = possibleVictim;
                    assignTime = Game.Time;
                    //Console.WriteLine("Assign - " + rektmate.ChampionName + " time: " + assignTime+"\n\n");
                }
            }
            catch (Exception ex) { }
            if (rektmate != default(Obj_AI_Hero))
            {
                //!(menu.SubMenu("misc").Item("TowerDive").GetValue<Slider>().Value < player.Health/player.MaxHealth && Utility.UnderTurret(rektmate, true)) && 
                if (player.Distance(rektmate) < R.Range * 2 + Orbwalking.GetRealAutoAttackRange(player) && player.Distance(rektmate) > Q.Range)
                    castREscape(rektmate.Position);
                else if (player.Distance(rektmate) < Q.Range)
                    RaperinoCasterino(rektmate);
                else rektmate = default(Obj_AI_Hero);//Target is out of range. Unassign.
            }
            else
            {
                orbwalker.SetAttacks(!Q.IsReady() && !E.IsReady());
                if (menu.SubMenu("combo").Item("useQ").GetValue<bool>())
                    CastQ(true);
                if (menu.SubMenu("combo").Item("useE").GetValue<bool>())
                    CastE(true);
                if (menu.SubMenu("combo").Item("useW").GetValue<bool>())
                    CastW();
                if (menu.SubMenu("combo").Item("useR").GetValue<bool>())
                {
                    Obj_AI_Hero target = SimpleTs.GetTarget(R.Range, SimpleTs.DamageType.Magical);
                    if ((target.IsValidTarget(R.Range) && target.Distance(player) > Orbwalking.GetRealAutoAttackRange(player)) || R.IsKillable(target))
                        R.Cast(target);
                }
            }
        }

        static void CastW()
        {
            //
            //menu.SubMenu("misc").AddItem(new MenuItem("PanicW", "In combo if № of enemies around").SetValue(new Slider(0, 0, 5)));
            //menu.SubMenu("misc").AddItem(new MenuItem("PanicWN", "In combo in %HP < ").SetValue(new Slider(25, 0, 100)));
            //
            byte enemiesAround = 0;
            foreach(Obj_AI_Hero enemy in ObjectManager.Get<Obj_AI_Hero>())
                if(enemy.Distance(player) < 400) ++enemiesAround;
            if (menu.Item("PanicW").GetValue<Slider>().Value > enemiesAround && menu.Item("PanicWN").GetValue<Slider>().Value < (int)(player.Health / player.MaxHealth * 100))
                return;
            W.Cast(player.Position);
        }

        static void RaperinoCasterino(Obj_AI_Hero victim)
        {
            try
            {
                orbwalker.SetAttacks(!Q.IsReady() && !E.IsReady() && player.Distance(victim) < 800f);
                orbwalker.ForceTarget(victim);
                foreach (var item in player.InventoryItems)
                    switch ((int)item.Id)
                    {
                        case 3144:
                            if (player.Spellbook.CanUseSpell((SpellSlot)item.Slot) == SpellState.Ready) item.UseItem(victim);
                            break;
                        case 3146:
                            if (player.Spellbook.CanUseSpell((SpellSlot)item.Slot) == SpellState.Ready) item.UseItem(victim);
                            break;
                        case 3128:
                            if (player.Spellbook.CanUseSpell((SpellSlot)item.Slot) == SpellState.Ready) item.UseItem(victim);
                            break;
                    }
                if (Q.IsReady() && Q.InRange(victim.Position)) Q.Cast(victim);
                if (E.IsReady() && E.InRange(victim.Position)) E.Cast();
                if (W.IsReady() && W.InRange(victim.Position) && !(hasBuff(victim, "AkaliMota") && player.Distance(victim) > Orbwalking.GetRealAutoAttackRange(player))) W.Cast(V2E(player.Position, victim.Position, player.Distance(victim) + W.Width * 2 - 20));
                if (R.IsReady() && R.InRange(victim.Position)) R.Cast(victim);
                if (IgniteSlot != SpellSlot.Unknown && player.SummonerSpellbook.CanUseSpell(IgniteSlot) == SpellState.Ready) player.SummonerSpellbook.CastSpell(IgniteSlot, victim);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        static double IsRapeble(Obj_AI_Hero victim)
        {
            int UC = ultiCount();
            int jumpCount = (UC - (int)(victim.Distance(player.Position) / R.Range));
            double comboDamage = 0d;
            if (Q.IsReady()) comboDamage += player.GetSpellDamage(victim, SpellSlot.Q) + player.CalcDamage(victim, Damage.DamageType.Magical, (45 + 35 * Q.Level + 0.5 * player.FlatMagicDamageMod));
            if (E.IsReady()) comboDamage += player.GetSpellDamage(victim, SpellSlot.E);

            if (hasBuff(victim, "AkaliMota")) comboDamage += player.CalcDamage(victim, Damage.DamageType.Magical, (45 + 35 * Q.Level + 0.5 * player.FlatMagicDamageMod));
            //comboDamage += player.GetAutoAttackDamage(victim, true);

            comboDamage += player.CalcDamage(victim, Damage.DamageType.Magical, CalcPassiveDmg());
            comboDamage += player.CalcDamage(victim, Damage.DamageType.Magical, CalcItemsDmg(victim));

            foreach (var item in player.InventoryItems)
                if ((int)item.Id == 3128)
                    if (player.Spellbook.CanUseSpell((SpellSlot)item.Slot) == SpellState.Ready)
                        comboDamage *= 1.2;
            if (hasBuff(victim, "deathfiregraspspell")) comboDamage *= 1.2;

            if (UC > 0) comboDamage += jumpCount > 0 ? player.GetSpellDamage(victim, SpellSlot.R) * jumpCount : player.GetSpellDamage(victim, SpellSlot.R);
            if (IgniteSlot != SpellSlot.Unknown && player.SummonerSpellbook.CanUseSpell(IgniteSlot) == SpellState.Ready)
                comboDamage += ObjectManager.Player.GetSummonerSpellDamage(victim, Damage.SummonerSpell.Ignite);
            return comboDamage;
        }

        static double CalcPassiveDmg()
        {
            return (0.06 + 0.01 * (player.FlatMagicDamageMod / 6)) * (player.FlatPhysicalDamageMod + player.BaseAttackDamage);
        }

        static double CalcItemsDmg(Obj_AI_Hero victim)
        {
            double result = 0d;
            foreach (var item in player.InventoryItems)
                switch ((int)item.Id)
                {
                    case 3100: // LichBane
                        if (player.Spellbook.CanUseSpell((SpellSlot)item.Slot) == SpellState.Ready)
                            result += player.BaseAttackDamage * 0.75 + player.FlatMagicDamageMod * 0.5;
                        break;
                    case 3057://Sheen
                        if (player.Spellbook.CanUseSpell((SpellSlot)item.Slot) == SpellState.Ready)
                            result += player.BaseAttackDamage;
                        break;
                    case 3144:
                        if (player.Spellbook.CanUseSpell((SpellSlot)item.Slot) == SpellState.Ready)
                            result += 100d;
                        break;
                    case 3146:
                        if (player.Spellbook.CanUseSpell((SpellSlot)item.Slot) == SpellState.Ready)
                            result += 150d + player.FlatMagicDamageMod * 0.4;
                        break;
                    case 3128:
                        if (player.Spellbook.CanUseSpell((SpellSlot)item.Slot) == SpellState.Ready)
                            result += victim.MaxHealth * 0.15;
                        break;
                }

            return result;
        }

        static void Escape()
        {
            Vector3 cursorPos = Game.CursorPos;
            Vector2 pos = V2E(player.Position, cursorPos, R.Range);
            Vector2 pass = V2E(player.Position, cursorPos, 120);
            Packet.C2S.Move.Encoded(new Packet.C2S.Move.Struct(pass.X, pass.Y)).Send();
            if (menu.SubMenu("misc").Item("RCounter").GetValue<Slider>().Value > ultiCount()) return;
            if (!IsWall(pos) && IsPassWall(player.Position, pos.To3D()) && MinionManager.GetMinions(cursorPos, 300, MinionTypes.All, MinionTeam.NotAlly).Count < 1)
                if (W.IsReady()) W.Cast(V2E(player.Position, cursorPos, W.Range));
            castREscape(cursorPos, true);
        }

        static void castREscape(Vector3 position, bool mouseJump = false)
        {
            Obj_AI_Base target = MinionManager.GetMinions(player.Position, 800, MinionTypes.All, MinionTeam.NotAlly)[0];
            foreach (Obj_AI_Base minion in ObjectManager.Get<Obj_AI_Base>())
                if (minion.IsValidTarget(R.Range, true) && player.Distance(position, true) > minion.Distance(position, true) && minion.Distance(position, true) < target.Distance(position, true))
                    if (mouseJump)
                    {
                        if (minion.Distance(position) < 200)
                            target = minion;
                    }
                    else
                    {
                        Console.WriteLine("Distance T-M: " + minion.Distance(position) + "  Distance T-P: " + player.Distance(position));
                        Console.WriteLine("Minion - X:" + minion.Position.X + "Y: " + minion.Position.Y + ",  player - X:" + player.Position.X + "Y: " + player.Position.Y + ",  position - X:" + position.X + "Y: " + position.Y);
                        target = minion;
                    }
            if (R.IsReady() && R.InRange(target.Position))
                if (mouseJump)
                {
                    if (target.Distance(position) < 200)
                        R.Cast(target);
                }
                else if (player.Distance(position, true) > target.Distance(position, true) && ((int)(player.Distance(position) / R.Range)) < ultiCount())
                    R.Cast(target);

        }

        static bool IsPassWall(Vector3 start, Vector3 end)
        {
            double count = Vector3.Distance(start, end);
            for (uint i = 0; i <= count; i += 10)
            {
                Vector2 pos = V2E(start, end, i);
                if (IsWall(pos)) return true;
            }
            return false;
        }

        static int ultiCount()
        {
            foreach (BuffInstance buff in player.Buffs)
                if (buff.Name == "AkaliShadowDance")
                    return buff.Count;
            return 0;
        }

        static bool IsWall(Vector2 pos)
        {
            return (NavMesh.GetCollisionFlags(pos.X, pos.Y) == CollisionFlags.Wall ||
                    NavMesh.GetCollisionFlags(pos.X, pos.Y) == CollisionFlags.Building);
        }

        static Vector2 V2E(Vector3 from, Vector3 direction, float distance)
        {
            return from.To2D() + distance * Vector3.Normalize(direction - from).To2D();
        }
        static bool hasBuff(Obj_AI_Base target, string buffName)
        {
            foreach (BuffInstance buff in target.Buffs)
                if (buff.Name == buffName) return true;
            return false;
        }

        static bool ableToGapclose(Obj_AI_Base target)
        {

            return false;
        }

        static void LoadMenu()
        {
            Menu targetSelector = new Menu("Target Selector", "ts");
            SimpleTs.AddToMenu(targetSelector);
            menu.AddSubMenu(targetSelector);

            Menu SOW = new Menu("Orbwalker", "orbwalker");
            orbwalker = new Orbwalking.Orbwalker(SOW);
            menu.AddSubMenu(SOW);

            menu.AddSubMenu(new Menu("Combo Options", "combo"));
            menu.SubMenu("combo").AddItem(new MenuItem("useQ", "Use Q in combo").SetValue(true));
            menu.SubMenu("combo").AddItem(new MenuItem("useW", "Use W in combo").SetValue(true));
            menu.SubMenu("combo").AddItem(new MenuItem("useE", "Use E in combo").SetValue(true));
            menu.SubMenu("combo").AddItem(new MenuItem("useR", "Use R in combo").SetValue(true));

            menu.AddSubMenu(new Menu("Harass Options", "harass"));
            menu.SubMenu("harass").AddItem(new MenuItem("useQ", "Use Q in harass").SetValue(false));
            menu.SubMenu("harass").AddItem(new MenuItem("useE", "Use E in harass").SetValue(true));

            menu.AddSubMenu(new Menu("Lane Clear", "laneclear"));
            menu.SubMenu("laneclear").AddItem(new MenuItem("useQ", "Use Q in laneclear").SetValue(true));
            menu.SubMenu("laneclear").AddItem(new MenuItem("useE", "Use E in laneclear").SetValue(true));
            menu.SubMenu("laneclear").AddItem(new MenuItem("hitCounter", "Use E if will hit min").SetValue(new Slider(3, 1, 6)));

            menu.AddSubMenu(new Menu("Miscellaneous", "misc"));
            menu.SubMenu("misc").AddItem(new MenuItem("escape", "Escape key").SetValue(new KeyBind('G', KeyBindType.Press)));
            menu.SubMenu("misc").AddItem(new MenuItem("RCounter", "Do not escape if R<").SetValue(new Slider(1, 1, 3)));
            menu.SubMenu("misc").AddItem(new MenuItem("RKillsteal", "Always try to KS with R").SetValue(false));
            menu.SubMenu("misc").AddItem(new MenuItem("", "                     Panic W:"));
            menu.SubMenu("misc").AddItem(new MenuItem("PanicW", "If # of enemies around").SetValue(new Slider(1, 1, 5)));
            menu.SubMenu("misc").AddItem(new MenuItem("PanicWN", "If your %HP < ").SetValue(new Slider(25, 0, 100)));

            var dmgAfterComboItem = new MenuItem("DamageAfterCombo", "Draw damage after a rotation").SetValue(true);
            Utility.HpBarDamageIndicator.DamageToUnit += hero => (float)IsRapeble(hero);
            Utility.HpBarDamageIndicator.Enabled = dmgAfterComboItem.GetValue<bool>();
            dmgAfterComboItem.ValueChanged += delegate(object sender, OnValueChangeEventArgs eventArgs)
            {
                Utility.HpBarDamageIndicator.Enabled = eventArgs.GetNewValue<bool>();
            };

            Menu drawings = new Menu("Drawings", "drawings");
            menu.AddSubMenu(drawings);
            drawings.AddItem(new MenuItem("Qrange", "Q Range").SetValue(new Circle(true, Color.FromArgb(150, Color.IndianRed))));
            drawings.AddItem(new MenuItem("Wrange", "W Range").SetValue(new Circle(true, Color.FromArgb(150, Color.IndianRed))));
            drawings.AddItem(new MenuItem("Erange", "E Range").SetValue(new Circle(false, Color.FromArgb(150, Color.DarkRed))));
            drawings.AddItem(new MenuItem("Rrange", "R Range").SetValue(new Circle(false, Color.FromArgb(150, Color.DarkRed))));
            drawings.AddItem(new MenuItem("RAPE", "Draw instakill target").SetValue<bool>(true));
            drawings.AddItem(dmgAfterComboItem);

            menu.AddToMainMenu();
        }

        static void UpdateChecks()
        {
            //https://raw.github.com/princer007/LeagueSharp/master/RoyalRapistAkali/version
            WebClient client = new WebClient();
            string version = client.DownloadString("https://raw.github.com/princer007/LeagueSharp/master/RoyalRapistAkali/version");
            Game.PrintChat("--------------------------------------------------------------------------------");
            if (version.Remove(4).Equals(localVersion))
                Game.PrintChat("== Your copy of Royal Rapist Akali is updated! GL & HF! ==");
            else
                Game.PrintChat("== Royal Rapist Akali have an update. Get it ASAP! ==");

        }
    }
}