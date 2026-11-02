import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock

import evaluate
import train


class LearningTests(unittest.TestCase):
    def setUp(self):
        self.base = {
            "format": "skynet-linear-v1", "featureSchema": train.SCHEMA,
            "version": "heuristic-v1", "weights": {},
        }
        self.episode = {
            "format": "skynet-episode-v1", "featureSchema": train.SCHEMA,
            "policyRevision": train.POLICY_REVISION,
            "id": "training", "sourceModel": "heuristic-v1", "group": "cohort",
            "result": 0, "truncated": False,
            "decisions": [{
                "features": [{"develop": 1}, {"pressure": 1}],
                "baselineScores": [0, 0], "probabilities": [0.5, 0.5], "chosen": 0,
            }],
        }
        self.training_group = next("train-" + str(i) for i in range(100)
                                   if not train.is_holdout("train-" + str(i), 0.2, "test"))
        self.holdout_group = next("holdout-" + str(i) for i in range(100)
                                  if train.is_holdout("holdout-" + str(i), 0.2, "test"))
        self.episode["group"] = self.training_group
        self.holdout = copy.deepcopy(self.episode)
        self.holdout.update(id="held-out", group=self.holdout_group, result=1)

    def fit(self, episodes=None):
        return train.train(episodes or [self.episode, self.holdout], self.base, 0.05, 0.2, "test")

    def test_win_and_loss_have_opposite_policy_gradient(self):
        win = train.episode_gradient(self.episode)
        self.episode["result"] = 1
        loss = train.episode_gradient(self.episode)
        self.assertEqual(win["develop"], 0.5)
        self.assertEqual(loss["develop"], -0.5)
        self.assertEqual(win["pressure"], -0.5)

    def test_draw_does_not_change_weights(self):
        self.episode["result"] = 2
        self.assertTrue(all(v == 0 for v in self.fit()["weights"].values()))

    def test_holdout_never_contributes_to_training(self):
        first = self.fit()
        self.holdout["result"] = 0
        self.holdout["decisions"][0]["chosen"] = 1
        second = self.fit()
        self.assertEqual(first["weights"], second["weights"])
        self.assertEqual(first["training"]["holdoutIds"], ["held-out"])
        self.assertGreater(first["weights"]["develop"], 0)
        self.assertLess(first["weights"]["pressure"], 0)

    def test_source_model_and_groups_are_enforced(self):
        self.episode["sourceModel"] = "different"
        with self.assertRaises(ValueError):
            self.fit()
        self.episode["sourceModel"] = "heuristic-v1"
        self.episode["group"] = ""
        with self.assertRaises(ValueError):
            self.fit()

    def test_missing_policy_revision_rejected(self):
        del self.episode["policyRevision"]
        with self.assertRaisesRegex(ValueError, "policy revision"):
            self.fit()

    def test_stale_policy_revision_rejected(self):
        self.episode["policyRevision"] = "skynet-policy-v1"
        with self.assertRaisesRegex(ValueError, "policy revision"):
            self.fit()

    def test_custom_checkpoint_does_not_allow_mixed_policy_revisions(self):
        self.base.update(version="custom-checkpoint", weights={"risk": -0.75})
        self.episode["sourceModel"] = self.holdout["sourceModel"] = self.base["version"]
        self.holdout["policyRevision"] = "skynet-policy-v1"
        with self.assertRaisesRegex(ValueError, "policy revision"):
            self.fit()
        del self.holdout["policyRevision"]
        with self.assertRaisesRegex(ValueError, "policy revision"):
            self.fit()

    def test_current_policy_episodes_accept_existing_custom_checkpoint(self):
        self.base.update(version="custom-checkpoint", weights={"risk": -0.75})
        self.episode["sourceModel"] = self.holdout["sourceModel"] = self.base["version"]
        original = copy.deepcopy(self.base)
        candidate = self.fit()
        self.assertEqual(candidate["training"]["policyRevision"], train.POLICY_REVISION)
        self.assertEqual(candidate["training"]["parent"], "custom-checkpoint")
        self.assertEqual(candidate["weights"]["risk"], -0.75)
        self.assertEqual(self.base, original)

    def test_candidate_identity_includes_policy_revision(self):
        candidate = self.fit()
        fingerprint = hashlib.sha256(json.dumps(
            {"weights": candidate["weights"], "training": candidate["training"]},
            sort_keys=True).encode()).hexdigest()[:16]
        self.assertEqual(candidate["version"], "candidate-" + fingerprint)
        next_revision = "synthetic-next-policy-for-fingerprint-test"
        self.episode["policyRevision"] = self.holdout["policyRevision"] = next_revision
        with mock.patch.object(train, "POLICY_REVISION", next_revision):
            next_candidate = self.fit()
        self.assertEqual(candidate["weights"], next_candidate["weights"])
        self.assertNotEqual(candidate["version"], next_candidate["version"])

    def test_duplicates_truncated_and_invalid_probability_rejected(self):
        with self.assertRaises(ValueError):
            self.fit([self.episode, self.episode, self.holdout])
        self.episode["truncated"] = True
        with self.assertRaises(ValueError):
            self.fit()
        self.episode["truncated"] = False
        self.episode["decisions"][0]["probabilities"] = [1, 1]
        with self.assertRaises(ValueError):
            self.fit()

    def test_candidate_publication_never_overwrites(self):
        with tempfile.TemporaryDirectory(prefix="SKYNETLearning-") as directory:
            path = Path(directory) / "candidate.json"
            model = self.fit()
            train.write_new_model(path, model)
            self.assertEqual(json.loads(path.read_text()), model)
            with self.assertRaises(ValueError):
                train.write_new_model(path, self.base)
            self.assertEqual(json.loads(path.read_text()), model)
            self.assertEqual(len(list(Path(directory).iterdir())), 1)

    def test_evaluation_reports_terminal_results_separately(self):
        rows = [
            {"model": "candidate", "opponent": "baseline", "seed": "1", "seat": "0", "result": "win"},
            {"model": "candidate", "opponent": "baseline", "seed": "1", "seat": "1", "result": "draw"},
        ]
        summary = evaluate.summarize(rows)[0]
        self.assertEqual(summary["matches"], 2)
        self.assertEqual(summary["scoreRate"], 0.75)
        self.assertTrue(summary["bothSeatsPresent"])
        self.assertLess(summary["winRateWilson95"][0], 0.5)
        self.assertGreater(summary["winRateWilson95"][1], 0.5)
        with self.assertRaises(ValueError):
            evaluate.summarize(rows + [rows[0]])

    def test_unknown_seeds_use_independent_match_identity(self):
        rows = [
            {"model": "candidate", "opponent": "baseline", "seed": "",
             "matchId": "run-a:duel-1", "seat": "0", "result": "win"},
            {"model": "candidate", "opponent": "baseline", "seed": "",
             "matchId": "run-a:duel-2", "seat": "0", "result": "loss"},
        ]
        summary = evaluate.summarize(rows)[0]
        self.assertEqual(summary["matches"], 2)
        self.assertEqual(summary["unknownDuelSeedMatches"], 2)
        with self.assertRaises(ValueError):
            evaluate.summarize(rows + [rows[0]])
        rows[0]["matchId"] = ""
        with self.assertRaises(ValueError):
            evaluate.summarize(rows)


if __name__ == "__main__":
    unittest.main()
