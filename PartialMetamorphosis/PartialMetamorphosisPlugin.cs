using BepInEx;
using BepInEx.Configuration;
using InLobbyConfig;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API.Utils;
using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PartialMetamorphosis
{
    [NetworkCompatibility(CompatibilityLevel.NoNeedForSync)]
    [BepInDependency("com.KingEnderBrine.InLobbyConfig")]
    [BepInDependency("com.bepis.r2api")]
    [BepInPlugin("com.KingEnderBrine.PartialMetamorphosis", "Partial Metamorphosis", "1.1.0")]
    public class PartialMetamorphosisPlugin : BaseUnityPlugin
    {
        private static readonly HashSet<NetworkUser> votedForMetamorphosis = new HashSet<NetworkUser>();
        private static ConfigEntry<bool> IsEnabled { get; set; }

        private ModConfigEntry ModConfig { get; set; }

        public void Awake()
        {
            IsEnabled = Config.Bind("Main", "enabled", true, "Is mod enabled");

            On.RoR2.PreGameController.StartRun += PreGameControllerStartRun;
            IL.RoR2.CharacterMaster.Respawn += CharacterMasterRespawn;

            ModConfig = new ModConfigEntry
            {
                DisplayName = "Partial Metamorphosis",
                EnableField = new InLobbyConfig.Fields.BooleanConfigField("", () => IsEnabled.Value, (newValue) => IsEnabled.Value = newValue)
            };
            ModConfigCatalog.Add(ModConfig);
        }

        public void Destroy()
        {
            On.RoR2.PreGameController.StartRun -= PreGameControllerStartRun;
            IL.RoR2.CharacterMaster.Respawn -= CharacterMasterRespawn;
            ModConfigCatalog.Remove(ModConfig);
        }

        private static void CharacterMasterRespawn(ILContext il)
        {
            var c = new ILCursor(il);

            c.GotoNext(x => x.MatchLdsfld(typeof(RoR2Content.Artifacts), "randomSurvivorOnRespawnArtifactDef"));
            c.Index += 3;
            var endIf = c.Previous.Operand;
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<CharacterMaster, bool>>(ShouldChangeCharacter);
            c.Emit(OpCodes.Brfalse_S, endIf);
        }

        private static bool ShouldChangeCharacter(CharacterMaster master)
        {
            return !IsEnabled.Value || votedForMetamorphosis.Any(el => el.master == master);
        }

        private static void PreGameControllerStartRun(On.RoR2.PreGameController.orig_StartRun orig, PreGameController self)
        {
            votedForMetamorphosis.Clear();
            var choice = RuleCatalog.FindChoiceDef("Artifacts.RandomSurvivorOnRespawn.On");
            foreach (var user in NetworkUser.readOnlyInstancesList)
            {
                var voteController = PreGameRuleVoteController.FindForUser(user);
                var isMetamorphosisVoted = voteController.IsChoiceVoted(choice);

                if (isMetamorphosisVoted)
                {
                    votedForMetamorphosis.Add(user);
                }
            }
            orig(self);
        }
    }
}