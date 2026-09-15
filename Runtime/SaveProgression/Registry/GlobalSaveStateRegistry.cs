using System;
using System.Collections.Generic;
using System.Linq;

namespace Setus.HorrorFramework.SaveProgression.Registry
{
    public sealed class GlobalSaveStateRegistry
    {
        private readonly Dictionary<string, GlobalSaveStateDeclaration> declarationsByKey =
            new Dictionary<string, GlobalSaveStateDeclaration>(StringComparer.Ordinal);

        public IReadOnlyList<GlobalSaveStateDeclaration> Declarations =>
            declarationsByKey.Values
                .OrderBy(declaration => declaration.StateKey, StringComparer.Ordinal)
                .ToArray();

        public int Revision { get; private set; }

        public void Register(GlobalSaveStateDeclaration declaration)
        {
            if (declaration == null)
            {
                throw new ArgumentNullException(nameof(declaration));
            }

            if (declarationsByKey.ContainsKey(declaration.StateKey))
            {
                throw new InvalidOperationException(
                    $"Global save state key is already declared: {declaration.StateKey}");
            }

            declarationsByKey.Add(declaration.StateKey, declaration);
            Revision++;
        }

        public bool TryGet(string stateKey, out GlobalSaveStateDeclaration declaration)
        {
            if (string.IsNullOrWhiteSpace(stateKey))
            {
                declaration = null;
                return false;
            }

            return declarationsByKey.TryGetValue(stateKey, out declaration);
        }
    }
}
