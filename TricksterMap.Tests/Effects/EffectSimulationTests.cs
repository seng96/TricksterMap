using System.Text;
using TricksterMap.Effects;

namespace TricksterMap.Tests.Effects;

public class EffectSimulationTests
{
    private static MapEffectInstance Placement(string name)
    { var raw = new byte[52]; Encoding.ASCII.GetBytes(name).CopyTo(raw, 20); return new MapEffectInstance(raw); }
    [Fact]
    public void Loop_does_not_rewind_children_and_seek_back_is_deterministic()
    {
        var document = EffectDocument.Parse(Encoding.UTF8.GetBytes("<CAKEWAPPER><CAKE Name='x'><SUBCREAM Time='0'><SubCreamName>c</SubCreamName></SUBCREAM><CAKEPLAY Time='0' Type='PlayLoop'/></CAKE><CREAM Name='c'><IsLoop>1</IsLoop><MaxCount>2</MaxCount><Time>5</Time><EmissionRate>10</EmissionRate><AnimationType>0</AnimationType><Velocity>5</Velocity><Destination>10 0 0</Destination></CREAM></CAKEWAPPER>"));
        var simulation = new EffectSimulation(document, new[] { Placement("x") });
        simulation.Seek(1); Assert.Equal(2, simulation.ParticleCount); var first = simulation.Particles.Select(p => p.Position.X).ToArray();
        simulation.Seek(2); Assert.Equal(2, simulation.ParticleCount);
        simulation.Seek(1); Assert.Equal(first, simulation.Particles.Select(p => p.Position.X).ToArray());
        Assert.Equal("", document.Creams["c"].Text("NotPresent"));
    }
    [Fact]
    public void Nonloop_emitter_stops_at_max_count_and_all_particles_expire()
    {
        var document = EffectDocument.Parse(Encoding.UTF8.GetBytes("<CAKEWAPPER><CAKE Name='x'><SUBCREAM Time='0'><SubCreamName>c</SubCreamName></SUBCREAM></CAKE><CREAM Name='c'><MaxCount>2</MaxCount><Time>.2</Time><EmissionRate>100</EmissionRate></CREAM></CAKEWAPPER>"));
        var simulation = new EffectSimulation(document, new[] { Placement("x") }); simulation.Seek(.1); Assert.Equal(2, simulation.ParticleCount);
        simulation.Seek(1); Assert.Empty(simulation.Particles);
    }
}
