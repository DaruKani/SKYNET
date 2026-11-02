import copy
import unittest

from inspect_demonstrations import inspect_records, selection_label
from ygo_agent_shadow import SCHEMA


def fixture():
    def record(kind, sequence, **fields):
        return dict(schema=SCHEMA, duelId="fixture", agent="expert-swordsoul",
                    kind=kind, sequence=sequence, **fields)
    candidates = [
        {"id": i, "known": i < 2, "code": 93490856 if i < 2 else 0,
         "card": {"controller": 0 if i < 2 else 1, "location": 1 if i < 2 else 2,
                  "sequence": i, "overlaySequence": -1}, "position": 0}
        for i in range(3)
    ]
    decision = record("decision", 1, prompt="select_card", unsupported=["chain-state-incomplete"],
                      selection={"min": 1, "max": 2, "cancelable": True, "hint": 506,
                                 "candidates": candidates, "solvingChainIndex": 1,
                                 "chain": [{"index": 1, "activatePlayer": 0, "code": 69248256,
                                            "description": 1107972096,
                                            "activation": {"controller": 0, "location": 4, "sequence": 2}}]},
                      selectedCandidateIds=[1], responseBytes=[1, 1], responseSource="policy")
    return [record("start", 0), decision, record("outcome", 2, outcome=2),
            record("end", 3, completed=True, outcome=2)]


class DemonstrationTests(unittest.TestCase):
    def test_complete_draw_preserves_duplicate_card_codes_and_raw_choice(self):
        rows = fixture()
        result = inspect_records(rows)
        self.assertTrue(result["completed"])
        self.assertEqual(2, result["outcome"])
        self.assertEqual(1, result["completedPolicySelections"])
        self.assertEqual((2, 1), (result["knownCandidateMentions"], result["unknownCandidateMentions"]))
        self.assertFalse(result["readyForNeuralTraining"])
        self.assertEqual([1], rows[1]["selectedCandidateIds"])

    def test_wire_mismatch_duplicates_and_out_of_offer_indices_are_rejected(self):
        for chosen, response in (([1], [1, 0]), ([1, 1], [2, 1, 1]), ([8], [1, 8]), ([], [0])):
            row = fixture()[1]
            row.update(selectedCandidateIds=chosen, responseBytes=response)
            with self.assertRaises(ValueError):
                selection_label(row)

    def test_hidden_identity_cannot_be_labelled_as_visible(self):
        for location, position in ((1, 0), (2, 0), (4, 8), (8, 2), (64, 8), (32, 8)):
            row = fixture()[1]
            hidden = row["selection"]["candidates"][2]
            hidden.update(known=True, code=14558127, position=position)
            hidden["card"]["location"] = location
            with self.assertRaisesRegex(ValueError, "Hidden candidate"):
                selection_label(row)
        row = fixture()[1]
        row["selection"]["candidates"][2]["code"] = 14558127
        with self.assertRaisesRegex(ValueError, "Hidden candidate"):
            selection_label(row)

    def test_cancellation_and_automatic_response_are_distinct(self):
        row = fixture()[1]
        row.update(cancelled=True, selectedCandidateIds=[], responseBytes=[255] * 4,
                   responseSource="automatic-field-first-cancel")
        self.assertTrue(selection_label(row)["cancelled"])
        row["selection"]["cancelable"] = False
        with self.assertRaises(ValueError):
            selection_label(row)

    def test_fallback_is_not_counted_as_expert_choice(self):
        rows = fixture()
        rows[1]["responseSource"] = "legal-fallback"
        result = inspect_records(rows)
        self.assertEqual(1, result["auditedSelections"])
        self.assertEqual(0, result["completedPolicySelections"])

    def test_truncated_disconnected_and_unconfirmed_outcomes_are_not_completed(self):
        for rows in (fixture()[:2], fixture()[:3]):
            self.assertEqual(0, inspect_records(rows)["completedPolicySelections"])
        rows = fixture()
        rows[-1]["completed"] = False
        self.assertFalse(inspect_records(rows)["completed"])
        rows = fixture()[:2]
        rows.append(dict(rows[0], sequence=2, kind="end", completed=False, outcome=None))
        self.assertIsNone(inspect_records(rows)["outcome"])

    def test_identity_sequence_and_terminal_validation(self):
        mutations = [
            lambda r: r[1].update(sequence=9),
            lambda r: r[1].update(sequence=True),
            lambda r: r[1].update(duelId="another-duel"),
            lambda r: r[-1].update(outcome=0),
            lambda r: r.append(copy.deepcopy(r[1])),
            lambda r: r[1].update(kind="start"),
            lambda r: r[1].update(kind="outcome", outcome=0),
        ]
        for mutate in mutations:
            rows = fixture()
            mutate(rows)
            with self.assertRaises(ValueError):
                inspect_records(rows)

    def test_chain_and_candidate_wire_order_must_remain_unambiguous(self):
        for change in ("chain", "candidate", "unknown-description", "overlay"):
            row = fixture()[1]
            if change == "chain":
                row["selection"]["solvingChainIndex"] = 2
            elif change == "candidate":
                row["selection"]["candidates"][1]["id"] = 0
            elif change == "overlay":
                row["selection"]["candidates"][1]["card"]["overlaySequence"] = 0
            else:
                row["selection"]["chain"][0]["code"] = 0
            with self.assertRaises(ValueError):
                selection_label(row)


if __name__ == "__main__":
    unittest.main()
