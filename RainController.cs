// AUTONOMA – RainController v0.8 (Godot 4.1+ .NET / C#)
// RainController: pronóstico → lluvia → secado. Mueve partículas, audio, niebla, luz,
//                 los uniforms globales de los charcos y el mojado/tiritar del personaje.
//
// Project Settings > Shader Globals: agregar  rain_wetness (float)  y  rain_intensity (float).

using Godot;

namespace Autonoma;

public partial class RainController : Node3D
{
    [Export] public AutonomaSim Sim;
    [Export] public CharacterMover Mover;            // para mojado y tiritar
    [Export] public GpuParticles3D RainParticles;    // hijo del rig de cámara, no del mundo
    [Export] public GpuParticles3D SplashParticles;  // salpicaduras en el piso alrededor de la cámara
    [Export] public AudioStreamPlayer RainAudio;
    [Export] public AudioStreamPlayer ThunderAudio;
    [Export] public WorldEnvironment Env;
    [Export] public DirectionalLight3D Sun;
    [Export] public int MaxDrops = 6000;
    [Export] public float ForecastHours = 1.5f;
    [Export] public Vector2 DurationHours = new(3f, 6f);
    [Export] public float WetUpPerHour = 1f, DryPerHour = 0.25f; // charcos: se forman en ~1 h, se secan en ~4 h

    public float GroundWetness { get; private set; }

    private float _target, _remaining, _baseSun = 1f, _baseFog, _baseVolFog, _flash;
    private bool _forecasting;
    private int _tier = -1;
    private readonly RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        _rng.Randomize();
        if (Sun != null) _baseSun = Sun.LightEnergy;
        if (Env?.Environment != null) { _baseFog = Env.Environment.FogDensity; _baseVolFog = Env.Environment.VolumetricFogDensity; }
        Sim.EventTriggered += (e, _) => { if (e.Id == "la_lluvia") Forecast(ForecastHours); };
    }

    /// Avisa con viento, hojas y cielo gris. Los que dominan Refugio construyen antes.
    public void Forecast(float hours) { _forecasting = true; Sim.World.RainForecastHours = hours; }

    public void StartRain(float intensity, float hours) { _target = Mathf.Clamp(intensity, 0f, 1f); _remaining = hours; }

    public override void _Process(double delta)
    {
        if (Sim == null || Sim.Paused) return;
        var w = Sim.World;
        float h = GameTime.ToGameHours(delta), dt = (float)delta;

        if (_forecasting)
        {
            w.RainForecastHours -= h;
            if (w.RainForecastHours <= 0f)
            {
                _forecasting = false;
                w.RainForecastHours = float.PositiveInfinity;
                StartRain(_rng.RandfRange(0.45f, 1f), _rng.RandfRange(DurationHours.X, DurationHours.Y));
            }
        }
        if (_remaining > 0f) { _remaining -= h; if (_remaining <= 0f) _target = 0f; }

        w.RainIntensity = Mathf.MoveToward(w.RainIntensity, _target, h * 1.5f); // rampa de ~40 min de juego
        if (w.RainIntensity > 0.7f) w.Weather = Weather.Tormenta;
        else if (w.RainIntensity > 0.01f) w.Weather = Weather.Lluvia;
        else if (w.Weather is Weather.Lluvia or Weather.Tormenta) w.Weather = Weather.Despejado;

        GroundWetness = Mathf.MoveToward(GroundWetness, w.IsRaining ? 1f : 0f,
                                         (w.IsRaining ? WetUpPerHour * (0.5f + w.RainIntensity) : DryPerHour) * h);
        RenderingServer.GlobalShaderParameterSet("rain_wetness", GroundWetness);
        RenderingServer.GlobalShaderParameterSet("rain_intensity", w.RainIntensity);

        UpdateParticles(w.RainIntensity);
        UpdateAudio(w.RainIntensity);
        UpdateSky(w, dt);
        UpdateCharacter();
    }

    private void UpdateParticles(float i)
    {
        // Por escalones: cambiar Amount reinicia el emisor, así que sólo se cambia al cruzar un umbral
        int tier = i < 0.01f ? 0 : i < 0.4f ? 1 : i < 0.75f ? 2 : 3;
        if (tier == _tier) return;
        _tier = tier;
        float ratio = tier switch { 1 => 0.35f, 2 => 0.7f, 3 => 1f, _ => 0f };
        if (RainParticles != null)
        {
            RainParticles.Emitting = tier > 0;
            if (tier > 0) RainParticles.Amount = Mathf.Max(1, (int)(MaxDrops * ratio));
        }
        if (SplashParticles != null)
        {
            SplashParticles.Emitting = tier > 0;
            if (tier > 0) SplashParticles.Amount = Mathf.Max(1, (int)(MaxDrops * 0.08f * ratio));
        }
    }

    private void UpdateAudio(float i)
    {
        if (RainAudio == null) return;
        if (i > 0.01f && !RainAudio.Playing) RainAudio.Play();
        if (i <= 0.01f && RainAudio.Playing) RainAudio.Stop();
        RainAudio.VolumeDb = Mathf.LinearToDb(Mathf.Max(0.0001f, i));
    }

    private void UpdateSky(WorldState w, float dt)
    {
        float i = w.RainIntensity;
        if (Env?.Environment != null)
        {
            Env.Environment.FogDensity = _baseFog + 0.02f * i;
            Env.Environment.VolumetricFogDensity = _baseVolFog + 0.03f * i;
        }
        if (Sun == null) return;

        // Relámpagos en tormenta
        if (w.Weather == Weather.Tormenta && _rng.Randf() < 0.004f)
        {
            _flash = 1f;
            ThunderAudio?.Play();
        }
        _flash = Mathf.MoveToward(_flash, 0f, dt * 4f);
        Sun.LightEnergy = _baseSun * (1f - 0.6f * i) + _flash * 3f;
    }

    private void UpdateCharacter()
    {
        var c = Sim.Character;
        if (Mover == null || c == null) return;
        float wet = c.Needs.Humedad / 100f;
        float shiver = Mathf.Clamp((c.Needs.Humedad - 40f) / 60f, 0f, 1f) + (c.IsSick ? 0.3f : 0f);
        Mover.SetWetness(wet, shiver);
    }
}
