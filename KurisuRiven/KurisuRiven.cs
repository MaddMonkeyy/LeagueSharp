using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using LeagueSharp;
using LeagueSharp.Common;
using Color = System.Drawing.Color;
using SharpDX;

namespace KurisuRiven
{
    internal class KurisuRiven
    {
        #region Riven: Main

        private static int lastq;
        private static int lastw;
        private static int laste;
        private static int lastaa;
        private static int lasthd;
        private static int lastwd;

        private static bool canq;
        private static bool canw;
        private static bool cane;
        private static bool canmv;
        private static bool canaa;
        private static bool canws;
        private static bool canhd;
        private static bool hashd;

        private static bool didq;
        private static bool didw;
        private static bool dide;
        private static bool didws;
        private static bool didaa;
        private static bool didhd;
        private static bool didhs;
        private static bool ssfl;

        private static Menu menu;
        private static Spell q, w, e, r;
        private static Orbwalking.Orbwalker orbwalker;
        private static Obj_AI_Hero player = ObjectManager.Player;
        private static HpBarIndicator hpi = new HpBarIndicator();
        private static Obj_AI_Base qtarg; // semi q target

        private static int qq;
        private static int cc;
        private static int pc;  
        private static bool uo;
        private static SpellSlot flash;

        private static float wrange;
        private static float truerange;
        private static Vector3 movepos;
        #endregion

        # region Riven: Utils

        private static bool menubool(string item)
        {
            return menu.Item(item).GetValue<bool>();
        }

        private static int menuslide(string item)
        {
            return menu.Item(item).GetValue<Slider>().Value;
        }

        private static int menulist(string item)
        {
            return menu.Item(item).GetValue<StringList>().SelectedIndex;
        }

        private static float xtra(float dmg)
        {
           return r.IsReady() ? (float) (dmg + (dmg*0.2)) : dmg;
        }

        private static bool IsLethal(Obj_AI_Base unit)
        {
            return ComboDamage(unit) / 1.65 >= unit.Health;
        }

        private static Obj_AI_Base GetCenterMinion()
        {
            var minionposition = MinionManager.GetMinions(300 + q.Range).Select(x => x.Position.To2D()).ToList();
            var center = MinionManager.GetBestCircularFarmLocation(minionposition, 250, 300 + q.Range);

            return center.MinionsHit >= 3
                ? MinionManager.GetMinions(1000).OrderBy(x => x.Distance(center.Position)).FirstOrDefault()
                : null;
        }

        private static void TryIgnote(Obj_AI_Base target)
        {
            var ignote = player.GetSpellSlot("summonerdot");
            if (player.Spellbook.CanUseSpell(ignote) == SpellState.Ready)
            {
                if (target.Distance(player.ServerPosition) <= 600)
                {
                    if (cc <= menuslide("userq") && q.IsReady() && menubool("useignote"))
                    {
                        if (ComboDamage(target) >= target.Health &&
                            target.Health / target.MaxHealth * 100 > menuslide("overk"))
                        {
                            if (r.IsReady() && uo)
                            {
                                player.Spellbook.CastSpell(ignote, target);
                            }
                        }
                    }
                }
            }
        }

        private static void useinventoryitems(Obj_AI_Base target)
        {
            if (Items.HasItem(3142) && Items.CanUseItem(3142))
                Items.UseItem(3142);

            if (target.Distance(player.ServerPosition, true) <= 450 * 450)
            {
                if (Items.HasItem(3144) && Items.CanUseItem(3144))
                    Items.UseItem(3144, target);
                if (Items.HasItem(3153) && Items.CanUseItem(3153))
                    Items.UseItem(3153, target);
            }
        }

        private static readonly string[] minionlist =
        {
            // summoners rift
            "SRU_Razorbeak", "SRU_Krug", "Sru_Crab", "SRU_Baron", "SRU_Dragon",
            "SRU_Blue", "SRU_Red", "SRU_Murkwolf", "SRU_Gromp", 
            
            // twisted treeline
            "TT_NGolem5", "TT_NGolem2", "TT_NWolf6", "TT_NWolf3",
            "TT_NWraith1", "TT_Spider"
        };

        #endregion

        public KurisuRiven()
        {
            CustomEvents.Game.OnGameLoad += args =>
            {
                try
                {
                    if (player.ChampionName != "Riven") 
                        return;

                    w = new Spell(SpellSlot.W, 250f);
                    e = new Spell(SpellSlot.E, 270f);

                    q = new Spell(SpellSlot.Q, 260f);
                    q.SetSkillshot(0.25f, 100f, 2200f, false, SkillshotType.SkillshotCircle);

                    r = new Spell(SpellSlot.R, 900f);
                    r.SetSkillshot(0.25f, (float) (45 * 0.5), 1600f, false, SkillshotType.SkillshotCircle);

                    flash = player.GetSpellSlot("summonerflash");

                    OnNewPath();
                    OnPlayAnimation();
                    Interrupter();
                    OnGapcloser();
                    OnCast();
                    Drawings();
                    OnMenuLoad();

                    Game.OnUpdate += Game_OnUpdate;
                    Game.OnWndProc += Game_OnWndProc;

                    Game.PrintChat("<b>Kurisu's Riven</b> - Loaded!");
                    Updater.UpdateCheck();

                    if (menu.Item("Farm").GetValue<KeyBind>().Key == menu.Item("semiq").GetValue<KeyBind>().Key ||
                        menu.Item("Orbwalk").GetValue<KeyBind>().Key == menu.Item("semiq").GetValue<KeyBind>().Key ||
                        menu.Item("LaneClear").GetValue<KeyBind>().Key == menu.Item("semiq").GetValue<KeyBind>().Key ||
                        menu.Item("LastHit").GetValue<KeyBind>().Key == menu.Item("semiq").GetValue<KeyBind>().Key)
                    {
                        Console.WriteLine(
                            "<b><font color=\"#FF9900\">" +
                            "WARNING: Semi-Q Keybind Should not be the same key as any of " +
                            "the other orbwalking modes or it will not Work!</font></b>");
                    }

                }

                catch (Exception e)
                {
                    Console.WriteLine("Fatal Error: " + e.Message);
                }
            };
        }

        private static Obj_AI_Hero _sh;
        void Game_OnWndProc(WndEventArgs args)
        {
            if (args.Msg == (ulong) WindowsMessages.WM_LBUTTONDOWN)
            {
                _sh = HeroManager.Enemies
                     .FindAll(hero => hero.IsValidTarget() && hero.Distance(Game.CursorPos, true) < 40000) // 200 * 200
                     .OrderBy(h => h.Distance(Game.CursorPos, true)).FirstOrDefault();
            }
        }

        private static Obj_AI_Hero riventarget()
        {
            var cursortarg = HeroManager.Enemies
                .Where(x => x.Distance(Game.CursorPos) <= 375 &&  x.Distance(player.ServerPosition) <= 1200)
                .OrderBy(x => x.Distance(Game.CursorPos)).FirstOrDefault(x => x.IsEnemy);

            var closetarg = HeroManager.Enemies
                .Where(x => x.Distance(player.ServerPosition) <= 1200)
                .OrderBy(x => x.Distance(player.ServerPosition)).FirstOrDefault(x => x.IsEnemy);

            return _sh ?? cursortarg ?? closetarg;
        }

