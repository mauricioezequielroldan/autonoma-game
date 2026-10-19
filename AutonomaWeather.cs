using System;
using System.Collections.Generic;

namespace Autonoma.Core
{
    public enum WeatherKind { Clear, Wind, Fog, Blizzard, Rain }
    public enum ShelterKind { None, LeanTo, LeafHut, ReinforcedHut, Cave }

    [Serializable]
    public sealed class WeatherConfig
    {
        public float ForecastLeadHours = 1.5f;
        public float MinStormHours = 3f;
        public float MaxStormHours = 6f;
        public float WetnessPerHourAtFullRain = 18f;
        public float WetnessDryPerHour = 7f;
        public float WindChillPerHour = 3f;
        public float FogVisibilityMin = 0.18f;
        public float BlizzardVisibilityMin = 0.08f;
        public float FireOutSecondsAtFullRain = 25f;
    }

    [Serializable]
    public sealed class WeatherSnapshot
    {
        public WeatherKind Kind;
        public float Intensity;          // 0..1
        public float Wind;               // 0..1
        public float Visibility = 1f;    // 0..1
        public float Wetness;             // 0..100
        public float ColdExposure;        // 0..100
        public float HoursUntilChange;
        public bool IsForecasted;
    }

    /// <summary>
    /// Engine-agnostic weather simulation. Call TickGameHours with game-time delta.
    /// A Three.js adapter can map Snapshot values to particles, fog and shaders.
    /// </summary>
    public sealed class AutonomaWeather
    {
        private readonly WeatherConfig _config;
        private readonly Random _random;
        private float _stormHoursRemaining;
        private float _forecastHoursRemaining;
        private WeatherKind _forecastKind = WeatherKind.Clear;

        public WeatherSnapshot Current { get; private set; }
        public event Action<WeatherSnapshot> Changed;

        public AutonomaWeather(WeatherConfig config = null, int? seed = null)
        {
            _config = config ?? new WeatherConfig();
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
            Current = new WeatherSnapshot { Kind = WeatherKind.Clear, Intensity = 0, Wind = 0, Visibility = 1 };
            ScheduleNextWeather();
        }

        public void TickGameHours(float gameHours, float realSeconds, bool fireIsExposed, ShelterKind shelter)
        {
            if (gameHours < 0 || realSeconds < 0) throw new ArgumentOutOfRangeException("Time delta cannot be negative.");

            if (_forecastHoursRemaining > 0)
            {
                _forecastHoursRemaining -= gameHours;
                if (_forecastHoursRemaining <= 0) BeginForecastedWeather();
            }

            if (_stormHoursRemaining > 0)
            {
                _stormHoursRemaining -= gameHours;
                if (_stormHoursRemaining <= 0)
                {
                    Current.Kind = WeatherKind.Clear;
                    Current.Intensity = 0;
                    Current.Wind = 0.1f;
                    Current.Visibility = 1f;
                    Current.IsForecasted = false;
                    ScheduleNextWeather();
                    RaiseChanged();
                }
            }

            bool protectedFromRain = shelter != ShelterKind.None;
            if (Current.Kind == WeatherKind.Rain || Current.Kind == WeatherKind.Blizzard)
            {
                float wetMultiplier = protectedFromRain ? 0.15f : 1f;
                Current.Wetness = Clamp(Current.Wetness + _config.WetnessPerHourAtFullRain * Current.Intensity * gameHours * wetMultiplier, 0, 100);
                Current.ColdExposure = Clamp(Current.ColdExposure + (Current.Kind == WeatherKind.Blizzard ? 12f : 3f) * Current.Intensity * gameHours, 0, 100);
            }
            else
            {
                Current.Wetness = Clamp(Current.Wetness - _config.WetnessDryPerHour * gameHours, 0, 100);
                Current.ColdExposure = Clamp(Current.ColdExposure - 5f * gameHours, 0, 100);
            }

            // Fire timers should be driven by real time, matching the v0.8 rain rule.
            if (fireIsExposed && Current.Kind == WeatherKind.Rain && shelter == ShelterKind.None && Current.Intensity > 0)
            {
                // Consumers can use this value to decrement their own fire lifetime.
                ExposedFireLifetimeSeconds = Math.Max(0, ExposedFireLifetimeSeconds - realSeconds * Current.Intensity);
            }
            else if (!fireIsExposed || shelter != ShelterKind.None || Current.Kind != WeatherKind.Rain)
            {
                ExposedFireLifetimeSeconds = _config.FireOutSecondsAtFullRain;
            }
        }

        public float ExposedFireLifetimeSeconds { get; private set; } = 25f;

        public bool ShouldSeekShelter(float hoursUntilWeather, bool knowsShelterSkill)
        {
            return (Current.Kind == WeatherKind.Rain || Current.Kind == WeatherKind.Blizzard ||
                    (_forecastHoursRemaining <= 2f && _forecastKind != WeatherKind.Clear))
                   && knowsShelterSkill;
        }

        public void ForceWeather(WeatherKind kind, float intensity, float durationGameHours, float wind = 0.3f)
        {
            Current.Kind = kind;
            Current.Intensity = Clamp(intensity, 0, 1);
            Current.Wind = Clamp(wind, 0, 1);
            _stormHoursRemaining = Math.Max(0, durationGameHours);
            Current.IsForecasted = false;
            RecalculateVisibility();
            RaiseChanged();
        }

        private void ScheduleNextWeather()
        {
            // Weighted and deliberately simple; tune probabilities in data/config later.
            double roll = _random.NextDouble();
            _forecastKind = roll < 0.40 ? WeatherKind.Clear :
                            roll < 0.62 ? WeatherKind.Rain :
                            roll < 0.78 ? WeatherKind.Wind :
                            roll < 0.91 ? WeatherKind.Fog : WeatherKind.Blizzard;
            _forecastHoursRemaining = _forecastKind == WeatherKind.Clear
                ? 2f + (float)_random.NextDouble() * 4f
                : _config.ForecastLeadHours;
            Current.HoursUntilChange = _forecastHoursRemaining;
            Current.IsForecasted = _forecastKind != WeatherKind.Clear;
        }

        private void BeginForecastedWeather()
        {
            Current.Kind = _forecastKind;
            Current.Intensity = 0.35f + (float)_random.NextDouble() * 0.65f;
            Current.Wind = Current.Kind == WeatherKind.Wind || Current.Kind == WeatherKind.Blizzard
                ? 0.55f + (float)_random.NextDouble() * 0.45f
                : 0.1f + (float)_random.NextDouble() * 0.35f;
            _stormHoursRemaining = _config.MinStormHours +
                (float)_random.NextDouble() * (_config.MaxStormHours - _config.MinStormHours);
            Current.IsForecasted = false;
            RecalculateVisibility();
            RaiseChanged();
        }

        private void RecalculateVisibility()
        {
            float visibility = 1f;
            if (Current.Kind == WeatherKind.Fog)
                visibility = 1f - Current.Intensity * (1f - _config.FogVisibilityMin);
            else if (Current.Kind == WeatherKind.Blizzard)
                visibility = 1f - Current.Intensity * (1f - _config.BlizzardVisibilityMin);
            else if (Current.Kind == WeatherKind.Rain)
                visibility = 1f - Current.Intensity * 0.25f;
            else if (Current.Kind == WeatherKind.Wind)
                visibility = 1f - Current.Intensity * 0.1f;
            Current.Visibility = Clamp(visibility, 0.02f, 1f);
        }

        private void RaiseChanged() => Changed?.Invoke(Current);
        private static float Clamp(float v, float min, float max) => Math.Max(min, Math.Min(max, v));
    }
}
