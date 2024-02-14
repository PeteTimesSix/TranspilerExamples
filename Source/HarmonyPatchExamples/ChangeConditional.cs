using HarmonyLib;
using PeteTimesSix.TranspilerExamples.Extras;
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
     * The objective of this patch is to allow for secondary brains to
     * also receive brain injuries.
     * 
     * This is the target method, as decompiled by ilSpy:

            public override bool OnHediffAdded(Pawn pawn, Hediff hediff)
            {
                if (!(hediff is Hediff_Injury))
                {
                    return false;
                }
                if (hediff.Part != pawn.health.hediffSet.GetBrain())
                {
                    return false;
                }
                float num = hediff.Severity / hediff.Part.def.GetMaxHealth(pawn);
                if (Rand.Value < num * chancePerDamagePct && TryApply(pawn))
                {
                    if ((pawn.Faction == Faction.OfPlayer || pawn.IsPrisonerOfColony) && !letter.NullOrEmpty())
                    {
                        Find.LetterStack.ReceiveLetter(letterLabel, letter.Formatted(pawn.Named("PAWN")).AdjustedFor(pawn), LetterDefOf.NegativeEvent, pawn);
                    }
                    return true;
                }
                return false;
            }

     * We will be adding 
     * " || hediff.Part.def == HediffDefOf.OctopusSecondaryBrain"
     * to 
     * "hediff.Part != pawn.health.hediffSet.GetBrain()".
     * 
     * The relevant IL code:

                // if (hediff.Part != pawn.health.hediffSet.GetBrain())
            IL_000a: ldarg.2
            IL_000b: callvirt instance class Verse.BodyPartRecord Verse.Hediff::get_Part()
            IL_0010: ldarg.1
            IL_0011: ldfld class Verse.Pawn_HealthTracker Verse.Pawn::health
            IL_0016: ldfld class Verse.HediffSet Verse.Pawn_HealthTracker::hediffSet
            IL_001b: callvirt instance class Verse.BodyPartRecord Verse.HediffSet::GetBrain()
            IL_0020: beq.s IL_0024

                // return false;
            IL_0022: ldc.i4.0
            IL_0023: ret

                // float num = hediff.Severity / hediff.Part.def.GetMaxHealth(pawn);
            IL_0024: ldarg.2
    */

    [HarmonyPatch(typeof(HediffGiver_BrainInjury), nameof(HediffGiver_BrainInjury.OnHediffAdded))]
    public static class ChangeConditional
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> ChangeConditional_Patch(IEnumerable<CodeInstruction> instructions)
        {
            // Using CodeMatcher instead of manipulating the instructions directly, but that is also an option.
            var codeMatcher = new CodeMatcher(instructions);

            // An array containing instruction(s) to find.
            // The longer and more specific this array is, the less likely it is you get a false positive...
            // but also more susceptible to other transpilers / game updates.
            // Your call.
            var toMatch = new CodeMatch[]
            {
                    //callvirt instance class Verse.BodyPartRecord Verse.HediffSet::GetBrain()
                new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(HediffSet), nameof(HediffSet.GetBrain))),
                    //beq.s IL_0024
                new CodeMatch(OpCodes.Beq_S) //CodeMatch can match partial instructions. Here we dont know the label operand yet.
            };

            // Replacement instruction(s).
            var replacement = new CodeInstruction[]
            {
                    // We need the hediff as a parameter for our check.
                new CodeInstruction(OpCodes.Ldarg_2),
                    // Call a method that puts true or false (our extra conditional) on the stack.
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ChangeConditional), nameof(OctobrainCheck))),
                    // We need a branch instruction. There are a few options to pick from. Most of the branching instructions begin with B.
                    // In this case we want to branch past the ret if the value on top of the stack is true, so we use Brtrue.
                    // The operand is not provided, as we don't know it yet.
                new CodeInstruction(OpCodes.Brtrue_S),
            };

            // Match the provided CodeMatch's, stopping at the last instruction of the match, or out of bounds if no match is found.
            codeMatcher.MatchEndForward(toMatch);

            if (codeMatcher.IsInvalid)
            {
                // CodeMatcher did not find the instruction(s).
                // Maybe another transpiler got there first and changed the IL code beyond recognition...
                // or the target code changed with a game update.

                Log.Warning("TranspilerExamples: Failed to apply ChangeConditional patch!");

                // Return unchanged instructions.
                return instructions;
            }
            else
            {
                // Retrieve the destination label operand from the original branch instruction and add it to our new one.
                replacement[2].operand = codeMatcher.Instruction.operand;
                // We need to insert our instructions *after* the first check, so advance past it.
                codeMatcher.Advance(1);
                // Insert our second check.
                codeMatcher.Insert(replacement);

                // Return modified instructions.
                return codeMatcher.InstructionEnumeration();
            }
        }

        /*
         * The single parameter is the hediff that we put on the stack right before the Call.
         * The returned result is put on top of the stack and consumed by the following branch instruction.
        */
        public static bool OctobrainCheck(Hediff hediff)
        {
            // Now we are back in comfortable C# land, writing code as normal.
            // An alternative to calling a static method is writing the IL instructions directly.
            // You can also compile once, then decompile your own assembly to see which instructions result from your method.

            // Check if part is secondary brain.
            var isSecondaryBrain = hediff.Part.def == BodyPartDefOf_Custom.OctopusTentacleBrain;
            return isSecondaryBrain;
        }
    }
}