        #region Riven: OnNewPath 
        private static void OnNewPath()
        {
            Obj_AI_Base.OnNewPath += (sender, args) =>
            {
                if (sender.IsMe && !args.IsDash)
                {
                    if (args.Path.Count() > 1 || didq)
                    {
                        didq = false;
                        canmv = true;
                        canaa = true;
                    }
                }
            };
        }

        #endregion

        #region Riven: OnUpdate
        private static void Game_OnUpdate(EventArgs args)
        {
            // harass active
            didhs = menu.Item("harasskey").GetValue<KeyBind>().Active;

            // ulti check
            uo = player.GetSpell(SpellSlot.R).Name != "RivenFengShuiEngine";

            // hydra check
            hashd = Items.HasItem(3077) || Items.HasItem(3074) || Items.HasItem(3748);
            canhd = canmv && (Items.CanUseItem(3077) || Items.CanUseItem(3074) || Items.CanUseItem(3748));

            // my radius
            truerange = player.AttackRange + player.Distance(player.BBox.Minimum) + 1;

            // if no valid target cancel to cursor pos
            if (!qtarg.IsValidTarget(truerange + 100))
                 qtarg = player;

            if (riventarget().IsValidTarget())
            {
                if (menu.Item("combokey").GetValue<KeyBind>().Active ||
                    menu.Item("harasskey").GetValue<KeyBind>().Active ||
                    menu.Item("shycombo").GetValue<KeyBind>().Active)
                {
                    orbwalker.ForceTarget(riventarget());
                }
            }

            else
                _sh = null;

            if (!canmv && didq)
            {
                player.IssueOrder(GameObjectOrder.MoveTo, movepos);
            }

            // riven w range
            wrange = uo ? w.Range + 25 : w.Range;

            switch (menulist("qcancel"))
            {
                case 0:
                    // move behind me
                    if (qtarg != player && qtarg.IsFacing(player) && qtarg.Distance(player.ServerPosition) < truerange + 120)
                        movepos = player.ServerPosition + (player.ServerPosition - qtarg.ServerPosition).Normalized() * 24;

                    // move towards target (thanks yol0)
                    if (qtarg != player && (!qtarg.IsFacing(player) || qtarg.Distance(player.ServerPosition) > truerange + 120))
                        movepos = player.ServerPosition.Extend(qtarg.ServerPosition, 350);

                    // move to game cursor pos
                    if (qtarg == player)
                        movepos = player.ServerPosition + (Game.CursorPos - player.ServerPosition).Normalized() * 125;
                    break;
                case 1:
                    // move behind me
                    if (qtarg != player && qtarg.Distance(player.ServerPosition) <= 500)
                        movepos = player.ServerPosition + (player.ServerPosition - qtarg.ServerPosition).Normalized() * 24;

                    // move to game cursor pos
                    if (qtarg == player)
                        movepos = player.ServerPosition + (Game.CursorPos - player.ServerPosition).Normalized() * 125;

                    break;
                case 2:
                    // move towards target (thanks yol0)
                    if (qtarg != player && qtarg.Distance(player.ServerPosition) <= 500)
                        movepos = player.ServerPosition.Extend(qtarg.ServerPosition, 350);

                    // move to game cursor pos
                    if (qtarg == player)
                        movepos = player.ServerPosition + (Game.CursorPos - player.ServerPosition).Normalized() * 95;
                    break;
                case 3:
                    // move to game cursor pos
                    movepos = player.ServerPosition + (Game.CursorPos - player.ServerPosition).Normalized() * 125;
                    break;
            }

            SemiQ();
            AuraUpdate();
            CombatCore();

            orbwalker.SetAttack(canmv);
            orbwalker.SetMovement(canmv);

            if (riventarget().IsValidTarget() && 
                menu.Item("combokey").GetValue<KeyBind>().Active)
            {
                ComboTarget(riventarget());
                TryIgnote(riventarget());
            }

            if (menu.Item("shycombo").GetValue<KeyBind>().Active)
            {
                OrbTo(riventarget(), 350);

                if (!riventarget().IsValidTarget())
                    return;

                if (riventarget().Distance(player.ServerPosition) <= wrange)
                    w.Cast();

                SomeDash(riventarget());
                TryIgnote(riventarget());

                if (q.IsReady() && riventarget().Distance(player.ServerPosition) <= truerange + 100)
                {
                    useinventoryitems(riventarget());
                    checkr();

                    if (canhd)
                    {
                        return;
                    }

                    if (canq)
                    {
                        if (Utils.GameTimeTickCount - lastw >= 350)
                        {
                            q.Cast(riventarget().ServerPosition);
                        }
                    }
                }

            }

            if (didhs && riventarget().IsValidTarget())
                HarassTarget(riventarget());

            if (player.IsValid && menu.Item("clearkey").GetValue<KeyBind>().Active)
            {
                Clear();
                Wave();
            }

            if (player.IsValid && menu.Item("fleekey").GetValue<KeyBind>().Active)
                Flee();

            Windslash();
        }

        #endregion

