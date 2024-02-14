using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace PeteTimesSix.TranspilerExamples
{
    public class TranspilerExamples_Mod : Mod
    {
        public static Harmony Harmony { get; internal set; }

        public TranspilerExamples_Mod(ModContentPack content) : base(content)
        {
            Harmony = new Harmony("PeteTimesSix.TranspilerExamples");
            Harmony.PatchAll();
        }
    }
}
