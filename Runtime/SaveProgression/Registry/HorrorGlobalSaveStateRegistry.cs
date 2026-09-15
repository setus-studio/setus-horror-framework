using Setus.HorrorFramework.SaveProgression.Serialization;

namespace Setus.HorrorFramework.SaveProgression.Registry
{
    public static class HorrorGlobalSaveStateRegistry
    {
        public const string InventoryStateKey = "inventory.state";
        public const string PlayerPoseStateKey = "player.pose";
        public const string RuntimeSettingsStateKey = "ui.runtime-settings";
        public const string ObjectiveStateKey = "objective.state";
        public const string NarrativeStateKey = "narrative.state";
        public const string AtmosphereScareStateKey = "atmosphere.scare-state";
        public const string AtmosphereTensionStateKey = "atmosphere.tension-state";

        public static GlobalSaveStateRegistry CreateDefault()
        {
            var registry = new GlobalSaveStateRegistry();
            registry.Register(new GlobalSaveStateDeclaration(
                InventoryStateKey,
                SaveStateTypeKeys.InventoryRuntimeV1,
                GlobalSaveStateRequirement.Required,
                introducedInSchemaVersion: 1,
                GlobalSaveStateOwnerScope.Context,
                GlobalSaveStateIntroductionPolicy.MigrationProvidesRecord));
            registry.Register(new GlobalSaveStateDeclaration(
                PlayerPoseStateKey,
                SaveStateTypeKeys.PlayerPoseV1,
                GlobalSaveStateRequirement.Required,
                introducedInSchemaVersion: 1,
                GlobalSaveStateOwnerScope.Scene,
                GlobalSaveStateIntroductionPolicy.MigrationProvidesRecord));
            registry.Register(new GlobalSaveStateDeclaration(
                RuntimeSettingsStateKey,
                SaveStateTypeKeys.RuntimeSettingsV2,
                GlobalSaveStateRequirement.Optional,
                introducedInSchemaVersion: 1,
                GlobalSaveStateOwnerScope.Context,
                GlobalSaveStateIntroductionPolicy.NoBackfillRequired));
            registry.Register(new GlobalSaveStateDeclaration(
                ObjectiveStateKey,
                SaveStateTypeKeys.ObjectiveRuntimeV1,
                GlobalSaveStateRequirement.Required,
                introducedInSchemaVersion: 2,
                GlobalSaveStateOwnerScope.Context,
                GlobalSaveStateIntroductionPolicy.MigrationProvidesRecord));
            registry.Register(new GlobalSaveStateDeclaration(
                NarrativeStateKey,
                SaveStateTypeKeys.NarrativeRuntimeV1,
                GlobalSaveStateRequirement.Required,
                introducedInSchemaVersion: 2,
                GlobalSaveStateOwnerScope.Context,
                GlobalSaveStateIntroductionPolicy.MigrationProvidesRecord));
            registry.Register(new GlobalSaveStateDeclaration(
                AtmosphereScareStateKey,
                SaveStateTypeKeys.AtmosphereScareRuntimeV1,
                GlobalSaveStateRequirement.Required,
                introducedInSchemaVersion: 3,
                GlobalSaveStateOwnerScope.Context,
                GlobalSaveStateIntroductionPolicy.MigrationProvidesRecord));
            registry.Register(new GlobalSaveStateDeclaration(
                AtmosphereTensionStateKey,
                SaveStateTypeKeys.AtmosphereTensionRuntimeV1,
                GlobalSaveStateRequirement.Required,
                introducedInSchemaVersion: 3,
                GlobalSaveStateOwnerScope.Context,
                GlobalSaveStateIntroductionPolicy.MigrationProvidesRecord));
            return registry;
        }
    }
}