        #region Riven: Menu
        private static void OnMenuLoad()
        {
            menu = new Menu("Kurisu's Riven VH", "kurisuriven", true);

            var orbwalkah = new Menu("Thả Diều", "rorb");
            orbwalker = new Orbwalking.Orbwalker(orbwalkah);
            menu.AddSubMenu(orbwalkah);

            var keybinds = new Menu("Cài Đặt Nút", "keybinds");
            keybinds.AddItem(new MenuItem("combokey", "Combo")).SetValue(new KeyBind(32, KeyBindType.Press));
            keybinds.AddItem(new MenuItem("harasskey", "Cấu rỉa")).SetValue(new KeyBind(67, KeyBindType.Press));
            keybinds.AddItem(new MenuItem("clearkey", "Dọn Lính/Rừng")).SetValue(new KeyBind(86, KeyBindType.Press));
            keybinds.AddItem(new MenuItem("fleekey", "Chạy Thoát")).SetValue(new KeyBind(65, KeyBindType.Press));
            keybinds.AddItem(new MenuItem("shycombo", "Dồn sát thương kiểu TheShy")).SetValue(new KeyBind('T', KeyBindType.Press));

            var mitem = new MenuItem("semiqlane", "Bán Tự Động Q Lính/Trụ (Ấn Giữ)");
            mitem.ValueChanged += (sender, args) =>
            {
                if (menu.Item("Farm").GetValue<KeyBind>().Key == args.GetNewValue<KeyBind>().Key ||
                    menu.Item("Orbwalk").GetValue<KeyBind>().Key == args.GetNewValue<KeyBind>().Key ||
                    menu.Item("LaneClear").GetValue<KeyBind>().Key == args.GetNewValue<KeyBind>().Key ||
                    menu.Item("LastHit").GetValue<KeyBind>().Key == args.GetNewValue<KeyBind>().Key)
                {
                    Game.PrintChat(
                        "<b><font color=\"#FF9900\">" +
                        "WARNING: Semi-Q Keybind Should not be the same key as any of " +
                        "the other orbwalking modes or it will not Work!</font></b>");
                }
            };

            keybinds.AddItem(mitem).SetValue(new KeyBind(71, KeyBindType.Press));
            keybinds.AddItem(new MenuItem("semiq", "Bán Tự Động Q Cấu rỉa/Rừng")).SetValue(true);
            menu.AddSubMenu(keybinds);

            var drMenu = new Menu("Hiển Thị", "drawings");
            drMenu.AddItem(new MenuItem("linewidth", "Chiều rộng đường kẻ")).SetValue(new Slider(1, 1, 6));
            drMenu.AddItem(new MenuItem("drawengage", "Hiển thị tầm giao tranh")).SetValue(new Circle(true, Color.FromArgb(150, Color.Gold)));
            drMenu.AddItem(new MenuItem("drawr2", "Hiển thị tầm R2(Chém gió)")).SetValue(new Circle(true, Color.FromArgb(150, Color.Gold)));
            drMenu.AddItem(new MenuItem("drawburst", "Hiển thị tầm dồn damage")).SetValue(new Circle(true, Color.FromArgb(150, Color.LawnGreen)));
            drMenu.AddItem(new MenuItem("drawf", "Hiển thị kẻ địch nhắm vào")).SetValue(new Circle(true, Color.FromArgb(255, Color.Red)));
            drMenu.AddItem(new MenuItem("drawdmg", "Hiển thị sát thương Combo")).SetValue(true);
            menu.AddSubMenu(drMenu);

            var combo = new Menu("Chiến!!", "combo");
            var qmenu = new Menu("Cài đặt Q", "rivenq");
            var advance = new Menu("Cài đặt Q nâng cao", "advance");
            advance.AddItem(new MenuItem("qcancel", "Hướng Cancel: "))
                .SetValue(new StringList(new[] {"Tự Động", "Phía Sau", "Kẻ Địch", "Con Trỏ"}, 0));
            advance.AddItem(new MenuItem("autoaq", "Thời gian trì hoãn Q (ms)")).SetValue(new Slider(15, -150, 300));
            advance.AddItem(new MenuItem("qqc", "Chạy thử ở chế độ Tùy Chọn với Cua Kì Cục")).SetFontStyle(FontStyle.Regular, SharpDX.Color.Gold);
            advance.AddItem(new MenuItem("qqa", "Thấp hơn = Q nhanh hơn nhưng có thể dẫn đến nhiều AA bị hủy"));
            advance.AddItem(new MenuItem("qqb", "Cao hơn = Q chậm nhưng ít hoặc không có AA bị hủy"));
            qmenu.AddSubMenu(advance);

            qmenu.AddItem(new MenuItem("wq3", "Cắm mắt + Q3 (Chạy)")).SetValue(true);
            qmenu.AddItem(new MenuItem("qint", "Phá đòn bằng Q3")).SetValue(true);
            qmenu.AddItem(new MenuItem("keepq", "Giữ Q được Buff Up")).SetValue(true);
            qmenu.AddItem(new MenuItem("usegap", "Áp sát bằng Q")).SetValue(true);
            qmenu.AddItem(new MenuItem("gaptimez", "Độ trì hoãn Áp Sát Q (ms)")).SetValue(new Slider(115, 0, 200));
            combo.AddSubMenu(qmenu);

            var wmenu = new Menu("Cài đặt W", "rivenw");
            wmenu.AddItem(new MenuItem("usecombow", "Dùng W trong Combo")).SetValue(true);
            wmenu.AddItem(new MenuItem("wmode", "Chế độ dùng W"))
                .SetValue(new StringList(new[] {"W -> AA -> Q", "W -> Q -> AA"}, 1));
            wmenu.AddItem(new MenuItem("wgap", "Dùng W lên kẻ bị Áp Sát")).SetValue(true);
            wmenu.AddItem(new MenuItem("wint", "Dùng W để Phá Đòn")).SetValue(true);
            combo.AddSubMenu(wmenu);

            var emenu = new Menu("Cài đặt E", "rivene");
            emenu.AddItem(new MenuItem("usecomboe", "Dùng E trong Combo")).SetValue(true);
            emenu.AddItem(new MenuItem("emode", "Chế độ dùng E"))
                .SetValue(new StringList(new[] { "E -> W/R -> Tiamat -> Q", "E -> Tiamat -> W/R -> Q" }));
            emenu.AddItem(new MenuItem("vhealth", "Dùng E nếu Máu% <=")).SetValue(new Slider(40));
            emenu.AddItem(new MenuItem("ashield", "Chắn Skill chọn mục tiêu khi LastHit")).SetValue(false);
            emenu.AddItem(new MenuItem("bshield", "Chặn Skill BảnThân/DiệnRộng khi LastHit")).SetValue(false);
            combo.AddSubMenu(emenu);

            var rmenu = new Menu("Cài đặt R", "rivenr");
            rmenu.AddItem(new MenuItem("useignote", "Dùng R1 + Thiêu Đốt")).SetValue(true);
            rmenu.AddItem(new MenuItem("user", "Dùng R1 trong Combo")).SetValue(new KeyBind('H', KeyBindType.Toggle, true)).Permashow();
            rmenu.AddItem(new MenuItem("overk", "Không R1 nếu Mục Tiêu có Máu% <=")).SetValue(new Slider(25, 1, 99));
            rmenu.AddItem(new MenuItem("userq", "Chỉ dùng R1 khi Q được số lần <=")).SetValue(new Slider(2, 1, 3));
            rmenu.AddItem(new MenuItem("ultwhen", "Dùng R1 khi")).SetValue(new StringList(new[] {"Giết Thường", "Khó Giết", "Luôn luôn"}, 2));
            rmenu.AddItem(new MenuItem("usews", "Dùng R2 trong Combo")).SetValue(true);
            rmenu.AddItem(new MenuItem("overaa", "Không R2 nếu kẻ địch có thể chết bằng số AA")).SetValue(new Slider(2, 1, 6));
            rmenu.AddItem(new MenuItem("wsmode", "Dùng R2 khi")).SetValue(new StringList(new[] {"Chỉ khi Giết", "Giết hoặc tối đa sát thương"}, 1));
            rmenu.AddItem(new MenuItem("multib", "Dồn sát thương kiểu TheShy khi")).SetValue(new StringList(new[] { "Có thể dồn kẻ địch", "Luôn luôn", "Không tốc biến" }, 1));

            combo.AddSubMenu(rmenu);

            menu.AddSubMenu(combo);

            var harass = new Menu("Cấu Rỉa", "harass");
            harass.AddItem(new MenuItem("qtoo", "Dùng Q thứ 3:"))
                .SetValue(new StringList(new[] {"Cách xa kẻ địch", "Đến trụ đồng minh", "Đến con trỏ"}, 1));
            harass.AddItem(new MenuItem("useharassw", "Dùng W trong Cấu Rỉa")).SetValue(true);
            harass.AddItem(new MenuItem("usegaph", "Dùng E trong Cấu Rỉa (Áp Sát)")).SetValue(true);
            harass.AddItem(new MenuItem("useitemh", "Dùng Tiamat/Mãng Xà")).SetValue(true);
            menu.AddSubMenu(harass);

            var farming = new Menu("Dọn Lính", "farming");

            var wc = new Menu("Dọn Rừng", "waveclear");
            wc.AddItem(new MenuItem("usejungleq", "Dùng Q Dọn Rừng")).SetValue(true);
            wc.AddItem(new MenuItem("usejunglew", "Dùng W Dọn Rừng")).SetValue(true);
            wc.AddItem(new MenuItem("usejunglee", "Dùng E Dọn Rừng")).SetValue(true);
            farming.AddSubMenu(wc);

            var jg = new Menu("WaveClear", "jungle");
            jg.AddItem(new MenuItem("uselaneq", "Dùng Q Dọn Lính")).SetValue(true);
            jg.AddItem(new MenuItem("uselanew", "Dùng W Dọn Lính")).SetValue(true);
            jg.AddItem(new MenuItem("wminion", "Dùng W khi có số lính >=")).SetValue(new Slider(3, 1, 6));
            jg.AddItem(new MenuItem("uselanee", "Dùng E Dọn Lính")).SetValue(true);
            farming.AddSubMenu(jg);

            menu.AddSubMenu(farming);
            menu.AddToMainMenu();
        }

