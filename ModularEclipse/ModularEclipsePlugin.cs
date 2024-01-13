using BepInEx;
using BepInEx.Configuration;
using RoR2;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security;
using System.Security.Permissions;
using UnityEngine;
using UnityEngine.AddressableAssets;

#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618 // Type or member is obsolete
[module: UnverifiableCode]
#pragma warning disable 
namespace ModularEclipse
{
    [BepInPlugin(guid, modName, version)]
    public class ModularEclipsePlugin : BaseUnityPlugin
    {
        #region plugin info
        public static PluginInfo PInfo { get; private set; }
        public const string guid = "com." + teamName + "." + modName;
        public const string teamName = "HouseOfFruits";
        public const string modName = "ModularEclipse";
        public const string version = "1.0.0";
        #endregion

        internal static ConfigFile ArtifactWhitelistConfig { get; set; }
        public static void SetArtifactDefaultWhitelist(ArtifactDef artifactDef, bool defaultValue)
        {
            string cachedName = artifactDef.cachedName;
            if (cachedName == "")
            {
                Debug.LogError("Artifact " + artifactDef.nameToken + " has no cached name! The Eclipse rule choice selection will not work for it.");
                return;
            }
            ArtifactWhitelistConfig.Bind<bool>("Eclipse: Whitelisted Artifacts", cachedName, defaultValue,
                    "If true, this artifact will be *allowed* for use in the Eclipse gamemode. Recommended only difficulty artifacts should be enabled.");
        }

        void Awake()
        {
            ArtifactWhitelistConfig = new ConfigFile(Paths.ConfigPath + $"\\{modName}.cfg", true);
            On.RoR2.EclipseRun.OverrideRuleChoices += EclipseRuleChoices;
        }
        private void EclipseRuleChoices(On.RoR2.EclipseRun.orig_OverrideRuleChoices orig, EclipseRun self, RuleChoiceMask mustInclude, RuleChoiceMask mustExclude, ulong runSeed)
        {
            //self.base.OverrideRuleChoices(mustInclude, mustExclude, runSeed);

            //get the current eclipse level for the currently selected character
            int maxEclipseLevel = 0;
            ReadOnlyCollection<NetworkUser> readOnlyInstancesList = NetworkUser.readOnlyInstancesList;
            for (int i = 0; i < readOnlyInstancesList.Count; i++)
            {
                NetworkUser networkUser = readOnlyInstancesList[i];
                SurvivorDef survivorPreference = networkUser.GetSurvivorPreference();
                if (survivorPreference)
                {
                    int eclipseLevelForCharacter = EclipseRun.GetNetworkUserSurvivorCompletedEclipseLevel(networkUser, survivorPreference) + 1;
                    maxEclipseLevel = ((maxEclipseLevel > 0) ? Math.Min(maxEclipseLevel, eclipseLevelForCharacter) : eclipseLevelForCharacter);
                }
            }
            //get the difficulty index for the max eclipse level
            maxEclipseLevel = Math.Min(maxEclipseLevel, EclipseRun.maxEclipseLevel);
            DifficultyIndex eclipseDifficultyIndex = EclipseRun.GetEclipseDifficultyIndex(maxEclipseLevel);

            //allow selection of all unlocked eclipse levels
            RuleDef difficultyRuleDef = RuleCatalog.FindRuleDef("Difficulty");
            foreach (RuleChoiceDef ruleChoice in difficultyRuleDef.choices)
            {
                if (ruleChoice.excludeByDefault == true && ruleChoice.difficultyIndex <= eclipseDifficultyIndex)
                {
                    if (ruleChoice.difficultyIndex == eclipseDifficultyIndex)
                        difficultyRuleDef.defaultChoiceIndex = ruleChoice.localIndex;
                    mustInclude[ruleChoice.globalIndex] = true;
                    mustExclude[ruleChoice.globalIndex] = false;
                    //self.ForceChoice(mustInclude, mustExclude, ruleChoice);
                }
                else
                {
                    mustInclude[ruleChoice.globalIndex] = false;
                    mustExclude[ruleChoice.globalIndex] = true;
                }
            }

            //disable beads of fealty (vanilla)
            self.ForceChoice(mustInclude, mustExclude, $"Items.{RoR2Content.Items.LunarTrinket.name}.Off");

            //disable all but whitelisted artifacts
            for (int j = 0; j < ArtifactCatalog.artifactCount; j++)
            {
                ArtifactDef artifactDef = ArtifactCatalog.GetArtifactDef((ArtifactIndex)j);
                string cachedName = artifactDef.cachedName;
                if(cachedName == "")
                {
                    Debug.LogError("Artifact " + artifactDef.nameToken + " has no cached name! The Eclipse rule choice selection will not work for it.");
                    continue;
                }

                bool artifactAllowed = ArtifactWhitelistConfig.Bind<bool>("Eclipse: Whitelisted Artifacts", cachedName, false,
                    "If true, this artifact will be *allowed* for use in the Eclipse gamemode. Recommended only difficulty artifacts should be enabled.").Value;

                //only perform the logic that force disables an artifact if its name is not included in the whitelist
                if (!artifactAllowed)
                {
                    RuleDef ruleDef = RuleCatalog.FindRuleDef("Artifacts." + cachedName);
                    self.ForceChoice(mustInclude, mustExclude, ruleDef.FindChoice("Off"));
                }
            }
        }
    }
}
