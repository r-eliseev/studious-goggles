# Owner notes: things to remember

Short items the owner asked to keep. Each cites where it came from. Not sources, not decisions: candidates for the concept.

| # | Item | Status | Origin |
|---|---|---|---|
| N1 | Metrics for the "near the loop" shift: owner minutes per merged task, number of returns, approval waiting time. | remember | QA on source 01 (B/Q3, [mine]); close to the pilot metrics in source 03 ("time from spec to merge, errors after release, review minutes") |
| N2 | Set the human's mode per **action type**, not per factory (approve a spec, merge, release, ...). | remember; explained to the owner, see below | QA on source 01 (B/Q4, [mine]); resembles the per-action LoopRails ladder [ext] and source 03's remark that autonomy level depends on the repository, not on the developer or the model |
| N3 | Autonomy rises only as far as automatic checks can be trusted ("checks first, then lift review"). | remember | source 03 (verification gates the levels) |

N2 in one example: today every task passes the owner twice (plan approval, review), so the whole factory is "in the loop". Per action it could be: plan for a small, well-specified change = agent-approved, owner notified; merge of a change that passes all gates = automatic; change to the data schema or to provider keys handling = owner approves. Same factory, different modes per action, and each action's mode can be raised or lowered separately as trust in its checks grows.