        #endregion

        #region Riven : Some Dash
        private static bool canburst(bool shy = false)
        {
            if (riventarget() == null || !r.IsReady() && !uo)
            {
                return false;
            }

            if (IsLethal(riventarget()) && menulist("multib") == 0)
            {
                return true;
            }

            if (shy && menulist("multib") != 0)
            {
                return true;
            }
            
            return false;
        }

        private static void SomeDash(Obj_AI_Hero target)
        {
            if (!menu.Item("shycombo").GetValue<KeyBind>().Active ||
                !target.IsValid<Obj_AI_Hero>() || uo)
                return;

            if (riventarget() == null || uo || !r.IsReady())
                return;

            if (flash.IsReady() && canburst(true) && menulist("multib") != 2)
            {
                if (e.IsReady() && target.Distance(player.ServerPosition) <= e.Range + 50 + 300)
                {
                    if (target.Distance(player.ServerPosition) > e.Range + truerange)
                    {
                        e.Cast(target.ServerPosition);
                        r.Cast();
                    }
                }

                if (!e.IsReady() && target.Distance(player.ServerPosition) <= 50 + 300)
                {
                    if (target.Distance(player.ServerPosition) > truerange + 35)
                    {
                        r.Cast();
                    }
                }
            }

            else
            {
                if (e.IsReady() && target.Distance(player.ServerPosition) <= e.Range + w.Range)
                {
                    e.Cast(target.ServerPosition);
                    r.Cast();
                }
            }
        }

        #endregion

        #region Riven: Combo

        private static void ComboTarget(Obj_AI_Base target)
        {
            // orbwalk ->
            OrbTo(target);

            // ignite ->
            TryIgnote(target);

            var outrange = e.IsReady() ? e.Range + w.Range + 50 : w.Range + q.Range + 50;

            if (e.IsReady() && cane && player.Health / player.MaxHealth * 100 <= menuslide("vhealth") || 
                e.IsReady() && cane && target.Distance(player.ServerPosition) <= 
                e.Range + w.Range - 25 && target.Distance(player.ServerPosition) > truerange ||
                e.IsReady() && uo && cane && target.Distance(player.ServerPosition) > truerange + 50)
            {
                if (menubool("usecomboe"))
                    e.Cast(target.ServerPosition);

                if (target.Distance(player.ServerPosition) <= e.Range + w.Range + 25)
                {
                    if (menulist("emode") == 1)
                    {
                        if (canhd && hashd && !canburst() && cc < 2)
                        {
                            Items.UseItem(3077);
                            Items.UseItem(3074);
                        }

                        else
                        {
                            checkr();
                        }
                    }

                    if (menulist("emode") == 0)
                    {
                        checkr();
                    }
                }
            }

            if (w.IsReady() && canw && menubool("usecombow") &&
                     target.Distance(player.ServerPosition) <= wrange)
            {
                useinventoryitems(target);
                checkr();

                if (menulist("emode") == 1)
                {
                    if (canhd && hashd && !canburst())
                    {
                        Items.UseItem(3077);
                        Items.UseItem(3074);
                        if (menubool("usecombow"))
                            Utility.DelayAction.Add(250, () => w.Cast());
                    }

                    else
                    {
                        checkr();
                        if (menubool("usecombow"))
                            w.Cast();
                    }
                }

                if (menulist("emode") == 0)
                {
                    if (menubool("usecombow"))
                        w.Cast();
                }
            }

            else if (q.IsReady() && target.Distance(player.ServerPosition) <= truerange + 150)
            {
                useinventoryitems(target);
                checkr();

                if (menulist("emode") == 0 || IsLethal(target))
                {
                    // wait for aa -> tiamat
                    if (canhd) return;
                }

                if (menulist("wsmode") == 1 && IsLethal(target))
                {
                    if (cc == 2 && e.IsReady() && cane)
                    {
                        e.Cast(target.ServerPosition);
                    }
                }

                if (canq) q.Cast(target.ServerPosition);
            }

            else if (target.Distance(player.ServerPosition) > outrange)
            {
                if (menubool("usegap"))
                {
                    if (Utils.GameTimeTickCount - lastq >= menuslide("gaptimez") * 10)
                    {
                        if (q.IsReady() && Utils.GameTimeTickCount - laste >= 600)
                        {
                            q.Cast(target.ServerPosition);
                        }
                    }
                }
            }
        }

        #endregion

        #region Riven: Harass

        private static void HarassTarget(Obj_AI_Base target)
        {
            Vector3 qpos;
            switch (menulist("qtoo"))
            {
                case 0:
                    qpos = player.ServerPosition + 
                        (player.ServerPosition - target.ServerPosition).Normalized()*500;
                    break;
                case 1:
                    qpos = ObjectManager.Get<Obj_AI_Turret>()
                        .Where(t => (t.IsAlly)).OrderBy(t => t.Distance(player.Position)).First().Position;
                    break;
                default:
                    qpos = Game.CursorPos;
                    break;
            }

            if (q.IsReady())
                OrbTo(target);

            if (cc == 2 && canq && q.IsReady())
            {
                orbwalker.SetAttack(false);
                orbwalker.SetAttack(false);

                canaa = false;
                canmv = false;

                player.IssueOrder(GameObjectOrder.MoveTo, qpos);
                Utility.DelayAction.Add(200, () =>
                {
                    q.Cast(qpos);
                    orbwalker.SetAttack(true);
                    orbwalker.SetAttack(true);
                });
            }

            if (!player.ServerPosition.Extend(target.ServerPosition, q.Range*3).UnderTurret(true))
            {
                if (q.IsReady() && canq && cc < 2)
                {
                    if (target.Distance(player.ServerPosition) <= truerange + q.Range)
                    {
                        q.Cast(target.ServerPosition);
                    }
                }
            }

            if (e.IsReady() && cane && q.IsReady() && cc < 1 &&
                target.Distance(player.ServerPosition) > truerange + 100 &&
                target.Distance(player.ServerPosition) <= e.Range + truerange + 50)
            {
                if (!player.ServerPosition.Extend(target.ServerPosition, e.Range).UnderTurret(true))
                {
                    if (menubool("usegaph"))
                    {
                        e.Cast(target.ServerPosition);

                        if (canhd)
                        {
                            if (Items.CanUseItem(3077))
                                Items.UseItem(3077);
                            if (Items.CanUseItem(3074))
                                Items.UseItem(3074);
                        }
                    }
                }
            }

            else if (w.IsReady() && canw && target.Distance(player.ServerPosition) <= w.Range + 10)
            {
                if (!target.ServerPosition.UnderTurret(true))
                {
                    if (menubool("useharassw"))
                    {
                        w.Cast();
                    }
                }
            }

        }

        #endregion
         
        #region Riven: Windslash

