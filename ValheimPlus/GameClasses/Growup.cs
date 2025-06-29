using HarmonyLib;
using JetBrains.Annotations;
using ValheimPlus.Configurations;

namespace ValheimPlus.GameClasses
{
    public static class GrowupHelpers
    {
        public static int GetGrowTimeLeft(Growup growup) =>
            (int)(growup.m_growTime - growup.m_baseAI.GetTimeSinceSpawned().TotalSeconds);
    }

    [HarmonyPatch(typeof(Growup), nameof(Growup.Start))]
    public static class Growup_Start_Patch
    {
        [UsedImplicitly]
        public static void Prefix(Growup __instance)
        {
            var eggConfig = Configuration.Current.Egg;
            var procreationConfig = Configuration.Current.Procreation;
            var humanoid = __instance.m_grownPrefab.GetComponent<Humanoid>();
            if (!humanoid) return;

            if (eggConfig.IsEnabled && humanoid.m_name == "$enemy_hen")
                __instance.m_growTime = eggConfig.growTime;
            else if (procreationConfig.IsEnabled && ProcreationHelpers.IsValidAnimalType(humanoid.m_name))
                Helper.applyModifierValueTo(ref __instance.m_growTime, procreationConfig.maturityDurationMultiplier);
        }
    }
}
