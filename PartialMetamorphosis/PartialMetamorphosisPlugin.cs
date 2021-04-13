using BepInEx;
using BepInEx.Configuration;
using InLobbyConfig;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour.HookGen;
using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

[assembly: R2API.Utils.ManualNetworkRegistration]
[assembly: EnigmaticThunder.Util.ManualNetworkRegistration]
namespace PartialMetamorphosis
{
    [BepInDependency("com.KingEnderBrine.InLobbyConfig")]
    [BepInPlugin("com.KingEnderBrine.PartialMetamorphosis", "Partial Metamorphosis", "1.2.1")]
    public class PartialMetamorphosisPlugin : BaseUnityPlugin
    {
        private static readonly MethodInfo startRun = typeof(PreGameController).GetMethod(nameof(PreGameController.StartRun), BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly HashSet<NetworkUser> votedForMetamorphosis = new HashSet<NetworkUser>();
        private static ConfigEntry<bool> IsEnabled { get; set; }

        private ModConfigEntry ModConfig { get; set; }

        public void Awake()
        {
            IsEnabled = Config.Bind("Main", "enabled", true, "Is mod enabled");

            HookEndpointManager.Add(startRun, (Action<Action<PreGameController>, PreGameController>)PreGameControllerStartRun);
            HookEndpointManager.Modify(typeof(CharacterMaster).GetMethod(nameof(CharacterMaster.Respawn)), (ILContext.Manipulator)CharacterMasterRespawn);

            ModConfig = new ModConfigEntry
            {
                DisplayName = "Partial Metamorphosis",
                EnableField = new InLobbyConfig.Fields.BooleanConfigField("", () => IsEnabled.Value, (newValue) => IsEnabled.Value = newValue)
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
            return !IsEnabled.Value || votedForMetamorphosis.Any(el => el.master == master);
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

namespace R2API.Utils
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class ManualNetworkRegistrationAttribute : Attribute { }
}

namespace EnigmaticThunder.Util
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class ManualNetworkRegistrationAttribute : Attribute { }
}