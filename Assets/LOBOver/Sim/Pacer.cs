using System;

namespace LOBOver.Sim
{
    /// <summary>The director supplies elapsed milliseconds; no timers or clocks enter the simulation.</summary>
    public sealed class Pacer
    {
        // Monitor/src/host/pacing.js, WO-03: cadence and bounded catch-up.
        public const double STEP_DAYS = 0.01, FULL_MS = 50, SLOW_MS = 200, FAST_MS = 2, STATE_MS = 250;
        public const int MAX_CATCH_UP_STEPS = 20;
        private readonly Func<Sim> getSim;
        private readonly Action afterTick;
        private Sim currentSim;
        private double? stepMs;
        private double remainderMs;
        private bool fast, closed;
        public Pacer(Func<Sim> getSim, Action afterTick = null)
        { this.getSim = getSim ?? throw new ArgumentNullException(nameof(getSim)); this.afterTick = afterTick ?? (() => { }); }
        private double? Interval(Sim sim) => closed || sim.Done || sim.Speed == "paused" ? (double?)null : fast ? FAST_MS : sim.Speed == "slow" ? SLOW_MS : FULL_MS;
        public void Sync()
        {
            var sim = getSim(); var nextMs = Interval(sim);
            if (!ReferenceEquals(sim, currentSim) || nextMs != stepMs)
            { currentSim = sim; stepMs = nextMs; remainderMs = 0; }
        }
        public void SetFast(bool value) { fast = value; Sync(); }
        public void Close() { closed = true; Sync(); }
        public int Advance(double elapsedMilliseconds)
        {
            if (!double.IsFinite(elapsedMilliseconds) || elapsedMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMilliseconds));
            Sync();
            if (stepMs == null) return 0;
            var sim = currentSim; var interval = stepMs.Value;
            remainderMs += elapsedMilliseconds;
            var due = Math.Floor(remainderMs / interval);
            // Preserve only the fractional step after a stall, as pacing.js does.
            remainderMs -= due * interval;
            var ticks = 0;
            for (var i = 0; i < Math.Min(due, MAX_CATCH_UP_STEPS); i++)
            {
                sim.Tick(STEP_DAYS); ticks++; afterTick();
                if (!ReferenceEquals(getSim(), sim) || Interval(sim) != interval) break;
            }
            Sync();
            return ticks;
        }
    }
}
