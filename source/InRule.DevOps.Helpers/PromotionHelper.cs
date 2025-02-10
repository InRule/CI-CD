using InRule.Repository;
using InRule.Repository.Client;
using System;
using System.Threading.Tasks;

namespace InRule.DevOps.Helpers
{
    public static class PromotionHelper
    {
        public static async Task Promote(RuleApplicationDef ruleAppDef, string moniker)
        {
            string TargetCatalogUri = SettingsManager.Get($"{moniker}.TargetCatalogUri");
            string TargetCatalogUsername = SettingsManager.Get($"{moniker}.TargetCatalogUsername");
            string TargetCatalogPassword = SettingsManager.Get($"{moniker}.TargetCatalogPassword");
            string PromotionComment = SettingsManager.Get($"{moniker}.PromotionComment");
            string ApplyLabel = SettingsManager.Get($"{moniker}.ApplyLabel");

            string Prefix = "PROMOTE";

            RuleCatalogConnection destCatCon = null;
            RuleApplicationDef newRuleAppDef = null;
            try
            {
                //TODO: Do we want to add /core to the catalog URI if necessary?
                destCatCon = new RuleCatalogConnection(new Uri(TargetCatalogUri), TimeSpan.FromSeconds(60), TargetCatalogUsername, TargetCatalogPassword, RuleCatalogAuthenticationType.BuiltIn);
                newRuleAppDef = destCatCon.PromoteRuleApplication(ruleAppDef, PromotionComment ?? "Promoted using DevOps");
            }
            catch (Exception ex)
            {
                await NotificationHelper.NotifyAsync($"Error performing Promotion to {TargetCatalogUri}: {ex.Message}", Prefix, "Debug");
            }

            if (!string.IsNullOrEmpty(ApplyLabel))
            {
                try
                {
                    destCatCon.ApplyLabel(newRuleAppDef, ApplyLabel);
                }
                catch (Exception ex)
                {
                    await NotificationHelper.NotifyAsync($"Error applying Label after Promotion: {ex.Message}", Prefix, "Debug");
                }
            }
        }
    }
}
