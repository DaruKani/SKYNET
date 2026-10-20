"""Train a bounded linear residual from completed SKYNET exploration episodes.

This is an episodic policy-gradient experiment, not a rules learner or evidence of
improved win rate. Holdout groups never contribute to the update. Evaluate the
candidate in fresh, frozen duels before choosing to use it.
"""
import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import tempfile

FEATURES = (
    "develop", "pressure", "advantage", "interaction", "recovery",
    "protection", "conserve", "risk", "synergy",
)
SCHEMA = "skynet-public-v1"
POLICY_REVISION = "skynet-policy-v4"


def require(condition, message):
    if not condition:
        raise ValueError(message)


def validate_weights(model):
    require(model.get("format") == "skynet-linear-v1"
            and model.get("featureSchema") == SCHEMA
            and isinstance(model.get("version"), str) and model["version"],
            "Incompatible base model.")
    weights = model.get("weights")
    require(isinstance(weights, dict), "Weights must be an object.")
    require(all(k in FEATURES and isinstance(v, (int, float))
                and math.isfinite(v) and abs(v) <= 20 for k, v in weights.items()),
            "Invalid base weights.")
    return dict(weights)


def episode_gradient(episode):
    require(episode.get("policyRevision") == POLICY_REVISION,
            "Incompatible policy revision: collect fresh episodes with " + POLICY_REVISION + ".")
    require(episode.get("format") == "skynet-episode-v1"
            and episode.get("featureSchema") == SCHEMA
            and not episode.get("truncated", False),
            "Incompatible or truncated episode.")
    require(episode.get("result") in (0, 1, 2), "Invalid terminal outcome.")
    decisions = episode.get("decisions", [])
    require(decisions and len(decisions) <= 4096, "Missing or excessive decisions.")
    gradient = {key: 0.0 for key in FEATURES}
    for decision in decisions:
        vectors = decision["features"]
        probabilities = decision["probabilities"]
        chosen = decision["chosen"]
        baselines = decision["baselineScores"]
        require(vectors and len(vectors) == len(probabilities) == len(baselines)
                and isinstance(chosen, int) and 0 <= chosen < len(vectors),
                "Invalid decision dimensions.")
        require(all(isinstance(p, (int, float)) and math.isfinite(p)
                    and 0 <= p <= 1 for p in probabilities)
                and abs(sum(probabilities) - 1.0) < 1e-8
                and probabilities[chosen] > 0, "Invalid behavior probabilities.")
        require(all(isinstance(b, (int, float)) and math.isfinite(b) for b in baselines),
                "Invalid baseline scores.")
        for vector in vectors:
            require(isinstance(vector, dict)
                    and all(k in FEATURES and isinstance(v, (int, float))
                            and math.isfinite(v) and abs(v) <= 100
                            for k, v in vector.items()), "Invalid candidate features.")
        for key in FEATURES:
            expected = sum(p * vector.get(key, 0.0)
                           for p, vector in zip(probabilities, vectors))
            gradient[key] += vectors[chosen].get(key, 0.0) - expected
    reward = {0: 1.0, 1: -1.0, 2: 0.0}[episode["result"]]
    return {key: reward * value / len(decisions) for key, value in gradient.items()}


def is_holdout(group, fraction, salt):
    digest = hashlib.sha256((salt + ":" + group).encode("utf-8")).digest()
    return int.from_bytes(digest[:8], "big") / 2**64 < fraction


def train(episodes, base_model, learning_rate, holdout_fraction, split_salt):
    weights = validate_weights(base_model)
    require(math.isfinite(learning_rate) and 0 < learning_rate <= 1,
            "Learning rate must be in (0, 1].")
    require(0 < holdout_fraction < 1, "Holdout fraction must be in (0, 1).")
    total = {key: 0.0 for key in FEATURES}
    seen = set()
    training_ids, holdout_ids, groups = [], [], set()
    for episode in episodes:
        episode_id = episode.get("id")
        group = episode.get("group")
        require(isinstance(episode_id, str) and episode_id and episode_id not in seen,
                "Missing or duplicate episode id.")
        require(isinstance(group, str) and group.strip(),
                "Every episode needs a stable deck/encounter group for holdout isolation.")
        require(episode.get("sourceModel") == base_model["version"],
                "Mixed behavior models: train each batch against its source checkpoint.")
        gradient = episode_gradient(episode)
        seen.add(episode_id)
        groups.add(group)
        if is_holdout(group, holdout_fraction, split_salt):
            holdout_ids.append(episode_id)
            continue
        training_ids.append(episode_id)
        for key in FEATURES:
            total[key] += gradient[key]
    require(training_ids and holdout_ids,
            "Need both training and holdout groups. Collect more distinct cohorts.")
    norm = math.sqrt(sum((value / len(training_ids))**2 for value in total.values()))
    scale = min(1.0, 1.0 / norm) if norm else 1.0
    for key in FEATURES:
        delta = learning_rate * total[key] / len(training_ids) * scale
        weights[key] = max(-20.0, min(20.0, weights.get(key, 0.0) + delta))
    provenance = {
        "parent": base_model["version"], "trainingIds": sorted(training_ids),
        "policyRevision": POLICY_REVISION,
        "holdoutIds": sorted(holdout_ids), "splitSalt": split_salt,
        "holdoutFraction": holdout_fraction, "learningRate": learning_rate,
        "groupCount": len(groups),
    }
    fingerprint = hashlib.sha256(json.dumps(
        {"weights": weights, "training": provenance}, sort_keys=True).encode()).hexdigest()[:16]
    return {
        "format": "skynet-linear-v1", "featureSchema": SCHEMA,
        "version": "candidate-" + fingerprint, "weights": weights,
        "training": provenance,
        "validation": {"status": "requires-fresh-frozen-duels",
                       "holdoutEpisodesExcludedFromTraining": len(holdout_ids)},
    }


def write_new_model(path, model):
    path = Path(path).resolve()
    require(not path.exists(), "Output exists; choose a new candidate filename.")
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", newline="\n",
                                         dir=path.parent, delete=False) as stream:
            temporary = Path(stream.name)
            json.dump(model, stream, indent=2, sort_keys=True, allow_nan=False)
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
        # An exclusive hard link publishes the complete same-filesystem file without
        # overwriting another trainer's candidate. No mutable live checkpoint is touched.
        os.link(temporary, path)
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--episodes", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--base", type=Path)
    parser.add_argument("--learning-rate", type=float, default=0.05)
    parser.add_argument("--holdout-fraction", type=float, default=0.2)
    parser.add_argument("--split-salt", default="skynet-holdout-v1")
    args = parser.parse_args()
    try:
        base = json.loads(args.base.read_text(encoding="utf-8-sig")) if args.base else {
            "format": "skynet-linear-v1", "featureSchema": SCHEMA,
            "version": "heuristic-v1", "weights": {},
        }
        episodes = [json.loads(path.read_text(encoding="utf-8-sig"))
                    for path in sorted(args.episodes.glob("*.json"))]
        model = train(episodes, base, args.learning_rate,
                      args.holdout_fraction, args.split_salt)
        write_new_model(args.output, model)
    except (OSError, ValueError, KeyError, TypeError) as error:
        parser.exit(2, "SKYNET training failed: " + str(error) + "\n")
    print(json.dumps({"candidate": str(args.output), "version": model["version"],
                      "trainingEpisodes": len(model["training"]["trainingIds"]),
                      "heldOutEpisodes": len(model["training"]["holdoutIds"]),
                      "status": model["validation"]["status"]}, indent=2))


if __name__ == "__main__":
    main()
