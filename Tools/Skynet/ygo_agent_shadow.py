"""Convert explicit public-observation captures to a bounded YGO Agent shadow report.

This is an offline mapping diagnostic, never inference or a policy. Unsupported
snapshots are reported without changing the real bot's decisions. No upstream
module, checkpoint, embedding or database is imported or executed.
"""
import argparse
import ast
from collections import Counter
import hashlib
import json
from pathlib import Path
import re

from inspect_ygo_agent import bounded_read, parse_vocabulary, source_read


SCHEMA = "skynet-public-observation-v1"
PIN = "dbf5142d49aab2e6beb4150788d4fffec39ae3e5"
SOURCE_HASHES = {
    "ygoinf/ygoinf/features.py": "5fb695821f0db41b94da3293756c78ccff6fd29cc14c0fb939ec4ed16365e83e",
    "ygoinf/ygoinf/models.py": "c934c32d6aa3c2c09911c4ae64886977e70da70ac46ad702825f14db98f3e9a8",
}
MAX_CAPTURE_BYTES = 16 * 1024 * 1024
MAX_ROW_BYTES = 1024 * 1024
MAX_ROWS = 4096
MAX_REPORT_BYTES = 64 * 1024 * 1024
LOCATIONS = dict(zip((1, 2, 4, 8, 16, 32, 64),
                     ("deck", "hand", "mzone", "szone", "grave", "removed", "extra")))
LOCATION_IDS = {name: i + 1 for i, name in enumerate(LOCATIONS.values())}
COUNT_NAMES = dict(zip(LOCATIONS.values(), ("deck", "hand", "monster", "spell", "grave", "banished", "extra")))
PHASES = dict(zip((1, 2, 4, 8, 16, 32, 64, 128, 256, 512),
                 ("draw", "standby", "main1", "battle_start", "battle_step",
                  "damage", "damage_calculation", "battle", "main2", "end")))
POSITIONS = {0: "none", 1: "faceup_attack", 2: "facedown_attack", 3: "attack",
             4: "faceup_defense", 5: "faceup", 8: "facedown_defense", 10: "facedown", 12: "defense"}
POSITION_IDS = dict(zip(POSITIONS.values(), range(9)))
RACES = ("warrior", "spellcaster", "fairy", "fiend", "zombie", "machine", "aqua",
         "pyro", "rock", "windbeast", "plant", "insect", "thunder", "dragon",
         "beast", "beast_warrior", "dinosaur", "fish", "sea_serpent", "reptile",
         "psycho", "devine", "creator_god", "wyrm", "cyberse", "illusion")
ATTRIBUTES = ("earth", "water", "fire", "wind", "light", "dark", "divine")
TYPE_BITS = (1, 2, 4, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192,
             16384, 65536, 131072, 262144, 524288, 1048576, 2097152, 4194304,
             8388608, 16777216, 33554432, 67108864)
TYPE_NAMES = ("monster", "spell", "trap", "normal", "effect", "fusion", "ritual",
              "trap_monster", "spirit", "union", "dual", "tuner", "synchro", "token",
              "quick_play", "continuous", "equip", "field", "counter", "flip",
              "toon", "xyz", "pendulum", "special", "link")
ACTIONS = {
    "summon": ("summon", 0, 4), "spsummon": ("sp_summon", 1, 3),
    "reposition": ("reposition", 2, 2), "mset": ("mset", 3, 5),
    "sset": ("set", 4, 1), "activate": ("activate", 5, 8),
    "battle": ("to_bp", 6, 0), "end": ("to_ep", 7, 0),
}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def integer(value, name, minimum=0, maximum=2147483647):
    require(type(value) is int and minimum <= value <= maximum, "Invalid " + name)
    return value


def boolean(value, name):
    require(type(value) is bool, "Invalid " + name)
    return value


def enum_value(value, table, name):
    require(type(value) is int and value in table, "Unsupported " + name)
    return table[value]


