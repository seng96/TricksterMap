using System.Text;
using System.Text.Json;
using TricksterMap.Effects;

namespace TricksterMap.Tests.Effects;

public class EffectMathTests
{
    [Fact]
    public void Particle_spawn_and_updates_match_independent_source_backed_python_vectors()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Effects/particle-vectors.json")));
        Assert.True(fixture.RootElement.GetProperty("cases").GetArrayLength() >= 10);
        foreach (var item in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var document = EffectDocument.Parse(Encoding.UTF8.GetBytes(item.GetProperty("xml").GetString()!));
            var random = new EffectRandom(1729);
            var particle = new EffectParticle(document.Creams.Values.Single(), random, new EffectVector(100, -200, 0), .7, 4);
            Compare(item.GetProperty("initial"), particle, random);
            for (int i = 0; i < item.GetProperty("steps").GetInt32(); i++) Assert.True(particle.Step(1.0 / 60));
            Compare(item.GetProperty("after"), particle, random);
        }
    }
    private static void Compare(JsonElement expected, EffectParticle p, EffectRandom random)
    {
        Vector(expected.GetProperty("position"), p.Position); Vector(expected.GetProperty("velocity"), p.Velocity); Vector(expected.GetProperty("color"), p.Color);
        var size = expected.GetProperty("size"); Near(size[0].GetDouble(), p.Size[0]); Near(size[1].GetDouble(), p.Size[1]);
        Near(expected.GetProperty("alpha").GetDouble(), p.Alpha); Near(expected.GetProperty("life").GetDouble(), p.Life);
        Near(expected.GetProperty("remaining").GetDouble(), p.Remaining); Near(expected.GetProperty("spin").GetDouble(), p.Spin); Near(expected.GetProperty("angle").GetDouble(), p.Angle);
        Assert.Equal(expected.GetProperty("rng").GetUInt32(), random.State); Assert.Equal(expected.GetProperty("frame").GetInt32(), p.AnimationStartFrame);
        Near(expected.GetProperty("animationSpeed").GetDouble(), p.AnimationSpeed);
    }
    private static void Vector(JsonElement expected, EffectVector actual)
    { Near(expected[0].GetDouble(), actual.X); Near(expected[1].GetDouble(), actual.Y); Near(expected[2].GetDouble(), actual.Z); }
    private static void Near(double expected, double actual) => Assert.True(Math.Abs(expected - actual) <= Math.Max(.002, Math.Abs(expected) * .000002), $"Expected {expected}, actual {actual}");

    [Fact]
    public void Zero_speed_animation_starts_first_frame_and_random_frame_stays_fixed()
    {
        var animation = new EffectAnimation(new[] { .1, .2 }, 0, 0); animation.Step(.1); Assert.Equal(0, animation.Frame);
        var random = new EffectAnimation(new[] { .1, .2 }, 0, 3, 1); random.Step(10); Assert.Equal(1, random.Frame);
    }
}
