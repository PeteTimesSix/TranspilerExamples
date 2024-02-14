using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;
using RimWorld;
using UnityEngine.UIElements;

namespace PeteTimesSix.TranspilerExamples.HarmonyPatchExamples
{
    /*
     * The objective of this patch is to cause damage to weapons on equip,
     * but ONLY if the pawn's name is Jerry.
     * 
     * This is the target code, as decompiled by ilSpy:

        ThingWithComps thingWithComps = (ThingWithComps)job.targetA.Thing;
		ThingWithComps thingWithComps2 = null;
		if (thingWithComps.def.stackLimit > 1 && thingWithComps.stackCount > 1)
		{
			thingWithComps2 = (ThingWithComps)thingWithComps.SplitOff(1);
		}
		else
		{
			thingWithComps2 = thingWithComps;
			thingWithComps2.DeSpawn();
		}
		pawn.equipment.MakeRoomFor(thingWithComps2);
		pawn.equipment.AddEquipment(thingWithComps2);
		if (thingWithComps.def.soundInteract != null)
		{
			thingWithComps.def.soundInteract.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
		}

     * However, this code is inside of an anonymous Toil initAction delegate,
     * which itself is in a iterator method MakeNewToils. 
     * Both are syntax sugar that are compiled into compiler-generated inner classes.
     * In order to get at them, we need to dig through the nested classes and find the target method
     * by looking for the code it contains or some other means of identifying it.
     * Using its name is not viable, as it's compiler-generated and prone to changing often.
     * 
     * We will be adding 
     * "JerryCheck(pawn, weapon)"
     * after 
     * "pawn.equipment.AddEquipment(thingWithComps2);".
     * 
     * The relevant IL code:

 	        // pawn.equipment.AddEquipment(thingWithComps2);
	    IL_0058: ldarg.0
	    IL_0059: ldfld class Verse.Pawn Verse.AI.JobDriver::pawn
	    IL_005e: ldfld class Verse.Pawn_EquipmentTracker Verse.Pawn::equipment
	    IL_0063: ldloc.1
	    IL_0064: callvirt instance void Verse.Pawn_EquipmentTracker::AddEquipment(class Verse.ThingWithComps)
    */

    [HarmonyPatch]
    public static class CompilerGeneratedClasses
    {
        // An array containing instruction(s) to find. Used in multiple places, so it is defined here.
        // The longer and more specific this array is, the less likely it is you get a false positive...
        // but also more susceptible to other transpilers / game updates.
        // Your call.
        private static readonly CodeMatch[] toMatch = new CodeMatch[]
        {
            new CodeMatch(OpCodes.Ldarg_0),
            new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(JobDriver), nameof(JobDriver.pawn))),
            new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(Pawn), nameof(Pawn.equipment))),
            new CodeMatch(OpCodes.Ldloc_1),
            new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.AddEquipment)))
        };

        //Check if we can find the methods to patch. Otherwise, Harmony throws an error if given no methods in HarmonyTargetMethods.
        [HarmonyPrepare]
        public static bool ShouldPatch()
        {
            // We can reuse the same method as HarmonyTargetMethods will use afterward.
            var methods = CalculateMethods();

            //check that we have one and only one match. If we get more, the match is giving false positives.
            if (methods.Count() == 1)
                return true;
            return false;
            //if targetting multiple methods, use 'return methods.Any()'

        }

        //Determine which method(s) we are going to patch.
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> CalculateMethods()
        {
            //Find all possible candidates, both from the wrapping type and all nested types.
            var candidates = AccessTools.GetDeclaredMethods(typeof(JobDriver_Equip)).ToHashSet();
            candidates.AddRange(typeof(JobDriver_Equip).GetNestedTypes(AccessTools.all).SelectMany(t => AccessTools.GetDeclaredMethods(t)));

            //check all candidates for the target instructions, return those that match.
            foreach (var method in candidates)
            {
                var instructions = PatchProcessor.GetCurrentInstructions(method);
                //var matched = instructions.Matches(toMatch); // Available in Harmony 2.3+
                var matched = new CodeMatcher(instructions).MatchStartForward(toMatch).IsValid;
                if (matched)
                    yield return method;
            }
            yield break;
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> CompilerGenerated_Patch(IEnumerable<CodeInstruction> instructions)
        {
            // Using CodeMatcher instead of manipulating the instructions directly, but that is also an option.
            var codeMatcher = new CodeMatcher(instructions);

            // New instruction(s) to insert.
            CodeInstruction[] newInstructions = new CodeInstruction[]
            {
                    // Our method requires the pawn and the weapon as parameters, so put them on the stack.
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(JobDriver), nameof(JobDriver.pawn))),
                new CodeInstruction(OpCodes.Ldloc_1),
                    // Call our additional method.
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CompilerGeneratedClasses), nameof(JerryCheck)))
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
                // We want the check to run *after* the pawn is done equipping the weapon.
                codeMatcher.Advance(1);
                // Insert the new instructions.
                codeMatcher.Insert(newInstructions);

                // Return modified instructions.
                return codeMatcher.InstructionEnumeration();
            }
        }

        /*
         * The parameters are the values we put on the stack and the category generated by the normal code.
         * Note the order - the last parameter (the ThingWithComps) is on top of the stack, and the first parameter (the Pawn) is at the bottom of the stack. 
        */
        public static void JerryCheck(Pawn pawn, ThingWithComps weapon)
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
            {
                var decrease = Math.Min(10, weapon.HitPoints - 1); // make sure not to reduce hitpoints below 1
                weapon.HitPoints -= decrease;
            }
        }
    }
}
