using UnityEngine;

namespace SocialSim
{
    public enum StudentAction { Home, Study, Playground }
    public enum StudentPhase { Deciding, Moving, Acting, Waiting, Dragging, Dead, Gathering }
    public struct UtilityScores
    {
        public float Study, Home, Playground;
        public float Get(StudentAction action) => action == StudentAction.Study ? Study : action == StudentAction.Home ? Home : Playground;
    }
    public static class UtilityScorer
    {
        public static StudentAction PreferredAction(UtilityScores scores, StudentAction? previous = null)
        {
            var best = previous ?? StudentAction.Home;
            foreach (StudentAction action in System.Enum.GetValues(typeof(StudentAction)))
                if (scores.Get(action) > scores.Get(best)) best = action;
            return best;
        }

        public static bool HasSocialSupport(int neighborCount, SimulationSettings settings) =>
            neighborCount >= settings.supportMinNeighbors && neighborCount <= settings.supportMaxNeighbors;

        public static UtilityScores Evaluate(float academic, float fatigue, float stress, float explorationSeconds)
        {
            float a = Mathf.Clamp01(academic / 100f), f = Mathf.Clamp01(fatigue / 100f);
            return new UtilityScores {
                Study = 100f * (1f - a) * (1f - f), Home = 100f * f,
                Playground = Mathf.Min(100f, Mathf.Min(45f, 5f + Mathf.Max(0f, explorationSeconds)) + Mathf.Clamp(stress, 0f, 100f)) * (1f - f)
            };
        }
        public static void UpdateNeeds(ref float academic, ref float fatigue, ref float stress, float seconds,
            StudentAction action, bool acting, SimulationSettings settings, int neighborCount = 0)
        {
            bool supported = HasSocialSupport(neighborCount, settings);
            bool recoveringAtPlayground = acting && action == StudentAction.Playground;
            float gain = academic < settings.stressAcademicThreshold ? settings.stressGain : 0f;
            float recovery = recoveringAtPlayground ? settings.playgroundStressRecovery : 0f;
            if (supported)
            {
                gain *= settings.supportedStressGainMultiplier;
                recovery *= settings.supportedRecoveryMultiplier;
            }
            float crowding = recoveringAtPlayground ? 0f :
                Mathf.Max(0, neighborCount - settings.supportMaxNeighbors) * settings.crowdStressPerNeighbor;
            stress += (gain + crowding - recovery) * seconds;
            academic += (-settings.academicDecay + (acting && action == StudentAction.Study ? settings.studyGain : 0f)) * seconds;
            if (acting && action == StudentAction.Study) fatigue += settings.studyFatigue * seconds;
            if (acting && action == StudentAction.Home) fatigue -= settings.restRecovery * seconds;
            academic = Mathf.Clamp(academic, 0f, 100f);
            fatigue = Mathf.Clamp(fatigue, 0f, 100f);
            stress = Mathf.Clamp(stress, 0f, 100f);
        }
    }
}
