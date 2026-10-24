using BepInEx;
using RoR2;
using SneedHooks;
using System;
using System.Collections.Generic;
using System.Security.Permissions;
using System.Security;
using UnityEngine;
using MonoMod.Cil;
using UnityEngine.AddressableAssets;
using RoR2.Skills;
using RoR2.Projectile;


[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
namespace BlightReturnsRework
{
    [BepInDependency("com.RiskyLives.LinearDamage", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.RiskyLives.SneedHooks")]
    [BepInPlugin("com.Moffein.BlightReturnsRework", "BlightReturnsRework", "1.0.3")]
    public class BlightReturnsReworkPlugin : BaseUnityPlugin
    {
        public static float damageMultPerBuff = 0.05f;
        public static PluginInfo pluginInfo;

        private void Awake()
        {
            pluginInfo = Info;
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.RiskyLives.RiskyMod"))
            {
                Debug.LogWarning("BlightReturnsRework: RiskyMod is loaded, disabling mod. RiskyMod's version of the tweak will be used instead.");
                return;
            }

            On.RoR2.Language.SetFolders += LoadLanguage.fixme;
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.Moffein.LinearDamage"))
            {
                SneedHooks.ModifyFinalDamage.ModifyFinalDamageActions += ModifyFinalDamage_Additive;
            }
            else
            {
                SneedHooks.ModifyFinalDamage.ModifyFinalDamageActions += ModifyFinalDamage;
            }

            On.RoR2.DotController.AddDot_GameObject_float_HurtBox_DotIndex_float_ProcChainMask_Nullable1_Nullable1_Nullable1 += ModifyBlight;

            IL.RoR2.GlobalEventManager.ProcessHitEnemy += GlobalEventManager_ProcessHitEnemy;

            RoR2Application.onLoad += OnLoadActions;

            var comp = Addressables.LoadAssetAsync<GameObject>("5954b24e8404c1e4695488397f07fd14").WaitForCompletion().AddComponent<SuppressBlightComponent>();
        }

        private void OnLoadActions()
        {
            Addressables.LoadAssetAsync<SkillDef>("RoR2/Base/Croco/CrocoPassiveBlight.asset").WaitForCompletion().skillDescriptionToken = "CROCO_PASSIVE_ALT_DESCRIPTION_RETURNSREWORK";
        }

        private void GlobalEventManager_ProcessHitEnemy(MonoMod.Cil.ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (c.TryGotoNext(x => x.MatchLdfld<DamageInfo>("damageType"), x => x.MatchLdcI4(1048576))
                && c.TryGotoNext(MoveType.After, x => x.MatchLdcR4(5)))
            {
                c.EmitDelegate<Func<float, float>>(x => 10f);
            }
        }

        private void ModifyFinalDamage(ModifyFinalDamage.DamageModifierArgs damageModifierArgs, DamageInfo damageInfo, HealthComponent victim, CharacterBody victimBody)
        {
            int buffCount = victimBody.GetBuffCount(RoR2Content.Buffs.Blight);
            if (buffCount > 0)
            {
                damageModifierArgs.damageMultFinal *= 1f + (damageMultPerBuff * buffCount);
                if (damageInfo.dotIndex != DotController.DotIndex.Blight) damageInfo.damageColorIndex = DamageColorIndex.Void;
            }
        }

        private void ModifyFinalDamage_Additive(ModifyFinalDamage.DamageModifierArgs damageModifierArgs, DamageInfo damageInfo, HealthComponent victim, CharacterBody victimBody)
        {
            int buffCount = victimBody.GetBuffCount(RoR2Content.Buffs.Blight);
            if (buffCount > 0)
            {
                damageModifierArgs.damageMultAdd += damageMultPerBuff * buffCount;
                if (damageInfo.dotIndex != DotController.DotIndex.Blight) damageInfo.damageColorIndex = DamageColorIndex.Void;
            }
        }


        private void ModifyBlight(On.RoR2.DotController.orig_AddDot_GameObject_float_HurtBox_DotIndex_float_ProcChainMask_Nullable1_Nullable1_Nullable1 orig, DotController self, GameObject attackerObject, float duration, HurtBox hitHurtBox, DotController.DotIndex dotIndex, float damageMultiplier, ProcChainMask procChainMask, uint? maxStacksFromAttacker, float? totalDamage, DotController.DotIndex? preUpgradeDotIndex)
        {
            //Set Blight damage to 0 if attacker already has a blight stack to prevent damage scaling
            if (dotIndex == DotController.DotIndex.Blight)
            {
                foreach (var stack in self.dotStackList)
                {
                    if (stack.attackerObject == attackerObject && stack.dotIndex == dotIndex)
                    {
                        damageMultiplier = 0f;
                        break;
                    }
                }
            }

            orig(self, attackerObject, duration, hitHurtBox, dotIndex, damageMultiplier, procChainMask, maxStacksFromAttacker, totalDamage, preUpgradeDotIndex);

            //Reset Blight duration
            if (dotIndex == DotController.DotIndex.Blight)
            {
                for (int i = 0; i < self.dotStackList.Count; i++)
                {
                    if (self.dotStackList[i].dotIndex == DotController.DotIndex.Blight)
                    {
                        self.dotStackList[i].timer = Mathf.Max(self.dotStackList[i].timer, duration);
                    }
                }
            }
        }
    }


    [RequireComponent(typeof(ProjectileDamage), typeof(ProjectileDotZone))]
    public class SuppressBlightComponent : MonoBehaviour
    {
        public int ticksPerBlight = 3;  //How many damage ticks before a new blight stack is applied

        private ProjectileDamage projectileDamage;
        private ProjectileDotZone dotZone;

        private float previousResetStopwatch;
        private int tickCount;

        private void Awake()
        {
            projectileDamage = GetComponent<ProjectileDamage>();
        }

        private void Start()
        {
            bool shouldRunCode = projectileDamage.damageType.damageType == RoR2.DamageType.BlightOnHit;

            if (!shouldRunCode)
            {
                Destroy(this);
                return;
            }

            projectileDamage.damageType.damageType = RoR2.DamageType.Generic;
            dotZone = GetComponent<ProjectileDotZone>();
            previousResetStopwatch = dotZone.resetStopwatch;
        }

        private void FixedUpdate()
        {
            if (dotZone.resetStopwatch < previousResetStopwatch)
            {
                tickCount++;
                if (tickCount >= ticksPerBlight)
                {
                    projectileDamage.damageType.damageType = RoR2.DamageType.BlightOnHit;
                    tickCount = 0;
                }
                else
                {
                    projectileDamage.damageType.damageType = RoR2.DamageType.Generic;
                }
            }
            previousResetStopwatch = dotZone.resetStopwatch;
        }
    }
}

namespace R2API.Utils
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class ManualNetworkRegistrationAttribute : Attribute
    {
    }
}