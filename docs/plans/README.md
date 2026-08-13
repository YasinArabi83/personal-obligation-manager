# docs/plans/

One file per task, never a single growing plan. See `AGENTS.md`  for when a plan file is required.

## Naming

`NNNN-short-slug.md` — 4-digit zero-padded, incrementing across the whole project (not per-phase). Example: `0001-obligation-crud.md`, `0002-recurrence-engine.md`.

## Lifecycle

1. Copy `0000-template.md`, rename it with the next number and a short slug.
2. Fill it in **before** writing any code.
3. If the task is large, wait for approval on the plan before implementing.
4. When the task is done, change `Status: Draft` to `Status: Done` at the top — don't delete the file. It stays as history.
5. Never add a second, unrelated task to an existing plan file.
