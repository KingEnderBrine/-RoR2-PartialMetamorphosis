using BepInEx;
using BepInEx.Configuration;
using InLobbyConfig;
using InLobbyConfig.Fields;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour.HookGen;
using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace PartialMetamorphosis
{
    [BepInDependency("com.KingEnderBrine.InLobbyConfig", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin("com.KingEnderBrine.PartialMetamorphosis", "Partial Metamorphosis", "1.2.2")]
    public class PartialMetamorphosisPlugin : BaseUnityPlugin
    {
        private static readonly MethodInfo startRun = typeof(PreGameController).GetMethod(nameof(PreGameController.StartRun), BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly HashSet<NetworkUser> votedForMetamorphosis = new HashSet<NetworkUser>();
        private static ConfigEntry<bool> IsEnabled { get; set; }
        private static ConfigEntry<int> StagesBetweenMetamorphosis { get; set; }

        private ModConfigEntry ModConfig { get; set; }

        public void Awake()
        {
            IsEnabled = Config.Bind("Main", "enabled", true, "Is mod enabled");
            StagesBetweenMetamorphosis = Config.Bind("Main", nameof(StagesBetweenMetamorphosis), 0, "How much stages should pass until metamorphosis will be applied next time. 0 - metamorphosis will be applied every stage");

            HookEndpointManager.Add(startRun, (Action<Action<PreGameController>, PreGameController>)PreGameControllerStartRun);
            HookEndpointManager.Modify(typeof(CharacterMaster).GetMethod(nameof(CharacterMaster.Respawn)), (ILContext.Manipulator)CharacterMasterRespawn);

            ModConfig = new ModConfigEntry
            {
                DisplayName = "Partial Metamorphosis",
                EnableField = new BooleanConfigField("", () => IsEnabled.Value, (newValue) => IsEnabled.Value = newValue),

            };
            ModConfig.SectionFields["Main"] = new List<IConfigField>
            {
                ConfigFieldUtilities.CreateFromBepInExConfigEntry(StagesBetweenMetamorphosis)
            }; 
            ModConfigCatalog.Add(ModConfig);
        }

        public void Destroy()
        {
            HookEndpointManager.Remove(startRun, (Action<Action<PreGameController>, PreGameController>)PreGameControllerStartRun);
            HookEndpointManager.Unmodify(typeof(CharacterMaster).GetMethod(nameof(CharacterMaster.Respawn)), (ILContext.Manipulator)CharacterMasterRespawn);
            
            ModConfigCatalog.Remove(ModConfig);
        }

        private static void CharacterMasterRespawn(ILContext il)
        {
            var c = new ILCursor(il);
            c.GotoNext(x => x.MatchCall(typeof(RoR2Content.Artifacts), "get_randomSurvivorOnRespawnArtifactDef"));
            c.Index += 3;
            var endIf = c.Previous.Operand;
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<CharacterMaster, bool>>(ShouldChangeCharacter);
            c.Emit(OpCodes.Brfalse_S, endIf);
        }

        private static bool ShouldChangeCharacter(CharacterMaster master)
        {
            return !IsEnabled.Value || (
                Run.instance.stageClearCount % (StagesBetweenMetamorphosis.Value + 1) == 0 && 
                votedForMetamorphosis.Any(el => el.master == master));
        }

        private static void PreGameControllerStartRun(Action<PreGameController> orig, PreGameController self)
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