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
     * The objective of this patch is to change the letter def sent 
     * when a pawn gains a brain injury from negative to positive
     * but ONLY if that pawn's name is Jerry.
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

     * We will be replacing the call to ReceiveLetter with a custom version.
     * 
     * The relevant IL code:

                // Find.LetterStack.ReceiveLetter(letterLabel, letter.Formatted(pawn.Named("PAWN")).AdjustedFor(pawn), LetterDefOf.NegativeEvent, pawn);
            IL_0078: call class Verse.LetterStack Verse.Find::get_LetterStack()
            IL_007d: ldarg.0
            IL_007e: ldfld string Verse.HediffGiver_BrainInjury::letterLabel
            IL_0083: call valuetype Verse.TaggedString Verse.TaggedString::op_Implicit(string)
            IL_0088: ldarg.0
            IL_0089: ldfld string Verse.HediffGiver_BrainInjury::letter
            IL_008e: ldarg.1
            IL_008f: ldstr "PAWN"
            IL_0094: call valuetype Verse.NamedArgument Verse.NamedArgumentUtility::Named(object, string)
            IL_0099: call valuetype Verse.TaggedString Verse.GrammarResolverSimpleStringExtensions::Formatted(string, valuetype Verse.NamedArgument)
            IL_009e: stloc.1
            IL_009f: ldloca.s 1
            IL_00a1: ldarg.1
            IL_00a2: ldstr "PAWN"
            IL_00a7: ldc.i4.1
            IL_00a8: call instance valuetype Verse.TaggedString Verse.TaggedString::AdjustedFor(class Verse.Pawn, string, bool)
            IL_00ad: ldsfld class Verse.LetterDef RimWorld.LetterDefOf::NegativeEvent
            IL_00b2: ldarg.1
            IL_00b3: call class Verse.LookTargets Verse.LookTargets::op_Implicit(class Verse.Thing)
            IL_00b8: ldnull
            IL_00b9: ldnull
            IL_00ba: ldnull
            IL_00bb: ldnull
            IL_00bc: callvirt instance void Verse.LetterStack::ReceiveLetter(valuetype Verse.TaggedString, valuetype Verse.TaggedString, class Verse.LetterDef, class Verse.LookTargets, class RimWorld.Faction, class RimWorld.Quest, class [mscorlib]System.Collections.Generic.List`1<class Verse.ThingDef>, string)
    */

    [HarmonyPatch(typeof(HediffGiver_BrainInjury), nameof(HediffGiver_BrainInjury.OnHediffAdded))]
    public static class ReplaceMethodCall
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> ReplaceMethodCall_Patch(IEnumerable<CodeInstruction> instructions)
        {
            // Using CodeMatcher instead of manipulating the instructions directly, but that is also an option.
            var codeMatcher = new CodeMatcher(instructions);

            // An array containing instruction(s) to find.
            // The longer and more specific this array is, the less likely it is you get a false positive...
            // but also more susceptible to other transpilers / game updates.
            // Your call.
            var toMatch = new CodeMatch[]
            {
                new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(LetterStack), nameof(LetterStack.ReceiveLetter), new Type[]
                {
                        // because there are multiple overloads of ReceiveLetter, we need to specify which one we want by providing the types of its parameters.
                        // For methods with no overloads, this Type array parameter is optional.
                    typeof(TaggedString), typeof(TaggedString), typeof(LetterDef), typeof(LookTargets), typeof(Faction), typeof(Quest), typeof(List<ThingDef>), typeof(string)
                }))
            };

            // Replacement instruction(s).
            var replacement = new CodeInstruction[]
            {
                    // Sometimes you need more data to work with in your replacement. In this case we will need the pawn as an additional parameter.
                new CodeInstruction(OpCodes.Ldarg_1),
                    // Call our own version of ReceiveLetter.
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ReplaceMethodCall), nameof(ReplacementReceiveLetter)))
            };

            // Match the provided CodeMatch's, stopping at the matching instruction, or out of bounds if no match is found.
            codeMatcher.MatchStartForward(toMatch);

            if (codeMatcher.IsInvalid)
            {
                // CodeMatcher did not find the instruction(s).
                // Maybe another transpiler got there first and changed the IL code beyond recognition...
                // or the target code changed with a game update.

                Log.Warning("TranspilerExamples: Failed to apply ReplaceMethodCall patch!");

                // Return unchanged instructions.
                return instructions;
            }
            else
            {
                // Remove the normal call.
                codeMatcher.RemoveInstruction();
                // Insert our replacements.
                codeMatcher.Insert(replacement);

                // Return modified instructions.
                return codeMatcher.InstructionEnumeration();
            }
        }

        /*      
         *      // This is a static method, not a virtual instance method like the original, so we need to consume the instance off the stack as the first parameter (think extension methods).
         *  LetterStack letterStackInstance,
         *      // In order to leave the stack in the correct state, we must consume the same parameters in the same order as the original method.
         *      // An alternative would be removing the instructions that put parameters we dont need on the stack...
         *      // or 'pop'ping the values off the stack before the call.
         *  TaggedString label, ... , string debugInfo,
         *      // Now consume our extra parameter.
         *  Pawn pawn   
         *      // Note the order - last parameter is on top of the stack, and the first parameter (the LetterStack instance) is at the bottom of the stack. 
         *      // Also note that parameters with default values have those values simply inserted before their are calls.
        */
        public static void ReplacementReceiveLetter(LetterStack letterStackInstance, TaggedString label, TaggedString text, LetterDef textLetterDef, LookTargets lookTargets, Faction relatedFaction, Quest quest, List<ThingDef> hyperlinkThingDefs, string debugInfo, Pawn pawn)
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

            // No one likes Jerry.
            if (isJerry)
                textLetterDef = LetterDefOf.PositiveEvent;

            // Now send off the letter as usual by calling the original method.
            letterStackInstance.ReceiveLetter(label, text, textLetterDef, lookTargets, relatedFaction, quest, hyperlinkThingDefs, debugInfo);
        }
    }
}
