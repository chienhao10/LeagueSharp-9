﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LeagueSharp;
using LeagueSharp.Common;
using DevCommom;
using SharpDX;

/*
 * ##### DevCassio Mods #####
 * 
 * + AntiGapCloser with R when LowHealth
 * + Interrupt Danger Spell with R when LowHealth
 * + LastHit E On Posioned Minions
 * + Ignite KS
 * + Menu No-Face Exploit (PacketCast)
 * + Skin Hack
 * + Show E Damage on Enemy HPBar
 * + Assisted Ult
 * + Block Ult if will not hit
 * + Auto Ult Enemy Under Tower
 * + Auto Ult if will hit X
 * + Jungle Clear
 * + R to Save Yourself, when MinHealth and Enemy IsFacing
 * + Auto Spell Level UP
 * + Play Legit Menu :)
 * done harass e, last hit,jungle no du use e,check gap close. 
 * --------------------------------------------------------------------------------------------
 *  - block r, check mim R enemym, fix e dmg
 *  +add gap close W, flee, Q,W,E, rocket r, flash r
*/

namespace DevCassio
{
    class Program
    {
        public const string ChampionName = "cassiopeia";

        public static Items.Item Belt = new Items.Item(3150, 800);
        public static Menu Config;
        public static Orbwalking.Orbwalker Orbwalker;
        public static List<Spell> SpellList = new List<Spell>();
        public static Obj_AI_Hero Player;
        public static Spell Q;
        public static Spell W;
        public static Spell E;
        public static Spell R;
        public static List<Obj_AI_Base> MinionList;
        /*public static SkinManager skinManager;*/
        public static LevelUpManager levelUpManager;
        public static AssemblyUtil assemblyUtil;
        public static SummonerSpellManager summonerSpellManager;

        private static long dtBurstComboStart = 0;
        private static long dtLastQCast = 0;
        private static long dtLastSaveYourself = 0;
        private static long dtLastECast = 0;
       
        public static bool mustDebug = false;
        public static bool mustDebugPredict = false;

        static void Main(string[] args)
        {
            LeagueSharp.Common.CustomEvents.Game.OnGameLoad += onGameLoad;
        }

        static void Game_OnGameUpdate(EventArgs args)
        {
            try
            {
                switch (Orbwalker.ActiveMode)
                {
                    case Orbwalking.OrbwalkingMode.Combo:
                        Combo();
                        BurstCombo();
                        break;
                    case Orbwalking.OrbwalkingMode.Mixed:
                        Harass();
                        LastHit();
                        break;
                    case Orbwalking.OrbwalkingMode.LaneClear:
                        JungleClear();
                        WaveClear();
                        LastHit();
                        break;
                    case Orbwalking.OrbwalkingMode.LastHit:
                        LastHit();

                        break;
                    default:
                        break;
                }

                if (Config.Item("Rflash").GetValue<KeyBind>().Active)
                {
                    FlashCombo();
                }

                if (Config.Item("UseSkin").GetValue<bool>())
                {
                    Player.SetSkin(Player.CharData.BaseSkinName, Config.Item("SkinID").GetValue<Slider>().Value);
                }

                if (Config.Item("HarassToggle").GetValue<KeyBind>().Active)
                    Harass();
                
                UseUltUnderTower();

                /*skinManager.Update();*/

                levelUpManager.Update();

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                if (mustDebug)
                    Game.PrintChat(ex.Message);
            }
        }

        
        public static void BurstCombo()
        {
            var eTarget = TargetSelector.GetTarget(R.Range, TargetSelector.DamageType.Magical);

            if (eTarget == null)
                return;

            var useQ = Config.Item("UseQCombo").GetValue<bool>();
            var useW = Config.Item("UseWCombo").GetValue<bool>();
            var useE = Config.Item("UseECombo").GetValue<bool>();
            var useR = Config.Item("UseRCombo").GetValue<bool>();
            var useIgnite = Config.Item("UseIgnite").GetValue<bool>();
            var packetCast = Config.Item("PacketCast").GetValue<bool>();

            double totalComboDamage = 0;
            if (R.IsReady())
            {
                totalComboDamage += Player.GetSpellDamage(eTarget, SpellSlot.R);
                totalComboDamage += Player.GetSpellDamage(eTarget, SpellSlot.Q);
                totalComboDamage += Player.GetSpellDamage(eTarget, SpellSlot.E);
            }
            totalComboDamage += Player.GetSpellDamage(eTarget, SpellSlot.Q);
            totalComboDamage += Player.GetSpellDamage(eTarget, SpellSlot.E);
            totalComboDamage += Player.GetSpellDamage(eTarget, SpellSlot.E);
            totalComboDamage += Player.GetSpellDamage(eTarget, SpellSlot.E);
            totalComboDamage += summonerSpellManager.GetIgniteDamage(eTarget);

            double totalManaCost = 0;
            if (R.IsReady())
            {
                totalManaCost += Player.Spellbook.GetSpell(SpellSlot.R).ManaCost;
            }
            totalManaCost += Player.Spellbook.GetSpell(SpellSlot.Q).ManaCost;

            if (mustDebug)
            {
                Game.PrintChat("BurstCombo Damage {0}/{1} {2}", Convert.ToInt32(totalComboDamage), Convert.ToInt32(eTarget.Health), eTarget.Health < totalComboDamage ? "BustKill" : "Harras");
                Game.PrintChat("BurstCombo Mana {0}/{1} {2}", Convert.ToInt32(totalManaCost), Convert.ToInt32(eTarget.Mana), Player.Mana >= totalManaCost ? "Mana OK" : "No Mana");
            }

            if (eTarget.Health < totalComboDamage && Player.Mana >= totalManaCost && !eTarget.IsInvulnerable)
            {
                if (R.IsReady() && useR && eTarget.IsValidTarget(R.Range) && eTarget.IsFacing(Player))
                {
                    if (totalComboDamage * 0.3 < eTarget.Health) // Anti R OverKill
                    {
                        if (mustDebug)
                            Game.PrintChat("BurstCombo R");
                        if (R.Cast(eTarget, packetCast, true) == Spell.CastStates.SuccessfullyCasted)
                            dtBurstComboStart = Environment.TickCount;
                    }
                    else
                    {
                        if (mustDebug)
                            Game.PrintChat("BurstCombo OverKill");
                        dtBurstComboStart = Environment.TickCount;
                    }
                }
            }


            if (dtBurstComboStart + 5000 > Environment.TickCount && summonerSpellManager.IsReadyIgnite() && eTarget.IsValidTarget(600))
            {
                if (mustDebug)
                    Game.PrintChat("Ignite");
                summonerSpellManager.CastIgnite(eTarget);
            }
        }

