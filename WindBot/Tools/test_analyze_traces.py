import json
import tempfile
import unittest
from pathlib import Path

import analyze_traces


class TraceAnalysisTests(unittest.TestCase):
    def write_capture(self, directory, rows):
        path = Path(directory) / "duel.jsonl"
        path.write_text("".join(json.dumps(row) + "\n" for row in rows), encoding="utf-8")
        return path

    def test_completed_capture_summarizes_actions_fallbacks_and_unsupported(self):
        common = {"schema": analyze_traces.SCHEMA, "duelId": "duel-1", "agent": "TestExecutor"}
        rows = [
            dict(common, sequence=0, kind="start"),
            dict(common, sequence=1, kind="decision", prompt="select_idlecmd",
                 observation={"global": {"turn": 2, "phase": 4}, "cards": [
                     {"known": True, "code": 123, "controller": 0, "location": 2, "sequence": 0}
                 ]}, offers=[{"id": 0, "type": "summon", "card": {
                     "controller": 0, "location": 2, "sequence": 0}}], selectedOfferId=0,
                 decisionSource="Summon:TestExecutor.Summon", unsupported=["shuffle-action-not-supported"]),
            dict(common, sequence=2, kind="selection", prompt="select_card",
                 observation={"global": {"turn": 2, "phase": 4}},
                 selection={"candidates": [{"id": 0, "known": True, "code": 456}]},
                 selectedCandidateIds=[0], responseSource="legal-fallback"),
            dict(common, sequence=3, kind="outcome", outcome=0),
            dict(common, sequence=4, kind="end", outcome=0, completed=True),
        ]
        with tempfile.TemporaryDirectory() as directory:
            self.write_capture(directory, rows)
            report = analyze_traces.analyze([directory])
        self.assertEqual(report["summary"]["completedCaptures"], 1)
        self.assertEqual(report["summary"]["outcomes"], {"win": 1})
        self.assertEqual(report["summary"]["actionTypes"], {"summon": 1})
        self.assertEqual(report["summary"]["responseSources"], {"legal-fallback": 1})
        self.assertEqual(report["summary"]["decisionSources"], {"Summon:TestExecutor.Summon": 1})
        self.assertEqual(report["decisionRows"][0]["selectedCardCode"], 123)
        self.assertEqual(report["decisionRows"][1]["selectedCandidateCodes"], "456")

    def test_rejects_non_contiguous_sequences(self):
        rows = [
            {"schema": analyze_traces.SCHEMA, "duelId": "duel-1", "agent": "A", "sequence": 0, "kind": "start"},
            {"schema": analyze_traces.SCHEMA, "duelId": "duel-1", "agent": "A", "sequence": 2, "kind": "end"},
        ]
        with tempfile.TemporaryDirectory() as directory:
            path = self.write_capture(directory, rows)
            with self.assertRaisesRegex(ValueError, "non-contiguous sequence"):
                analyze_traces.read_capture(path)


if __name__ == "__main__":
    unittest.main()
