# Instruction for a new agent

You are joining the MarkdownToDocX project (GitHub: VaderConsulting/MarkdownToDocX). It is a C#/.NET converter that turns Markdown, including Mermaid-style flowchart, enterprise (ArchiMate 3.2) and EIP diagrams, into .docx files. It has its own grid layout and routing engine. Work on branch codex/currentstatus-tasks (PR #27). Do not merge.

Read these first, in this order, before you change any code:
1. docs/currentstatus.md, the task list and the current state. Work on the open tasks in the order listed there.
2. docs/Instructions/LAYOUT-RULEBOOK.md, the only layout spec. Section 9.6 sets precedence. Section 10 lists legality faults, which nothing may break. The Routing faults list after it can be overridden by a line hint. The pictures it refers to are in docs/Instructions/pictures and docs/briefs.
3. The briefs: docs/BRIEF-PLACEMENT-HINTS.md, docs/BRIEF-BEND-TESTS.md, docs/BRIEF-CODE-SLOP-CLEANUP.md and docs/BRIEF-ENTERPRISE-SYNTAX-GAPS.md.
4. The scenario files flow.md, enterprise.md and EIP.md. Scenarios A164 to A169 are recreations of real diagrams that use placement hints, notes and plain connections.

The rulebook was revised on 12 October 2026. Rules 2.4, 2.7, 2.9, 2.10 (any-size many-to-many blocks), 2.11, 2.13, 2.16, 2.17, 2.18, 3.4 to 3.9, 4.8, 4.11, 5.5 to 5.9 and 6.1 changed or are new. Assume the code does not yet match.

## Goals
1. Audit every rule against the code and list each one that is not enforced or only partly enforced, for flowchart, enterprise and EIP diagrams.
2. Fix them in shared layout code. Write no scenario-specific code, use no fixed caps or iteration limits (stop when no progress is made), and never let the engine give up on large inputs.
3. Implement the bend-test checker in docs/BRIEF-BEND-TESTS.md, checks (a) to (j), with its unit tests, including the 2x3, 3x3 and 4x2 block tests. The baseline may only shrink.
4. Run the checker and the rule checks on every scenario in all three files. A failure is a defect to fix, never a new baseline.
5. Placement hints override all automatic placement and routing rules. Only legality beats a hint.

## Working rules
- Keep the changes focused and do no side experiments.
- Write commit messages and the PR description in native English: an imperative subject with no full stop, and a body of plain sentences on what changed and why. Mention no people, agents or review rounds.
- Keep line endings as they are: CRLF for the rulebook and briefs, LF for currentstatus.md.

## Before you finish
Update docs/currentstatus.md with each task's state and a table of rules (enforced, tested, scenarios failing). List every remaining fault with its rule number and the reason it remains. Report a short summary only, because the owner does not review individual diagrams.