        private static void FlashCombo()
        {
            var rTarget = TargetSelector.GetTarget(R.Range, TargetSelector.DamageType.Magical);
            if (rTarget == null && R.IsReady() && Player.Spellbook.CanUseSpell(Player.GetSpellSlot("SummonerFlash")) == SpellState.Ready)
            {
                int minr = Config.Item("Rminflash").GetValue<Slider>().Value;

                if (minr > 1)
                {
                    foreach (
                            PredictionOutput pred in
                                ObjectManager.Get<Obj_AI_Hero>()
                                    .Where(x => x.IsValidTarget(R.Range))
                                    .Select(x => R.GetPrediction(x, true))
                                    .Where(pred => pred.Hitchance >= HitChance.High && pred.AoeTargetsHitCount >= minr)
                            )
                    {
                        PredictionOutput pred1 = pred;
                        Player.Spellbook.CastSpell(Player.GetSpellSlot("SummonerFlash"), pred1.CastPosition);
                        Utility.DelayAction.Add(10, () => R.Cast(pred1.CastPosition));
                    }
                }
                else
                {
                    Obj_AI_Hero target = TargetSelector.GetTarget(
                        R.Range,
                        TargetSelector.DamageType.Magical);
                    if (target != null)
                    {
                        Player.Spellbook.CastSpell(Player.GetSpellSlot("SummonerFlash"), target.Position);
                        Utility.DelayAction.Add(50, () => R.Cast(target.Position));
                    }
                }
            }
            if (Orbwalking.CanMove(100))
            {
                Orbwalking.MoveTo(Game.CursorPos, 80f);
                Combo();
            }
        }

        public static void Combo()
        {

            var eTarget = TargetSelector.GetTarget(W.Range, TargetSelector.DamageType.Magical);

            if (eTarget == null)
                return;

            var useQ = Config.Item("UseQCombo").GetValue<bool>();
            var useW = Config.Item("UseWCombo").GetValue<bool>();
            var useE = Config.Item("UseECombo").GetValue<bool>();
            var useR = Config.Item("UseRCombo").GetValue<bool>();
            var useIgnite = Config.Item("UseIgnite").GetValue<bool>();
            var packetCast = Config.Item("PacketCast").GetValue<bool>();

            var RMinHit = Config.Item("RMinHit").GetValue<Slider>().Value;
            var RMinHitFacing = Config.Item("RMinHitFacing").GetValue<Slider>().Value;

            var UseRSaveYourself = Config.Item("UseRSaveYourself").GetValue<bool>();
            var UseRSaveYourselfMinHealth = Config.Item("UseRSaveYourselfMinHealth").GetValue<Slider>().Value;

            if (eTarget.IsValidTarget(R.Range) && R.IsReady() && UseRSaveYourself)
            {
                if (Player.GetHealthPerc() < UseRSaveYourselfMinHealth && eTarget.IsFacing(Player) && !eTarget.IsInvulnerable)
                {
                    R.Cast(eTarget, true);
                    if (dtLastSaveYourself + 3000 < Environment.TickCount)
                    {
                        Game.PrintChat("Save Yourself!");
                        dtLastSaveYourself = Environment.TickCount;
                    }
                }
            }

            if (eTarget.IsValidTarget(R.Range) && R.IsReady() && useR)
            {
                var castPred = R.GetPrediction(eTarget, true, R.Range);       
                var enemiesHit = DevHelper.GetEnemyList().Where(x => R.WillHit(x, castPred.CastPosition) && !x.IsInvulnerable).ToList();
                var enemiesFacing = enemiesHit.Where(x => x.IsFacing(Player)).ToList();

                if (mustDebug)
                    Game.PrintChat("Hit:{0} Facing:{1}", enemiesHit.Count(), enemiesFacing.Count());

                if (enemiesHit.Count() >= RMinHit && enemiesFacing.Count() >= RMinHitFacing)
                    R.Cast(castPred.CastPosition);
            }
                
            if (E.IsReady() && useE)
            {
                var eTargetCastE = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Magical);
                if (eTargetCastE != null && eTargetCastE.HasBuffOfType(BuffType.Poison))
                {
                    var query = DevHelper.GetEnemyList().Where(x => x.IsValidTarget(E.Range) && x.HasBuffOfType(BuffType.Poison));
                    if (query.Any())
                        eTargetCastE = query.First();
                }
                else
                if (eTargetCastE != null && eTargetCastE.IsValidTarget(E.Range) && !eTargetCastE.IsZombie)
                {
                    CastE(eTarget);
                }

                if (eTargetCastE != null)
                {
                    var buffEndTime = GetPoisonBuffEndTime(eTargetCastE);
                    if (buffEndTime > (Game.Time + E.Delay) || Player.GetSpellDamage(eTargetCastE, SpellSlot.E) > eTargetCastE.Health * 0.9)
                    {
                        CastE(eTarget);
                        if (Player.GetSpellDamage(eTargetCastE, SpellSlot.E) > eTargetCastE.Health * 0.9)
                            return;
                    }
                }
            }