        private static void Windslash()
        {
            if (uo && menubool("usews") && r.IsReady())
            {
                foreach (var t in ObjectManager.Get<Obj_AI_Hero>().Where(h => h.IsValidTarget(r.Range)))
                {
                    if (menu.Item("shycombo").GetValue<KeyBind>().Active && canburst())
                    {
                        if (t.Distance(player.ServerPosition) <= player.AttackRange + 100)
                        {
                            if (canhd) return;
                        }
                    }

                    if (player.GetAutoAttackDamage(t, true) * menuslide("overaa") >= t.Health &&
                       (Orbwalking.InAutoAttackRange(t) && player.CountEnemiesInRange(900) > 1)) 
                        return;

                    // only kill or killsteal etc ->
                    if (r.GetDamage(t) >= t.Health && canws)
                    {
                        if (r.GetPrediction(t, true).Hitchance == HitChance.VeryHigh)
                            r.Cast(r.GetPrediction(t, true).CastPosition);
                    }
                }

                if (menulist("wsmode") == 1)
                {
                    if (riventarget().IsValidTarget(r.Range) && !riventarget().IsZombie)
                    {
                        if (menu.Item("shycombo").GetValue<KeyBind>().Active && canburst())
                        {
                            if (riventarget().Distance(player.ServerPosition) <= player.AttackRange + 100)
                            {
                                if (canhd) return;
                            }
                        }

                        if (r.GetDamage(riventarget()) / riventarget().MaxHealth * 100 >= 50)
                        {
                            if (r.GetPrediction(riventarget(), true).Hitchance >= HitChance.Medium && canws)
                                r.Cast(r.GetPrediction(riventarget(), true).CastPosition);
                        }

                        if (q.IsReady() && cc <= 2)
                        {
                            var damage = r.GetDamage(riventarget()) 
                                + player.GetAutoAttackDamage(riventarget()) * 2 
                                + Qdmg(riventarget()) * 2;

                            if (riventarget().Health <= xtra((float) damage))
                            {
                                if (riventarget().Distance(player.ServerPosition) <= truerange + q.Range)
                                {
                                    if (r.GetPrediction(riventarget(), true).Hitchance >= HitChance.High && canws)
                                        r.Cast(r.GetPrediction(riventarget(), true).CastPosition);
                                }
                            }
                        }
                    }
                }
            }        
        }

        #endregion

        #region Riven: Lane/Jungle

        private static void Clear()
        {
            var minions = MinionManager.GetMinions(player.Position, 600f,
                MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth);

            foreach (var unit in minions.Where(m => !m.Name.Contains("Mini")))
            {
                OrbTo(unit);

                if (e.IsReady() && cane && menubool("usejunglee"))
                {
                    if (player.Health / player.MaxHealth * 100 <= 70 ||
                        unit.Distance(player.ServerPosition) > truerange + 30)
                    {
                        e.Cast(unit.ServerPosition);
                    }
                }

                if (w.IsReady() && canw && menubool("usejunglew"))
                {
                    if (unit.Distance(player.ServerPosition) <= w.Range + 25)
                    {
                        w.Cast();
                    }
                }

                else if (q.IsReady() && canq && menubool("usejungleq"))
                {
                    if (unit.Distance(player.ServerPosition) <= truerange + q.Range)
                    {
                        if (menulist("emode") == 0)
                        {
                            if (canhd) return;
                        }

                        q.Cast(unit.ServerPosition);
                    }
                }
            }
        }

        private static void Wave()
        {
            var minions = MinionManager.GetMinions(player.Position, 600f);

            foreach (var unit in minions.Where(x => x.IsMinion))
            {
                if (player.GetAutoAttackDamage(unit, true) >= unit.Health)
                    OrbTo(GetCenterMinion().IsValidTarget() ? GetCenterMinion() : unit);

                if (q.IsReady() && unit.Distance(player.ServerPosition) <= truerange + 100)
                {
                    if (canq && menubool("uselaneq") && minions.Count >= 2 &&
                        !player.ServerPosition.Extend(unit.ServerPosition, q.Range).UnderTurret(true))
                    {
                        if (GetCenterMinion().IsValidTarget())
                            q.Cast(GetCenterMinion());
                        else
                            q.Cast(unit.ServerPosition);
                    }
                }

                if (w.IsReady())
                {
                    if (minions.Count(m => m.Distance(player.ServerPosition) <= w.Range + 10) >= menuslide("wminion"))
                    {
                        if (canw && menubool("uselanew"))
                        {
                            Items.UseItem(3077);
                            Items.UseItem(3074);
                            Items.UseItem(3748);
                            w.Cast();
                        }
                    }
                }

                if (e.IsReady() && !player.ServerPosition.Extend(unit.ServerPosition, e.Range).UnderTurret(true))
                {
                    if (unit.Distance(player.ServerPosition) > truerange + 30)
                    {
                        if (cane && menubool("uselanee"))
                        {
                            if (GetCenterMinion().IsValidTarget())
                                e.Cast(GetCenterMinion());
                            else
                                e.Cast(unit.ServerPosition);
                        }
                    }

                    else if (player.Health / player.MaxHealth * 100 <= 70)
                    {
                        if (cane && menubool("uselanee"))
                        {
                            if (GetCenterMinion().IsValidTarget())
                                q.Cast(GetCenterMinion());
                            else
                                q.Cast(unit.ServerPosition);
                        }
                    }
                }
            }
        }

        #endregion

        #region Riven: Flee

        private static void Flee()
        {
            if (canmv)
            {
                player.IssueOrder(GameObjectOrder.MoveTo, Game.CursorPos);
            }

            if (cc > 2 && didq && Items.GetWardSlot() != null && menubool("wq3"))
            {
                var attacker = HeroManager.Enemies.FirstOrDefault(x => x.Distance(player.ServerPosition) <= q.Range);
                if (attacker.IsValidTarget(q.Range) && !player.IsFacing(attacker))
                {
                    if (Utils.GameTimeTickCount - lastwd >= 1000)
                    {
                        Utility.DelayAction.Add(100,
                            () => Items.UseItem((int) Items.GetWardSlot().Id, attacker.ServerPosition));
                    }
                }
            }

            if (player.CountEnemiesInRange(w.Range) > 0)
            {
                if (w.IsReady())
                    w.Cast();
            }

            if (ssfl)
            {
                if (Utils.GameTimeTickCount - lastq >= 600)
                {
                    q.Cast(Game.CursorPos);
                }

                if (cane && e.IsReady())
                {
                    if (cc >= 2 || !q.IsReady() && !player.HasBuff("RivenTriCleave", true))
                    {
                        if (!player.ServerPosition.Extend(Game.CursorPos, e.Range + 10).IsWall())
                            e.Cast(Game.CursorPos);
                    }
                }
            }

            else
            {
                if (q.IsReady())
                {
                    q.Cast(Game.CursorPos);
                }

                if (e.IsReady() && Utils.GameTimeTickCount - lastq >= 250)
                {
                    if (!player.ServerPosition.Extend(Game.CursorPos, e.Range).IsWall())
                        e.Cast(Game.CursorPos);
                }
            }
        }

        #endregion

        #region Riven: Semi Q 

