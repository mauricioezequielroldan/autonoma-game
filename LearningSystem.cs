using System;
using System.Collections.Generic;
using System.Linq;

namespace Autonoma.Core
{
    public enum LearningPhase { Unlearned, Imitating, Practicing, Dominated }

    [Serializable]
    public sealed class SkillDefinition
    {
        public string Id;
        public float Difficulty = 1f;
        public string[] Requirements = Array.Empty<string>();
        public float DemoGain = 30f;
        public float PracticeGain = 8f;
        public float UtilityThresholdPracticing = 70f;
        public float UtilityThresholdDominated = 40f;
    }

    [Serializable]
    public sealed class SkillAgent
    {
        public string Id;
        public float Knowledge;
        public long LastDecayGameMinute;
        public long LastPracticeGameMinute;
        public readonly Queue<EpisodeToken> EpisodeMemory = new Queue<EpisodeToken>();
        public readonly Dictionary<string, float> QuirkWeights = new Dictionary<string, float>();
        public LearningPhase Phase => Knowledge >= 100f ? LearningPhase.Dominated :
                                      Knowledge >= 60f ? LearningPhase.Practicing :
                                      Knowledge >= 30f ? LearningPhase.Imitating : LearningPhase.Unlearned;
    }

    [Serializable]
    public sealed class EpisodeToken
    {
        public string ActionId;
        public string TargetTag;
        public byte RelativeX;
        public byte RelativeY;
        public byte RelativeZ;
        public byte Quality;
        public long GameMinute;
    }

    public sealed class LearningCharacterContext
    {
        public float Bond;
        public float Fear;
        public float Faith = 50;
        public float Loyalty = 50;
        public float CuriosityTrait = 50;
        public float Resilience = 50;
        public bool IsPanicking;
        public long GameMinute;
        public string DayKey;
        public HashSet<string> DominatedSkills = new HashSet<string>();
    }

    /// <summary>
    /// One shared executor/system per character. SkillAgent objects are data only.
    /// Suggested three fixed scene nodes per character: NeedsAndTraits, LearningSystem,
    /// SkillExecutor. Keep rendering/physics out of the agents.
    /// </summary>
    public sealed class LearningSystem
    {
        public const float DemonstrationCap = 59f;
        public const float PossessionFaithCost = 30f;
        public const float BasePossessionSeconds = 35f;
        public const float ForgettingIntervalGameHours = 48f;
        public const float ForgettingFraction = 0.10f;
        public const float PanicLossPoints = 20f;
        public const int MaxEpisodeTokens = 64;

        private readonly Dictionary<string, SkillDefinition> _definitions;
        private readonly Dictionary<string, SkillAgent> _agents = new Dictionary<string, SkillAgent>();
        private readonly Dictionary<string, long> _lastDemoByDay = new Dictionary<string, long>();
        private readonly Random _random;

        public LearningSystem(IEnumerable<SkillDefinition> definitions, int? seed = null)
        {
            _definitions = definitions.ToDictionary(d => d.Id, d => d, StringComparer.Ordinal);
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
            foreach (SkillDefinition def in _definitions.Values)
                _agents[def.Id] = new SkillAgent { Id = def.Id };
        }

        public IReadOnlyCollection<SkillAgent> Agents => _agents.Values;

        public bool CanTeach(string skillId)
        {
            if (!_definitions.TryGetValue(skillId, out SkillDefinition def)) return false;
            return def.Requirements.All(req => _agents.TryGetValue(req, out SkillAgent a) && a.Knowledge >= 60f);
        }

        public float GetKnowledge(string skillId) => _agents.TryGetValue(skillId, out SkillAgent a) ? a.Knowledge : 0f;

        public bool TryBeginPossession(LearningCharacterContext context, out float durationSeconds)
        {
            durationSeconds = 0;
            if (context == null || context.Faith < PossessionFaithCost || context.Bond < 25f) return false;
            context.Faith -= PossessionFaithCost;
            durationSeconds = BasePossessionSeconds + 15f * Clamp(context.Bond, 0, 100) / 100f;
            if (context.Loyalty < 25f) durationSeconds -= 8f;
            if (context.Fear > 70f) durationSeconds -= 8f;
            durationSeconds = Math.Max(8f, durationSeconds);
            return true;
        }

