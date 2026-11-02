"""Boundary checks for the static YGO Agent inspector; no upstream execution."""
from pathlib import Path
import tempfile
import unittest

import inspect_ygo_agent as inspector


class YgoAgentInspectionTests(unittest.TestCase):
    def test_vocabulary_order_and_duplicates(self):
        self.assertEqual([42, 7], inspector.parse_vocabulary(b"42\n7\n")["codes"])
        document = inspector.parse_vocabulary(b"42 1\n7 0\n")
        self.assertEqual([42, 7], document["codes"])
        self.assertEqual(2, document["columns"])
        self.assertEqual({42: True, 7: False}, document["scriptFlags"])
        for raw in (b"42\n42\n", b"0\n", b"4294967296\n", b"1; import os\n"):
            with self.assertRaises(ValueError):
                inspector.parse_vocabulary(raw)
        for raw in (b"42 2\n", b"42 1\n7\n", b"42\n7 0\n", b"42 1\n42 0\n"):
            with self.assertRaises(ValueError):
                inspector.parse_vocabulary(raw)

    def test_coverage_is_not_runtime_support(self):
        with tempfile.TemporaryDirectory() as directory:
            deck = Path(directory) / "deck.ydk"
            deck.write_text("#created by test\n#main\n42\n42\n7\n#extra\n9\n!side\n7\n",
                            encoding="utf-8")
            report = inspector.inspect_deck(deck, {42, 9})
        self.assertEqual(3, report["uniqueCards"])
        self.assertEqual(2, report["vocabularyUniqueCards"])
        self.assertEqual([{"code": 7, "copies": 2}], report["missing"])
        self.assertFalse(report["runtimeSupportImplied"])
        self.assertEqual("not-tested", report["checkpointCompatibility"])
        self.assertEqual(2, report["sections"]["main"]["vocabularyCopies"])

    def test_deck_input_limits_and_structure(self):
        for raw in (b"42\n", b"#main\n0\n", b"#main\n42\n!unknown\n", b"#extra\n42\n",
                    b"#main\n" + b"42\n" * (inspector.MAX_DECK_CARDS + 1)):
            with self.assertRaises(ValueError):
                inspector.parse_deck(raw)

    def test_reads_are_bounded_and_source_cannot_escape(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory) / "root"
            root.mkdir()
            outside = Path(directory) / "outside"
            outside.write_bytes(b"12345")
            with self.assertRaises(ValueError):
                inspector.bounded_read(outside, 4)
            with self.assertRaises(ValueError):
                inspector.source_read(root, Path("../outside"))

    def test_feature_parser_never_executes_code(self):
        with tempfile.TemporaryDirectory() as directory:
            marker = Path(directory) / "must-not-exist"
            source = "open(%r, 'w').write('executed')\n" % str(marker)
            source += "\n".join("%s = 24" % name for name in inspector.LIMIT_NAMES)
            source += "\nmsg_to_id = {MsgName.select_chain: 2}\n"
            source += "\ndef init_code_list():\n    return int(line)\n"
            report = inspector.inspect_features(source.encode("utf-8"))
            self.assertFalse(marker.exists())
        self.assertEqual({"select_chain": 2}, report["declaredPromptIds"])
        self.assertTrue(report["loaderReadsWholeLineAsInteger"])
        dynamic = source.replace("MAX_CARDS = 24", "MAX_CARDS = int('24')")
        with self.assertRaises(ValueError):
            inspector.inspect_features(dynamic.encode("utf-8"))

    def test_source_list_and_loader_format_mismatch_is_reported(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            code_file = root / inspector.CODE_LIST_PATH
            code_file.parent.mkdir(parents=True)
            code_file.write_text("42 1\n", encoding="utf-8")
            feature_file = root / inspector.FEATURE_PATH
            feature_file.parent.mkdir(parents=True)
            source = "\n".join("%s = 24" % name for name in inspector.LIMIT_NAMES)
            source += "\nmsg_to_id = {MsgName.select_chain: 2}\n"
            source += "\ndef init_code_list():\n    return int(line)\n"
            feature_file.write_text(source, encoding="utf-8")
            deck = root / "deck.ydk"
            deck.write_text("#main\n42\n", encoding="utf-8")
            report = inspector.inspect(root, [deck])
        self.assertEqual(1, report["vocabularyCards"])
        self.assertTrue(report["decks"][0]["allDeckCodesInVocabulary"])
        self.assertEqual(1, len(report["sourceCompatibilityFindings"]))
        self.assertEqual(2, len(report["sourceFiles"]))
        self.assertTrue(all(len(item["sha256"]) == 64 for item in report["sourceFiles"]))

    def test_output_is_exclusive_and_inside_workspace(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory) / "root"
            root.mkdir()
            report = root / "report.json"
            inspector.write_report({"test": 1}, report, root)
            with self.assertRaises(ValueError):
                inspector.write_report({"test": 2}, report, root)
            with self.assertRaises(ValueError):
                inspector.write_report({}, Path(directory) / "outside.json", root)
            self.assertIn('"test": 1', report.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
