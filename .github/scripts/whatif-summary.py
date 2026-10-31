#!/usr/bin/env python3
"""Fasst das Ergebnis von `az deployment group what-if --result-format FullResourcePayloads` lesbar zusammen.

Aufruf: whatif-summary.py whatif.json  (Markdown nach $GITHUB_STEP_SUMMARY, sonst auf stdout)

Azure meldet viele Eigenschaften als Änderung, die keine sind: Standardwerte, die das Bicep nicht
angibt, oder Werte, die die API beim Lesen nicht zurückgibt. Diese «Rauschen» filtert das Skript
heraus. Übrig bleiben die echten Änderungen.
"""

import json
import os
import re
import sys

# Rauschen: (Ressourcentyp-Endung, Regex auf den Eigenschaftspfad, Art der Änderung oder None für alle).
NOISE = [
    # Container: Standard-Verschlüsselung von Azure, im Bicep nicht angegeben.
    ("/blobservices/containers", r"^properties\.(defaultEncryptionScope|denyEncryptionScopeOverride)$", None),
    # Blob-Service: Standardwert von Azure.
    ("/blobservices", r"^properties\.deleteRetentionPolicy\.allowPermanentDelete$", None),
    # Queue-Service: Logging und Metriken als Standardwerte von Azure.
    ("/queueservices", r"^properties(\..*)?$", None),
    # Web App: siteConfig liefert die API beim Lesen der Site nicht zurück, what-if hält sie für neu.
    ("microsoft.web/sites", r"^properties\.siteConfig(\..*)?$", "Create"),
    # Budget: Azure setzt Enddatum und Format des Startdatums selbst.
    ("microsoft.consumption/budgets", r"^properties\.timePeriod(\..*)?$", None),
    # Application Insights: interne Standardwerte.
    ("microsoft.insights/components", r"^properties\.(Flow_Type|Request_Source)$", None),
]

SENSITIVE_PATH = re.compile(r"(connectionstring|password|secret|key|adminlogin|login)", re.IGNORECASE)
EMAIL = re.compile(r"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")
MAX_INLINE = 80


def resource_type(resource_id):
    """Typ ohne Namen, z.B. microsoft.storage/storageaccounts/blobservices/containers."""
    tail = resource_id.split("/providers/")[-1]
    parts = tail.split("/")
    return "/".join([parts[0]] + parts[1::2]).lower()


def short_name(resource_id):
    tail = resource_id.split("/providers/")[-1].split("/")
    return f"{tail[1].split('.')[-1] if len(tail) > 1 else tail[0]} {'/'.join(tail[2::2])}".strip()


def leaves(delta, prefix=""):
    for entry in delta or []:
        path = f"{prefix}.{entry['path']}" if prefix else entry["path"]
        if entry.get("children"):
            yield from leaves(entry["children"], path)
        else:
            yield path, entry


def is_noise(rtype, path, entry):
    if entry.get("propertyChangeType") == "NoEffect":
        return True
    for type_suffix, pattern, kind in NOISE:
        if rtype.endswith(type_suffix) and re.match(pattern, path) and (kind is None or entry.get("propertyChangeType") == kind):
            return True
    return False


def mask(path, value):
    if value is None:
        return "–"
    text = value if isinstance(value, str) else json.dumps(value, ensure_ascii=False)
    if SENSITIVE_PATH.search(path):
        return "***"
    return EMAIL.sub("***", text)


def cell(text):
    text = text.replace("|", "\\|")
    if len(text) <= MAX_INLINE and "\n" not in text:
        return f"`{text}`" if text != "–" else text
    first = text.splitlines()[0][:MAX_INLINE]
    body = text.replace("\n", "<br>")
    return f"<details><summary><code>{first}…</code></summary>{body}</details>"


def summarize(data):
    real, noise = [], []
    created, deleted, unchanged = [], [], 0
    for change in data.get("changes", []):
        kind = change["changeType"]
        rid = change["resourceId"]
        rtype, name = resource_type(rid), short_name(rid)
        if kind in ("NoChange", "Ignore"):
            unchanged += 1
            continue
        if kind == "Create":
            created.append(name)
            real.append((name, "Create", "(neue Ressource)", "–", "–"))
            continue
        if kind == "Delete":
            deleted.append(name)
            real.append((name, "⚠️ Delete", "(Ressource wird gelöscht)", "–", "–"))
            continue
        rows = []
        for path, entry in leaves(change.get("delta")):
            row = (name, entry.get("propertyChangeType", kind), path, mask(path, entry.get("before")), mask(path, entry.get("after")))
            (noise if is_noise(rtype, path, entry) else rows).append(row)
        if rows:
            real.extend(rows)
        else:
            unchanged += 1
    changed = sorted({r[0] for r in real})

    out = ["## What-if", ""]
    out.append(
        f"**Echt geändert: {len(changed)} Ressourcen**"
        + (f" ({', '.join(changed)})" if changed else "")
        + f". Neu: {len(created)}. Gelöscht: {len(deleted)}. Unverändert: {unchanged}."
    )
    if deleted:
        out += ["", f"> ⚠️ **Gelöscht:** {', '.join(deleted)}"]
    if real:
        out += ["", "| Ressource | Art | Eigenschaft | vorher | nachher |", "|---|---|---|---|---|"]
        out += [f"| {n} | {k} | `{p}` | {cell(b)} | {cell(a)} |" for n, k, p, b, a in real]
    if noise:
        out += ["", f"<details><summary>Rauschen ({len(noise)}): Standardwerte von Azure, keine echte Änderung</summary>", ""]
        out += ["| Ressource | Art | Eigenschaft |", "|---|---|---|"]
        out += [f"| {n} | {k} | `{p}` |" for n, k, p, _, _ in noise]
        out += ["", "</details>"]
    return "\n".join(out) + "\n"


def main(argv):
    with open(argv[1], encoding="utf-8") as f:
        markdown = summarize(json.load(f))
    target = os.environ.get("GITHUB_STEP_SUMMARY")
    if target:
        with open(target, "a", encoding="utf-8") as f:
            f.write(markdown)
    print(markdown)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
