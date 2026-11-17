using System;
using System.Collections.Generic;
using System.Linq;

namespace TricksterMap.Effects
{
    /// <summary>Deterministic offline clock; it never writes map or XML state.</summary>
    public sealed class EffectSimulation
    {
        private sealed class Instance
        {
            internal EffectCake Cake;
            internal EffectVector Anchor;
            internal double Clock;
            internal int Child, Play;
            internal bool Loop;
            internal readonly List<Emitter> Tracked = new List<Emitter>();
        }
        private sealed class Emitter
        {
            internal EffectCream Cream;
            internal Instance Owner;
            internal string Sort;
            internal double Budget, Interval;
            internal int Spawned;
            internal bool Stopped, Blocked;
            internal readonly List<EffectParticle> Particles = new List<EffectParticle>();
        }
        private readonly EffectDocument document;
        private readonly MapEffectInstance[] placements;
        private readonly Func<string, double[]> delays;
        private readonly List<Instance> instances = new List<Instance>();
        private readonly List<Emitter> emitters = new List<Emitter>();
        private EffectRandom random;
        public const double StepSeconds = 1.0 / 60;
        public const int Limit = 3000;
        public double Time { get; private set; }
        public HashSet<string> Warnings { get; } = new HashSet<string>();
        public IEnumerable<EffectParticle> Particles => emitters.AsEnumerable().Reverse().OrderBy(e => SortOrder(e.Sort)).SelectMany(e => e.Particles);
        public int ParticleCount => emitters.Sum(e => e.Particles.Count);
        public EffectSimulation(EffectDocument document, IEnumerable<MapEffectInstance> placements, Func<string, double[]> delays = null)
        { this.document = document; this.placements = placements.Take(Limit).ToArray(); this.delays = delays ?? (name => new[] { .1 }); Reset(); }
        public static int SortOrder(string sort) => sort == "Ground_Over" ? 0 : sort == "Object_Over" ? 1 : sort == "UI_Over" ? 3 : 2;
        public void Reset()
        {
            random = new EffectRandom(); Time = 0; instances.Clear(); emitters.Clear();
            foreach (var placement in placements)
            {
                EffectCake cake;
                if (!document.Cakes.TryGetValue(placement.Name, out cake)) { Warnings.Add("EFF 缺少 CAKE：" + placement.Name); continue; }
                instances.Add(new Instance { Cake = cake, Anchor = new EffectVector(placement.X, -placement.Y / Math.Sqrt(.5), 0) });
            }
        }
        public void Seek(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            // Explicit preview budget, independent of the native event/RNG clock.
            seconds %= 60;
            int target = (int)Math.Round(seconds / StepSeconds), current = (int)Math.Round(Time / StepSeconds);
            if (target < current) { Reset(); current = 0; }
            for (; current < target; current++) Step();
        }
        private void Step()
        {
            const double dt = StepSeconds;
            foreach (var instance in instances.ToArray())
            {
                if (instance.Child == instance.Cake.Children.Count && instance.Play == instance.Cake.Plays.Count && !instance.Loop)
                { foreach (var tracked in instance.Tracked) tracked.Stopped = true; instances.Remove(instance); continue; }
                instance.Clock += dt;
                while (instance.Child < instance.Cake.Children.Count && instance.Cake.Children[instance.Child].Time <= instance.Clock)
                {
                    var child = instance.Cake.Children[instance.Child++]; EffectCream cream;
                    if (!document.Creams.TryGetValue(child.Name, out cream)) { Warnings.Add("缺少 CREAM：" + child.Name); continue; }
                    if (emitters.Count >= Limit) { Warnings.Add("已達效果發射器預覽上限。"); break; }
                    var emitter = new Emitter { Cream = cream, Owner = instance, Sort = child.Sort };
                    emitters.Add(emitter); if (cream.Number("IsLoop") != 0) instance.Tracked.Add(emitter);
                }
                while (instance.Play < instance.Cake.Plays.Count && instance.Cake.Plays[instance.Play].Time <= instance.Clock)
                {
                    string mode = instance.Cake.Plays[instance.Play].Mode;
                    if (mode == "PlayLoop") { instance.Clock = 0; instance.Loop = true; break; }
                    if (mode == "PlayRestart") { instance.Clock = 0; instance.Child = instance.Play = 0; instance.Tracked.Clear(); instance.Loop = false; break; }
                    if (mode == "PlayStop") { foreach (var tracked in instance.Tracked) tracked.Stopped = true; instance.Tracked.Clear(); }
                    if (mode != "PlayStop" && mode != "PlayNormal") Warnings.Add("未支援的 CAKEPLAY：" + mode);
                    instance.Loop = false; instance.Play++;
                }
            }
            int total = ParticleCount;
            foreach (var emitter in emitters.AsEnumerable().Reverse().OrderBy(e => SortOrder(e.Sort)).ToArray())
            {
                var spec = emitter.Cream; int cap = Math.Min(Limit, Math.Max(1, (int)spec.Number("MaxCount", 1)));
                if (!emitter.Stopped)
                {
                    if (emitter.Blocked) emitter.Budget = emitter.Interval;
                    emitter.Budget += dt; int requests = 0;
                    while (emitter.Budget > emitter.Interval && requests < Limit)
                    {
                        emitter.Budget -= emitter.Interval; emitter.Interval = EffectMath.Interval(spec, random); requests++;
                        if (emitter.Interval <= 0) { emitter.Stopped = true; Warnings.Add("無效的效果發射間隔。"); break; }
                    }
                    if (requests == Limit) Warnings.Add("已達發射迭代預覽上限。");
                    for (int request = 0; request < requests; request++)
                    {
                        emitter.Blocked = emitter.Particles.Count >= cap;
                        if (emitter.Blocked || emitter.Stopped) break;
                        if (total >= Limit) { Warnings.Add("已達 3000 粒子預覽上限。"); break; }
                        try
                        {
                            var timing = delays(spec.Text("TextureName"));
                            var particle = new EffectParticle(spec, random, emitter.Owner.Anchor, timing.Sum(), timing.Length) { Sort = emitter.Sort };
                            particle.Pattern = EffectPattern.Create(particle, random);
                            if (particle.Pattern == null) Warnings.Add("未支援或退化的 Pattern：" + spec.Text("Pattern"));
                            int initial = particle.AnimationType == 3 || spec.Text("Pattern") == "Mud" ? particle.AnimationStartFrame : -1;
                            particle.Animation = new EffectAnimation(timing, particle.AnimationSpeed, particle.AnimationType, initial);
                            emitter.Particles.Insert(0, particle); emitter.Spawned++; total++;
                        }
                        catch (ArgumentException ex) { Warnings.Add(ex.Message); emitter.Stopped = true; break; }
                        if (spec.Number("IsLoop") == 0 && emitter.Spawned >= cap) { emitter.Stopped = true; break; }
                    }
                }
                foreach (var particle in emitter.Particles.ToArray())
                {
                    if (!particle.Step(dt)) { emitter.Particles.Remove(particle); total--; continue; }
                    particle.Animation.Speed = particle.AnimationSpeed; particle.Animation.Step(dt);
                    particle.Pattern?.Update(particle, EffectMath.F(dt)); particle.Animation.Speed = particle.AnimationSpeed;
                    if (particle.Animation.Limited) Warnings.Add("已達材質動畫迭代上限。");
                }
                if (emitter.Particles.Count < cap) emitter.Blocked = false;
                if (emitter.Stopped && emitter.Particles.Count == 0) emitters.Remove(emitter);
            }
            Time += dt;
        }
    }
}
