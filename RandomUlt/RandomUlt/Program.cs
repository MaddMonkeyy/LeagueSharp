﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Color = System.Drawing.Color;
using LeagueSharp;
using LeagueSharp.Common;
using RandomUlt.Helpers;

namespace RandomUlt
{
    internal class Program
    {
        public static Menu config;
        public static Orbwalking.Orbwalker orbwalker;
        public static LastPositions positions;
        public static readonly Obj_AI_Hero player = ObjectManager.Player;

        private static void Main(string[] args)
        {
                CustomEvents.Game.OnGameLoad += Game_OnGameLoad;         
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            config = new Menu("RandomUlt Beta VH", "RandomUlt Beta", true);
            Menu RandomUltM = new Menu("Cài đặt", "Options");
            positions = new LastPositions(RandomUltM);
            config.AddSubMenu(RandomUltM);
            config.AddItem(new MenuItem("RandomUlt ", "Việt Hóa By MaddMonkeyy"));
            config.AddToMainMenu();
            Notifications.AddNotification(new Notification("Source by Soresu", 3000, true).SetTextColor(Color.Peru));
        }
    }
}