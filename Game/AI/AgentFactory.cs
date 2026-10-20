using System;
using Skynet.Game.AI.Experts;
using Skynet.Game.AI.Strategy;

namespace Skynet.Game.AI
{
    public static class AgentFactory
    {
        public static void ValidateOptions(string agent, bool policyTraining, string policyModel)
        {
            bool expert = string.Equals(agent, "expert-oldschool", StringComparison.OrdinalIgnoreCase);
            if (!expert && !string.Equals(agent, "skynet", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Agent must be skynet or expert-oldschool.");
            if (expert && (policyTraining || !string.IsNullOrWhiteSpace(policyModel)))
                throw new ArgumentException("Expert compatibility owns its full decision lifecycle and does not accept PolicyTraining or PolicyModel.");
        }

        public static Executor Create(GameAI ai, Duel duel, GameClient game)
        {
            ValidateOptions(game.Agent, game.PolicyTraining, game.PolicyModel);
            if (string.Equals(game.Agent, "expert-oldschool", StringComparison.OrdinalIgnoreCase))
            {
                // A whole expert owns every callback and its pending selectors.
                // Never expose these mutable predicates to ranked policy scoring.
                OldSchoolExecutor expert = new OldSchoolExecutor(ai, duel);
                expert.Deck = "AI_ExpertOldSchool";
                return expert;
            }

            SkynetAgent agent = new SkynetAgent(ai, duel);
            agent.Deck = "AI_Skynet";
            agent.Learning = new SkynetLearningSession(game.PolicyModel,
                game.PolicyTraining, game.PolicyData, game.PolicySeed);
            agent.Learning.TrainingGroup = game.PolicyGroup ?? (game.DeckFile ?? agent.Deck);
            return agent;
        }
    }
}
