using System;

namespace AmqTool
{
    /// <summary>
    /// The yellow bulb button on get clips jobs: picks the volume slider value for "AMQ volume".
    /// AMQ songs sit around -18 dB mean (most between -20 and -16), and the max shouldn't get to 0 dB,
    /// so it aims for -18 mean but gives up some loudness to keep the max at -1 dB or lower
    /// (e.g. if -18 would put the max at 0 dB, it goes for -19 with the max at -1 instead).
    /// </summary>
    public static class AutoVolume
    {
        public const double TargetMean = -18;
        public const double MaxPeak = -1;

        /// <returns>dB for the volume slider (0.5 steps, within the slider's -20..20)</returns>
        public static double SuggestGain(double max, double mean)
        {
            double want = TargetMean - mean;   // what hits -18 mean
            double room = MaxPeak - max;       // the most we can add before the max goes over -1
            double gain = want <= room
                ? Math.Round(want * 2, MidpointRounding.AwayFromZero) / 2
                : Math.Floor(room * 2) / 2;
            if (gain > room) gain = Math.Floor(room * 2) / 2; // rounding to the nearest 0.5 can overshoot the room a bit
            if (want > 0 && gain < 0) gain = 0; // too quiet but already peaking near 0 dB: don't make it even quieter
            return Math.Max(-20, Math.Min(20, gain));
        }

        /// <summary>True when the max (not the mean) decided the value, so the mean ends up below -18</summary>
        public static bool IsPeakLimited(double max, double mean) => SuggestGain(max, mean) < Math.Round((TargetMean - mean) * 2, MidpointRounding.AwayFromZero) / 2;
    }
}