        private static void SemiQ()
        {
            if (canq && Utils.GameTimeTickCount - lastaa >= 150)
            {
                if (menubool("semiq") || menu.Item("semiqlane").GetValue<KeyBind>().Active)
                {
                    if (q.IsReady() && Utils.GameTimeTickCount - lastaa < 1200 && qtarg != null)
                    {
                        if (qtarg.IsValidTarget(q.Range + 100) &&
                            !menu.Item("clearkey").GetValue<KeyBind>().Active &&
                            !menu.Item("harasskey").GetValue<KeyBind>().Active &&
                            !menu.Item("combokey").GetValue<KeyBind>().Active &&
                            !menu.Item("shycombo").GetValue<KeyBind>().Active)
                        {
                            if (qtarg.IsValid<Obj_AI_Hero>())
                                q.Cast(qtarg.ServerPosition);
                        }

                        if (!menu.Item("harasskey").GetValue<KeyBind>().Active &&
                            !menu.Item("clearkey").GetValue<KeyBind>().Active &&
                            !menu.Item("combokey").GetValue<KeyBind>().Active &&
                            !menu.Item("shycombo").GetValue<KeyBind>().Active)
                        {
                            if (qtarg.IsValidTarget(q.Range + 100) && !qtarg.Name.Contains("Mini"))
                            {
                                if (!qtarg.Name.StartsWith("Minion") && minionlist.Any(name => qtarg.Name.StartsWith(name)))
                                {
                                    q.Cast(qtarg.ServerPosition);
                                }
                            }

                            if (qtarg.IsValidTarget(q.Range + 100))
                            {
                                if (qtarg.IsValid<Obj_AI_Minion>() || qtarg.IsValid<Obj_AI_Turret>())
                                {
                                    if (menu.Item("semiqlane").GetValue<KeyBind>().Active)
                                        q.Cast(qtarg.ServerPosition);
                                }

                                if (qtarg.IsValid<Obj_AI_Hero>() || qtarg.IsValid<Obj_AI_Turret>())
                                {
                                    if (uo)
                                        q.Cast(qtarg.ServerPosition);
                                }
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region Riven: Check R
        private static void checkr()
        {
            if (!r.IsReady() || uo || !menu.Item("user").GetValue<KeyBind>().Active)
                return;

            if (menulist("ultwhen") == 2 && cc <= menuslide("userq"))
                r.Cast();

            var targets = HeroManager.Enemies.Where(ene => ene.IsValidTarget(r.Range + 100));
            var heroes = targets as IList<Obj_AI_Hero> ?? targets.ToList();

            foreach (var target in heroes)
            {
                if (cc <= menuslide("userq") && (q.IsReady() || Utils.GameTimeTickCount - lastq < 1000))
                {
                    if (heroes.Count(ene => ene.Distance(player.ServerPosition) <= 750) > 1)
                        r.Cast();

                    if (heroes.Count() < 2)
                    {
                        if (target.Health / target.MaxHealth * 100 <= menuslide("overk") && IsLethal(target))
                            return;
                    }

                    if (menulist("ultwhen") == 0)
                    {
                        if ((ComboDamage(target)/1.3) >= target.Health && target.Health >= (ComboDamage(target)/1.8))
                        {
                            r.Cast();
                        }
                    }

                    if (menulist("ultwhen") == 1)
                    {
                        if (ComboDamage(target) >= target.Health && target.Health >= ComboDamage(target)/1.8)
                        {
                            r.Cast();
                        }
                    }
                }
            }        
        }

        #endregion

        #region Riven: On Cast

        private static void OnCast()
        {
            Obj_AI_Base.OnProcessSpellCast += (sender, args) =>
            {
                if (!sender.IsMe)
                {
                    return;
                }

                if (!didq && args.SData.Name.ToLower().Contains("attack"))
                {
                    didaa = true;
                    canaa = false;
                    canq = false;
                    canw = false;
                    cane = false;
                    canws = false;
                    lastaa = Utils.GameTimeTickCount;
                    qtarg = (Obj_AI_Base) args.Target;
                }

                if (args.SData.Name.ToLower().Contains("ward"))
                    lastwd = Utils.GameTimeTickCount;

                switch (args.SData.Name)
                {
                    case "RivenTriCleave":
                        cc += 1;
                        canmv = false;
                        didq = true;
                        didaa = false;
                        lastq = Utils.GameTimeTickCount;
                        canq = false;

                        if (cc >= 2)
                            Utility.DelayAction.Add(425 - (100 - Game.Ping / 2),
                                () => Orbwalking.LastAATick = 0);

                        if (!uo) ssfl = false;
                        break;
                    case "RivenMartyr":
                        canmv = false;
                        didw = true;
                        lastw = Utils.GameTimeTickCount;
                        canw = false;

                        if (menulist("wmode") == 1)
                        {
                            if (!menu.Item("shycombo").GetValue<KeyBind>().Active && 
                                 menu.Item("combokey").GetValue<KeyBind>().Active && !canburst())
                            {
                                if (canhd) return;

                                if (riventarget() != null)
                                {
                                    Utility.DelayAction.Add(Game.Ping + 130, () => q.Cast(riventarget().ServerPosition));
                                    return;
                                }

                                if (orbwalker.GetTarget() != null)
                                {
                                    Utility.DelayAction.Add(Game.Ping + 130, () => q.Cast(orbwalker.GetTarget().Position));
                                }
                            }
                        }

                        break;
                    case "RivenFeint":
                        canmv = false;
                        dide = true;
                        didaa = false;
                        laste = Utils.GameTimeTickCount;
                        cane = false;

                        if (menu.Item("fleekey").GetValue<KeyBind>().Active)
                        {
                            if (uo && r.IsReady() && cc == 2 && q.IsReady())
                            {
                                r.Cast(Game.CursorPos);
                            }
                        }

                        if (menu.Item("combokey").GetValue<KeyBind>().Active)
                        {
                            if (cc == 2 && !uo)
                            {
                                checkr();
                                Utility.DelayAction.Add(Game.Ping + 200, () => q.Cast(Game.CursorPos));
                            }

                            if (menulist("wsmode") == 1 && cc == 2 && uo)
                            {
                                if (riventarget().IsValidTarget(r.Range + 100) && IsLethal(riventarget()))
                                {
                                    Utility.DelayAction.Add(100 + Game.Ping,
                                    () => r.Cast(r.CastIfHitchanceEquals(riventarget(), HitChance.Medium)));
                                }
                            }
                        }

                        break;
                    case "RivenFengShuiEngine":
                        ssfl = true;

                        if (riventarget() != null && canburst(true))
                        {
                            if (!flash.IsReady() || menulist("multib") == 2)
                                return;

                            if (menu.Item("shycombo").GetValue<KeyBind>().Active)
                            {
                                if (riventarget().Distance(player.ServerPosition) > e.Range + 50 &&
                                    riventarget().Distance(player.ServerPosition) <= e.Range + wrange + 300)
                                {
                                    var second =
                                        HeroManager.Enemies.Where(
                                            x => x.NetworkId != riventarget().NetworkId &&
                                                 x.Distance(riventarget().ServerPosition) <= r.Range)
                                            .OrderByDescending(xe => xe.Distance(riventarget().ServerPosition))
                                            .FirstOrDefault();

                                    if (second != null)
                                    {
                                        var pos = riventarget().ServerPosition +
                                                  (riventarget().ServerPosition - second.ServerPosition).Normalized() * 75;

                                        player.Spellbook.CastSpell(flash, pos);
                                    }

                                    else
                                    {
                                        player.Spellbook.CastSpell(flash,
                                            riventarget().ServerPosition.Extend(player.ServerPosition, 115));
                                    }
                                }
                            }
                        }

                        break;
                    case "rivenizunablade":
                        ssfl = false;
                        didws = true;
                        canws = false;

                        if (w.IsReady() && riventarget().IsValidTarget(wrange))
                            w.Cast();

                        if (q.IsReady() && riventarget().IsValidTarget())
                            q.Cast(riventarget().ServerPosition);

                        break;
                    case "ItemTiamatCleave":
                    case "ItemTitanicHydraCleave":  
                        lasthd = Utils.GameTimeTickCount;
                        didhd = true;
                        canws = true;
                        canhd = false;

                        if (menulist("wsmode") == 1 && uo && canws)
                        {
                            if (menu.Item("combokey").GetValue<KeyBind>().Active)
                            {
                                if (canburst())
                                {
                                    if (riventarget().IsValidTarget() && !riventarget().IsZombie)
                                    {
                                        Utility.DelayAction.Add(125 + Game.Ping,
                                            () => r.Cast(r.CastIfHitchanceEquals(riventarget(), HitChance.Medium)));
                                    }
                                }
                            }

                            if (menu.Item("shycombo").GetValue<KeyBind>().Active)
                            {
                                if (canburst(true))
                                {
                                    if (riventarget().IsValidTarget() && !riventarget().IsZombie)
                                    {
                                        Utility.DelayAction.Add(125 + Game.Ping,
                                            () => r.Cast(r.CastIfHitchanceEquals(riventarget(), HitChance.Medium)));
                                    }
                                }
                            }
                        }

                        if (menulist("emode") == 1 && Utils.GameTimeTickCount - laste >= 1500)
                        {
                            if (menu.Item("combokey").GetValue<KeyBind>().Active && !uo)
                            {
                                checkr();
                                Utility.DelayAction.Add(Game.Ping + 175, () => q.Cast(Game.CursorPos));
                            }
                        }

                        break;
                    default:
                        if (args.SData.Name.ToLower().Contains("attack"))
                        {
                            if (menu.Item("combokey").GetValue<KeyBind>().Active || 
                                menu.Item("shycombo").GetValue<KeyBind>().Active)
                            {
                                if (canburst() || menulist("emode") == 0 && !canburst() ||
                                    menu.Item("shycombo").GetValue<KeyBind>().Active)
                                {
                                    // delay till after aa
                                    Utility.DelayAction.Add(
                                        50 + (int) (player.AttackDelay * 100) + Game.Ping / 2 + menuslide("autoaq"), delegate
                                        {
                                            if (Items.CanUseItem(3077))
                                                Items.UseItem(3077);
                                            if (Items.CanUseItem(3074))
                                                Items.UseItem(3074);
                                            if (Items.CanUseItem(3748))
                                                Items.UseItem(3748);
                                        });
                                }

                                else if (!menubool("usecombow") || !menubool("usecomboe"))
                                {
                                    // delay till after aa
                                    Utility.DelayAction.Add(
                                        50 + (int) (player.AttackDelay * 100) + Game.Ping / 2 + menuslide("autoaq"), delegate
                                        {
                                            if (Items.CanUseItem(3077))
                                                Items.UseItem(3077);
                                            if (Items.CanUseItem(3074))
                                                Items.UseItem(3074);
                                            if (Items.CanUseItem(3748))
                                                Items.UseItem(3748);
                                        });
                                }
                            }

                            else if (menu.Item("clearkey").GetValue<KeyBind>().Active)
                            {
                                if (qtarg.IsValid<Obj_AI_Base>() && !qtarg.Name.StartsWith("Minion"))
                                {
                                    Utility.DelayAction.Add(
                                        50 + (int) (player.AttackDelay*100) + Game.Ping/2 + menuslide("autoaq"), delegate
                                        {
                                            if (Items.CanUseItem(3077))
                                                Items.UseItem(3077);
                                            if (Items.CanUseItem(3074))
                                                Items.UseItem(3074);
                                            if (Items.CanUseItem(3748))
                                                Items.UseItem(3748);
                                        });
                                }
                            }
                        }
                        break;
                }

                #region Riven Shield (Use at your own discretion)
                // Kappa: Spelldata sux atm
                if (sender.IsEnemy && sender.Type == player.Type)
                {
                    var epos = player.ServerPosition +
                              (player.ServerPosition - sender.ServerPosition).Normalized() * 300;

                    if (player.Distance(sender.ServerPosition) <= args.SData.CastRange)
                    {
                        switch (args.SData.TargettingType)
                        {
                            case SpellDataTargetType.Unit:

                                if (args.Target.NetworkId == player.NetworkId && menubool("ashield"))
                                {
                                    if (orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LastHit)
                                    {
                                        e.Cast(epos);
                                    }
                                }

                                break;
                            case SpellDataTargetType.SelfAoe:

                                if (orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LastHit && menubool("bshield"))
                                {
                                    e.Cast(epos);
                                }

                                break;
                        }
                    }
                }

                #endregion
            };
        }

        #endregion

        #region Riven: Misc Events
        private static void Interrupter()
        {
            Interrupter2.OnInterruptableTarget += (sender, args) =>
            {
                if (menubool("wint") && w.IsReady())
                {
                    if (!sender.Position.UnderTurret(true))
                    {
                        if (sender.IsValidTarget(w.Range))
                            w.Cast();

                        if (sender.IsValidTarget(w.Range + e.Range) && e.IsReady())
                        {
                            e.Cast(sender.ServerPosition);
                        }
                    }
                }

                if (menubool("qint") && q.IsReady() && cc >= 2)
                {
                    if (!sender.Position.UnderTurret(true))
                    {
                        if (sender.IsValidTarget(q.Range))
                            q.Cast(sender.ServerPosition);

                        if (sender.IsValidTarget(q.Range + e.Range) && e.IsReady())
                        {
                            e.Cast(sender.ServerPosition);
                        }
                    }
                }
            };
        }

        private static void OnGapcloser()
        {
            AntiGapcloser.OnEnemyGapcloser += gapcloser =>
            {
                if (menubool("wgap") && w.IsReady())
                {
                    if (gapcloser.Sender.IsValidTarget(w.Range))
                    {
                        if (!gapcloser.Sender.ServerPosition.UnderTurret(true))
                        {
                            w.Cast();
                        }
                    }               
                }
            };
        }

        private void OnPlayAnimation()
        {
        }

        #endregion

        #region Riven: Aura

        private static void AuraUpdate()
        {
            if (!player.IsDead)
            {
                foreach (var buff in player.Buffs)
                {
                    //if (buff.Name == "RivenTriCleave")
                    //    cc = buff.Count;

                    if (buff.Name == "rivenpassiveaaboost")
                        pc = buff.Count;
                }

                if (player.HasBuff("RivenTriCleave", true))
                {
                    if (Utils.GameTimeTickCount - lastq >= 3650)
                    {
                        if (!player.IsRecalling() && !player.Spellbook.IsChanneling)
                        {
                            var qext = player.ServerPosition.To2D() + 
                                       player.Direction.To2D().Perpendicular() * q.Range + 100;

                            if (menubool("keepq") && !qext.To3D().UnderTurret(true))
                                q.Cast(Game.CursorPos);
                        }
                    }
                }

                if (!player.HasBuff("rivenpassiveaaboost", true))
                    Utility.DelayAction.Add(1000, () => pc = 1);

                if (cc > 2)
                    Utility.DelayAction.Add(1000, () => cc = 0);
            }
        }

        #endregion

        #region Riven : Combat/Orbwalk

        private static void OrbTo(Obj_AI_Base target, float rangeoverride = 0f)
        {
            if (canmv)
            {
                if (menu.Item("shycombo").GetValue<KeyBind>().Active)
                {
                    if (target.IsValidTarget(600))
                        Orbwalking.Orbwalk(target, Game.CursorPos, 80f, 0f, false, false);

                    else
                        player.IssueOrder(GameObjectOrder.MoveTo, Game.CursorPos);
                }
            }

            if (canmv && q.IsReady())
            {
                if (target.IsValidTarget(truerange + 100 + rangeoverride))
                {
                    Orbwalking.LastAATick = 0;
                }
            }
        }

        private static void CombatCore()
        {
            if (didhd && canhd && Utils.GameTimeTickCount - lasthd >= 250)
                didhd = false;

            if (didq && Utils.GameTimeTickCount - lastq >= 500 + Game.Ping / 2)
            {
                didq = false;
                canmv = true;
                canaa = true;
            }

            if (didw && Utils.GameTimeTickCount - lastw >= 266)
            {
                didw = false;
                canmv = true;
                canaa = true;
            }

            if (dide && Utils.GameTimeTickCount - laste >= 300)
            {
                dide = false;
                canmv = true;
                canaa = true;
            }

            if (!canw && w.IsReady() && !(didaa || didq || dide))
                 canw = true;

            if (!cane && e.IsReady() && !(didaa || didq || didw))
                 cane = true;

            if (!canws && r.IsReady() && (!(didaa || didw) && uo))
                 canws = true;

            if (!canaa && !(didq || didw || dide || didws || didhd || didhs) && 
                Utils.GameTimeTickCount - lastaa >= 1000)
                canaa = true;

            if (!canmv && !(didq || didw || dide || didws || didhd || didhs) && 
                Utils.GameTimeTickCount - lastaa >= 1100)
                canmv = true;

            if (didaa &&
                Utils.GameTimeTickCount - lastaa >=
                25 + (player.AttackDelay * 100) + Game.Ping / 2 + menuslide("autoaq"))
            {
                didaa = false;
                canmv = true;
                canq = true;
                cane = true;
                canw = true;
                canws = true;
            }
        }

        #endregion

        #region Riven: Math/Damage

        private static float ComboDamage(Obj_AI_Base target)
        {
            if (target == null)
                return 0f;

            var ignote = player.GetSpellSlot("summonerdot");
            var ad = (float)player.GetAutoAttackDamage(target);
            var runicpassive = new[] { 0.2, 0.25, 0.3, 0.35, 0.4, 0.45, 0.5 };

            var ra = ad +
                        (float)
                            ((+player.FlatPhysicalDamageMod + player.BaseAttackDamage) *
                            runicpassive[player.Level / 3]);

            var rw = Wdmg(target);
            var rq = Qdmg(target);
            var rr = r.IsReady() ? r.GetDamage(target) : 0;

            var ii = (ignote != SpellSlot.Unknown && player.GetSpell(ignote).State == SpellState.Ready && r.IsReady()
                ? player.GetSummonerSpellDamage(target, Damage.SummonerSpell.Ignite)
                : 0);

            var tmt = Items.HasItem(3077) && Items.CanUseItem(3077)
                ? player.GetItemDamage(target, Damage.DamageItems.Tiamat)
                : 0;

            var hyd = Items.HasItem(3074) && Items.CanUseItem(3074)
                ? player.GetItemDamage(target, Damage.DamageItems.Hydra)
                : 0;

            var tdh = Items.HasItem(3748) && Items.CanUseItem(3748)
                ? player.GetItemDamage(target, Damage.DamageItems.Hydra)
                : 0;

            var bwc = Items.HasItem(3144) && Items.CanUseItem(3144)
                ? player.GetItemDamage(target, Damage.DamageItems.Bilgewater)
                : 0;

            var brk = Items.HasItem(3153) && Items.CanUseItem(3153)
                ? player.GetItemDamage(target, Damage.DamageItems.Botrk)
                : 0;

            var items = tmt + hyd + tdh + bwc + brk;

            var damage = (rq * 3 + ra * 3 + rw + rr + ii + items);

            return xtra((float) damage);
        }


        private static double Wdmg(Obj_AI_Base target)
        {
            double dmg = 0;
            if (w.IsReady() && target != null)
            {
                dmg += player.CalcDamage(target, Damage.DamageType.Physical,
                    new[] {50, 80, 110, 150, 170}[w.Level - 1] + 1*player.FlatPhysicalDamageMod + player.BaseAttackDamage);
            }

            return dmg;
        }

        private static double Qdmg(Obj_AI_Base target)
        {
            double dmg = 0;
            if (q.IsReady() && target != null)
            {
                dmg += player.CalcDamage(target, Damage.DamageType.Physical,
                    -10 + (q.Level * 20) + (0.35 + (q.Level * 0.05)) * (player.FlatPhysicalDamageMod + player.BaseAttackDamage));
            }

            return dmg;
        }

        #endregion

        #region Riven: Drawings

        private static void Drawings()
        {
            Drawing.OnDraw += args =>
            {
                if (!player.IsDead) 
                {
                    if (_sh.IsValidTarget())
                    {
                        if (menu.Item("drawf").GetValue<Circle>().Active)
                            Render.Circle.DrawCircle(_sh.Position, 200, menu.Item("drawf").GetValue<Circle>().Color, 6);
                    }

                    if (menu.Item("drawengage").GetValue<Circle>().Active)
                    {
                        Render.Circle.DrawCircle(player.Position,
                            player.AttackRange + e.Range + 35, menu.Item("drawengage").GetValue<Circle>().Color,
                            menu.Item("linewidth").GetValue<Slider>().Value);
                    }

                    if (menu.Item("drawr2").GetValue<Circle>().Active)
                    {
                        Render.Circle.DrawCircle(player.Position, r.Range, menu.Item("drawr2").GetValue<Circle>().Color,
                            menu.Item("linewidth").GetValue<Slider>().Value);
                    }

                    if (menu.Item("drawburst").GetValue<Circle>().Active && canburst(true) && riventarget().IsValidTarget())
                    {
                        var xrange = menulist("multib") != 2 && flash.IsReady() ? 300 : 0;
                        Render.Circle.DrawCircle(riventarget().Position, e.Range + 75 + xrange,
                            menu.Item("drawengage").GetValue<Circle>().Color, menu.Item("linewidth").GetValue<Slider>().Value);
                    }
                }
            };

            Drawing.OnEndScene += args =>
            {
                if (!menubool("drawdmg"))
                    return;

                foreach (
                    var enemy in
                        ObjectManager.Get<Obj_AI_Hero>()
                            .Where(ene => ene.IsValidTarget() && !ene.IsZombie))
                {
                    var color = r.IsReady() && IsLethal(enemy)
                        ? new ColorBGRA(0, 255, 0, 90)
                        : new ColorBGRA(255, 255, 0, 90);

                    hpi.unit = enemy;
                    hpi.drawDmg(ComboDamage(enemy), color);
                }

            };
        }

        #endregion

    }
}
