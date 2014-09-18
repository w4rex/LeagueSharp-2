using System;
using LeagueSharp;
using LeagueSharp.Common;
using System.Collections.Generic;
using SharpDX;

using Color = System.Drawing.Color;

//// Half of this stolen from Hellsing's code :* \\
/* 
 * TODO:
 * Комбо: Q+W+E, R - как и в БОЛе
 * Smart W - аналогично ///Готово - ПРОВЕРИТЬ
 * Objective steal
 * Проверка всех интерраптов и гепклозеров ///ПРОВЕРИТЬ
 * Игнайт
 * Item update
 * Harass
*/
 
namespace Ziggs
{
    class Program
    {
        private static readonly string champName = "Ziggs";                 //Ziggy
        private static readonly Obj_AI_Hero player = ObjectManager.Player;  //Player object
        private static Spell Q1, Q2, Q3, W, E, R;                           //Spells
        private static readonly List<Spell> spellList = new List<Spell>();  //Spell list (for?)
        private static bool DOTReady, TFMode, igniteCheck, wIsSet = false;  //Ignite, TeamFightMode, has ignite, does W exist
        private enum beingFocusedModes { NONE, TURRET, CHAMPION };        //Being focused by
        private enum escapeModes { TOMOUSE, FROMTURRET }       //Escape to mouse, escape away from turret
        private enum WModes { NONE, INTERRUPTOR, ANTIGAPCLOSER, ESCAPE, COMBAT }            //Modes of second W cast
        private static beingFocusedModes beingFocusedBy;
        private static escapeModes escapeMode;
        private static WModes Wmode;
        private static Vector3 escapePos,  wPos;                      //Position to escape from focus, TurretUnitPosition, Explosive( W ) position
        private static TUnit TUnit_obj = new TUnit();
        private static string wObj = "ZiggsW_mis_ground.troy";              //Well, W object, as is
        private static Menu menu;                                           //Menu! (@_@ )
        private static Orbwalking.Orbwalker SOW;                            //SOW! (^_^ )
        private static Items.Item DFG = new Items.Item(3128, 750);          //DFG!!!!!!
        
        //(?)List of damage sources to calc
        private static readonly List<Tuple<DamageLib.SpellType, DamageLib.StageType>> mainCombo = new List<Tuple<DamageLib.SpellType, DamageLib.StageType>>();

