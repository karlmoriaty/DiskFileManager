import json
import os
import re
import urllib.error
import urllib.request

cred_path = os.path.expanduser("~/.git-credentials")
token = None
with open(cred_path, encoding="utf-8", errors="ignore") as handle:
    for line in handle:
        if "github.com" in line:
            match = re.match(r"https://([^:]+):([^@]+)@github\.com", line.strip())
            if match:
                token = match.group(2)
                break

if not token:
    raise SystemExit("NO_TOKEN")

body = r"""## What does the pull request do

Restores the P2 changes that never reached `main`. PR #7 was merged into `feature/p1-hardening`, but that branch had already been merged into `main` a few seconds earlier, so `main` currently lacks the About dialog, deletion safeguards, the permission hint, recent targets and the startup-stall fix.

This PR merges `origin/feature/p1-hardening` (which contains the #7 merge) into a branch taken from `main`. The merge is clean and the full suite passes on the main baseline.

## Current behavior on main

- `main` has P1 (duplicate preview, memory-bounded scan, log retention, coverage).
- `main` does **not** have P2: no About window, no double confirmation for large permanent deletes, no permission hint, no recent target folders, and the startup stall on slow removable drives is still present.

## Expected behavior

- `main` contains P1 and P2 together, matching the intended state.

## How implemented

- No new code: a merge commit of `origin/feature/p1-hardening` into a branch from `main`.
- Verified on the merged tree: `dotnet build` 0 warnings / 0 errors, `dotnet test` 92/92.

## Checklist

- [x] Clean merge, no conflicts
- [x] Build: 0 warnings / 0 errors
- [x] Tests: 92/92 pass
- [ ] Manual check after merge

## Note

Merge this before starting the UI work so the visual overhaul is based on the complete codebase.
"""

payload = json.dumps({
    "title": "Bring P2 changes into main (restore stacked merge)",
    "head": "fix/p2-into-main",
    "base": "main",
    "body": body
}).encode("utf-8")

request = urllib.request.Request(
    "https://api.github.com/repos/karlmoriaty/DiskFileManager/pulls",
    data=payload,
    headers={
        "Authorization": "token " + token,
        "Accept": "application/vnd.github+json",
        "Content-Type": "application/json",
        "User-Agent": "DiskManager-PR",
    },
    method="POST",
)

try:
    with urllib.request.urlopen(request, timeout=30) as response:
        data = json.load(response)
        print("STATUS=" + str(response.status))
        print("NUMBER=" + str(data.get("number")))
        print("URL=" + str(data.get("html_url")))
except urllib.error.HTTPError as error:
    print("HTTP_ERROR=" + str(error.code))
    print(error.read().decode("utf-8", errors="replace"))
    raise SystemExit(1)