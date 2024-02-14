using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace PeteTimesSix.TranspilerExamples.Extras
{
    [DefOf]
    public static class BodyPartDefOf_Custom
    {
        public static BodyPartDef OctopusTentacleBrain;

        static BodyPartDefOf_Custom()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(BodyPartDefOf_Custom));
        }
    }
}
