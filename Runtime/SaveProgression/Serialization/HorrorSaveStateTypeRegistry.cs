using System;
using System.Globalization;
using Setus.HorrorFramework.Interaction.Interactables;
using Setus.HorrorFramework.Interaction.Triggers;
using Setus.HorrorFramework.Atmosphere.Scares;
using Setus.HorrorFramework.Atmosphere.Tension;
using Setus.HorrorFramework.AI.Memory;
using Setus.HorrorFramework.Inventory.Runtime;
using Setus.HorrorFramework.Narrative.Objectives;
using Setus.HorrorFramework.Narrative.Scenario;
using Setus.HorrorFramework.Player.Controller;
using Setus.HorrorFramework.UI.Settings;

namespace Setus.HorrorFramework.SaveProgression.Serialization
{
    public static class HorrorSaveStateTypeRegistry
    {
        public static SaveStateTypeRegistry CreateDefault()
        {
            var registry = new SaveStateTypeRegistry();

            registry.Register(
                new SaveStateCodec<string>(SaveStateTypeKeys.StringV1, value => value, value => value),
                "System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
            registry.Register(
                new SaveStateCodec<bool>(SaveStateTypeKeys.BooleanV1, Format, bool.Parse),
                "System.Boolean, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
            registry.Register(
                new SaveStateCodec<int>(SaveStateTypeKeys.Int32V1, Format, ParseInt32),
                "System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
            registry.Register(
                new SaveStateCodec<long>(SaveStateTypeKeys.Int64V1, Format, ParseInt64),
                "System.Int64, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
            registry.Register(
                new SaveStateCodec<float>(SaveStateTypeKeys.SingleV1, Format, ParseSingle),
                "System.Single, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
            registry.Register(
                new SaveStateCodec<double>(SaveStateTypeKeys.DoubleV1, Format, ParseDouble),
                "System.Double, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");

            registry.Register(
                SaveStateCodec<PlayerPose>.CreateJson(SaveStateTypeKeys.PlayerPoseV1),
                "Setus.HorrorFramework.Player.Controller.PlayerPose, Setus.HorrorFramework.Runtime, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
            registry.Register(
                SaveStateCodec<InventoryState>.CreateJson(SaveStateTypeKeys.InventoryRuntimeV1),
                "Setus.HorrorFramework.Inventory.Runtime.InventoryState, Setus.HorrorFramework.Runtime, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
            registry.Register(
                SaveStateCodec<RuntimeSettingsStateV1>.CreateJson(SaveStateTypeKeys.RuntimeSettingsV1));
            registry.Register(
                SaveStateCodec<RuntimeSettingsState>.CreateJson(SaveStateTypeKeys.RuntimeSettingsV2),
                "Setus.HorrorFramework.UI.Settings.RuntimeSettingsState, Setus.HorrorFramework.Runtime, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
            registry.Register(
                SaveStateCodec<InteractionObjectState>.CreateJson(SaveStateTypeKeys.InteractionObjectV1),
                "Setus.HorrorFramework.Interaction.Interactables.InteractionObjectState, Setus.HorrorFramework.Runtime, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
            registry.Register(
                SaveStateCodec<InteractionTriggerState>.CreateJson(SaveStateTypeKeys.InteractionTriggerV1),
                "Setus.HorrorFramework.Interaction.Triggers.InteractionTriggerState, Setus.HorrorFramework.Runtime, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
            registry.Register(
                SaveStateCodec<ObjectiveRuntimeState>.CreateJson(SaveStateTypeKeys.ObjectiveRuntimeV1));
            registry.Register(
                SaveStateCodec<NarrativeRuntimeState>.CreateJson(SaveStateTypeKeys.NarrativeRuntimeV1));
            registry.Register(
                SaveStateCodec<ScareRuntimeState>.CreateJson(SaveStateTypeKeys.AtmosphereScareRuntimeV1));
            registry.Register(
                SaveStateCodec<TensionRuntimeState>.CreateJson(SaveStateTypeKeys.AtmosphereTensionRuntimeV1));
            registry.Register(
                SaveStateCodec<StalkerAiRuntimeState>.CreateJson(SaveStateTypeKeys.StalkerAiRuntimeV1));

            return registry;
        }

        private static string Format<T>(T value) where T : IConvertible
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static int ParseInt32(string value)
        {
            return int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        private static long ParseInt64(string value)
        {
            return long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        private static float ParseSingle(string value)
        {
            return float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static double ParseDouble(string value)
        {
            return double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}
