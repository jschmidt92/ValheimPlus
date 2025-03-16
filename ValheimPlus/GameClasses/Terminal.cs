using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using Splatform;
using ValheimPlus.Configurations;

namespace ValheimPlus.GameClasses
{
    [HarmonyPatch(typeof(Terminal), nameof(Terminal.AddString), typeof(PlatformUserID), typeof(string),
        typeof(Talker.Type), typeof(bool))]
    public static class Terminal_AddString_Transpiler
    {
        private static readonly MethodInfo Method_String_ToUpper =
            AccessTools.Method(typeof(string), nameof(string.ToUpper));

        private static readonly MethodInfo Method_String_ToLowerInvariant =
            AccessTools.Method(typeof(string), nameof(string.ToLowerInvariant));

        /// <summary>
        ///  Replaces enforced case conversions for Shouts and Whispers
        /// </summary>
        [UsedImplicitly]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
            ILGenerator generator)
        {
            var config = Configuration.Current.Chat;
            if (!config.IsEnabled || config.forcedCase) return instructions;

            var il = instructions.ToList();
            try
            {
                return new CodeMatcher(il, generator)
                    .MatchStartForward(
                        OpCodes.Ldarg_2,
                        new CodeMatch(OpCodes.Callvirt, Method_String_ToLowerInvariant),
                        OpCodes.Starg_S
                    )
                    .ThrowIfNotMatch("No match for code that sets whispers to lower case.")
                    .RemoveInstructions(3)
                    .Start()
                    .MatchStartForward(
                        OpCodes.Ldarg_2,
                        new CodeMatch(OpCodes.Callvirt, Method_String_ToUpper),
                        OpCodes.Starg_S
                    )
                    .ThrowIfNotMatch("No match for code that sets shouts to upper case.")
                    .RemoveInstructions(3)
                    .InstructionEnumeration();
            }
            catch (Exception e)
            {
                ValheimPlusPlugin.Logger.LogError(
                    "Failed to apply `Terminal_AddString_Transpiler`." +
                    $" This may cause the Chat.forcedCase setting to not function correctly. Exception is:\n{e}");
                return il;
            }
        }
    }
}