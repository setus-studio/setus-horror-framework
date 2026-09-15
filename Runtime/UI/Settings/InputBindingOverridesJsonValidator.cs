using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Setus.HorrorFramework.UI.Settings
{
    internal static class InputBindingOverridesJsonValidator
    {
        public static bool TryValidate(string json, out string diagnostic)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                diagnostic = string.Empty;
                return true;
            }

            JToken root;
            try
            {
                root = JToken.Parse(json);
            }
            catch (JsonReaderException exception)
            {
                diagnostic = $"Input binding override JSON is malformed: {exception.Message}";
                return false;
            }

            if (root.Type != JTokenType.Object)
            {
                diagnostic = "Input binding overrides must be a JSON object containing a bindings array.";
                return false;
            }

            var bindings = root["bindings"];
            if (bindings == null || bindings.Type != JTokenType.Array)
            {
                diagnostic = "Input binding override JSON must contain a bindings array.";
                return false;
            }

            var index = 0;
            foreach (var binding in bindings.Children())
            {
                if (binding.Type != JTokenType.Object)
                {
                    diagnostic = $"Input binding override at index {index} must be a JSON object.";
                    return false;
                }

                var idToken = binding["id"];
                if (idToken == null ||
                    idToken.Type != JTokenType.String ||
                    string.IsNullOrWhiteSpace(idToken.Value<string>()))
                {
                    diagnostic = $"Input binding override at index {index} requires a binding id.";
                    return false;
                }

                var id = idToken.Value<string>();
                if (!Guid.TryParse(id, out _))
                {
                    diagnostic = $"Input binding override at index {index} has invalid binding id '{id}'.";
                    return false;
                }

                if (!HasValidOptionalString(binding, "action") ||
                    !HasValidOptionalString(binding, "path") ||
                    !HasValidOptionalString(binding, "interactions") ||
                    !HasValidOptionalString(binding, "processors"))
                {
                    diagnostic = $"Input binding override at index {index} contains a non-string override field.";
                    return false;
                }

                index++;
            }

            diagnostic = string.Empty;
            return true;
        }

        private static bool HasValidOptionalString(JToken binding, string propertyName)
        {
            var value = binding[propertyName];
            return value == null || value.Type == JTokenType.String || value.Type == JTokenType.Null;
        }
    }
}
