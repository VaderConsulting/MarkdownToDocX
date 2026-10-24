#!/usr/bin/env python3
"""Patch rules.json from rules.yaml while preserving the legacy PowerShell JSON layout."""
from __future__ import annotations

import re
import subprocess
import sys
from pathlib import Path
from typing import Any

import yaml

BASE_REF = "1ffc6af9:docs/Instructions/rules.json"
BASE_YAML = "1ffc6af9:docs/Instructions/rules.yaml"


def git_show(ref: str) -> str:
    return subprocess.check_output(["git", "show", ref], text=True, cwd=Path(__file__).resolve().parents[2])


def escape_string(value: str) -> str:
    pieces: list[str] = []
    for ch in value:
        code = ord(ch)
        if ch == "\\":
            pieces.append("\\\\")
        elif ch == '"':
            pieces.append('\\"')
        elif ch == "\n":
            pieces.append("\\n")
        elif ch == "\r":
            pieces.append("\\r")
        elif ch == "\t":
            pieces.append("\\t")
        elif ch == "'":
            pieces.append("\\u0027")
        elif code < 32:
            pieces.append(f"\\u{code:04x}")
        else:
            pieces.append(ch)
    return "".join(pieces)


def emit_object_fields(obj: dict[str, Any], indent: int, lines: list[str]) -> None:
    keys = list(obj.keys())
    for index, key in enumerate(keys):
        value = obj[key]
        last = index == len(keys) - 1
        pad = " " * indent
        comma = "" if last else ","
        if isinstance(value, str):
            lines.append(f'{pad}"{key}":  "{escape_string(value)}"{comma}')
        elif isinstance(value, bool):
            lines.append(f'{pad}"{key}":  {"true" if value else "false"}{comma}')
        elif isinstance(value, int):
            lines.append(f'{pad}"{key}":  {value}{comma}')
        elif isinstance(value, float):
            if value.is_integer():
                value = int(value)
            lines.append(f'{pad}"{key}":  {value}{comma}')
        elif value is None:
            lines.append(f'{pad}"{key}":  null{comma}')
        elif isinstance(value, list):
            if not value:
                lines.append(f'{pad}"{key}":  [')
                lines.append("")
                lines.append(f'{" " * 35}]{comma}')
            elif all(isinstance(item, str) for item in value):
                lines.append(f'{pad}"{key}":  [')
                inner = " " * 35
                for item in value:
                    lines.append(f'{inner}"{escape_string(item)}",')
                lines.append(f"{pad}]{comma}")
            else:
                raise TypeError(f"unsupported array field {key}")
        else:
            raise TypeError(f"unsupported field {key}: {type(value)}")


def emit_array_item(obj: dict[str, Any]) -> str:
    lines: list[str] = ["                     {"]
    emit_object_fields(obj, 25, lines)
    lines.append("                     },")
    return "\n".join(lines)


def normalize_item(item: dict[str, Any]) -> dict[str, Any]:
    item = dict(item)
    text = item.get("text")
    if isinstance(text, str):
        item["text"] = [text]
    return item


def normalize_collection(items: list[dict[str, Any]]) -> list[dict[str, Any]]:
    return [normalize_item(item) for item in items]


def items_by_id(items: list[dict[str, Any]]) -> dict[str, dict[str, Any]]:
    return {item["id"]: normalize_item(item) for item in items}


def find_rule_block_span(text: str, rule_id: str) -> tuple[int, int] | None:
    marker = f'"id":  "{rule_id}"'
    index = text.find(marker)
    if index < 0:
        return None
    start = text.rfind("                     {", 0, index)
    if start < 0:
        return None
    depth = 0
    position = start
    while position < len(text):
        if text.startswith("{", position):
            depth += 1
            position += 1
            continue
        if text.startswith("}", position):
            depth -= 1
            position += 1
            if depth == 0:
                end = text.find(",", position)
                if end < 0:
                    end = position
                else:
                    end += 1
                return start, end
            continue
        position += 1
    return None


def replace_or_insert_rule(text: str, rule_id: str, block: str) -> str:
    span = find_rule_block_span(text, rule_id)
    if span:
        return text[: span[0]] + block + text[span[1] :]
    anchor = find_rule_block_span(text, "4.1")
    if anchor is None:
        raise RuntimeError("Could not find anchor rule 4.1 for insertion")
    return text[: anchor[0]] + block + text[anchor[0] :]


def scalar_changed(old_doc: dict[str, Any], new_doc: dict[str, Any], key: str) -> bool:
    return old_doc.get(key) != new_doc.get(key)


def patch_top_scalar(text: str, key: str, value: str) -> str:
    pattern = rf'("{key}":  )"[^"]*"'
    return re.sub(pattern, rf'\1"{escape_string(value)}"', text, count=1)


def collection_changed(old_items: list[dict[str, Any]], new_items: list[dict[str, Any]]) -> list[str]:
    old_map = items_by_id(old_items)
    new_map = items_by_id(new_items)
    changed: list[str] = []
    for item_id, item in new_map.items():
        if item_id not in old_map or item != old_map[item_id]:
            changed.append(item_id)
    for item_id in new_map:
        if item_id not in old_map:
            changed.append(item_id)
    return sorted(set(changed))


def patch_collection(text: str, collection_key: str, new_items: list[dict[str, Any]], changed_ids: list[str]) -> str:
    new_map = items_by_id(new_items)
    for item_id in changed_ids:
        block = emit_array_item(new_map[item_id])
        if collection_key == "rules":
            text = replace_or_insert_rule(text, item_id, block)
        else:
            span = find_rule_block_span(text, item_id)
            if span is None:
                raise RuntimeError(f"Missing {collection_key} item {item_id} in reference json")
            text = text[: span[0]] + block + text[span[1] :]
    return text


def main() -> int:
    repo = Path(__file__).resolve().parents[2]
    yaml_path = Path(__file__).resolve().parent / "rules.yaml"
    json_path = Path(__file__).resolve().parent / "rules.json"
    ref_text = git_show(BASE_REF)
    old_doc = yaml.safe_load(git_show(BASE_YAML))
    new_doc = yaml.safe_load(yaml_path.read_text(encoding="utf-8"))
    text = ref_text
    for key in ("version", "generated_from", "built", "render_file", "line_ending"):
        if scalar_changed(old_doc, new_doc, key):
            text = patch_top_scalar(text, key, str(new_doc[key]))
    for collection in ("sections", "terms", "rules", "parameters", "precedence", "entries"):
        changed = collection_changed(old_doc.get(collection, []), new_doc.get(collection, []))
        if changed:
            text = patch_collection(text, collection, new_doc[collection], changed)
    json_path.write_text(text, encoding="utf-8", newline="\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
