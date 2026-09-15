using System;

namespace Setus.HorrorFramework.SaveProgression.SaveSlots
{
    public readonly struct SaveSlotId : IEquatable<SaveSlotId>
    {
        public SaveSlotId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Save slot ID must not be empty.", nameof(value));
            }

            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (!char.IsLetterOrDigit(character) && character != '-' && character != '_')
                {
                    throw new ArgumentException(
                        "Save slot IDs may contain only letters, digits, '-' and '_'.",
                        nameof(value));
                }
            }

            Value = value;
        }

        public string Value { get; }

        public bool Equals(SaveSlotId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is SaveSlotId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value != null ? Value.GetHashCode() : 0;
        }

        public override string ToString()
        {
            return Value;
        }
    }
}
