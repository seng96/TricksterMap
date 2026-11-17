using System;

namespace TricksterMap.Effects
{
    /// <summary>Source-backed CREAM math; float stores approximate native x87 precision.</summary>
    public sealed class EffectRandom
    {
        public EffectRandom(uint seed = 1) { State = seed; }
        public uint State { get; private set; }
        public int Next() { State = unchecked(State * 214013 + 2531011); return (int)(State >> 16 & 32767); }
        public double R() => (float)((Next() % 201 - 100) * EffectMath.Percent);
    }
    public static class EffectMath
    {
        public const double Percent = 0.009999999776482582;
        public static double F(double value) => (float)value;
        public static double Shape(double r, double dispersion, double balance) => r + dispersion * dispersion * dispersion * (balance - r);
        public static double Shaped(EffectCream spec, EffectRandom random, string prefix) => Shape(random.R(), spec.Number(prefix + "Dispersion"), spec.Number(prefix + "Balance"));
        public static uint Unsigned(double value) => unchecked((uint)(long)value);
        public static double Plp(double value, double limit, double shape) => F(value + Unsigned(limit) * value * shape * Percent);
        public static double Interval(EffectCream spec, EffectRandom random)
        {
            double rate = spec.Number("EmissionRate"); if (rate <= 0) return double.PositiveInfinity;
            double interval = F(1 / rate); uint limit = Unsigned(spec.Number("EmissionRatePermittedLimitPercent"));
            return limit == 0 ? interval : F(interval + interval * limit * Shaped(spec, random, "Emission") * Percent);
        }
        public static double[] Sizes(EffectCream spec, EffectRandom random, string prefix)
        {
            var values = spec.OrderedXY(prefix + "Size", 100); var limits = spec.OrderedXY(prefix + "SizePermittedLimitPercent", 0);
            double shaped = Shaped(spec, random, prefix + "Size"), x = F(values[0] + values[0] * limits[0] * shaped * Percent);
            double y = spec.Number("Is" + prefix + "SizePLPUseOne", 1) == 1 ? F(values[1] + values[1] * limits[0] * shaped * Percent) :
                F(values[1] + values[1] * limits[1] * Shaped(spec, random, prefix + "Size") * Percent);
            return new[] { x, y };
        }
        public static EffectVector Color(EffectCream spec, EffectRandom random, string endpoint)
        {
            var main = spec.Vector("Main" + endpoint + "Color", new EffectVector(255, 255, 255));
            var sub = spec.Vector("Sub" + endpoint + "Color", main);
            double factor = (Shaped(spec, random, endpoint + "Color") + 1) * .5;
            return main + (sub - main) * factor;
        }
        public static double Alpha(EffectCream spec, EffectRandom random, string endpoint)
        {
            uint main = Unsigned(spec.Number("Main" + endpoint + "Alpha", endpoint == "Start" ? 255 : 0));
            uint sub = Unsigned(spec.Number("Sub" + endpoint + "Alpha", main));
            double delta = endpoint == "Start" ? (double)unchecked(main - sub) : unchecked((int)(main - sub));
            double result = F(main - delta * (Shaped(spec, random, endpoint + "Alpha") + 1) * .5);
            return endpoint == "Start" ? result : Math.Max(0, Math.Min(255, result));
        }
        private static double Warp(double value, double dispersion, double balance)
        { double magnitude = F((Shape(2 * Math.Abs(value) - 1, dispersion, balance) + 1) * .5); return value < 0 ? -magnitude : magnitude; }
        public static EffectVector Volume(EffectVector position, EffectVector radius, EffectVector values, double dispersion, double balance, EffectRandom random)
        {
            var warped = new EffectVector(Warp(values.X, dispersion, balance), Warp(values.Y, dispersion, balance), Warp(values.Z, dispersion, balance));
            int axis = random.Next() % 3;
            return new EffectVector(position.X + radius.X * (axis == 0 ? warped.X : values.X * Math.Abs(warped.X)),
                position.Y + radius.Y * (axis == 1 ? warped.Y : values.Y * Math.Abs(warped.Y)),
                position.Z + radius.Z * (axis == 2 ? warped.Z : values.Z * Math.Abs(warped.Z)));
        }
    }
    public sealed class EffectAlpha
    {
        private double current, target, thresholdA, thresholdB, rate, acceleration;
        private int phase = 1;
        public EffectAlpha(EffectCream spec, double start, double end, double life)
        {
            current = start; target = end; thresholdA = life; double delta = end - start;
            if (spec.Number("IsSineAlpha") != 0)
            { double q = EffectMath.F(-4 * delta / (life * life)); acceleration = EffectMath.F(q + q); rate = EffectMath.F(-q * life); }
            else if (spec.Number("MiddleAlphaTime") == 0) rate = EffectMath.F(delta / life);
            else
            {
                double first = EffectMath.F(life * spec.Number("MiddleAlphaTime")), part = EffectMath.F(spec.Number("MiddleAlphaAlpha") * delta);
                rate = EffectMath.F(part / first); double remaining = EffectMath.F(life - first), finalRate = EffectMath.F((delta - part) * 2 / remaining - rate), duration = remaining;
                if (finalRate < 0) { finalRate = 0; double middle = spec.Number("MiddleAlphaAlpha"); duration = EffectMath.F((100 - middle) * (first / middle) * 2); }
                phase = 0; thresholdA = remaining; thresholdB = EffectMath.F(remaining - duration); acceleration = EffectMath.F((finalRate - rate) / duration);
            }
        }
        public double Current => current;
        public double Step(double dt, double remaining)
        {
            if (phase != 2)
            {
                if (thresholdA <= remaining) return current = EffectMath.F(current + rate * dt);
                if (thresholdB <= remaining)
                {
                    double previous = current;
                    if (phase == 0) { phase = 1; current = EffectMath.F(current + rate * dt); dt = EffectMath.F(dt - (thresholdA - remaining)); }
                    double oldRate = rate; rate = EffectMath.F(rate + acceleration * dt); current = EffectMath.F(current + (oldRate + rate) * dt * .5);
                    if ((previous - target) * (current - target) >= 0) return current;
                }
                phase = 2;
            }
            return current = target;
        }
    }
}
