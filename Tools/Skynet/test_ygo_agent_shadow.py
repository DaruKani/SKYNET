"""Golden mapping, privacy and refusal checks; no upstream package is imported."""
import copy
import json
from pathlib import Path
import tempfile
import unittest

import ygo_agent_shadow as shadow


SOURCE_ROOT = Path(__file__).resolve().parents[2] / "Research" / "ygo-agent"


def card(controller, location, sequence, code=0, position=0):
    return {
        "known": code != 0, "code": code, "controller": controller, "location": location,
        "sequence": sequence, "position": position, "overlaySequence": -1,
        "attribute": 1, "race": 1, "level": 4, "rank": 0, "linkMarker": 0,
        "counter": 0, "negated": False, "attack": 1800, "defense": 1500,
        "type": 17, "overlayCount": 0,
    }


def update_counts(record):
    counts = []
    for side in (0, 1):
        values = dict.fromkeys(shadow.COUNT_NAMES.values(), 0)
        for row in record["observation"]["cards"]:
            if row["controller"] == side:
                values[shadow.COUNT_NAMES[shadow.LOCATIONS[row["location"]]]] += 1
        counts.append(values)
    record["observation"]["counts"] = counts


def fixture():
    cards = [
        card(0, 1, 0),
        card(0, 2, 0, 98234196),
        card(0, 4, 3, 66073051, 1),
        card(1, 1, 0),
        card(1, 2, 0),
        card(1, 4, 2, 44095762, 8),
    ]
    record = {
        "schema": shadow.SCHEMA, "kind": "decision", "duelId": "fixture", "sequence": 3,
        "agent": "SKYNET", "prompt": "select_idlecmd", "unsupported": [],
        "observation": {
            "global": {"myLp": 8000, "opLp": 7000, "turn": 5, "phase": 4,
                       "isFirst": True, "isMyTurn": True},
            "cards": cards, "counterStateAvailable": True,
        },
        "offers": [
            {"id": "summon-2", "type": "summon", "actionCode": 0, "index": 2,
             "wireResponse": 2 << 16, "card": {k: cards[1][k] for k in
                 ("controller", "location", "sequence", "overlaySequence")},
             "description": -1, "isMandatory": False},
            {"id": "effect-4", "type": "activate", "actionCode": 5, "index": 4,
             "wireResponse": (4 << 16) + 5, "card": {k: cards[2][k] for k in
                 ("controller", "location", "sequence", "overlaySequence")},
             "description": 66073051 * 16 + 3, "isMandatory": False},
            {"id": "end", "type": "end", "actionCode": 7, "index": 0,
             "wireResponse": 7, "card": None, "description": -1, "isMandatory": False},
        ],
        "selectedOfferId": "effect-4", "selectedWireResponse": (4 << 16) + 5,
    }
    update_counts(record)
    return record


class YgoShadowTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.contract = shadow.load_contract(SOURCE_ROOT)

    def test_real_pin_and_wire_response_mapping(self):
        mapped = shadow.convert_snapshot(fixture(), self.contract)
        self.assertEqual("mapped", mapped["status"])
        self.assertFalse(mapped["modelReady"])
        self.assertEqual({"offerId": "effect-4", "upstreamActionIndex": 1,
                          "wireResponse": 262149}, mapped["observedChoice"])
        commands = mapped["input"]["action_msg"]["data"]["idle_cmds"]
        self.assertEqual("select_idlecmd", mapped["input"]["action_msg"]["data"]["msg_type"])
        self.assertEqual(["summon", "activate", "to_ep"], [entry["cmd_type"] for entry in commands])
        self.assertEqual({"code": 66073051, "controller": "me", "location": "mzone",
                          "sequence": 3}, commands[1]["data"]["card_info"])
        encoded = mapped["unpaddedFeaturePreview"]
        self.assertEqual([31, 64, 27, 88, 5, 2, 1, 1,
                          1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0], encoded["global_"])
        self.assertEqual([1, 8, 0, 5, 0, 0, 0, 0, 0], encoded["actions_"][1][3:])
        self.assertEqual([0, 0, 0, 1, 0, 0, 0, 3, 0, 0, 0, 0], encoded["actions_"][2])
        self.assertEqual(3, encoded["actions_"][1][0])
        self.assertEqual(4, encoded["cards_"][2][3])  # raw sequence + 1 exactly once
        self.assertEqual([7, 8, 5, 220], encoded["cards_"][1][12:16])
        self.assertEqual([1, 0, 0, 1] + [0] * 21, encoded["cards_"][1][16:])

    def test_private_identity_and_stats_cannot_change_the_mapping(self):
        original = fixture()
        mutated = copy.deepcopy(original)
        for index in (0, 3, 4, 5):
            mutated["observation"]["cards"][index].update({
                "code": 999999999, "attribute": 255, "race": 999, "level": 200,
                "rank": 100, "linkMarker": 511, "type": 0xffffffff,
                "attack": 99999, "defense": -99999, "negated": True, "counter": 99,
            })
        first = shadow.convert_snapshot(original, self.contract)
        second = shadow.convert_snapshot(mutated, self.contract)
        self.assertEqual(first, second)
        for index in (0, 3, 4, 5):
            result = first["input"]["cards"][index]
            self.assertEqual(0, result["code"])
            self.assertEqual(0, result["attack"])
            self.assertEqual([], result["types"])
            self.assertEqual([0] * 34, first["unpaddedFeaturePreview"]["cards_"][index][7:])

    def test_unknown_descriptions_and_types_abstain(self):
        for description in (-1, 9999, 66073051 * 16 + 14):
            record = fixture()
            record["offers"][1]["description"] = description
            with self.assertRaises(ValueError):
                shadow.convert_snapshot(record, self.contract)
        record = fixture()
        record["observation"]["cards"][1]["type"] = 8
        with self.assertRaisesRegex(ValueError, "type bits"):
            shadow.convert_snapshot(record, self.contract)
        record = fixture()
        record["observation"]["global"]["phase"] = 1024
        with self.assertRaisesRegex(ValueError, "phase"):
            shadow.convert_snapshot(record, self.contract)
        record = fixture()
        record["offers"][1]["fieldOnly"] = True
        with self.assertRaisesRegex(ValueError, "Field-only"):
            shadow.convert_snapshot(record, self.contract)

    def test_public_extra_card_is_kept_and_facedown_extra_is_redacted(self):
        record = fixture()
        record["observation"]["cards"].extend([
            card(1, 64, 0, 98234196, 1), card(1, 64, 1, 66073051, 8),
        ])
        update_counts(record)
        mapped = shadow.convert_snapshot(record, self.contract)
        self.assertEqual(98234196, mapped["input"]["cards"][-2]["code"])
        self.assertEqual(0, mapped["input"]["cards"][-1]["code"])

    def test_zero_and_system_descriptions_match_pinned_encoding(self):
        record = fixture()
        record["offers"][1]["description"] = 0
        self.assertEqual(1, shadow.convert_snapshot(record, self.contract)
                         ["unpaddedFeaturePreview"]["actions_"][1][6])
        record["offers"][1]["description"] = 1050
        self.assertEqual(16, shadow.convert_snapshot(record, self.contract)
                         ["unpaddedFeaturePreview"]["actions_"][1][6])

    def test_source_mismatch_and_unrecognized_vocabulary_are_rejected(self):
        record = fixture()
        record["observation"]["cards"][1]["code"] = 2147483647
        with self.assertRaisesRegex(ValueError, "vocabulary"):
            shadow.convert_snapshot(record, self.contract)
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "ygoinf/ygoinf/features.py"
            path.parent.mkdir(parents=True)
            path.write_text("print('not executed')", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "Pinned source changed"):
                shadow.load_contract(directory)

    def test_action_and_card_caps_never_truncate(self):
        record = fixture()
        record["offers"] = record["offers"] * 9
        with self.assertRaisesRegex(ValueError, "1..24"):
            shadow.convert_snapshot(record, self.contract)
        record = fixture()
        record["observation"]["cards"] = [card(0, 1, i) for i in range(80)]
        record["observation"]["cards"] += [card(0, 2, 0, 98234196), card(1, 1, 0)]
        update_counts(record)
        with self.assertRaisesRegex(ValueError, "80 per player"):
            shadow.convert_snapshot(record, self.contract)
        record["observation"]["cards"] *= 2
        with self.assertRaisesRegex(ValueError, "2..160"):
            shadow.convert_snapshot(record, self.contract)

    def test_wire_selection_card_reference_and_counts_must_agree(self):
        for change in ("wire", "chosen", "reference", "counts", "duplicate"):
            record = fixture()
            if change == "wire":
                record["offers"][0]["wireResponse"] = 0
            elif change == "chosen":
                record["selectedWireResponse"] = 7
            elif change == "reference":
                record["offers"][0]["card"]["sequence"] = 3
            elif change == "counts":
                record["observation"]["counts"][0]["hand"] = 2
            else:
                record["observation"]["cards"][2] = record["observation"]["cards"][1]
            with self.assertRaises(ValueError, msg=change):
                shadow.convert_snapshot(record, self.contract)

    def test_counter_limitation_requires_explicit_shadow_override(self):
        record = fixture()
        record["observation"]["counterStateAvailable"] = False
        with self.assertRaisesRegex(ValueError, "Counter state unavailable"):
            shadow.convert_snapshot(record, self.contract)
        result = shadow.convert_snapshot(record, self.contract, True)
        self.assertTrue(result["warnings"])
        self.assertFalse(result["modelReady"])
        record["unsupported"] = ["counter-update-observed"]
        with self.assertRaisesRegex(ValueError, "incomplete"):
            shadow.convert_snapshot(record, self.contract, True)
        record = fixture()
        record["observation"]["cards"][2]["overlayCount"] = 1
        with self.assertRaisesRegex(ValueError, "Overlay"):
            shadow.convert_snapshot(record, self.contract)

    def test_capture_size_duplicates_and_row_boundaries(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "capture.jsonl"
            path.write_text('{"schema":1,"schema":2}\n', encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "Duplicate"):
                shadow.read_capture(path)
            path.write_bytes(b" " * (shadow.MAX_ROW_BYTES + 1))
            with self.assertRaisesRegex(ValueError, "row size"):
                shadow.read_capture(path)
            path.write_text(json.dumps(fixture()) + "\n", encoding="utf-8")
            records, digest = shadow.read_capture(path)
            self.assertEqual(1, len(records))
            self.assertEqual(64, len(digest))

    def test_report_keeps_original_choice_and_reports_unsupported_prompts(self):
        original = fixture()
        other = copy.deepcopy(original)
        other["prompt"] = "select_chain"
        report = shadow.create_report([{"schema": shadow.SCHEMA, "kind": "start"}, original, other],
                                      self.contract)
        self.assertEqual(1, report["mapped"])
        self.assertEqual(1, report["unsupported"])
        self.assertEqual({"start": 1}, report["skippedRecordKinds"])
        self.assertEqual("effect-4", report["decisions"][0]["observedChoice"]["offerId"])
        self.assertNotIn("input", report["decisions"][1])

    def test_shuffle_offer_is_never_silently_removed(self):
        record = fixture()
        record["offers"].append({
            "id": "shuffle", "type": "shuffle", "actionCode": 8, "index": 0,
            "wireResponse": 8, "card": None, "description": -1, "isMandatory": False,
        })
        with self.assertRaisesRegex(ValueError, "Unsupported idle action"):
            shadow.convert_snapshot(record, self.contract, True)
        record["unsupported"] = ["shuffle-action-not-supported"]
        report = shadow.create_report([record], self.contract, True)
        self.assertEqual(0, report["mapped"])
        self.assertIn("shuffle-action-not-supported", report["decisions"][0]["reason"])
        self.assertEqual(4, len(record["offers"]))


if __name__ == "__main__":
    unittest.main()
