using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;
using ValheimPlus.Configurations;

namespace ValheimPlus.GameClasses
{
    /// <summary>
    /// Change Ping and global message behavior
    /// </summary>
    [HarmonyPatch(typeof(Chat), nameof(Chat.OnNewChatMessage), typeof(GameObject), typeof(long), typeof(Vector3),
        typeof(Talker.Type), typeof(UserInfo), typeof(string))]
    public static class Chat_OnNewChatMessage_Patch
    {
        [UsedImplicitly]
        private static bool Prefix(ref Chat __instance, GameObject go, long senderID, Vector3 pos, Talker.Type type,
            UserInfo sender, string text)
        {
            var config = Configuration.Current.Chat;
            if (!config.IsEnabled) return true;

            return type switch
            {
                Talker.Type.Ping => config.pingDistance <= 0 ||
                                    Helper.IsSenderPlayerInRange(senderID, config.pingDistance),
                Talker.Type.Shout => config.shoutDistance <= 0 || config.outOfRangeShoutsDisplayInChatWindow ||
                                     Helper.IsSenderPlayerInRange(senderID, config.shoutDistance),
                _ => true
            };
        }
    }

    // This code expects that AddInworldText is only called with shouts from OnNewChatMessage.
    // If this changes, then Chat.outOfRangeShoutsDisplayInChatWindow may malfunction.
    [HarmonyPatch(typeof(Chat), nameof(Chat.AddInworldText), typeof(GameObject), typeof(long), typeof(Vector3),
        typeof(Talker.Type), typeof(UserInfo), typeof(string))]
    // sic: "Inworld" is from the game code 
    public static class Chat_AddInworldText_Patch
    {
        /// <summary>
        ///  Skips if the text is from a shout from a player out of range.
        /// </summary>
        [UsedImplicitly]
        private static bool Prefix(ref Chat __instance, long senderID, Talker.Type type, UserInfo user, string text)
        {
            if (type != Talker.Type.Shout) return true;

            var config = Configuration.Current.Chat;
            if (!config.IsEnabled || !config.outOfRangeShoutsDisplayInChatWindow) return true;

            var shoutDistance = config.shoutDistance;
            return shoutDistance <= 0 || Helper.IsSenderPlayerInRange(senderID, shoutDistance);
        }
    }

    [HarmonyPatch(typeof(Chat), nameof(Chat.AddInworldText), typeof(GameObject), typeof(long), typeof(Vector3),
        typeof(Talker.Type), typeof(UserInfo), typeof(string))]
    // sic: "Inworld" is from the game code 
    public static class Chat_AddInworldText_Transpiler
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
                        OpCodes.Ldarg_S,
                        new CodeMatch(OpCodes.Callvirt, Method_String_ToLowerInvariant),
                        OpCodes.Starg_S
                    )
                    .ThrowIfNotMatch("No match for code that sets whispers to lower case.")
                    .RemoveInstructions(3)
                    .Start()
                    .MatchStartForward(
                        OpCodes.Ldarg_S,
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
                    "Failed to apply `Chat_AddInworldText_Transpiler`." +
                    $" This may cause the Chat.forcedCase setting to not function correctly. Exception is:\n{e}");
                return il;
            }
        }
    }
}