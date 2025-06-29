﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;
using ValheimPlus.Configurations;

namespace ValheimPlus.GameClasses
{
    // TODO: clamp stun value

    /// <summary>
    /// Forces a tamed creature to stay asleep if it's recovering from being stunned.
    /// </summary>
    [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateSleep))]
    public static class MonsterAI_UpdateSleep_Patch
    {
        public static void Prefix(MonsterAI __instance, ref float dt)
        {
            if (Configuration.Current.Tameable.IsEnabled)
            {
                Tameable tamed = __instance.GetComponent<Tameable>();
                if (tamed == null)
                    return;

                MonsterAI monsterAI = __instance;
                ZDO zdo = monsterAI.m_nview.GetZDO();
                var mortality = (TameableMortalityTypes)Configuration.Current.Tameable.mortality;
                if (mortality != TameableMortalityTypes.Essential ||
                    zdo == null ||
                    !zdo.GetBool("isRecoveringFromStun")) return;

                if (monsterAI.m_character.m_moveDir != Vector3.zero)
                    monsterAI.StopMoving();

                if (monsterAI.m_sleepTimer != 0f)
                    monsterAI.m_sleepTimer = 0f;

                float timeSinceStun = zdo.GetFloat("timeSinceStun") + dt;
                zdo.Set("timeSinceStun", timeSinceStun);

                if (timeSinceStun >= Configuration.Current.Tameable.stunRecoveryTime)
                {
                    zdo.Set("timeSinceStun", 0f);
                    monsterAI.m_sleepTimer = 0.5f;
                    monsterAI.m_character.m_animator.SetBool("sleeping", false);
                    zdo.Set("sleeping", false);
                    zdo.Set("isRecoveringFromStun", false);
                }

                dt = 0f;
            }
        }
    }

    [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
    public static class MonsterAI_UpdateAI_Transpiler
    {
        [UsedImplicitly]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
            ILGenerator ilGenerator)
        {
            var il = instructions.ToList();
            if (!Configuration.Current.Tameable.IsEnabled || !Configuration.Current.Tameable.ignoreAlerted) return il;
            
            // Ignores alerted state when tameable mob is looking for food in order to start the taming process.
            // This is the _only_ way to ignore IsAlerted without destroying the AI for all other creatures.
            var matcher = new CodeMatcher(il, ilGenerator);
            try
            {
                var updateConsumeItem = AccessTools.Method(typeof(MonsterAI), nameof(MonsterAI.UpdateConsumeItem));
                var updateConsumeItemLabel = matcher
                    .MatchStartForward(
                        OpCodes.Ldarg_0,
                        OpCodes.Ldloc_0,
                        OpCodes.Ldarg_1,
                        new CodeMatch(inst => inst.Calls(updateConsumeItem)))
                    .ThrowIfNotMatch("No match for UpdateConsumeItem method call.")
                    .Labels
                    .First();

                return matcher
                    .MatchStartBackwards(OpCodes.Ret)
                    .ThrowIfNotMatch("Could not find the end of the conditional before UpdateConsumeItem call.")
                    .Advance(1)
                    .Set(OpCodes.Br_S, updateConsumeItemLabel)
                    .InstructionEnumeration();
            }
            catch (Exception ex)
            {
                ValheimPlusPlugin.Logger.LogError(ex);
            }

            return il;
        }
    }
}
