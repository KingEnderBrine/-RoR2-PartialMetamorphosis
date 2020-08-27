using BepInEx;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API.Utils;
using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PartialMetamorphosis
{
    [NetworkCompatibility(CompatibilityLevel.NoNeedForSync)]
    [BepInDependency("com.bepis.r2api")]
    [R2APISubmoduleDependency(nameof(CommandHelper))]
    [BepInPlugin("com.KingEnderBrine.PartialMetamorphosis", "Partial Metamorphosis", "1.0.1")]
    public class PartialMetamorphosis : BaseUnityPlugin
    {
        private static readonly HashSet<NetworkUser> votedForMetamorphosis = new HashSet<NetworkUser>();

        private static ConfigEntry<bool> IsEnabled { get; set; }


        public void Awake()
        {
            CommandHelper.AddToConsoleWhenReady();
            
            IsEnabled = Config.Bind("Main", "enabled", true, "Is mod enabled");
            
            On.RoR2.PreGameController.StartRun += (orig, self) =>
            {
                votedForMetamorphosis.Clear();
                foreach (var user in NetworkUser.readOnlyInstancesList)
                {
                    var voteController = PreGameRuleVoteController.FindForUser(user);
                    var isMetamorphosisVoted = voteController.IsChoiceVoted(RuleCatalog.FindChoiceDef("Artifacts.RandomSurvivorOnRespawn.On"));
                    
                    if (isMetamorphosisVoted)
                    {
                        votedForMetamorphosis.Add(user);
                    }
                }
                orig(self);
            };

            IL.RoR2.CharacterMaster.Respawn += (il) =>
            {
                var c = new ILCursor(il);
                c.GotoNext(x => x.MatchLdsfld(typeof(RoR2Content.Artifacts), "randomSurvivorOnRespawnArtifactDef"));
                c.Index += 3;
                var endIf = c.Previous.Operand;
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<CharacterMaster, bool>>((master) => {
                    return !IsEnabled.Value || votedForMetamorphosis.Any(el => el.master == master);
                });
                c.Emit(OpCodes.Brfalse_S, endIf);
            };
        }

        [ConCommand(commandName = "pm_enable", flags = ConVarFlags.None, helpText = "Enable partial metamorphosis")]
        private static void CCEnable(ConCommandArgs args)
        {
            if (IsEnabled.Value)
            {
                return;
            }
            IsEnabled.Value = true;
            IsEnabled.ConfigFile.Save();
            Debug.Log($"[PartialMetamorphosis] is enabled");
        }

        [ConCommand(commandName = "pm_disable", flags = ConVarFlags.None, helpText = "Disable partial metamorphosis")]
        private static void CCDisable(ConCommandArgs args)
        {
            if (!IsEnabled.Value)
            {
                return;
            }
            IsEnabled.Value = false;
            IsEnabled.ConfigFile.Save();
            Debug.Log($"[PartialMetamorphosis] is disabled");
        }

        [ConCommand(commandName = "pm_status", flags = ConVarFlags.None, helpText = "Shows is partial metamorphosis enabled or disabled")]
        private static void CCStatus(ConCommandArgs args)
        {
            Debug.Log($"[PartialMetamorphosis] is {(IsEnabled.Value ? "enabled" : "disabled")}");
        }
    }
}