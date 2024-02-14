using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace PeteTimesSix.TranspilerExamples.HarmonyPatchExamples
{
    /*
     * The objective of this patch is to increase the chance of slave rebellion
     * but ONLY if the slave pawn's name is Jerry.
     * 
     * This is the target method, as decompiled by ilSpy:
     
  	        private static float InitiateSlaveRebellionMtbDaysHelper(Pawn pawn)
	        {
		        if (!CanParticipateInSlaveRebellion(pawn))
		        {
			        return -1f;
		        }
		        Need_Suppression need_Suppression = pawn.needs.TryGetNeed<Need_Suppression>();
		        if (need_Suppression == null)
		        {
			        return -1f;
		        }
		        float num = 45f;
		        num /= MovingCapacityFactorCurve.Evaluate(pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving));
		        num /= SuppresionRebellionFactorCurve.Evaluate(need_Suppression.CurLevelPercentage);
		        num /= SlaveCountFactorCurve.Evaluate(pawn.Map.mapPawns.SlavesOfColonySpawned.Count);
		        if (pawn.needs.mood != null)
		        {
			        num /= MoodRebellionFactorCurve.Evaluate(pawn.needs.mood.CurLevelPercentage);
		        }
		        if (InRoomTouchingMapEdge(pawn))
		        {
			        num /= 1.7f;
		        }
		        if (CanApplyWeaponFactor(pawn))
		        {
			        num /= 4f;
		        }
		        if (IsUnattendedByColonists(pawn.Map))
		        {
			        num /= 20f;
		        }
		        return num;
	        }
     
     * We will be inserting another check after all the others at the destination of the last conditional's branch.
     * 
     * The relevant IL code:
     
      	        // if (IsUnattendedByColonists(pawn.Map))
		    IL_00cb: ldarg.0
		    IL_00cc: callvirt instance class Verse.Map Verse.Thing::get_Map()
		    IL_00d1: call bool RimWorld.SlaveRebellionUtility::IsUnattendedByColonists(class Verse.Map)
		    IL_00d6: brfalse.s IL_00e0

		        // num /= 20f;
		    IL_00d8: ldloc.1
		    IL_00d9: ldc.r4 20
		    IL_00de: div
		    IL_00df: stloc.1

		        // return num;
		    IL_00e0: ldloc.1
		    IL_00e1: ret
     */

    // Because InitiateSlaveRebellionMtbDaysHelper is private, we can't use nameof(InitiateSlaveRebellionMtbDaysHelper).
    [HarmonyPatch(typeof(SlaveRebellionUtility), "InitiateSlaveRebellionMtbDaysHelper")]
    public static class LabelRelocation
    {

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> LabelRelocation_Patch(IEnumerable<CodeInstruction> instructions)
        {
            // Using CodeMatcher instead of manipulating the instructions directly, but that is also an option.
            var codeMatcher = new CodeMatcher(instructions);

            // An array containing instruction(s) to find.
            // The longer and more specific this array is, the less likely it is you get a false positive...
            // but also more susceptible to other transpilers / game updates.
            // Your call.
            var toMatch = new CodeMatch[]
            {
                new CodeMatch(OpCodes.Ldloc_1),
                new CodeMatch(OpCodes.Ret)
            };

            // New instruction(s) to insert.
            var newInstructions = new CodeInstruction[]
            {
                    // We will need the pawn as a parameter.
                new CodeInstruction(OpCodes.Ldarg_0),
                    // We want to modify the value.
                new CodeInstruction(OpCodes.Ldloc_1),
                    // Call our JerryModifier method.
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(LabelRelocation), nameof(JerryModifier))),
                    // We need to store the modified value.
                new CodeInstruction(OpCodes.Stloc_1),
            };

            // Match the provided CodeMatch's, stopping at the first instruction of the match, or out of bounds if no match is found.
            codeMatcher.MatchStartForward(toMatch);

            if (codeMatcher.IsInvalid)
            {
                // CodeMatcher did not find the instruction(s).
                // Maybe another transpiler got there first and changed the IL code beyond recognition...
                // or the target code changed with a game update.

                Log.Warning("TranspilerExamples: Failed to apply LabelRelocation patch!");

                // Return unchanged instructions.
                return instructions;
            }
            else
            {
                // Move the labels from the current destination instruction to our new one.
                newInstructions[0].MoveLabelsFrom(codeMatcher.Instruction);
                // Insert our additional instructions.
                codeMatcher.Insert(newInstructions);

                // Return modified instructions.
                return codeMatcher.InstructionEnumeration();
            }
        }


        /*
         * The parameters are the values we put on the stack and the category generated by the normal code.
         * The returned result is put on top of the stack, replacing the consumed float.
         * Note the order - the last parameter (the float) is on top of the stack, and the first parameter (the Pawn) is at the bottom of the stack. 
        */
        public static float JerryModifier(Pawn pawn, float num)
        {
            // Now we are back in comfortable C# land, writing code as normal.
            // An alternative to calling a static method is writing the IL instructions directly.
            // You can also compile once, then decompile your own assembly to see which instructions result from your method.

            // Determine Jerry-ness of pawn.
            bool isJerry = false;
            if (pawn.Name is NameTriple tripleName && tripleName.Nick == "Jerry")
                isJerry = true;
            else if (pawn.Name is NameSingle singleName && singleName.Name == "Jerry")
                isJerry = true;

            // Dammit, Jerry.
            if (isJerry)
                num /= 25f;

            return num;
        }
    }
}