            if (eTarget.IsValidTarget(Q.Range) && Q.IsReady() && useQ)
            {
                if (Q.Cast(eTarget, true) == Spell.CastStates.SuccessfullyCasted)
                    dtLastQCast = Environment.TickCount;
            }

            if (W.IsReady() && useW)
            {
                W.CastIfHitchanceEquals(eTarget, HitChance.High);
            }

            if (useW)
                useW = (!eTarget.HasBuffOfType(BuffType.Poison) || (!eTarget.IsValidTarget(Q.Range) && eTarget.IsValidTarget(W.Range + (W.Width / 2))));

            if (W.IsReady() && useW && Environment.TickCount > dtLastQCast + Q.Delay * 1000)
            {
                W.CastIfHitchanceEquals(eTarget, eTarget.IsMoving ? HitChance.High : HitChance.Medium);
            }

            double igniteDamage = summonerSpellManager.GetIgniteDamage(eTarget) + (Player.GetSpellDamage(eTarget, SpellSlot.E) * 2);
            if (eTarget.Health < igniteDamage && E.Level > 0 && eTarget.IsValidTarget(600) && eTarget.HasBuffOfType(BuffType.Poison))
            {
                summonerSpellManager.CastIgnite(eTarget);
            }

        }

        public static void Harass()
        {
            var eTarget = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Magical);

            if (eTarget == null)
                return;

            var useQ = Config.Item("UseQHarass").GetValue<bool>();
            var useW = Config.Item("UseWHarass").GetValue<bool>();
            var useE = Config.Item("UseEHarass").GetValue<bool>();
            var packetCast = Config.Item("PacketCast").GetValue<bool>();

            if (mustDebug)
                Game.PrintChat("Harass Target -> " + eTarget.SkinName);

            if (E.IsReady() && useE)
            {
                var eTargetCastE = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Magical);

                if (eTargetCastE != null && eTargetCastE.HasBuffOfType(BuffType.Poison))
                {
                    // keep priority target
                }
                else
                {
                    var query = DevHelper.GetEnemyList().Where(x => x.IsValidTarget(E.Range) && x.HasBuffOfType(BuffType.Poison));
                    if (query.Any())
                        eTargetCastE = query.First();
                }

