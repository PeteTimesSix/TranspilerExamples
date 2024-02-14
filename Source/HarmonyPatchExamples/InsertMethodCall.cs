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
     * The objective of this patch is to change the quality of recipe products to Awful
     * but ONLY if the worker pawn's name is Jerry.
     * 
     * This is the target method, as decompiled by ilSpy:
     
            private static Thing PostProcessProduct(Thing product, RecipeDef recipeDef, Pawn worker, Precept_ThingStyle precept = null, ThingStyleDef style = null, int? overrideGraphicIndex = null)
            {
	            CompQuality compQuality = product.TryGetComp<CompQuality>();
	            if (compQuality != null)
	            {
		            if (recipeDef.workSkill == null)
		            {
			            Log.Error(string.Concat(recipeDef, " needs workSkill because it creates a product with a quality."));
		            }
		            QualityCategory q = QualityUtility.GenerateQualityCreatedByPawn(worker, recipeDef.workSkill);
		            compQuality.SetQuality(q, ArtGenerationContext.Colony);
		            QualityUtility.SendCraftNotification(product, worker);
	            }
    
                //Irrelevant additonal code...

	            return product;
            }
     
     * We will be inserting a call after GenerateQualityCreatedByPawn generates a
     * QualityCategory but before stloc.2 stores its value into a local.
     * 
     * The relevant IL code:
     
      	        // QualityCategory q = QualityUtility.GenerateQualityCreatedByPawn(worker, recipeDef.workSkill);
     	    IL_0022: ldarg.2
	        IL_0023: ldarg.1
	        IL_0024: ldfld class RimWorld.SkillDef Verse.RecipeDef::workSkill
	        IL_0029: call valuetype RimWorld.QualityCategory RimWorld.QualityUtility::GenerateQualityCreatedByPawn(class Verse.Pawn, class RimWorld.SkillDef)
	        IL_002e: stloc.2
     */

    // Because PostProcessProduct is private, we can't use nameof(PostProcessProduct).
    [HarmonyPatch(typeof(GenRecipe), "PostProcessProduct")]
    public static class InsertMethodCall
    {

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> InsertMethodCall_Patch(IEnumerable<CodeInstruction> instructions)
        {
            // Using CodeMatcher instead of manipulating the instructions directly, but that is also an option.
            var codeMatcher = new CodeMatcher(instructions);

            // An array containing instruction(s) to find.
            // The longer and more specific this array is, the less likely it is you get a false positive...
            // but also more susceptible to other transpilers / game updates.
            // Your call.
            var toMatch = new CodeMatch[]
            {
                new CodeMatch(OpCodes.Ldarg_2),
                new CodeMatch(OpCodes.Ldarg_1),
                new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(RecipeDef), nameof(RecipeDef.workSkill))),
                new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(QualityUtility), nameof(QualityUtility.GenerateQualityCreatedByPawn), new Type[] {
                        // because there are multiple overloads of GenerateQualityCreatedByPawn, we need to specify which one we want by providing the types of its parameters.
                        // For methods with no overloads, this Type array parameter is optional.
                    typeof(Pawn), typeof(SkillDef) 
                }))
            };

            // New instruction(s) to insert.
            var newInstructions = new CodeInstruction[]
            {
                    // Sometimes you need more data to work with in your method. In this case we will need the pawn as an additional parameter.
                new CodeInstruction(OpCodes.Ldarg_2),
                    // Call our JerryModifier method.
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(InsertMethodCall), nameof(JerryModifier))),
            };

            // Match the provided CodeMatch's, stopping at the last instruction of the match, or out of bounds if no match is found.
            codeMatcher.MatchEndForward(toMatch);

            if (codeMatcher.IsInvalid)
            {
                // CodeMatcher did not find the instruction(s).
                // Maybe another transpiler got there first and changed the IL code beyond recognition...
                // or the target code changed with a game update.

                Log.Warning("TranspilerExamples: Failed to apply InsertMethodCall patch!");

                // Return unchanged instructions.
                return instructions;
            }
            else
            {
                // We want our instructions placed right *after* the normal quality category is generated.
                codeMatcher.Advance(1);
                // Insert our replacements.
                codeMatcher.Insert(newInstructions);

                // Return modified instructions.
                return codeMatcher.InstructionEnumeration();
            }
        }


        /*
         * The parameters are the values we put on the stack and the category generated by the normal code.
         * The returned result is put on top of the stack, replacing the consumed QualityCategory.
         * Note the order - the last parameter (the Pawn) is on top of the stack, and the first parameter (the QualityCategory) is at the bottom of the stack. 
        */
        public static QualityCategory JerryModifier(QualityCategory category, Pawn pawn)
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
                return QualityCategory.Awful;

            // Everyone else is fine.
            return category;
        }
    }
}
