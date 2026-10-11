# Working practices for cloud agents

These are spelled out in full on purpose. Some of them will seem obvious, but follow every one exactly.

## Baseline and test counts
1. Before changing any code, run `dotnet test` on every test project named in the briefing, and write down the passed and failed counts and the names of the failing tests. That is the baseline.
2. The counts in the briefing are always plain `dotnet test` results for one test project. Never build your own counter, script or batch tool to produce a different number, and never compare against one.
3. To compare with the base branch, check it out in a separate git worktree (`git worktree add ../base <branch>`). Never run `git checkout`, `git stash` or `git reset` on the base branch in your own working tree, because that can wipe your uncommitted work.
4. Compare failing tests by name. A test that passed on the base branch and fails on yours is a regression and must be fixed before you finish. A test that already failed on the base branch is not yours to fix unless the briefing asks.

## Committing and pushing
5. Commit and push to the working branch after every change that builds and doesn't make any test count worse. Don't hold more than about 30 minutes of uncommitted work.
6. Before every commit, run `git status` and make sure only files you meant to change are staged. Never commit `extern/Naiad/.editorconfig` or `extern/Naiad/src/Shared.sln.DotSettings`.
7. Commit messages, PR titles and PR descriptions follow `COMMIT-MESSAGE-STYLE.md`.
8. Push only to the working branch named in the briefing. Never merge, never force-push, never open a second PR, and keep a draft PR as a draft.

## Staying on track
9. Work towards the goal in the briefing, not a smaller one. Don't stop and report while a required fix is still undone, unless you are genuinely stuck.
10. If the same test still fails after three different attempts, stop trying variations. Write down exactly what fails, what you tried and why it didn't work, then move on to the next item and include it in your report.
11. Keep fixes general. Never tune code for one diagram, node or line, and never edit, weaken or delete an existing test, threshold or fixture to make it pass. List clashes for the owner to decide.
12. Rebuild before rerunning tests (`dotnet build`, or `dotnet test` without `--no-build`), so you're never testing stale code.
13. Delete any temporary debug tests or scripts before committing.

## Cost
14. Work alone, with no helper agents, CI watchers or side experiments. Run filtered tests while you work, and do a full run of every test project at each milestone and before your final report.

## Final report
15. Rerun every test project on the final pushed commit, then report passed and failed counts next to the baseline, plus the names of any tests that fail now but passed on the base branch.
16. Render the pictures from the final pushed commit, save them under the artifacts folder, and list every path in the report.
17. List the commits you pushed, with hashes and subjects, and anything you could not do, with the reason.

18. Rules must be generic. No layout, routing or other engine rule may depend on a test name, sample number, file name, node id, node label, group title or any other attribute that identifies a particular diagram. Base every rule only on generic structural or geometric attributes, such as rank, number of parents and children, node side, whether ports are free or taken, whether ends line up, box and frame bounds, and group nesting. Before each commit, search the diff for checks on specific ids, labels, sample or test names in engine code and replace them with generic rules. Say in the final report that you checked.

19. Run only the tests that cover your change. While working on a fix, run just the tests for the code you changed (use `dotnet test --filter`). Record the master baseline once, at the start, in the separate worktree, and don't rerun it. Run the full suites only once, on the final commit, for the report. Don't rerun a test to check whether it's flaky; read its failure message instead.

## Keeping merges easy
20. Keep one branch and one PR per goal. Branch from the base branch named in the briefing and never from another working branch.
21. Before you start, and again before the final report, merge the latest base branch into your branch and fix conflicts yourself. Run the tests after each merge.
22. Make small commits that each do one thing. Never mix a code change, a documentation change and a generated file in one commit.
23. Never commit generated files: images, SVGs, DOCX files, build output or test results. Save them under the artifacts folder only.
24. Don't reformat, rename or move code you aren't changing. A line you did not need to touch must not appear in the diff.
25. Edit only the files the task needs. Don't edit shared documents such as `README.md` or `docs/currentstatus.md`, unless the briefing asks. Report status in your final message instead.
26. Build each commit on the previous commit of your own branch. If you need work from another branch, bring it in with git (merge or cherry-pick) so the history is kept. Never retype or copy-paste it.
27. Before the final report, run `git diff --stat <base>...HEAD` and check that every file listed belongs to the task. Include that list in the report.