def bytes16(value):
    value %= 65536
    return [value // 256, value % 256]


def load_contract(source_root):
    root = Path(source_root).resolve(strict=True)
    sources = {}
    for relative, expected in SOURCE_HASHES.items():
        raw = source_read(root, Path(relative))
        text = raw.decode("utf-8-sig").replace("\r\n", "\n")
        digest = hashlib.sha256(text.encode("utf-8")).hexdigest()
        require(digest == expected, "Pinned source changed; review mapping before using this revision: " + relative)
        sources[relative] = text
    tree = ast.parse(sources["ygoinf/ygoinf/features.py"])
    system_strings = None
    for node in tree.body:
        if (isinstance(node, ast.Assign) and len(node.targets) == 1
                and isinstance(node.targets[0], ast.Name) and node.targets[0].id == "system_strings"):
            require(isinstance(node.value, ast.List), "Unrecognized system-string declaration")
            system_strings = [
                integer(item.value, "system string", 0, 9999)
                for item in node.value.elts if isinstance(item, ast.Constant)
            ]
            require(len(system_strings) == len(node.value.elts), "Nonliteral system string")
    require(system_strings is not None, "Missing system strings")
    code_raw = source_read(root, Path("scripts/code_list.txt"))
    vocabulary = parse_vocabulary(code_raw)
    return {
        "revision": PIN,
        "sourceHashesNormalizedLf": SOURCE_HASHES,
        "vocabularySha256": hashlib.sha256(code_raw).hexdigest(),
        "vocabularyColumns": vocabulary["columns"],
        "codes": {code: i + 1 for i, code in enumerate(vocabulary["codes"])},
        "systemStrings": {code: i + 16 for i, code in enumerate(system_strings)},
    }


def location_key(card):
    require(isinstance(card, dict), "Invalid card reference")
    controller = integer(card.get("controller"), "controller", 0, 1)
    location = integer(card.get("location"), "location", 0, 1024)
    require(location in LOCATIONS, "Unsupported card location")
    sequence = integer(card.get("sequence"), "sequence", 0, 79)
    overlay = integer(card.get("overlaySequence"), "overlay sequence", -1, -1)
    return controller, location, sequence, overlay


def single_flag(value, names, name):
    value = integer(value, name, 0, 1 << len(names))
    if value == 0:
        return "none", 0
    require(value & (value - 1) == 0 and value < 1 << len(names), "Unsupported " + name)
    index = value.bit_length() - 1
    return names[index], index + 1


def convert_card(raw, contract):
    controller, location, sequence, overlay = location_key(raw)
    position_value = integer(raw.get("position"), "position", 0, 12)
    position = enum_value(position_value, POSITIONS, "position")
    known = boolean(raw.get("known"), "known flag")
    require(integer(raw.get("overlayCount", 0), "overlay count", 0, 255) == 0,
            "Overlay state is incomplete")
    hidden = (not known or location == 1
              or (controller == 1 and (location == 2 or position_value & 10
                                      or (location == 64 and not position_value & 5))))
    code = 0 if hidden else integer(raw.get("code"), "card code", 1, 0x7fffffff)
    require(code == 0 or code in contract["codes"], "Public card is outside the selected vocabulary")
    attribute, attribute_id = single_flag(0 if hidden else raw.get("attribute"), ATTRIBUTES, "attribute")
    race, race_id = single_flag(0 if hidden else raw.get("race"), RACES, "race")
    type_value = 0 if hidden else integer(raw.get("type"), "card type", 0, 0x7fffffff)
    require(type_value & ~sum(TYPE_BITS) == 0, "Unsupported card type bits")
    types = [name for bit, name in zip(TYPE_BITS, TYPE_NAMES) if type_value & bit]
    level = 0 if hidden else integer(raw.get("level"), "level", 0, 255)
    rank = 0 if hidden else integer(raw.get("rank"), "rank", 0, 255)
    if "xyz" in types:
        level = rank
    if "link" in types:
        markers = integer(raw.get("linkMarker"), "link marker", 0, 511)
        require(markers & 16 == 0, "Unsupported center link marker")
        level = markers.bit_count()
    counter = 0 if hidden else integer(raw.get("counter", 0), "counter", 0, 65535)
    negated = False if hidden else boolean(raw.get("negated"), "negated flag")
    attack = 0 if hidden else integer(raw.get("attack"), "attack", -2, 65535)
    defense = 0 if hidden else integer(raw.get("defense"), "defense", -2, 65535)
    result = {
        "code": code, "controller": ("me", "opponent")[controller],
        "location": LOCATIONS[location], "sequence": sequence, "position": position,
        "overlay_sequence": overlay, "attribute": attribute, "race": race,
        "level": level, "counter": counter, "negated": negated,
        "attack": attack, "defense": defense, "types": types,
    }
    encoded_position = position
    if location in (1, 2, 64) and position in ("facedown_attack", "facedown_defense", "facedown"):
        encoded_position = "facedown"
    encoded = [0] * 41
    encoded[:2] = bytes16(contract["codes"].get(code, 0))
    encoded[2:12] = [
        LOCATION_IDS[LOCATIONS[location]], sequence + 1 if location in (4, 8, 16) else 0,
        controller, POSITION_IDS[encoded_position], 0, attribute_id, race_id,
        min(level, 13), min(counter, 15), int(negated),
    ]
    encoded[12:16] = bytes16(attack) + bytes16(defense)
    for name in types:
        encoded[16 + TYPE_NAMES.index(name)] = 1
    return result, encoded


def convert_snapshot(record, contract, allow_incomplete_counter_state=False):
    require(isinstance(record, dict) and record.get("schema") == SCHEMA, "Unsupported capture schema")
    require(record.get("kind") == "decision" and record.get("prompt") == "select_idlecmd",
            "Only select_idlecmd decision records are supported")
    unsupported = record.get("unsupported")
    require(isinstance(unsupported, list) and len(unsupported) <= 32
            and all(isinstance(reason, str) and re.fullmatch(r"[a-z][a-z0-9-]{0,79}", reason)
                    for reason in unsupported), "Invalid capture support flags")
    require(not unsupported, "Capture marks this snapshot incomplete: " + ", ".join(unsupported))
    observation = record.get("observation")
    require(isinstance(observation, dict), "Missing observation")
    counter_available = boolean(observation.get("counterStateAvailable"), "counter availability")
    warnings = []
    if not counter_available:
        require(allow_incomplete_counter_state, "Counter state unavailable; limited shadow override required")
        warnings.append("Counter state was not available. Counter values are placeholders; not complete model parity.")
    raw_cards = observation.get("cards")
    require(isinstance(raw_cards, list) and 2 <= len(raw_cards) <= 160, "Card row limit is 2..160")
    cards, encoded_cards, by_key = [], [], {}
    totals = Counter()
    for raw in raw_cards:
        key = location_key(raw)
        require(key not in by_key, "Duplicate card location")
        card, encoded = convert_card(raw, contract)
        cards.append(card)
        encoded_cards.append(encoded)
        by_key[key] = len(cards) - 1
        totals[key[0], key[1]] += 1
    require(all(sum(count for (player, _), count in totals.items() if player == side) <= 80
                for side in (0, 1)), "Card row limit is 80 per player")
    counts = observation.get("counts")
    require(isinstance(counts, list) and len(counts) == 2, "Missing per-player counts")
    for player, item in enumerate(counts):
        require(isinstance(item, dict), "Invalid player counts")
        for location, name in LOCATIONS.items():
            require(integer(item.get(COUNT_NAMES[name]), "zone count", 0, 80) == totals[player, location],
                    "Card rows do not match public zone counts")
    raw_global = observation.get("global")
    require(isinstance(raw_global, dict), "Missing global state")
    global_ = {
        "my_lp": integer(raw_global.get("myLp"), "own LP", 0, 65535),
        "op_lp": integer(raw_global.get("opLp"), "opponent LP", 0, 65535),
        "turn": integer(raw_global.get("turn"), "turn", 0, 1000000),
        "phase": enum_value(raw_global.get("phase"), PHASES, "phase"),
        "is_first": boolean(raw_global.get("isFirst"), "first-player flag"),
        "is_my_turn": boolean(raw_global.get("isMyTurn"), "turn-player flag"),
    }
    require(global_["phase"] in ("main1", "main2"), "Idle prompt outside a main phase")
    require(global_["is_my_turn"], "Idle prompt outside the local player's turn")
    offers = record.get("offers")
    require(isinstance(offers, list) and 1 <= len(offers) <= 24, "Legal-action limit is 1..24")
    commands, encoded_actions, mappings = [], [], []
    ids, responses = set(), set()
    for offer in offers:
        require(isinstance(offer, dict) and offer.get("type") in ACTIONS, "Unsupported idle action")
        require(offer.get("fieldOnly", False) is False, "Field-only effects are unsupported")
        identifier = offer.get("id")
        require(type(identifier) in (int, str) and not isinstance(identifier, bool), "Invalid offer ID")
        require(identifier not in ids, "Duplicate offer ID")
        ids.add(identifier)
        kind = offer["type"]
        command_name, action_code, act_id = ACTIONS[kind]
        require(integer(offer.get("actionCode"), "action code", 0, 8) == action_code,
                "Action code does not match its type")
        index = integer(offer.get("index"), "response index", 0, 32767)
        wire = integer(offer.get("wireResponse"), "wire response")
        require(wire == (index << 16) + action_code and wire not in responses,
                "Invalid or duplicate wire response")
        responses.add(wire)
        require(offer.get("isMandatory") is False, "Unexpected mandatory idle action")
        encoded = [0] * 12
        encoded[3:5] = [1, act_id]
        command = {"cmd_type": command_name, "data": None}
        if kind in ("battle", "end"):
            require(offer.get("card") is None and index == 0, "Phase response has card/index data")
            encoded[7] = 1 if kind == "battle" else 3
        else:
            key = location_key(offer.get("card"))
            require(key in by_key, "Offered card is missing from the observation")
            card_index = by_key[key]
            card = cards[card_index]
            require(card["code"] != 0, "Offered card identity is unavailable")
            description = integer(offer.get("description"), "effect description", -1, 0x7fffffff)
            card_id = contract["codes"][card["code"]]
            encoded[0] = card_index + 1
            if kind == "activate":
                require(description >= 0, "Unknown activation description")
                if description == 0:
                    encoded[6] = 1
                elif description < 10000:
                    require(description in contract["systemStrings"], "Unsupported system effect description")
                    encoded[6] = contract["systemStrings"][description]
                else:
                    effect_code, offset = description >> 4, description & 15
                    require(offset < 14, "Unsupported effect string offset")
                    require(effect_code in contract["codes"], "Effect source is outside the vocabulary")
                    card_id = contract["codes"][effect_code]
                    encoded[6] = offset + 2
            encoded[1:3] = bytes16(card_id)
            command["data"] = {
                "card_info": {key: card[key] for key in ("code", "controller", "location", "sequence")},
                "effect_description": description, "response": wire,
            }
        commands.append(command)
        encoded_actions.append(encoded)
        mappings.append({"offerId": identifier, "upstreamActionIndex": len(commands) - 1, "wireResponse": wire})
    selected = record.get("selectedOfferId")
    require(type(selected) in (int, str), "Invalid selected offer ID")
    selected_wire = integer(record.get("selectedWireResponse"), "selected wire response")
    matches = [mapping for mapping in mappings if mapping["offerId"] == selected]
    require(len(matches) == 1 and matches[0]["wireResponse"] == selected_wire,
            "Chosen action does not match a real offered response")
    global_encoded = bytes16(global_["my_lp"]) + bytes16(global_["op_lp"]) + [
        min(global_["turn"], 16), list(PHASES.values()).index(global_["phase"]),
        int(global_["is_first"]), int(global_["is_my_turn"]),
    ] + [totals[player, location] for player in (0, 1) for location in LOCATIONS] + [0]
    return {
        "status": "mapped", "modelReady": False, "warnings": warnings,
        "input": {"global": global_, "cards": cards,
                  "action_msg": {"data": {"msg_type": "select_idlecmd", "idle_cmds": commands}}},
        "unpaddedFeaturePreview": {"cards_": encoded_cards, "global_": global_encoded, "actions_": encoded_actions},
        "actionMapping": mappings, "observedChoice": matches[0],
        "limitations": [
            "No checkpoint was loaded and no action was proposed or executed.",
            "This slice has no other prompts or recurrent history; it cannot drive a complete duel.",
            "Source vocabulary membership and feature mapping do not prove checkpoint compatibility.",
            "Deck identities are redacted even when upstream training may have represented own-deck composition.",
        ],
    }


def reject_duplicates(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, "Duplicate JSON key")
        result[key] = value
    return result


def read_capture(path):
    raw = bounded_read(path, MAX_CAPTURE_BYTES)
    rows = raw.splitlines()
    require(0 < len(rows) <= MAX_ROWS, "Capture row limit exceeded")
    result = []
    for raw_row in rows:
        require(len(raw_row) <= MAX_ROW_BYTES, "Capture row size limit exceeded")
        if raw_row.strip():
            result.append(json.loads(
                raw_row.decode("utf-8-sig"), object_pairs_hook=reject_duplicates,
                parse_constant=lambda value: require(False, "Nonfinite JSON number is not allowed"),
            ))
    require(result, "Capture contains no records")
    return result, hashlib.sha256(raw).hexdigest()


def create_report(records, contract, allow_incomplete_counter_state=False):
    results = []
    skipped = Counter()
    for record in records:
        require(isinstance(record, dict) and record.get("schema") == SCHEMA, "Unsupported capture schema")
        if record.get("kind") != "decision":
            skipped[str(record.get("kind"))[:80]] += 1
            continue
        identity = {name: record.get(name) for name in ("duelId", "sequence", "prompt")}
        try:
            mapped = convert_snapshot(record, contract, allow_incomplete_counter_state)
        except (ValueError, TypeError, KeyError) as error:
            mapped = {"status": "unsupported", "modelReady": False, "reason": str(error)[:300]}
        results.append({**identity, **mapped})
    return {
        "format": "skynet-ygo-agent-shadow-v1", "upstreamRevision": PIN,
        "sourceHashesNormalizedLf": contract["sourceHashesNormalizedLf"],
        "vocabularySha256": contract["vocabularySha256"],
        "mode": "offline-shadow-no-inference",
        "allowIncompleteCounterState": allow_incomplete_counter_state,
        "mapped": sum(item["status"] == "mapped" for item in results),
        "unsupported": sum(item["status"] == "unsupported" for item in results),
        "skippedRecordKinds": dict(skipped), "decisions": results,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", required=True, type=Path)
    parser.add_argument("--capture", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--workspace-root", type=Path, default=Path.cwd())
    parser.add_argument("--allow-incomplete-counter-state", action="store_true")
    args = parser.parse_args()
    try:
        contract = load_contract(args.source_root)
        records, digest = read_capture(args.capture)
        report = create_report(records, contract, args.allow_incomplete_counter_state)
        report["captureSha256"] = digest
        encoded = (json.dumps(report, indent=2) + "\n").encode("utf-8")
        require(len(encoded) <= MAX_REPORT_BYTES, "Report exceeds the 64 MiB output limit")
        workspace = args.workspace_root.resolve(strict=True)
        output = args.output.resolve()
        require(output.is_relative_to(workspace) and output.suffix.lower() == ".json",
                "Output must be a JSON file inside the workspace")
        require(not output.exists(), "Output exists; select a new report path")
        output.parent.mkdir(parents=True, exist_ok=True)
        with output.open("xb") as stream:
            stream.write(encoded)
        print("Shadow mapping: %d mapped, %d unsupported. No model ran."
              % (report["mapped"], report["unsupported"]))
    except (OSError, UnicodeError, ValueError, SyntaxError, RecursionError) as error:
        parser.exit(2, "SKYNET shadow conversion failed: " + str(error) + "\n")


if __name__ == "__main__":
    main()
