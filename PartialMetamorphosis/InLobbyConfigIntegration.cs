using BepInEx.Bootstrap;
using InLobbyConfig;
using InLobbyConfig.Fields;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PartialMetamorphosis
{
    public static class InLobbyConfigIntegration
    {
        public const string GUID = "com.KingEnderBrine.InLobbyConfig";
        private static bool Enabled => Chainloader.PluginInfos.ContainsKey(GUID);
        private static object ModConfig { get; set; }

        public static void OnStart()
        {
            if (Enabled)
            {
                OnStartInternal();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void OnStartInternal()
        {
            var modConfig = new ModConfigEntry
            {
                DisplayName = "Partial Metamorphosis",
                EnableField = new BooleanConfigField("", () => PartialMetamorphosisPlugin.IsEnabled.Value, (newValue) => PartialMetamorphosisPlugin.IsEnabled.Value = newValue),

            };

            modConfig.SectionFields["Main"] = new List<IConfigField>
            {
                ConfigFieldUtilities.CreateFromBepInExConfigEntry(PartialMetamorphosisPlugin.StagesBetweenMetamorphosis)
            }; 
            ModConfigCatalog.Add(modConfig);
            ModConfig = modConfig;
        }
        public static void OnDestroy()
        {
            if (Enabled)
            {
                OnDestroyInternal();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void OnDestroyInternal()
        {
            ModConfigCatalog.Remove(ModConfig as ModConfigEntry);
            ModConfig = null;
        }
    }
}
