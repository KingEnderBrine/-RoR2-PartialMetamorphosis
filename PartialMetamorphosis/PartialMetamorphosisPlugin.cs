using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour.HookGen;
using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Permissions;
using UnityEngine;

[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
[assembly: AssemblyVersion(PartialMetamorphosis.PartialMetamorphosisPlugin.Version)]
namespace PartialMetamorphosis
{
    [BepInDependency(InLobbyConfigIntegration.GUID, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(GUID, Name, Version)]
    public class PartialMetamorphosisPlugin : BaseUnityPlugin
    {
        public const string GUID = "com.KingEnderBrine.PartialMetamorphosis";
        public const string Name = "Partial Metamorphosis";
        public const string Version = "1.3.1";

        private static readonly MethodInfo startRun = typeof(PreGameController).GetMethod(nameof(PreGameController.StartRun), BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo respawn = typeof(CharacterMaster).GetMethod(nameof(CharacterMaster.Respawn), new[] { typeof(Vector3), typeof(Quaternion), typeof(bool) });
        private static readonly HashSet<NetworkUser> votedForMetamorphosis = new HashSet<NetworkUser>();

        internal static PartialMetamorphosisPlugin Instance { get; private set; }
        internal static ManualLogSource InstanceLogger => Instance.Logger;

        internal static ConfigEntry<bool> IsEnabled { get; set; }
        internal static ConfigEntry<int> StagesBetweenMetamorphosis { get; set; }

        public void Start()
        {
            IsEnabled = Config.Bind("Main", "enabled", true, "Is mod enabled");
            StagesBetweenMetamorphosis = Config.Bind("Main", nameof(StagesBetweenMetamorphosis), 0, "How much stages should pass until metamorphosis will be applied next time. 0 - metamorphosis will be applied every stage");

            HookEndpointManager.Add(startRun, (Action<Action<PreGameController>, PreGameController>)PreGameControllerStartRun);
            HookEndpointManager.Modify(respawn, (ILContext.Manipulator)CharacterMasterRespawn);

            InLobbyConfigIntegration.OnStart();
        }

        public void Destroy()
        {
            HookEndpointManager.Remove(startRun, (Action<Action<PreGameController>, PreGameController>)PreGameControllerStartRun);
            HookEndpointManager.Unmodify(respawn, (ILContext.Manipulator)CharacterMasterRespawn);
            
            InLobbyConfigIntegration.OnDestroy();
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

        public static bool ShouldChangeCharacter(CharacterMaster master)
        {
            return !IsEnabled.Value || (
                Run.instance.stageClearCount % (StagesBetweenMetamorphosis.Value + 1) == 0 && 
                votedForMetamorphosis.Any(el => el.master == master));
        }

        private static void PreGameControllerStartRun(Action<PreGameController> orig, PreGameController self)
        {
            try
            {
                votedForMetamorphosis.Clear();
                var choice = RuleCatalog.FindChoiceDef("Artifacts.RandomSurvivorOnRespawn.On");
                foreach (var user in NetworkUser.readOnlyInstancesList)
                {
                    var voteController = PreGameRuleVoteController.FindForUser(user);
                    if (voteController && voteController.IsChoiceVoted(choice))
                    {
                        votedForMetamorphosis.Add(user);
                    }
                }
            }
            catch (Exception ex)
            {
                InstanceLogger.LogError(ex.ToString());
            }
            orig(self);
        }
    }
}