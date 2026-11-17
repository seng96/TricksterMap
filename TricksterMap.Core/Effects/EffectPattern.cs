using System;
using System.Linq;

namespace TricksterMap.Effects
{
    internal sealed class EffectPattern
    {
        private string name;
        private double[] args, end;
        private double elapsed, bounces, threshold, speed, acceleration, endThreshold, savedSpeed;
        private int phase, impactFrame, endFrame;
        private bool impacted;
        internal static EffectPattern Create(EffectParticle p, EffectRandom random)
        {
            string name = p.Spec.Text("Pattern").Trim();
            var result = new EffectPattern { name = name, args = (p.Spec.Text("PatternArguments") + " 0 0 0 0").Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Take(4).Select(s => EffectCream.Parse(s)).ToArray() };
            var a = result.args;
            if (name == "" || name == "None" || name == "Test") return result;
            if (name == "Bounding") { result.bounces = EffectMath.F(a[3]); return result; }
            if (name == "SizeResistance")
            {
                double first = EffectMath.F(p.Life * a[0] * EffectMath.Percent), tail = EffectMath.F(p.Life - first);
                if (first == 0 || tail == 0 || a[1] == 0) return null;
                double delta = EffectMath.F(p.EndSize[0] - p.Size[0]), distance = EffectMath.F(a[1] * EffectMath.Percent * delta);
                double speed = EffectMath.F(distance / first), final = EffectMath.F((delta - distance) * 2 / tail - speed), duration = tail;
                if (final < 0) { final = 0; duration = EffectMath.F((100 - a[1]) * (first / a[1]) * 2); }
                if (duration == 0) return null;
                result.end = (double[])p.EndSize.Clone(); result.threshold = tail; result.speed = speed;
                result.acceleration = EffectMath.F((final - speed) / duration); result.endThreshold = EffectMath.F(tail - duration);
                p.SizeRate = new[] { 0.0, 0.0 }; return result;
            }
            if (name == "Mud")
            {
                var bounds = new[] { (int)a[1], (int)a[2], (int)a[3], p.FrameCount }; int segments = 1;
                while (segments < 3) { if (bounds[segments] <= bounds[segments - 1]) { bounds[segments] = p.FrameCount; break; } segments++; }
                int selected = random.Next() % segments, start = bounds[selected], finish = bounds[selected + 1] - 1;
                result.savedSpeed = p.AnimationSpeed; result.impactFrame = Math.Min(start + 1, finish); result.endFrame = finish;
                p.AnimationStartFrame = start; p.AnimationSpeed = 0; return result;
            }
            return null;
        }
        private void Constant(EffectParticle p, double dt) { double amount = EffectMath.F(speed * dt); for (int i = 0; i < 2; i++) p.Size[i] = EffectMath.F(p.Size[i] + amount); }
        internal void Update(EffectParticle p, double dt)
        {
            var a = args;
            if (name == "Test") { elapsed = EffectMath.F(elapsed + dt); p.Position.X = EffectMath.F(p.Position.X + EffectMath.F(Math.Sin(elapsed)) * a[0]); }
            else if (name == "Bounding" && p.Position.Y <= a[0] && p.Velocity.Y < 0)
            {
                p.Velocity.Y = EffectMath.F(-p.Velocity.Y * a[1] * EffectMath.Percent); p.Spin = EffectMath.F(a[2] * p.Spin * EffectMath.Percent); bounces = EffectMath.F(bounces - 1);
                if (bounces < 0) { p.Velocity = new EffectVector(); p.Spin = bounces = 0; }
            }
            else if (name == "SizeResistance")
            {
                if (phase == 2) p.Size = (double[])end.Clone();
                else if (threshold < p.Remaining) Constant(p, dt);
                else if (endThreshold < p.Remaining)
                {
                    var old = (double[])p.Size.Clone();
                    if (phase == 0) { double crossing = EffectMath.F(threshold - p.Remaining); Constant(p, crossing); dt = EffectMath.F(dt - crossing); phase = 1; }
                    double previous = speed; speed = EffectMath.F(speed + acceleration * dt); double amount = EffectMath.F((previous + speed) * dt * .5);
                    for (int i = 0; i < 2; i++) { p.Size[i] = EffectMath.F(p.Size[i] + amount); if ((old[i] - end[i]) * (p.Size[i] - end[i]) < 0) { p.Size[i] = end[i]; phase = 2; } }
                }
                else { phase = 2; p.Size = (double[])end.Clone(); }
            }
            else if (name == "Mud")
            {
                if (!impacted && p.Position.Y <= a[0] && p.Velocity.Y < 0)
                { p.Animation.Reset(impactFrame); p.AnimationSpeed = savedSpeed; p.Velocity = new EffectVector(); p.Spin = p.Angle = 0; impacted = true; }
                else if (impacted) { p.Velocity = new EffectVector(); p.Spin = 0; if (p.Animation.Frame == endFrame) p.AnimationSpeed = 0; }
            }
        }
    }
}
