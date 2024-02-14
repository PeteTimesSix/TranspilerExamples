using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace PeteTimesSix.LIFE
{
    public class LIFE_Mod : Mod
    {
        public static LIFE_Mod ModSingleton { get; private set; }
        public static LIFE_Settings Settings { get; internal set; }

        public static Harmony Harmony { get; internal set; }

        public LIFE_Mod(ModContentPack content) : base(content)
        {
            ModSingleton = this;

            Harmony = new Harmony("PeteTimesSix.LIFE");
            Harmony.PatchAll();
        }

        public override string SettingsCategory()
        {
            return "LIFE_ModTitle".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoSettingsWindowContents(inRect);
        }
    }


    [StaticConstructorOnStartup]
    public static class LIFE_PostInit
    {
        static LIFE_PostInit()
        {
            LIFE_Mod.Settings = LIFE_Mod.ModSingleton.GetSettings<LIFE_Settings>();
        }
    }

}