        public float RecordDemonstration(string skillId, float quality, LearningCharacterContext context,
                                         IEnumerable<EpisodeToken> observedActions, string dayKey)
        {
            if (!_agents.TryGetValue(skillId, out SkillAgent agent) ||
                !_definitions.TryGetValue(skillId, out SkillDefinition def) || context == null)
                return 0;

            if (!CanTeach(skillId)) return 0;
            float q = Clamp(quality, 0, 1);
            float bondFactor = 0.5f + Clamp(context.Bond, 0, 100) / 200f;
            float personality = 1f + (Clamp(context.Loyalty, 0, 100) - 50f) / 500f;
            float gain = def.DemoGain * q * bondFactor * personality / Math.Max(0.1f, def.Difficulty);

            if (dayKey != null && _lastDemoByDay.TryGetValue(skillId + ":" + dayKey, out long previousMinute))
            {
                // Repeat demonstrations on the same in-game day yield ×0.8.
                gain *= 0.8f;
            }
            if (dayKey != null) _lastDemoByDay[skillId + ":" + dayKey] = context.GameMinute;

            agent.Knowledge = Math.Min(DemonstrationCap, agent.Knowledge + gain);
            if (observedActions != null)
                foreach (EpisodeToken token in observedActions) AddEpisode(agent, token);
            return gain;
        }

        public float RecordAutonomousPractice(string skillId, float quality, float utility,
                                              LearningCharacterContext context, bool playerWhisperedPractice = false)
        {
            if (!_agents.TryGetValue(skillId, out SkillAgent agent) ||
                !_definitions.TryGetValue(skillId, out SkillDefinition def) || context == null) return 0;

            LearningPhase phase = agent.Phase;
            if (phase == LearningPhase.Unlearned || phase == LearningPhase.Dominated) return 0;
            if (phase == LearningPhase.Imitating && !playerWhisperedPractice) return 0;
            if (phase == LearningPhase.Practicing && utility < def.UtilityThresholdPracticing && !playerWhisperedPractice) return 0;
            if (phase == LearningPhase.Practicing && utility < 70f && !playerWhisperedPractice) return 0;

            float q = Clamp(quality, 0, 1);
            float personality = 1f + (Clamp(context.CuriosityTrait, 0, 100) - 50f) / 500f;
            float practiceGain = def.PracticeGain * q * personality;
            if (context.IsPanicking) practiceGain *= 0.5f;
            agent.Knowledge = Math.Min(100f, agent.Knowledge + practiceGain);
            agent.LastPracticeGameMinute = context.GameMinute;
            if (agent.Knowledge >= 100f) context.DominatedSkills.Add(skillId);
            return practiceGain;
        }

        public void TickForgetting(LearningCharacterContext context)
        {
            if (context == null) return;
            foreach (SkillAgent agent in _agents.Values)
            {
                if (agent.Knowledge <= 0 || agent.Phase == LearningPhase.Dominated) continue;
                if (agent.LastDecayGameMinute == 0) agent.LastDecayGameMinute = context.GameMinute;
                long elapsed = context.GameMinute - agent.LastDecayGameMinute;
                long intervalMinutes = (long)(ForgettingIntervalGameHours * 60f);
                if (elapsed < intervalMinutes) continue;
                int intervals = (int)(elapsed / intervalMinutes);
                for (int i = 0; i < intervals; i++)
                    agent.Knowledge *= (1f - ForgettingFraction);
                agent.LastDecayGameMinute += intervals * intervalMinutes;
                if (agent.Knowledge < 85f && agent.Knowledge >= 100f) agent.Knowledge = 84.99f;
            }
        }

        public void ApplyPanic(LearningCharacterContext context)
        {
            if (context == null) return;
            foreach (SkillAgent agent in _agents.Values)
            {
                if (agent.Phase == LearningPhase.Dominated) continue;
                // Preserve personality exception: high resilience mitigates panic loss.
                float loss = PanicLossPoints;
                if (context.Resilience >= 65f) loss *= 0.5f;
                agent.Knowledge = Math.Max(0, agent.Knowledge - loss);
            }
            context.IsPanicking = true;
        }

        public void ObserveMasterQuirk(string skillId, string quirkId, float strength)
        {
            if (!_agents.TryGetValue(skillId, out SkillAgent agent) || string.IsNullOrWhiteSpace(quirkId)) return;
            float old;
            agent.QuirkWeights.TryGetValue(quirkId, out old);
            agent.QuirkWeights[quirkId] = Clamp(old + strength, 0, 1);
        }

        public string GetStrongestMasterQuirk(string skillId)
        {
            if (!_agents.TryGetValue(skillId, out SkillAgent agent) || agent.QuirkWeights.Count == 0) return null;
            return agent.QuirkWeights.OrderByDescending(kv => kv.Value).First().Key;
        }

        public IReadOnlyList<EpisodeToken> GetEpisodeMemory(string skillId)
        {
            if (!_agents.TryGetValue(skillId, out SkillAgent agent)) return Array.Empty<EpisodeToken>();
            return agent.EpisodeMemory.ToArray();
        }

        private static void AddEpisode(SkillAgent agent, EpisodeToken token)
        {
            if (token == null) return;
            while (agent.EpisodeMemory.Count >= MaxEpisodeTokens) agent.EpisodeMemory.Dequeue();
            agent.EpisodeMemory.Enqueue(token);
        }

        private static float Clamp(float v, float min, float max) => Math.Max(min, Math.Min(max, v));
    }
}