        static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;                // OnGameLoad callback
        }
        /// <summary>
        /// OnGameLoad callback. Executes on loading game
        /// </summary>
        /// <param name="args"></param>
        private static void Game_OnGameLoad(EventArgs args)
        {
            if (player.ChampionName != champName) return;                   //Champion validation

            //Spell init
            Q1 = new Spell(SpellSlot.Q, 850f);
            Q2 = new Spell(SpellSlot.Q, 1125f);
            Q3 = new Spell(SpellSlot.Q, 1400f);
            W = new Spell(SpellSlot.W, 970);
            E = new Spell(SpellSlot.E, 900);
            R = new Spell(SpellSlot.R, 5300);

            //SetSkillshot(float delay, float width, float speed, bool collision, SkillshotType type, Vector3 from = null, Vector3 rangeCheckFrom = null);
            Q1.SetSkillshot(0.3f,            130, 1700, false, SkillshotType.SkillshotCircle);
            Q2.SetSkillshot(0.25f + Q1.Delay,130, 1700, false, SkillshotType.SkillshotCircle);
            Q3.SetSkillshot(0.30f + Q2.Delay,130, 1700, false, SkillshotType.SkillshotCircle);
            W.SetSkillshot(0.250f,           275, 1800, false, SkillshotType.SkillshotCircle);
            E.SetSkillshot(0.700f,           235, 2700, false, SkillshotType.SkillshotCircle);
            R.SetSkillshot(1.014f,           525, 1750, false, SkillshotType.SkillshotCircle);
            spellList.AddRange(new[] { Q1, Q2, Q3, W, E, R });
            //Ignite
            var ignite = player.Spellbook.GetSpell(player.GetSpellSlot("SummonerDot"));
            if (ignite != null && ignite.Slot != SpellSlot.Unknown)
            {
                DOTReady = true;
                igniteCheck = true;
            }
            //Combo settings
            mainCombo.Add(Tuple.Create(DamageLib.SpellType.AD, DamageLib.StageType.Default));
            mainCombo.Add(Tuple.Create(DamageLib.SpellType.Q, DamageLib.StageType.Default));
            mainCombo.Add(Tuple.Create(DamageLib.SpellType.W, DamageLib.StageType.Default));
            mainCombo.Add(Tuple.Create(DamageLib.SpellType.E, DamageLib.StageType.Default));
            mainCombo.Add(Tuple.Create(DamageLib.SpellType.R, DamageLib.StageType.Default));
            mainCombo.Add(Tuple.Create(DamageLib.SpellType.IGNITE, DamageLib.StageType.Default));
            mainCombo.Add(Tuple.Create(DamageLib.SpellType.DFG, DamageLib.StageType.Default));
            //Menu loading
            LoadMenu();
            //Presets
            Wmode = WModes.NONE;
            //Additional callbacks
            Game.OnGameUpdate += Game_OnGameUpdate;
            Drawing.OnDraw += Drawing_OnDraw;
            AntiGapcloser.OnEnemyGapcloser += AntiGapcloser_OnEnemyGapcloser;
            Interrupter.OnPosibleToInterrupt += Interrupter_OnPosibleToInterrupt;
            GameObject.OnCreate += GO_OnCreate;
            GameObject.OnDelete += GO_OnRemove;
            Game.OnGameProcessPacket += OnRecievePacket;
        }
        private static void OnRecievePacket(GamePacketEventArgs args)
        {
            if (PacketChannel.S2C == args.Channel && args.PacketData[0] == Packet.S2C.TowerAggro.Header)//Header checks
            {
                Packet.S2C.TowerAggro.Struct temp = Packet.S2C.TowerAggro.Decoded(args.PacketData);//Decode packet
                if (temp.TargetNetworkId != player.NetworkId) return;
                TUnit_obj.Position = ObjectManager.GetUnitByNetworkId<Obj_AI_Turret>(temp.TurretNetworkId).Position;//Get turret position
                TUnit_obj.LastAggroTime = Game.Time;
                TUnit_obj.isAggred = true;
            }
        }
        /// <summary>
        /// Per tick
        /// </summary>
        /// <param name="args"></param>
        private static void Game_OnGameUpdate(EventArgs args)
        {
            // Combo
            if (menu.SubMenu("combo").Item("Active").GetValue<KeyBind>().Active)
                Combo();

            // Harass
            if (menu.SubMenu("harass").Item("Active").GetValue<KeyBind>().Active)
                Harass();

            // Wave clear
            if (menu.SubMenu("waveClear").Item("Active").GetValue<KeyBind>().Active)
                LaneClear();

            if (menu.SubMenu("misc").Item("MAKEMETHEHELLOUTTAHEREMAN").GetValue<KeyBind>().Active)
                if (TUnit_obj.isAggred)
                    Escape(escapeModes.FROMTURRET);
                else
                    Escape(escapeModes.TOMOUSE);
            if (Game.Time - 8 > TUnit_obj.LastAggroTime) TUnit_obj.isAggred = false;//Ye, my awful english.
            if (player.Spellbook.GetSpell(player.GetSpellSlot("SummonerDot")).Cooldown <= 0)//???WTF am i doing? Who knows what can it return!
                igniteCheck = true;
            else
                igniteCheck = false;
            WExploder();
        }
        /// <summary>
        /// On create gameobject
        /// </summary>
        /// <param name="GO">GameObject class</param>
        /// <param name="args"></param>
        private static void GO_OnCreate(LeagueSharp.GameObject GO, EventArgs args)
        {
            if (GO.Name == wObj)
            {
                wPos = GO.Position;
                wIsSet = true;
            }
        }
        /// <summary>
        /// On remove gameobject
        /// </summary>
        /// <param name="GO">GameObject class</param>
        /// <param name="args"></param>
        private static void GO_OnRemove(LeagueSharp.GameObject GO, EventArgs args)
        {
            if (GO.Name == wObj)
            {
                wPos = default(Vector3);
                wIsSet = false;
                Wmode = WModes.NONE;
            }
        }
        /// <summary>
        /// Drawings
        /// </summary>
        /// <param name="args"></param>
        private static void Drawing_OnDraw(EventArgs args)
        {
            //Drawing.DrawText(250, 250, Color.PaleVioletRed, isInDanger(SimpleTs.GetTarget(Q1.Range, SimpleTs.DamageType.Magical)).ToString());
            //Drawing.DrawCircle(debugPred, 10, Color.Aqua);
        }
        /// <summary>
        /// Anti-gapcloser
        /// </summary>
        /// <param name="gapcloser">Object with gapcloser params</param>
        private static void AntiGapcloser_OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (W.IsReady() && menu.SubMenu("misc").Item("antigapcloser").GetValue<bool>())
            {
                if (gapcloser.SkillType == GapcloserType.Skillshot)
                    W.Cast(gapcloser.End, true); //TODO: разные интеррапты для лисинов\джарванов\леон
                else//Проверить работоспособность на разных гепклозерах
                    //W.Cast(gapcloser.Sender);
                Wmode = WModes.ANTIGAPCLOSER;
            }
        }
        /// <summary>
        /// Interruptor
        /// </summary>
        /// <param name="unit">Unit that causing interruptable spell</param>
        /// <param name="spell">Spell that can be interrupted</param>
        private static void Interrupter_OnPosibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (W.IsReady() &&
                Vector3.Distance(player.Position, unit.Position) <= -1 + W.Range + W.Width / 2 &&
                menu.SubMenu("misc").Item("interrupt").GetValue<bool>() &&
                spell.DangerLevel >= InterruptableDangerLevel.Medium)
            {
                Vector3 pos = V3E(player.Position, unit.Position, -1 + Vector3.Distance(player.Position, unit.Position) - W.Width/2);
                W.Cast(pos, true);
                Wmode = WModes.INTERRUPTOR;
            }
        }
        /// <summary>
        /// Comboing
        /// </summary>
        private static void Combo()
        {
            bool useQ = Q1.IsReady() && menu.SubMenu("combo").Item("UseQ").GetValue<bool>();
            bool useW = W.IsReady()  && menu.SubMenu("combo").Item("UseW").GetValue<bool>();
            bool useE = E.IsReady()  && menu.SubMenu("combo").Item("UseE").GetValue<bool>();
            bool useR = R.IsReady()  && menu.SubMenu("combo").Item("UseQ").GetValue<bool>();
            Obj_AI_Hero targetQ = new TargetSelector(Q3.Range, TargetSelector.TargetingMode.LessCast).Target;
            Obj_AI_Hero targetW = new TargetSelector(W.Range, TargetSelector.TargetingMode.LessCast).Target;
            Obj_AI_Hero targetE = new TargetSelector(E.Range, TargetSelector.TargetingMode.LessCast).Target;
            Obj_AI_Hero targetR = new TargetSelector(R.Range, TargetSelector.TargetingMode.LessCast).Target;
            if (targetW.IsValid && DamageLib.IsKillable(targetW, mainCombo))
            {
                if (igniteCheck)
                {

                }
                if(DFG.IsReady())
                    DFG.Cast(targetW);
            }
            if (useW)
            {
                if (menu.SubMenu("misc").Item("GETOVERHERE").GetValue<bool>())
                {
                    PredictionOutput prediction = W.GetPrediction(targetW);
                    Vector3 pos = V3E(player.Position, targetW.Position,
                        Vector3.Distance(player.Position, targetW.Position) + 30);
                    if (Vector3.Distance(player.Position, prediction.CastPosition) <=
                        Vector3.Distance(player.Position, pos))
                    {
                        W.Cast(pos);
                        Wmode = WModes.COMBAT;
                    }
                    else
                    {
                        pos = V3E(player.Position, prediction.CastPosition,
                            Vector3.Distance(player.Position, prediction.CastPosition));
                        W.Cast(pos);
                        Wmode = WModes.COMBAT;
                    }
                }
                else
                {
                    W.Cast(V3E(player.Position, targetW.Position, -10));
                    Wmode = WModes.COMBAT;
                }
            }
            if (useQ)
            {
                PredictionOutput prediction = Q1.GetPrediction(targetQ);
                if (prediction.Hitchance >= HitChance.Medium) Q1.Cast(prediction.CastPosition);
            }
            if (useE)
            {
                PredictionOutput prediction = E.GetPrediction(targetE);
                if (prediction.Hitchance >= HitChance.Medium) E.Cast(prediction.CastPosition);
            }
        }
        /// <summary>
        /// Harass
        /// </summary>
        private static void Harass()
        {
            Obj_AI_Hero target = new TargetSelector(Q3.Range, TargetSelector.TargetingMode.LessCast).Target;
            if (menu.SubMenu("harass").Item("UseQ").GetValue<bool>() && Q1.IsReady())
                Q1.Cast(target);
            target = new TargetSelector(E.Range, TargetSelector.TargetingMode.LessCast).Target;
            if (menu.SubMenu("harass").Item("UseE").GetValue<bool>() && E.IsReady())
                E.Cast(target);
        }
        /// <summary>
        /// Farming function
        /// </summary>
        private static void LaneClear()
        {
            //Minions
            List<Obj_AI_Base> minions = MinionManager.GetMinions(player.Position, Q1.Range);
            //Farm locations for spells
            MinionManager.FarmLocation QPos = Q1.GetCircularFarmLocation(minions);
            MinionManager.FarmLocation WPos = W.GetCircularFarmLocation(minions);
            MinionManager.FarmLocation EPos = E.GetCircularFarmLocation(minions);
            //Minons count
            int numToHit = menu.SubMenu("waveClear").Item("waveNum").GetValue<Slider>().Value;
            //Using of spells
            bool useQ = menu.SubMenu("waveClear").Item("UseQ").GetValue<bool>();
            //bool useW = menu.SubMenu("waveClear").Item("UseW").GetValue<bool>();
            bool useE = menu.SubMenu("waveClear").Item("UseE").GetValue<bool>();
            //Casts
            if (useQ && QPos.MinionsHit >= numToHit) Q1.Cast(QPos.Position, true);
            if (false && WPos.MinionsHit >= numToHit) W.Cast(WPos.Position, true);
            if (useE && EPos.MinionsHit >= numToHit) E.Cast(EPos.Position, true);
        }
        private static void Escape(escapeModes mode)
        {
            switch (mode)
            {
                case escapeModes.TOMOUSE://Escaping to mouse
                    {
                        Vector3 cursorPos = Game.CursorPos;
                        Vector3 pos = V3E(player.Position, cursorPos, (float)-15);
                        if (!IsPassWall(player.Position, cursorPos))//Escaping to mouse pos
                        {
                            Game.PrintChat("V storony kursora");
                            Packet.C2S.Move.Encoded(new Packet.C2S.Move.Struct(cursorPos.X, cursorPos.Y)).Send();
                        }
                        else//Escape through the wall (Flash fail)
                        {
                            Vector3 jumpPred = V3E(pos, cursorPos, 700);
                            if (IsWall(jumpPred.To2D()) && IsPassWall(player.Position, jumpPred))//Can't we jump over?
                            {
                                Game.PrintChat("V stotony steni");
                                Vector3 pass = V3E(player.Position, jumpPred, 50);//Point to move closer to the wall (could be better, i know, i'll improve it)
                                Packet.C2S.Move.Encoded(new Packet.C2S.Move.Struct(pass.X, pass.Y)).Send();//Move closer to the wall
                                return;
                            }
                            else//Yes! We can!
                            {//Stand still
                                Game.PrintChat("Ne ripatsya");
                                Packet.C2S.Move.Encoded(new Packet.C2S.Move.Struct(player.Position.X, player.Position.Y)).Send();
                            }
                        }
                        if (!W.IsReady()) return;//Simple!
                        W.Cast(pos);//Poof! W cast!
                        Wmode = WModes.ESCAPE;//WMode
                    }
                    break;
                case escapeModes.FROMTURRET:
                    {
                        Vector3 WPos = V3E(player.Position, TUnit_obj.Position, 40);//Positions
                        Vector3 escapePos = V3E(player.Position, TUnit_obj.Position, -450);
                        Game.PrintChat("Nahui ot turri");
                        Packet.C2S.Move.Encoded(new Packet.C2S.Move.Struct(escapePos.X, escapePos.Y)).Send();//Move away
                        if (!W.IsReady()) return;//Simple!
                        W.Cast(WPos);//Cast W to move away
                        TUnit_obj.isAggred = false;//Turrent isn't focusing us anymore
                        Wmode = WModes.ESCAPE;//???PROFIT
                    }
                    break;
            }
        }
        /// <summary>
        /// Menu creation
        /// </summary>
        private static void LoadMenu()
        {
            // Initialize the menu
            menu = new Menu(champName, champName, true);

            // Target selector
            Menu targetSelector = new Menu("Target Selector", "ts");
            SimpleTs.AddToMenu(targetSelector);
            menu.AddSubMenu(targetSelector);

            // Orbwalker
            Menu orbwalker = new Menu("Orbwalker", "orbwalker");
            SOW = new Orbwalking.Orbwalker(orbwalker);
            menu.AddSubMenu(orbwalker);

            // Combo
            Menu combo = new Menu("Combo", "combo");
            menu.AddSubMenu(combo);
            combo.AddItem(new MenuItem("UseQ", "Use Q").SetValue(true));
            combo.AddItem(new MenuItem("UseW", "Use W").SetValue(true));
            combo.AddItem(new MenuItem("UseE", "Use E").SetValue(true));
            combo.AddItem(new MenuItem("UseR", "Use R").SetValue(true));
            combo.AddItem(new MenuItem("Active", "Combo active").SetValue<KeyBind>(new KeyBind(32, KeyBindType.Press)));

            // Harass
            Menu harass = new Menu("Harass", "harass");
            menu.AddSubMenu(harass);
            harass.AddItem(new MenuItem("UseQ", "Use Q").SetValue(true));
            harass.AddItem(new MenuItem("UseE", "Use E").SetValue(true));
            harass.AddItem(new MenuItem("Active", "Harass active").SetValue<KeyBind>(new KeyBind('C', KeyBindType.Press)));

            // Wave clear
            Menu waveClear = new Menu("WaveClear", "waveClear");
            menu.AddSubMenu(waveClear);
            waveClear.AddItem(new MenuItem("UseQ", "Use Q").SetValue(true));
            //waveClear.AddItem(new MenuItem("UseW", "Use W").SetValue(true));
            waveClear.AddItem(new MenuItem("UseE", "Use E").SetValue(true));
            waveClear.AddItem(new MenuItem("waveNum", "Minions to hit").SetValue<Slider>(new Slider(3, 1, 10)));
            waveClear.AddItem(new MenuItem("Active", "WaveClear active").SetValue<KeyBind>(new KeyBind('V', KeyBindType.Press)));

            // Drawings
            Menu misc = new Menu("Misc", "misc");
            menu.AddSubMenu(misc);
            misc.AddItem(new MenuItem("interrupt", "Interrupt spells").SetValue(true));
            //misc.AddItem(new MenuItem("interruptLevel", "Interrupt only with danger level").SetValue<InterruptableDangerLevel>(InterruptableDangerLevel.Medium));
            misc.AddItem(new MenuItem("antigapcloser", "Anti-Gapscloser").SetValue(true));
            misc.AddItem(new MenuItem("GETOVERHERE", "Try to throw enemy closer in combo").SetValue(true));
            misc.AddItem(new MenuItem("MAKEMETHEHELLOUTTAHEREMAN", "Smart W").SetValue<KeyBind>(new KeyBind('G', KeyBindType.Press)));
            misc.AddItem(new MenuItem("forceR", "Force ultimate").SetValue<KeyBind>(new KeyBind('T', KeyBindType.Press)));

            Menu ulti = new Menu("Ultimate", "ulti");
            menu.AddSubMenu(ulti);

            // Drawings
            Menu drawings = new Menu("Drawings", "drawings");
            menu.AddSubMenu(drawings);
            drawings.AddItem(new MenuItem("drawRangeQ", "Q range").SetValue(new Circle(true, Color.FromArgb(150, Color.IndianRed))));
            drawings.AddItem(new MenuItem("drawRangeW", "W range").SetValue(new Circle(true, Color.FromArgb(150, Color.IndianRed))));
            drawings.AddItem(new MenuItem("drawRangeE", "E range").SetValue(new Circle(false, Color.FromArgb(150, Color.DarkRed))));
            drawings.AddItem(new MenuItem("drawRangeR", "R range").SetValue(new Circle(false, Color.FromArgb(150, Color.Red))));

            // Finalize menu
            menu.AddToMainMenu();
        }
        /*
        /// <summary>
        /// Is spells on cooldown
        /// </summary>
        /// <param name="spells"></param>
        /// <returns></returns>
        private static bool isCooldown(List<Spell> spells)
        {
            foreach(Spell spell in spells)
            {
                if (!spell.IsReady()) return true;
            }
            return false;
        }
        */
        /// <summary>
        /// Get Vector3 position in direction by distance
        /// </summary>
        /// <param name="from">Start point</param>
        /// <param name="direction">Direction of vector(End point)</param>
        /// <param name="distance">Distance</param>
        /// <returns>Vector3</returns>
        private static Vector3 V3E(Vector3 from, Vector3 direction, float distance)
        {
            return (from.To2D() + distance * Vector2.Normalize(direction.To2D() - from.To2D())).To3D();
        }
        /// <summary>
        /// Is hit wall in this direcion :D Similar version of "collision wall"
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        private static bool IsPassWall(Vector3 start, Vector3 end)
        {
            double count = Vector3.Distance(start, end);
            for (uint i = 0; i <= count; i += 10)
            {
                Vector2 pos = V3E(start, end, i).To2D();
                if (IsWall(pos)) return true;
            }
            return false;
        }
        /// <summary>
        /// Is current point a wall
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        private static bool IsWall(Vector2 pos)
        {
            return (NavMesh.GetCollisionFlags(pos.X, pos.Y) == CollisionFlags.Wall);
        }
        /// <summary>
        /// No explanation
        /// </summary>
        private static void WExploder()
        {
            if (Wmode != WModes.NONE) W.Cast(default(Vector3), true);
        }
    }
    class TUnit
    {
        public Vector3 Position = default(Vector3);
        public bool isAggred = false;
        public float LastAggroTime = 0f;//10 секунд тайминг
    }
}
