using System;
using System.Collections.Generic;

namespace LOBOver.Sim
{
    public sealed class Rng
    {
        // Monitor/src/common/rng.js: Mulberry32's unsigned word and divisor.
        private const uint Increment = 0x6d2b79f5;
        private const double WordRange = 0x100000000;
        private const double MaxSafeInteger = 9007199254740991;
        public uint State { get; private set; }
        public Rng(double seed)
        {
            if (!double.IsFinite(seed) || seed != Math.Floor(seed) || seed < 0 || seed > uint.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(seed), "RNG seed must be an unsigned 32-bit integer");
            State = (uint)seed;
        }
        public double Next()
        {
            unchecked
            {
                State += Increment;
                var value = State;
                value = (value ^ (value >> 15)) * (value | 1);
                value ^= value + (value ^ (value >> 7)) * (value | 61);
                return (value ^ (value >> 14)) / WordRange;
            }
        }
        public double Int(double lo, double hi)
        {
            bool Safe(double n) => double.IsFinite(n) && n == Math.Floor(n) && Math.Abs(n) <= MaxSafeInteger;
            if (!Safe(lo) || !Safe(hi) || lo > hi || !Safe(hi - lo + 1))
                throw new ArgumentOutOfRangeException(nameof(lo), "RNG integer bounds must be ordered safe integers");
            return lo + Math.Floor(Next() * (hi - lo + 1));
        }
        public T Pick<T>(IReadOnlyList<T> array)
        {
            if (array == null || array.Count == 0) throw new ArgumentException("RNG pick requires a non-empty array");
            return array[(int)Int(0, array.Count - 1)];
        }
    }
}
