using System;
using System.Linq;

namespace TricksterMap.Effects
{
    public sealed class EffectParticle
    {
        public EffectCream Spec { get; }
        public EffectVector Position, Velocity, Color, Gravity, GravityCenter, Anchor;
        private EffectVector colorRate;
        public double[] Size, EndSize, SizeRate;
        public double Life, Remaining, Age, Alpha, Spin, AngularAcceleration, Angle, Resistance, GravityPower, AnimationSpeed;
        public bool Linked;
        public int AnimationType, AnimationStartFrame, FrameCount;
        public EffectAlpha AlphaState;
        public string Sort;
        public EffectAnimation Animation;
        internal EffectPattern Pattern;
        public EffectParticle(EffectCream spec, EffectRandom random, EffectVector anchor, double animationDuration = 0, int frameCount = 1)
        {
            Spec = spec; Anchor = anchor; Linked = spec.Number("IsLink") != 0;
            double x = random.R(), y = random.R(), z = random.R(); var values = new EffectVector(z, y, x);
            Position = EffectMath.Volume(spec.Vector("EmitterPosition"), spec.Vector("EmitterRadius"), values, spec.Number("EmitterDispersion"), spec.Number("EmitterBalance"), random);
            x = random.R(); y = random.R(); z = random.R(); var fresh = new EffectVector(z, y, x);
            double randomness = spec.Number("DestinationRandomness");
            var mixed = values * (1 - randomness) + fresh * randomness;
            var destination = EffectMath.Volume(spec.Vector("Destination"), spec.Vector("DestinationRadius"), mixed, spec.Number("DestinationDispersion"), spec.Number("DestinationBalance"), random);
            double speed = EffectMath.Plp(spec.Number("Velocity"), spec.Number("VelocityPermittedLimitPercent"), EffectMath.Shaped(spec, random, "Velocity"));
            Velocity = (destination - Position).Normalized * speed;
            if (!Linked) Position += anchor;
            Color = EffectMath.Color(spec, random, "Start"); var endColor = EffectMath.Color(spec, random, "End");
            Size = EffectMath.Sizes(spec, random, "Start"); EndSize = EffectMath.Sizes(spec, random, "End");
            double startAlpha = EffectMath.Alpha(spec, random, "Start"), endAlpha = EffectMath.Alpha(spec, random, "End");
            double factor = EffectMath.F(1 - (EffectMath.Shaped(spec, random, "Gravity") + 1) * .5 * spec.Number("GravityResistance"));
            Gravity = spec.Vector("Gravity") * factor; GravityCenter = spec.Vector("GravityBallPosition");
            if (!Linked) GravityCenter += anchor;
            GravityPower = EffectMath.F((1 - (EffectMath.Shaped(spec, random, "GravityBall") + 1) * .5 * spec.Number("GravityBallResistance")) * spec.Number("GravityBallPower") * 100);
            Resistance = EffectMath.Plp(spec.Number("AirResistance"), spec.Number("AirResistancePermittedLimitPercent"), EffectMath.Shaped(spec, random, "AirResistance"));
            Life = EffectMath.Plp(spec.Number("Time", 1), spec.Number("TimePermittedLimitPercent"), EffectMath.Shaped(spec, random, "Time"));
            if (Life <= 0 || double.IsNaN(Life) || double.IsInfinity(Life)) throw new ArgumentException("CREAM sampled life must be finite and positive.");
            Remaining = Life; FrameCount = frameCount; AnimationType = (int)spec.Number("AnimationType"); AnimationSpeed = 1;
            if (AnimationType == 0 || AnimationType == 1)
                AnimationSpeed = EffectMath.F(EffectMath.Plp(spec.Number("AnimationSpeed", 100), spec.Number("AnimationSpeedPermittedLimitPercent"), EffectMath.Shaped(spec, random, "AnimationSpeed")) * EffectMath.Percent);
            else if (AnimationType == 2) AnimationSpeed = EffectMath.F(animationDuration / Life);
            else if (AnimationType == 3) { AnimationStartFrame = random.Next() % Math.Max(1, frameCount); AnimationSpeed = 0; }
            AlphaState = new EffectAlpha(spec, startAlpha, endAlpha, Life); Alpha = AlphaState.Current;
            SizeRate = new[] { EffectMath.F((EndSize[0] - Size[0]) / Life), EffectMath.F((EndSize[1] - Size[1]) / Life) };
            colorRate = (endColor - Color) / Life;
            factor = (EffectMath.Shaped(spec, random, "AngleVelocity") + 1) * .5;
            Spin = EffectMath.F(spec.Number("MainAngleVelocity") + (spec.Number("SubAngleVelocity") - spec.Number("MainAngleVelocity")) * factor);
            factor = (EffectMath.Shaped(spec, random, "AngleAccelation") + 1) * .5;
            AngularAcceleration = EffectMath.F(spec.Number("MainAngleAccelation") + (spec.Number("SubAngleAccelation") - spec.Number("MainAngleAccelation")) * factor);
            double r = random.R();
            Angle = EffectMath.F(spec.Number("StartAngle") + Math.Abs(EffectMath.Shape(r, spec.Number("StartAngleDispersion"), spec.Number("StartAngleBalance"))) * Math.Abs(spec.Number("EndAngle") - spec.Number("StartAngle")) * r);
        }
        public bool Step(double dt)
        {
            dt = EffectMath.F(dt); Remaining = EffectMath.F(Remaining - dt); Age = EffectMath.F(Age + dt);
            if (Remaining < 0) return false;
            Position += Velocity * dt; Color += colorRate * dt; Alpha = AlphaState.Step(dt, Remaining);
            for (int i = 0; i < 2; i++) Size[i] = EffectMath.F(Size[i] + SizeRate[i] * dt);
            Velocity += Gravity * dt;
            if (GravityPower != 0)
            {
                var delta = Position - GravityCenter; double distance = EffectMath.F(delta.Length);
                if (distance > 5) Velocity -= delta.Normalized * (GravityPower * dt / distance);
            }
            var direction = Velocity.Normalized; var velocity = Velocity - direction * (Resistance * dt);
            Velocity = new EffectVector(velocity.X * direction.X < 0 ? 0 : velocity.X, velocity.Y * direction.Y < 0 ? 0 : velocity.Y, velocity.Z * direction.Z < 0 ? 0 : velocity.Z);
            Spin = EffectMath.F(Spin + AngularAcceleration * dt); Angle = EffectMath.F(Angle + Spin * dt);
            return true;
        }
    }
    public sealed class EffectAnimation
    {
        private readonly double[] delays;
        private double remaining;
        private readonly bool loop;
        public EffectAnimation(double[] delays, double speed, int kind, int frame = -1)
        { this.delays = delays.Length == 0 ? new[] { 0.0 } : delays; Speed = speed; loop = kind == 1; Frame = Math.Min(Math.Max(-1, frame), this.delays.Length - 1); remaining = Frame < 0 ? 0 : this.delays[Frame]; }
        public double Speed { get; set; }
        public int Frame { get; private set; }
        public bool Limited { get; private set; }
        private void Next() { Frame++; if (Frame >= delays.Length) Frame = loop ? 0 : delays.Length - 1; remaining = delays[Frame]; }
        public void Reset(int frame) { Frame = Math.Max(0, Math.Min(delays.Length - 1, frame)); remaining = delays[Frame]; }
        public void Step(double dt)
        {
            remaining -= dt * Speed; if (remaining > 0) return;
            double old = remaining; Next(); remaining -= -old * Speed;
            for (int i = 0; remaining <= 0 && Frame != delays.Length - 1; i++)
            {
                if (remaining < -delays.Sum()) { remaining = 0; return; }
                if (i >= 4096) { Limited = true; return; }
                old = remaining; Next(); remaining -= -old * Speed;
            }
        }
    }
}
