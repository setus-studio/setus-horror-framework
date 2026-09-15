using System;

namespace Setus.HorrorFramework.SaveProgression.Registry
{
    public enum GlobalSaveStateRequirement
    {
        Required = 0,
        Optional = 1
    }

    public enum GlobalSaveStateOwnerScope
    {
        Context = 0,
        Scene = 1
    }

    public enum GlobalSaveStateIntroductionPolicy
    {
        MigrationProvidesRecord = 0,
        NoBackfillRequired = 1
    }

    public sealed class GlobalSaveStateDeclaration
    {
        public GlobalSaveStateDeclaration(
            string stateKey,
            string stateTypeKey,
            GlobalSaveStateRequirement requirement,
            int introducedInSchemaVersion,
            GlobalSaveStateOwnerScope ownerScope,
            GlobalSaveStateIntroductionPolicy introductionPolicy)
        {
            if (string.IsNullOrWhiteSpace(stateKey))
            {
                throw new ArgumentException("Global state key must not be empty.", nameof(stateKey));
            }

            if (string.IsNullOrWhiteSpace(stateTypeKey))
            {
                throw new ArgumentException("Global state type key must not be empty.", nameof(stateTypeKey));
            }

            if (introducedInSchemaVersion < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(introducedInSchemaVersion),
                    "Global state introduction version must not be negative.");
            }

            if (requirement == GlobalSaveStateRequirement.Required &&
                introductionPolicy != GlobalSaveStateIntroductionPolicy.MigrationProvidesRecord)
            {
                throw new ArgumentException(
                    "Required global state must be introduced through an explicit migration-provided record.",
                    nameof(introductionPolicy));
            }

            StateKey = stateKey;
            StateTypeKey = stateTypeKey;
            Requirement = requirement;
            IntroducedInSchemaVersion = introducedInSchemaVersion;
            OwnerScope = ownerScope;
            IntroductionPolicy = introductionPolicy;
        }

        public string StateKey { get; }
        public string StateTypeKey { get; }
        public GlobalSaveStateRequirement Requirement { get; }
        public int IntroducedInSchemaVersion { get; }
        public GlobalSaveStateOwnerScope OwnerScope { get; }
        public GlobalSaveStateIntroductionPolicy IntroductionPolicy { get; }

        public bool AppliesToSchema(int schemaVersion)
        {
            return schemaVersion >= IntroducedInSchemaVersion;
        }

        public bool IsRequiredForSchema(int schemaVersion)
        {
            return Requirement == GlobalSaveStateRequirement.Required && AppliesToSchema(schemaVersion);
        }
    }
}
