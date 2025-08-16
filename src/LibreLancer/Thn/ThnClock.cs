using System;

namespace LibreLancer.Thn
{
    public sealed class ThnClock
    {
        // clamp: duże skoki czasu (lagi) nie rozwalą timelinu
        const double MAX_FRAME_DELTA = 0.05;     // 50 ms
        // stały krok do deterministycznego odtwarzania
        const double FIXED_STEP      = 1.0 / 60.0;

        public bool   UseFixedStep   { get; set; } = true;
        public bool   Paused         { get; private set; }
        public double PlaybackSpeed  { get; set; } = 1.0;
        public double Time           { get; private set; }

        double accum;

        public void Reset(double t = 0) { Time = t; accum = 0; Paused = false; }
        public void Pause(bool p)       => Paused = p;
        public void Step(double s = FIXED_STEP)
        {
            if (Paused) Time += Math.Max(0, s);
        }

        public void Tick(double realDeltaSeconds, Action<double, double> onStep /* (thnTime, step) */)
        {
            if (Paused || PlaybackSpeed <= 0) return;

            var dt = Math.Min(Math.Max(realDeltaSeconds, 0), MAX_FRAME_DELTA) * PlaybackSpeed;

            if (!UseFixedStep)
            {
                Time += dt;
                onStep?.Invoke(Time, dt);
                return;
            }

            accum += dt;
            while (accum >= FIXED_STEP)
            {
                Time  += FIXED_STEP;
                accum -= FIXED_STEP;
                onStep?.Invoke(Time, FIXED_STEP);
            }
        }
    }
}