                if (eTargetCastE != null)
                {
                    var buffEndTime = GetPoisonBuffEndTime(eTargetCastE);
                    if (buffEndTime > (Game.Time + E.Delay) || Player.GetSpellDamage(eTargetCastE, SpellSlot.E) > eTargetCastE.Health * 0.9)
                    {
                        CastE(eTarget);
                        if (Player.GetSpellDamage(eTargetCastE, SpellSlot.E) > eTargetCastE.Health * 0.9)
                            return;
                    }
                }
            }

            if (E.IsReady() && useE)
            {
                var eTargetCastE = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Magical);

                if (eTargetCastE != null && eTargetCastE.IsValidTarget(E.Range) && !eTargetCastE.IsZombie)
                {
                    CastE(eTarget);
                }
            }
                if (eTarget.IsValidTarget(Q.Range) && Q.IsReady() && useQ)
            {
                if (Q.Cast(eTarget, true) == Spell.CastStates.SuccessfullyCasted)
                    dtLastQCast = Environment.TickCount;
            }

            if (W.IsReady() && useW)
            {
                W.CastIfHitchanceEquals(eTarget, HitChance.High);
            }

            if (useW)
                useW = (!eTarget.HasBuffOfType(BuffType.Poison) || (!eTarget.IsValidTarget(Q.Range) && eTarget.IsValidTarget(W.Range + (W.Width / 2))));

            if (W.IsReady() && useW && Environment.TickCount > dtLastQCast + Q.Delay * 1000)
            {
                W.CastIfHitchanceEquals(eTarget, eTarget.IsMoving ? HitChance.High : HitChance.Medium);
            }
        }

        public static void WaveClear()
        {
            var useQ = Config.Item("UseQLaneClear").GetValue<bool>();
            var useW = Config.Item("UseWLaneClear").GetValue<bool>();
            var useE = Config.Item("UseELaneClear").GetValue<bool>();
            var UseELastHitLaneClear = Config.Item("UseELastHitLaneClear").GetValue<bool>();
            var UseELastHitLaneClearNonPoisoned = Config.Item("UseELastHitLaneClearNonPoisoned").GetValue<bool>();
            var packetCast = Config.Item("PacketCast").GetValue<bool>();
            var LaneClearMinMana = Config.Item("LaneClearMinMana").GetValue<Slider>().Value;

            if (Q.IsReady() && useQ && Player.GetManaPerc() >= LaneClearMinMana)
            {
                var allMinionsQ = MinionManager.GetMinions(Player.Position, Q.Range + Q.Width, MinionTypes.All, MinionTeam.Enemy, MinionOrderTypes.Health).ToList();
                var allMinionsQNonPoisoned = allMinionsQ.Where(x => !x.HasBuffOfType(BuffType.Poison)).ToList();

                if (allMinionsQNonPoisoned.Any())
                {
                    var farmNonPoisoned = Q.GetCircularFarmLocation(allMinionsQNonPoisoned, Q.Width * 0.8f);
                    if (farmNonPoisoned.MinionsHit >= 3)
                    {
                        Q.Cast(farmNonPoisoned.Position);
                        dtLastQCast = Environment.TickCount;
                        return;
                    }
                }

                if (allMinionsQ.Any())
                {
                    var farmAll = Q.GetCircularFarmLocation(allMinionsQ, Q.Width * 0.8f);
                    //if (farmAll.MinionsHit >= 2 || allMinionsQ.Count == 1)
                    {
                        Q.Cast(farmAll.Position);
                        dtLastQCast = Environment.TickCount;
                        return;
                    }
                }
            }

            if (W.IsReady() && useW && Player.GetManaPerc() >= LaneClearMinMana && Environment.TickCount > dtLastQCast + Q.Delay * 1000)
            {
                var allMinionsW = MinionManager.GetMinions(Player.ServerPosition, W.Range + W.Width, MinionTypes.All).ToList();
                var allMinionsWNonPoisoned = allMinionsW.Where(x => !x.HasBuffOfType(BuffType.Poison)).ToList();

                if (allMinionsWNonPoisoned.Any())
                {
                    var farmNonPoisoned = W.GetCircularFarmLocation(allMinionsWNonPoisoned, W.Width * 0.8f);
                    if (farmNonPoisoned.MinionsHit >= 3)
                    {
                        W.Cast(farmNonPoisoned.Position);
                        return;
                    }
                }

                if (allMinionsW.Any())
                {
                    var farmAll = W.GetCircularFarmLocation(allMinionsW, W.Width * 0.8f);
                    if (farmAll.MinionsHit >= 2 || allMinionsW.Count == 1)
                    {
                        W.Cast(farmAll.Position);
                        return;
                    }
                }
            }

            if (E.IsReady() && useE)
            {
                MinionList = MinionManager.GetMinions(Player.ServerPosition, E.Range, MinionTypes.All, MinionTeam.Enemy, MinionOrderTypes.Health);

                foreach (var minion in MinionList.Where(x => UseELastHitLaneClearNonPoisoned || x.HasBuffOfType(BuffType.Poison)))
                {
                    var buffEndTime = UseELastHitLaneClearNonPoisoned ? float.MaxValue : GetPoisonBuffEndTime(minion);
                    if (buffEndTime > Game.Time + E.Delay)
                    {
                        if (UseELastHitLaneClear)
                        {
                            //var landTime = Q.Delay + 1000 * Player.Distance(minion) / 1400;
                                if (ObjectManager.Player.GetSpellDamage(minion, SpellSlot.E) * 0.9 > HealthPrediction.GetHealthPrediction(minion, (int)(E.Delay + (minion.Distance(ObjectManager.Player.Position) / E.Speed))))
                            {
                                CastE(minion);
                            }
                        }
                        else if (Player.GetManaPerc() >= LaneClearMinMana)
                        {
                            CastE(minion);
                        }
                    }
                    else
                    {
                        if (mustDebug)
                            Game.PrintChat("DONT CAST : buffEndTime " + buffEndTime);
                    }
                }
            }
        }

        /*private static double GetEDamageToMinion(Obj_AI_Base minion)
        {
            // Workaround cause DamageLib does not have the correct values for Cassio
            var damageSpell = new DamageSpell { Slot = SpellSlot.E, DamageType = LeagueSharp.Common.Damage.DamageType.Magical, Damage = (source, target, level) => new double[] { 45, 85, 120, 155, 190 }[level] + (0.55 * source.FlatMagicDamageMod) };
            var rawDamage = damageSpell.Damage(Player, minion, Math.Max(1, Math.Min(Player.Spellbook.GetSpell(SpellSlot.Q).Level - 1, 6)));

            return CalcMagicDamage(Player, minion, rawDamage);
        }

        private static double CalcMagicDamage(Obj_AI_Base source, Obj_AI_Base target, double amount)
        {
            var magicResist = (target.SpellBlock * source.PercentMagicPenetrationMod) - source.FlatMagicPenetrationMod;

            double k;
            if (magicResist < 0)
            {
                k = 2 - 100 / (100 - magicResist);
            }
            else
            {
                k = 100 / (100 + magicResist);
            }

            k = k * (1 - target.PercentMagicReduction) * (1 + target.PercentMagicDamageMod);

            return k * amount;
        }*/

        private static float GetPoisonBuffEndTime(Obj_AI_Base target)
        {
            var buffEndTime = target.Buffs.OrderByDescending(buff => buff.EndTime - Game.Time)
                    .Where(buff => buff.Type == BuffType.Poison)
                    .Select(buff => buff.EndTime)
                    .FirstOrDefault();
            return buffEndTime;
        }

        /*public static void Freeze()
        {
            MinionList = MinionManager.GetMinions(Player.ServerPosition, Q.Range, MinionTypes.All, MinionTeam.Enemy, MinionOrderTypes.Health);

            if (!MinionList.Any())
                return;

            var packetCast = Config.Item("PacketCast").GetValue<bool>();
            var useE = Config.Item("UseEFreeze").GetValue<bool>();

            if (useE)
            {
                foreach (var minion in MinionList)
                {
                    var buffEndTime = GetPoisonBuffEndTime(minion);

                    if (E.IsReady() && buffEndTime > (Game.Time + E.Delay) && minion.IsValidTarget(E.Range))
                    {
                        if (GetEDamageToMinion(minion) * 0.9 > minion.Health)
                        {
                            CastE(minion);
                        }
                    }
                }
            }

        }*/

        private static void LastHit()
        {
            var castE = Config.Item("LastHitE").GetValue<bool>() && E.IsReady();
            var LHE = Config.Item("UseELastHit").GetValue<bool>() && E.IsReady();
            var minions = MinionManager.GetMinions(ObjectManager.Player.ServerPosition, Q.Range, MinionTypes.All, MinionTeam.NotAlly);

            if (LHE)
            {
                foreach (var minion in minions)
                {
                    if (HealthPrediction.GetHealthPrediction(minion, (int)(E.Delay + (minion.Distance(ObjectManager.Player.Position) / E.Speed))) < ObjectManager.Player.GetSpellDamage(minion, SpellSlot.E))
                    {
                        E.Cast(minion);
                    }
                }
            }
            if (castE)
            {
                foreach (var minion in minions)
                {
                    if (HealthPrediction.GetHealthPrediction(minion, (int)(E.Delay + (minion.Distance(ObjectManager.Player.Position) / E.Speed))) < ObjectManager.Player.GetSpellDamage(minion, SpellSlot.E))
                    {
                        E.Cast(minion);
                    }
                }
            }
        }
        private static void JungleClear()
        {
            var mobs = MinionManager.GetMinions(Player.ServerPosition, Q.Range, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth);

            if (!mobs.Any())
                return;

            var UseQJungleClear = Config.Item("UseQJungleClear").GetValue<bool>();
            var UseEJungleClear = Config.Item("UseEJungleClear").GetValue<bool>();
            var packetCast = Config.Item("PacketCast").GetValue<bool>();

            var mob = mobs.First();

            if (UseQJungleClear && Q.IsReady() && mob.IsValidTarget(Q.Range))
            {
                Q.Cast(mob.ServerPosition);
            }

            if (UseEJungleClear && E.IsReady() && mob.IsValidTarget(E.Range))
            {
                CastE(mob);
            }
        }

        private static void UseUltUnderTower()
        {
            var packetCast = Config.Item("PacketCast").GetValue<bool>();
            var UseUltUnderTower = Config.Item("UseUltUnderTower").GetValue<bool>();

            if (UseUltUnderTower)
            {
                foreach (var eTarget in DevHelper.GetEnemyList())
                {
                    if (eTarget.IsValidTarget(R.Range) && eTarget.IsUnderEnemyTurret() && R.IsReady() && !eTarget.IsInvulnerable)
                    {
                        R.Cast(eTarget.ServerPosition);
                    }
                }
            }
        }

        public static void CastE(Obj_AI_Base unit)
        {
            var packetCast = Config.Item("PacketCast").GetValue<bool>();
            var PlayLegit = Config.Item("PlayLegit").GetValue<bool>();
            var DisableNFE = Config.Item("DisableNFE").GetValue<bool>();
            var LegitCastDelay = Config.Item("LegitCastDelay").GetValue<Slider>().Value;

            if (PlayLegit && DisableNFE)
                packetCast = false;

            if (PlayLegit)
            {
                if (Environment.TickCount > dtLastECast + LegitCastDelay)
                {
                    E.CastOnUnit(unit);
                    dtLastECast = Environment.TickCount;
                }
                else if (mustDebug)
                {
                    Game.PrintChat("E delay!!");
                }
            }
            else
            {
                E.CastOnUnit(unit);
                dtLastECast = Environment.TickCount;
            }
        }

        public static void CastAssistedUlt()
        {
            if (mustDebug)
                Game.PrintChat("CastAssistedUlt Start");

            var eTarget = Player.GetNearestEnemy();
            var packetCast = Config.Item("PacketCast").GetValue<bool>();

            if (eTarget.IsValidTarget(R.Range) && R.IsReady())
            {
                R.Cast(eTarget.ServerPosition);
            }

        }

        private static void onGameLoad(EventArgs args)
        {
            try
            {
                Player = ObjectManager.Player;

                if (!Player.ChampionName.ToLower().Contains(ChampionName))
                    return;

                InitializeSpells();

                /*InitializeSkinManager();*/

                InitializeLevelUpManager();

                InitializeMainMenu();

                InitializeAttachEvents();

                Game.PrintChat(string.Format("<font color='#fb762d'>{0} Loaded v{1}</font>", Assembly.GetExecutingAssembly().GetName().Name, Assembly.GetExecutingAssembly().GetName().Version));

                assemblyUtil = new AssemblyUtil(Assembly.GetExecutingAssembly().GetName().Name);
                assemblyUtil.onGetVersionCompleted += AssemblyUtil_onGetVersionCompleted;
                assemblyUtil.GetLastVersionAsync();

            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
                if (mustDebug)
                    Game.PrintChat(ex.ToString());
            }
        }

        static void AssemblyUtil_onGetVersionCompleted(OnGetVersionCompletedArgs args)
        {
            if (args.LastAssemblyVersion == Assembly.GetExecutingAssembly().GetName().Version.ToString())
                Game.PrintChat(string.Format("<font color='#fb762d'>DevCassio You have the latest version.</font>"));
            else
                Game.PrintChat(string.Format("<font color='#fb762d'>DevCassio NEW VERSION available! Tap F8 for Update! {0}</font>", args.LastAssemblyVersion));
        }

        private static void InitializeAttachEvents()
        {
            if (mustDebug)
                Game.PrintChat("InitializeAttachEvents Start");

            Game.OnUpdate += Game_OnGameUpdate;
            Game.OnSendPacket += Game_OnGameSendPacket;
            Game.OnWndProc += Game_OnWndProc;
            Drawing.OnDraw += OnDraw;
            AntiGapcloser.OnEnemyGapcloser += AntiGapcloser_OnEnemyGapcloser;
            //Interrupter.OnPossibleToInterrupt += Interrupter_OnPossibleToInterrupt;
            Interrupter2.OnInterruptableTarget += Interrupter2_OnInterruptableTarget;
            Spellbook.OnCastSpell += Spellbook_OnCastSpell;
            Orbwalking.BeforeAttack += Orbwalking_BeforeAttack;
            GameObject.OnCreate += GameObject_OnCreate;
            
            // Damage Bar
            Config.Item("EDamage").ValueChanged += (object sender, OnValueChangeEventArgs e) => { Utility.HpBarDamageIndicator.Enabled = e.GetNewValue<bool>(); };
            if (Config.Item("EDamage").GetValue<bool>())
            {
                Utility.HpBarDamageIndicator.DamageToUnit += GetEDamage;
                Utility.HpBarDamageIndicator.Enabled = true;
            }

            // Ult Range
            Config.Item("UltRange").ValueChanged += (object sender, OnValueChangeEventArgs e) => { R.Range = e.GetNewValue<Slider>().Value; };
            R.Range = Config.Item("UltRange").GetValue<Slider>().Value;

            if (mustDebug)
                Game.PrintChat("InitializeAttachEvents Finish");
        }

        static void Interrupter2_OnInterruptableTarget(Obj_AI_Hero sender, Interrupter2.InterruptableTargetEventArgs args)
        {
            var packetCast = Config.Item("PacketCast").GetValue<bool>();
            var RInterrupetSpell = Config.Item("RInterrupetSpell").GetValue<bool>();
            var RAntiGapcloserMinHealth = Config.Item("RAntiGapcloserMinHealth").GetValue<Slider>().Value;

            if (RInterrupetSpell && Player.GetHealthPerc() < RAntiGapcloserMinHealth && sender.IsValidTarget(R.Range) && args.DangerLevel >= Interrupter2.DangerLevel.High)
            {
                if (R.CastIfHitchanceEquals(sender, sender.IsMoving ? HitChance.High : HitChance.Medium))
                    Game.PrintChat(string.Format("OnPosibleToInterrupt -> RInterrupetSpell on {0} !", sender.SkinName));
            }
        }

        private static void Spellbook_OnCastSpell(Spellbook sender, SpellbookCastSpellEventArgs args)
        {
            var enemy = Orbwalker.GetTarget() as Obj_AI_Hero;
            var enemyr = HeroManager.Enemies.Where(x => R.WillHit(enemy, args.Target.Position));
            if (Config.Item("BlockR").GetValue<bool>())
            {
                return;
            }
            else
            /*var query = DevHelper.GetEnemyList().Where(x => !R.WillHit(enemy, args.StartPosition));*/
            /*if (HeroManager.Enemies.All(enemy => !enemy.IsValidTarget(R.Range) || !R.WillHit(enemy, args.StartPosition)))*/
            if (Config.Item("BlockR").GetValue<bool>() && enemyr.Count() == 0)
            {
                args.Process = false;
                Game.PrintChat(string.Format("Ult Blocked"));
            }
        }

            static void GameObject_OnCreate(GameObject sender, EventArgs args)
        {

        }


        static void Orbwalking_BeforeAttack(Orbwalking.BeforeAttackEventArgs args)
        {
            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo)
            {
                args.Process = Config.Item("UseAACombo").GetValue<bool>();

                if (args.Target is Obj_AI_Base)
                {
                    var target = args.Target as Obj_AI_Base;
                    if (E.IsReady() && target.HasBuffOfType(BuffType.Poison) && target.IsValidTarget(E.Range))
                        args.Process = false;
                }
            }
        }

        private static void InitializeSpells()
        {
            if (mustDebug)
                Game.PrintChat("InitializeSpells Start");

            Q = new Spell(SpellSlot.Q, 850);
            Q.SetSkillshot(0.7f, 75f, float.MaxValue, false, SkillshotType.SkillshotCircle);

            W = new Spell(SpellSlot.W, 800);
            W.SetSkillshot(0.75f, 160f, 1000, false, SkillshotType.SkillshotCircle);

            E = new Spell(SpellSlot.E, 700);
            E.SetTargetted(0.125f, 1000);

            R = new Spell(SpellSlot.R, 825);
            R.SetSkillshot(0.5f, (float)(80 * Math.PI / 180), 3200, false, SkillshotType.SkillshotCone);

            SpellList.Add(Q);
            SpellList.Add(W);
            SpellList.Add(E);
            SpellList.Add(R);

            summonerSpellManager = new SummonerSpellManager();

            if (mustDebug)
                Game.PrintChat("InitializeSpells Finish");
        }

        /*private static void InitializeSkinManager()
        {
            if (mustDebug)
                Game.PrintChat("InitializeSkinManager Start");

            skinManager = new SkinManager();
            skinManager.Add("Cassio");
            skinManager.Add("Desperada Cassio");
            skinManager.Add("Siren Cassio");
            skinManager.Add("Mythic Cassio");
            skinManager.Add("Jade Fang Cassio");

            if (mustDebug)
                Game.PrintChat("InitializeSkinManager Finish");
        }*/

        private static void InitializeLevelUpManager()
        {
            if (mustDebug)
                Game.PrintChat("InitializeLevelUpManager Start");

            var priority1 = new int[] { 0, 2, 2, 1, 2, 3, 2, 0, 2, 0, 3, 0, 0, 1, 2, 3, 1, 1 };
            var priority2 = new int[] { 0, 2, 1, 2, 2, 3, 2, 0, 2, 0, 3, 0, 0, 1, 1, 3, 1, 1 };

            levelUpManager = new LevelUpManager();
            levelUpManager.Add("Q > E > E > W ", priority1);
            levelUpManager.Add("Q > E > W > E ", priority2);

            if (mustDebug)
                Game.PrintChat("InitializeLevelUpManager Finish");
        }

        static void Game_OnGameSendPacket(GamePacketEventArgs args)
        {
            //var BlockUlt = Config.Item("BlockUlt").GetValue<bool>();

            //if (BlockUlt && args.PacketData[0] == Packet.C2S.Cast.Header)
            //{
            //    var decodedPacket = Packet.C2S.Cast.Decoded(args.PacketData);
            //    if (decodedPacket.SourceNetworkId == Player.NetworkId && decodedPacket.Slot == SpellSlot.R)
            //    {
            //        Vector3 vecCast = new Vector3(decodedPacket.ToX, decodedPacket.ToY, 0);
            //        var query = DevHelper.GetEnemyList().Where(x => R.WillHit(x, vecCast));

            //        if (query.Count() == 0)
            //        {
            //            args.Process = false;
            //            Game.PrintChat(string.Format("Ult Blocked"));
            //        }
            //    }
            //}
        }

        static void Game_OnWndProc(WndEventArgs args)
        {
            if (MenuGUI.IsChatOpen)
                return;

            var UseAssistedUlt = Config.Item("UseAssistedUlt").GetValue<bool>();
            var AssistedUltKey = Config.Item("AssistedUltKey").GetValue<KeyBind>().Key;

            if (UseAssistedUlt && args.WParam == AssistedUltKey)
            {
                if (mustDebug)
                    Game.PrintChat("CastAssistedUlt");

                args.Process = false;
                CastAssistedUlt();
            }
        }

        //static void Interrupter_OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        //{
        //    var packetCast = Config.Item("PacketCast").GetValue<bool>();
        //    var RInterrupetSpell = Config.Item("RInterrupetSpell").GetValue<bool>();
        //    var RAntiGapcloserMinHealth = Config.Item("RAntiGapcloserMinHealth").GetValue<Slider>().Value;

        //    if (RInterrupetSpell && Player.GetHealthPerc() < RAntiGapcloserMinHealth && unit.IsValidTarget(R.Range) && spell.DangerLevel >= InterruptableDangerLevel.High)
        //    {
        //        if (R.CastIfHitchanceEquals(unit, unit.IsMoving ? HitChance.High : HitChance.Medium, packetCast))
        //            Game.PrintChat(string.Format("OnPosibleToInterrupt -> RInterrupetSpell on {0} !", unit.SkinName));
        //    }
        //}

        static void AntiGapcloser_OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            var packetCast = Config.Item("PacketCast").GetValue<bool>();
            var RAntiGapcloser = Config.Item("RAntiGapcloser").GetValue<bool>();
            var RAntiGapcloserMinHealth = Config.Item("RAntiGapcloserMinHealth").GetValue<Slider>().Value;

            if (RAntiGapcloser && Player.GetHealthPerc() <= RAntiGapcloserMinHealth && gapcloser.Sender.IsValidTarget(R.Range) && R.IsReady() && !gapcloser.Sender.IsInvulnerable)
            {
                R.Cast(gapcloser.Sender.ServerPosition);
            }
        }

        private static float GetEDamage(Obj_AI_Hero hero)
        {
            return (float)Damage.GetSpellDamage(Player, hero, SpellSlot.E);
        }

        private static void OnDraw(EventArgs args)
        {
            foreach (var spell in SpellList)
            {
                var menuItem = Config.Item(spell.Slot + "Range").GetValue<Circle>();
                if (menuItem.Active)
                {
                    if (spell.IsReady())
                        Render.Circle.DrawCircle(ObjectManager.Player.Position, spell.Range, System.Drawing.Color.Green);
                    else
                        Render.Circle.DrawCircle(ObjectManager.Player.Position, spell.Range, System.Drawing.Color.Red);
                }
            }

            if (mustDebugPredict)
                DrawPrediction();
        }

        public static void DrawPrediction()
        {
            var eTarget = TargetSelector.GetTarget(Q.Range * 5, TargetSelector.DamageType.Magical);

            if (eTarget == null)
                return;

            var Qpredict = Q.GetPrediction(eTarget, true);
            Render.Circle.DrawCircle(Qpredict.CastPosition, Q.Width, Qpredict.Hitchance >= HitChance.High ? System.Drawing.Color.Green : System.Drawing.Color.Red);
        }


        private static void InitializeMainMenu()
        {
            if (mustDebug)
                Game.PrintChat("InitializeMainMenu Start");

            Config = new Menu("DevCassio", "DevCassio", true);

            var targetSelectorMenu = new Menu("Target Selector", "Target Selector");
            TargetSelector.AddToMenu(targetSelectorMenu);
            Config.AddSubMenu(targetSelectorMenu);

            Config.AddSubMenu(new Menu("Orbwalking", "Orbwalking"));
            Orbwalker = new Orbwalking.Orbwalker(Config.SubMenu("Orbwalking"));

            Config.AddSubMenu(new Menu("Ultimate", "Ultimate"));
            Config.SubMenu("Ultimate").AddItem(new MenuItem("UseAssistedUlt", "Use AssistedUlt").SetValue(true));
            Config.SubMenu("Ultimate").AddItem(new MenuItem("AssistedUltKey", "Assisted Ult Key").SetValue((new KeyBind("R".ToCharArray()[0], KeyBindType.Press))));
            Config.SubMenu("Ultimate").AddItem(new MenuItem("BlockR", "Block Ult When 0 Hit").SetValue(true));
            Config.SubMenu("Ultimate").AddItem(new MenuItem("UseUltUnderTower", "Ult Enemy Under Tower").SetValue(true));
            Config.SubMenu("Ultimate").AddItem(new MenuItem("UltRange", "Ultimate Range").SetValue(new Slider(650, 0, 800)));
            Config.SubMenu("Ultimate").AddItem(new MenuItem("RMinHit", "Min Enemies Hit").SetValue(new Slider(2, 1, 5)));
            Config.SubMenu("Ultimate").AddItem(new MenuItem("RMinHitFacing", "Min Enemies Facing").SetValue(new Slider(1, 1, 5)));

            Config.AddSubMenu(new Menu("Combo", "Combo"));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseQCombo", "Use Q").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseWCombo", "Use W").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseECombo", "Use E").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseRCombo", "Use R").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseIgnite", "Use Ignite").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseAACombo", "Use AA in Combo").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("ProbeltR", "Use Problet R").SetValue(false));
            Config.SubMenu("Combo").AddItem(new MenuItem("Rflash", "Use Flash R").SetValue(true)).SetValue((new KeyBind("J".ToCharArray()[0], KeyBindType.Press)));
            Config.SubMenu("Combo").AddItem(new MenuItem("Rminflash", "Min Enemies F + R").SetValue(new Slider(2, 1, 5)));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseRSaveYourself", "Use R Save Yourself").SetValue(true));
            Config.SubMenu("Combo").AddItem(new MenuItem("UseRSaveYourselfMinHealth", "Use R Save MinHealth").SetValue(new Slider(25, 0, 100)));

            Config.AddSubMenu(new Menu("Harass", "Harass"));
            Config.SubMenu("Harass").AddItem(new MenuItem("HarassToggle", "Harras Active (toggle)").SetValue(new KeyBind("G".ToCharArray()[0], KeyBindType.Toggle)));
            Config.SubMenu("Harass").AddItem(new MenuItem("UseQHarass", "Use Q").SetValue(true));
            Config.SubMenu("Harass").AddItem(new MenuItem("UseWHarass", "Use W").SetValue(false));
            Config.SubMenu("Harass").AddItem(new MenuItem("UseEHarass", "Use E").SetValue(true));
            Config.SubMenu("Harass").AddItem(new MenuItem("LastHitE", "LastHit E While Harass").SetValue(false));

            Config.AddSubMenu(new Menu("LastHit", "LastHit"));
            Config.SubMenu("LastHit").AddItem(new MenuItem("UseELastHit", "Use E").SetValue(true));

            Config.AddSubMenu(new Menu("LaneClear", "LaneClear"));
            Config.SubMenu("LaneClear").AddItem(new MenuItem("UseQLaneClear", "Use Q").SetValue(true));
            Config.SubMenu("LaneClear").AddItem(new MenuItem("UseWLaneClear", "Use W").SetValue(false));
            Config.SubMenu("LaneClear").AddItem(new MenuItem("UseELaneClear", "Use E").SetValue(true));
            Config.SubMenu("LaneClear").AddItem(new MenuItem("UseELastHitLaneClear", "Use E Only LastHit").SetValue(true));
            Config.SubMenu("LaneClear").AddItem(new MenuItem("UseELastHitLaneClearNonPoisoned", "Use E LastHit on Non Poisoned creeps").SetValue(false));
            Config.SubMenu("LaneClear").AddItem(new MenuItem("LaneClearMinMana", "LaneClear Min Mana").SetValue(new Slider(25, 0, 100)));

            Config.AddSubMenu(new Menu("JungleClear", "JungleClear"));
            Config.SubMenu("JungleClear").AddItem(new MenuItem("UseQJungleClear", "Use Q").SetValue(true));
            Config.SubMenu("JungleClear").AddItem(new MenuItem("UseEJungleClear", "Use E").SetValue(true));

            Config.AddSubMenu(new Menu("Gapcloser", "Gapcloser"));
            Config.SubMenu("Gapcloser").AddItem(new MenuItem("RAntiGapcloser", "R AntiGapcloser").SetValue(true));
            Config.SubMenu("Gapcloser").AddItem(new MenuItem("RInterrupetSpell", "R InterruptSpell").SetValue(true));
            Config.SubMenu("Gapcloser").AddItem(new MenuItem("RAntiGapcloserMinHealth", "R AntiGapcloser Min Health").SetValue(new Slider(60, 0, 100)));

            Config.AddSubMenu(new Menu("Misc", "Misc"));
            Config.SubMenu("Misc").AddItem(new MenuItem("PacketCast", "No-Face Exploit (PacketCast)").SetValue(true)).SetTooltip("Packet Does Not Work");

            Config.AddSubMenu(new Menu("Im Legit! :)", "Legit"));
            Config.SubMenu("Legit").AddItem(new MenuItem("PlayLegit", "Play Legit :)").SetValue(false));
            Config.SubMenu("Legit").AddItem(new MenuItem("DisableNFE", "Disable No-Face Exploit").SetValue(true));
            Config.SubMenu("Legit").AddItem(new MenuItem("LegitCastDelay", "Cast E Delay").SetValue(new Slider(1000, 0, 2000)));

            Config.AddSubMenu(new Menu("Drawings", "Drawings"));
            Config.SubMenu("Drawings").AddItem(new MenuItem("QRange", "Q Range").SetValue(new Circle(true, System.Drawing.Color.FromArgb(255, 255, 255, 255))));
            Config.SubMenu("Drawings").AddItem(new MenuItem("WRange", "W Range").SetValue(new Circle(false, System.Drawing.Color.FromArgb(255, 255, 255, 255))));
            Config.SubMenu("Drawings").AddItem(new MenuItem("ERange", "E Range").SetValue(new Circle(false, System.Drawing.Color.FromArgb(255, 255, 255, 255))));
            Config.SubMenu("Drawings").AddItem(new MenuItem("RRange", "R Range").SetValue(new Circle(false, System.Drawing.Color.FromArgb(255, 255, 255, 255))));
            Config.SubMenu("Drawings").AddItem(new MenuItem("EDamage", "Show E Damage on HPBar").SetValue(true));

            Config.AddSubMenu(new Menu("Skins Menu", "SkinMenu"));
            Config.SubMenu("SkinMenu").AddItem(new MenuItem("SkinID", "Skin ID")).SetValue(new Slider(4, 0, 8));
            var UseSkin = Config.SubMenu("UseSkin").AddItem(new MenuItem("UseSkin", "Enabled Skin Change").SetValue(true));
            UseSkin.ValueChanged += (sender, eventArgs) =>
            {
                if (!eventArgs.GetNewValue<bool>())
                {
                    ObjectManager.Player.SetSkin(ObjectManager.Player.CharData.BaseSkinName, ObjectManager.Player.BaseSkinId);
                }
            };

            /* skinManager.AddToMenu(ref Config);*/

            levelUpManager.AddToMenu(ref Config);

            Config.AddToMainMenu();

            if (mustDebug)
                Game.PrintChat("InitializeMainMenu Finish");
        }
    }
}