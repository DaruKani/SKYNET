"""Summarize fresh frozen duel results; training traces are not strength evidence.

CSV columns: model,opponent,seed,seat,result (win/loss/draw), plus matchId when
the core duel seed is unknown. A match identity is not an RNG seed. Use a fixed,
predeclared opponent/deck/seed manifest and both seats. This utility reports
separate model/opponent cohorts; it never changes or promotes a checkpoint.
"""
import argparse
import csv
import json
import math
from collections import defaultdict


def summarize(rows):
    cohorts = defaultdict(lambda: {
        "wins": 0, "losses": 0, "draws": 0, "seats": set(), "unknownSeeds": 0,
    })
    seen = set()
    for row in rows:
        match_id, seed = row.get("matchId", "").strip(), row.get("seed", "").strip()
        if not match_id and not seed:
            raise ValueError("Unknown duel seeds require a unique matchId.")
        identity = (row["model"], row["opponent"],
                    ("matchId", match_id) if match_id else ("seed", seed), row["seat"])
        if identity in seen:
            raise ValueError("Duplicate match identity: " + repr(identity))
        seen.add(identity)
        if row["seat"] not in ("0", "1") or row["result"] not in ("win", "loss", "draw"):
            raise ValueError("Invalid seat or terminal result.")
        cohort = cohorts[(row["model"], row["opponent"])]
        cohort[{"win": "wins", "loss": "losses", "draw": "draws"}[row["result"]]] += 1
        cohort["seats"].add(row["seat"])
        if not seed:
            cohort["unknownSeeds"] += 1
    result = []
    for (model, opponent), cohort in sorted(cohorts.items()):
        n = cohort["wins"] + cohort["losses"] + cohort["draws"]
        p, z = cohort["wins"] / n, 1.959963984540054
        center = (p + z*z/(2*n)) / (1 + z*z/n)
        half = z * math.sqrt(p*(1-p)/n + z*z/(4*n*n)) / (1 + z*z/n)
        result.append({
            "model": model, "opponent": opponent, "matches": n,
            "wins": cohort["wins"], "losses": cohort["losses"], "draws": cohort["draws"],
            "winRate": p, "winRateWilson95": [center - half, center + half],
            "scoreRate": (cohort["wins"] + 0.5 * cohort["draws"]) / n,
            "bothSeatsPresent": cohort["seats"] == {"0", "1"},
            "unknownDuelSeedMatches": cohort["unknownSeeds"],
        })
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("results_csv")
    args = parser.parse_args()
    try:
        with open(args.results_csv, newline="", encoding="utf-8-sig") as stream:
            result = summarize(csv.DictReader(stream))
        if not result:
            raise ValueError("No completed duel results.")
    except (OSError, ValueError, KeyError) as error:
        parser.exit(2, "SKYNET evaluation failed: " + str(error) + "\n")
    print(json.dumps({"cohorts": result, "automaticPromotion": False}, indent=2))


if __name__ == "__main__":
    main()
