# FPF update and practical-pattern audit - 2026-11-10

## Outcome and scope

Updated the FPF reference guidance on `master`, fast-forwarded to `90e87dc7cafee8cdb51a06197c117e280c6e728b`. This is the public framework checkout. Pre-existing untracked work was preserved.

The selected candidate is `b223469ecafb9735c1d9bac220671e3336618339`, retrieved from the FPF repository on 2026-11-10. Tracked guidance previously cited `70518df35a8b876c2700e60e847406df4903c4a7`; this checkout's older local acceptance was `a5ac87ac15e78517d12bc66155b9da3ca254b643`. A merged instruction update does not adopt its edition for another checkout. Accepted the reviewed edition with `Sync-Fpf.ps1 -Action Accept -Revision b223469ecafb9735c1d9bac220671e3336618339 -ReviewNote <scoped-review>`. Subsequent Status in both supported shells and PowerShell 7 `Ensure -Offline` returned `ready`, selected the new edition, and reported no pending policy review, migration or cleanup. Index version remains 2.

Instruction pack `1.13.10` adds three optional question routes: shortening an account without losing needed meaning, comparing complete ways to obtain the same result, and choosing worthwhile workflow improvements while reducing avoidable work. The existing closeout rule now compares changes with current practice using setup, review, support and downstream rework, while preserving required results and safeguards. An already justified repair can proceed directly. Agent evaluation distinguishes a specific property check from a comparative improvement claim, and persistence from transfer. The root instructions and runtime are unchanged. Copied applications retain their own extensions, contracts and adoption state.

FPF Library author credit is centralized in the guide. Five historical summaries received attribution-only wording/link updates; their source editions, technical conclusions and verification records were preserved.

The source update retains 51 indexed source files, 403 Core patterns and 36 domain frameworks. Against the previous tracked edition, 41 files and 488 pattern change records differ; against this checkout's older acceptance, the inventory reports 49 files and 866 pattern change records. These are structural counts, not claims that every changed pattern was semantically reviewed. Reviewed changes simplify redundant wording and make evidence, migration and evaluation requirements more conditional on the actual claim and supported use. Licensing and the local retrieval contract remain unchanged.

## Pattern applicability review

This review assesses 36 FPF Library references relevant to software development, agent workflows, technical communication and improvement. GOALS and RMP include separate assessments of their focused GOALS.7 and RMP.7 methods. Whole-framework references use selected methods appropriate to repository work. English focus labels explain practical uses rather than replace the source's full titles.

**Explicit** means an existing named route; **Analogous** means current instructions contain a similar operation; **Partial** means only part is covered; **On demand** means a useful specialized method without a standing named route. These classifications concern current instructions and tooling, not observed consistency, formal conformance or measured effectiveness. The evidence column refers to [root rules](../../../AGENTS.md), [workflow](../workflow.md), [verification](../verification.md), [delegation](../delegation.md), [review routing](../review-routing.md), [review criteria](../code_reviewer.md), [architecture](../../architecture.md), the [FPF guide](../fpf.md) and [profile](../fpf-profile.md). New routes are identified in the last column.

| Reference and focus | Before this update | Existing evidence | Disposition after the audit |
| --- | --- | --- | --- |
| **F.19** - Clear technical prose | Explicit | FPF profile: wording and access-language routes. | Keep actor, action, conditions and contract meaning; remove caveats only when they add no useful distinction. |
| **C.11.DUA** - Useful advice and evidence burden | Explicit | FPF profile and verification: choose checks that can change the result. | Give the strongest supported answer with decision-changing limits; required checks and permissions still apply. |
| **C.39.QR** - Return to the original question | Explicit | FPF profile; workflow: consume intermediate results against the intended outcome. | Retain the existing route; a source extract or passing component check does not alone complete the request. |
| **GOALS** - Construct and regulate goals | Partial | Workflow: result, constraints, acceptance and scope; delegation: bounded packets. | Use selected goal methods for competing goals or changing commitments. A full organization-wide goals practice is outside routine coding scope. |
| **GOALS.7** - Assign and continue goal-directed AI work | Analogous | Workflow and delegation: output, ownership, stop conditions, handoff and authority. | Use for complex delegated work when those relations are unclear; a broad goal grants no new permissions. |
| **A.15.10** - Resume and hand off interrupted work | Analogous | Workflow: recover outcome, instructions, established result, unfinished work and next evidence. | Retain existing guidance; inspect actual effects before retrying. No additional checkpoint form. |
| **B.5.RR** - Revise after a changed premise | Analogous | Workflow: revisit dependent conclusions and checks when a requirement, source, assumption or result changes. | Use for consequential corrections; keep still-valid evidence and revise affected conclusions. |
| **B.5.SD** - Develop a substantive discussion | Partial | Review procedure: evidence-based findings and resolution of disagreements. | Use when an objection reveals a missing argument or alternative; produce a revised answer or precise unresolved decision. |
| **EXD** - Design an explanation | On demand | General receiving-use guidance; no dedicated current EXD route. | Use for developer guidance or onboarding when the reader cannot make a specific inference or take the next action. |
| **NSTD** - Build and assess a narrative | On demand | No dedicated current narrative route. | Useful for tutorials, handovers and coherent accounts from notes. Preserve source meaning and intended voice; routine fixes need no story. |
| **A.6.3.CSC** - Shorten an account safely | Partial | Meaning and contract preservation already required; dedicated compression route added in this update. | Compare the shorter text with its source for the declared use; retain action-changing conditions and access to omitted detail. |
| **C.39.WA** - Explain the whole subject | Explicit | FPF profile: organizing grounds, relations and their effect on choices about parts. | Use for framework explanations and architecture; isolated component descriptions may leave the larger question unanswered. |
| **NOT** - Make notation usable | On demand | Repository uses tables, schemas and diagrams; this alone does not establish NOT application. | Use when a representation obstructs reading, tracing dependencies or editing. Check the actual operation the reader needs. |
| **F.0.1** - Recover source-local meaning | Analogous | Root rules and FPF guide: actual contracts, source conditions and complete references. | Use when a familiar term is ambiguous; establish what that author means before mapping it to framework terminology. |
| **F.0.2** - Synthesize across sources | On demand | Sources are checked against current code; no dedicated cross-source synthesis route. | Use for conflicting upstream and application accounts; preserve differences and identify any new construction as ours. |
| **F.1** - Find and select sources | Explicit | FPF profile and guide: question-led search, alternate queries and access limits. | Keep the local route; inspect relevant source bodies and reformulate failed searches. |
| **KCAE** - Engineer knowledge access | Analogous | Local snapshots, exact revisions, bounded reader, checksums, continuation and repository search routes. | Use for a real retrieval failure or corpus-maintenance problem; distinguish finding a passage from establishing its relevance. |
| **A.10** - Ground claims in evidence | Analogous | Verification and summaries tie claims to actual commands, sources, scope and omissions. | For consequential claims retain a recoverable source-to-use account. Add an explicit evidence graph only when the use needs it. |
| **RMP** - Conduct research | On demand | No blanket research process in normal development; source and verification practices supply part of the need. | Use selected methods for a real research question, not for every lookup or implementation task. |
| **RMP.7** - Synthesize research evidence | On demand | No dedicated current research-synthesis route. | Use for competing research findings; track shared datasets/results, disagreements and limits rather than counting repeated reports as independent evidence. |
| **B.5.2** - Generate rival explanations | Analogous | Existing SYSE.38 diagnosis route asks for observations that distinguish plausible causes. | Use when the first plausible diagnosis is underdetermined; compare rivals before choosing a fix. |
| **C.28.CM** - Construct and challenge causal models | Partial | Diagnosis and verification trace actual paths and distinguish claims from observations. | Use for causal performance or failure claims; state the mechanism, alternatives and discriminating observations. |
| **A.7** - Keep unlike things distinct | Analogous | Verification distinguishes reviewed/executed/passed; profile distinguishes procedure, capability and support. | Retain distinctions among plan, execution, tool availability, observation and permission. |
| **C.39** - Find a way to obtain a result | Explicit | FPF guide/profile and workflow start with receiving use, constraints and remaining gaps. | Use where the means or a consequential connection is missing; an adequate existing answer can finish the task. |
| **C.40** - Construct genuinely different variants | Partial | PSDA already routes alternative generation; C.40.JD covers a different question. Direct C.40 route added. | Construct and examine actual variations, retaining useful differences for a next operation. Renamed options or novelty alone are insufficient. |
| **PSDA** - Design and compare decisions | Explicit, selected methods | Profile routes PSDA.5, PSDA.7, PSDA.8 and PSDA.9. | Use alternate formulations, consequences, uncertainty and reversal conditions. Different outcomes need explicit comparison; generation is not selection. |
| **A.1.STM** - Connect system and project reasoning | Analogous | Architecture and workflow trace receiving use, responsibilities, actual consumers and acceptance. | Use when project boundaries or creator/product responsibilities are confused; reconnect use, functioning, construction and obtaining the means. |
| **C.32** - Develop alternative architectures | Partial | Architecture guide owns boundaries and permits justified alternatives; no direct C.32 route. | Use for structurally different configurations of an explicitly identified system, after establishing the C.30 architecture question. Routine extensions follow established owners. |
| **C.38** - Compare complete ways to the same result | Partial | Smallest sufficient change and compatibility cost already matter; direct comparison route added. | Compare reuse, build, buy and AI assistance for the same outcome, including integration, operation, maintenance and exit. |
| **C.11.CRC** - Compare a change with the incumbent | Partial | Workflow and architecture require a concrete benefit; baseline comparisons exist. Direct route added. | Keep current practice as a real alternative; include transition and ongoing burden and consequences shifted to other participants. |
| **C.43** - Connect observation, choice and action | Analogous | Execution, verification, changed-premise handling and bounded review form a feedback process. | Use when observations fail to affect the next action. Applying the pattern does not authorize a new runtime control feature. |
| **STR.18** - Make strategy commitments fit | On demand | Task scope and constraints cover only part of strategy; no full strategy process is claimed. | Useful for framework roadmaps and adoption plans when value, means, people and resources are known. Owners must supply real commitments. |
| **C.39.WI** - Choose a worthwhile improvement | Partial | Workflow promotes verified recurring causes; SYSE.47/46 routes procedure repair and evaluation. | Optional route added for choosing the next worthwhile improvement; compare a concrete friction or blocked result with current practice and the full change burden. An already justified repair proceeds directly. |
| **C.42** - Remove avoidable work | Analogous | Direct execution, selective delegation, bounded search, proportional verification and no duplicate tests. | Optional route and existing closeout rule refined to account for setup, review, support and downstream rework. Preserve required results and safeguards; fewer tokens alone do not establish improvement. |
| **E.23** - Evaluate an improvement loop | Analogous | Review gates, severity-based stopping and verification own the existing improvement loop. | For substantive changes compare the actual result, preserve working behavior and stop when the selected acceptance is met. |
| **HCD** - Develop human capability | Limited, intentional | FPF profile explicitly distinguishes human learning from fluent agent output. | Useful for developer onboarding and practice with feedback and later transfer checks. Editing agent instructions does not train model parameters or prove human learning. |

Specialized methods remain available when their particular receiving question arises. Source methods can help with reasoning; real facts, resources, permissions, commitments and evidence of human learning still require their actual sources. No measured improvement in speed, token use or defect rate is claimed.

## Source review and local retrieval

All source-method readings used `b223469ecafb9735c1d9bac220671e3336618339`. Read the complete `USING-FPF.md`, `Readme.md`, `LICENSING.md` and `FPF-LIBRARY-REFERENCE.md` section `Library.Reference:11.1`. Inspected the complete file inventory and compared affected current profile passages with the previous tracked edition. `LICENSE` and `LICENSING.md` are unchanged. Existing source IDs and carriers remain valid. The previous merged adoption summary supplies historical review context for the earlier edition, not current authority.

Reviewed Core bodies: `F.19`, `C.11.DUA`, `C.39.QR`, `A.15.10`, `B.5.RR`, `B.5.SD`, `A.6.3.CSC`, `C.39.WA`, `F.0.1`, `F.0.2`, `F.1`, `A.10`, `B.5.2`, `C.28.CM`, `A.7`, `C.39`, `C.40`, `C.32`, `C.38`, `C.11.CRC`, `C.43`, `C.39.WI`, `C.42` and `E.23` in `FPF-Spec.md`. For `A.1.STM`, read its entry, attention map and operative sections 4.1-4.4; this was a targeted application-fit assessment. Selected methods are Stable; E.23 is specifically a Method-description pattern. A whole framework retains its own evolving status.

Whole-document references were assessed through their declared scope/overview and the following selected methods, not by reading each whole DPF:

| Document | Local publication carrier | Selected complete method bodies |
| --- | --- | --- |
| GOALS | `DPFs/Management DPF Suite/GOAL-CONSTRUCTION-AND-REGULATION-PRINCIPLES-FRAMEWORK.md` | GOALS.7; overview excerpt only |
| EXD | `DPFs/EXPLANATION-DESIGN-PRINCIPLES-FRAMEWORK.md` | EXD.6 |
| NSTD | `DPFs/Narrativization-and-Narrative-Studies-Principles-Framework.md` | NSTD.3 |
| NOT | `DPFs/Foundational Thinking DPF Suite/NOTATIONAL-ENGINEERING-DPF.md` | NOT.7 |
| KCAE | `DPFs/Engineering DPF Suite/KNOWLEDGE-CORPUS-ACCESS-ENGINEERING-PRINCIPLES-FRAMEWORK.md` | KCAE.DELIVER |
| RMP | `DPFs/RESEARCH-METHOD-PRACTICE-PRINCIPLES-FRAMEWORK.md` | RMP.7 |
| PSDA | `DPFs/DECISION-DESIGN-PRINCIPLES-FRAMEWORK.md` | PSDA.1, PSDA.5, PSDA.7, PSDA.8, PSDA.9 |
| HCD | `DPFs/Human Capability Engineering DPF Suite/HUMAN-CAPABILITY-DEVELOPMENT-PRINCIPLES-FRAMEWORK.md` | HCD.1, HCD.3, HCD.12 |

Also read complete `STR.18` in `DPFs/Management DPF Suite/STRATEGY-PRINCIPLES-FRAMEWORK.md` and complete `SYSE.46` in `DPFs/Engineering DPF Suite/SYSTEMS-ENGINEERING-PRINCIPLES-FRAMEWORK.md`. The latter's sections 4.1, 4.2 and 4.4 supply the refined evaluation route. Complete new profile entries were assessed under their source conditions: CSC's ordinary shortening branch preserves meaning for a bounded use; stronger construction/transfer claims require its separate exact branch. C.38 establishes comparable complete ways; C.40 constructs and examines variations; C.11.CRC compares a finite proposed change with the incumbent. None of those supplies permission or a final choice.

Use the existing local helper with the full edition and exact source ID, for example:

```powershell
pwsh -NoProfile -File docs/agents/tools/Read-Fpf.ps1 -Action Read -Revision b223469ecafb9735c1d9bac220671e3336618339 -Path FPF-Spec.md -PatternId A.6.3.CSC
```

Continue with the returned NextLine/NextColumn when truncated, preserving the selector. The guide explains the same process for a DPF carrier, subsection or whole-document discovery. Search and reading remain local with zero Git calls; only explicit writable refresh/recovery uses Git. No online-reading-service links were added to maintained repository files.

Adapted from [FPF Library at the reviewed edition](https://github.com/ailev/FPF/tree/b223469ecafb9735c1d9bac220671e3336618339), under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). See [author attribution](../fpf.md#attribution-and-verification). Changes: English practical-use summaries, repository evidence mapping and selective software-development routes. No endorsement is implied. External research cited inside source methods was not independently verified.

## Verification and review

Passed:

- `pwsh -NoProfile -File docs/agents/tools/Test-AgentInstructions.ps1` and `powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs/agents/tools/Test-AgentInstructions.ps1`: all 14 groups.
- `Normalize-TextFiles.ps1 -Check` on all 11 changed files, including workflow and the five attribution-only historical edits; `git diff --check`: strict UTF-8 without BOM and CRLF.
- Exact-ID/carrier validation for all 29 profile references; the review table retains all 36 reviewed references exactly once. Both supported shells passed the 14 instruction-validation groups after the attribution and workflow changes.
- Full bounded `USING-FPF.md` reads and a small `A.6.3.CSC` page plus its exact continuation in both PowerShell 7 and Windows PowerShell 5.1.
- Both licensing files match each of the two baselines. The public files contain exactly one author-name credit, in the guide, with local attribution links elsewhere. FPF source references retain upstream edition links and local retrieval instructions. The changed guidance/report contain no private machine paths or non-English prose.
- `git ls-remote https://github.com/ailev/FPF.git refs/heads/main` reconfirmed the same candidate after the source audit.

Manual routing checks:

- An adequate local correction remains direct; none of the new routes forces an FPF lookup.
- Shortening an instruction must preserve a consequential exception, permission boundary or evidence limit; a summary that drops one fails the new compression route. Source access and a return condition support narrower use, not formal conformance or proof of source truth.
- A code-generation option cannot be compared with a complete delivery option just by counting output tokens. The same-result route includes integration, verification, ongoing support and exit; uncertainty about actual means remains explicit.
- Resolving all 29 source IDs supports that structural property. It does not establish faster development; a comparative performance claim would require a real baseline and representative work. Persistence and transfer require the corresponding delayed or changed-condition evidence.
- An optional method's cost argument does not waive a required check or grant permission to buy, deploy, send, publish or change shared data.
- A proposed shortcut that shifts reconstruction or review to another participant must include that work in the comparison. An adequate existing procedure may remain; a known sufficient correction proceeds without an additional improvement survey.

These are bounded routing/read walkthroughs, not measured agent trials. A fresh independent `reviewer_high` with the agent-workflow overlay found no blocking issues in the revised policy and attribution diff. The same source edition was reaccepted for the updated policy; both supported shells and the offline check returned ready with no pending policy review, migration or cleanup. The supplemental audit confirmed the reference counts, source inventory, attribution, local adoption state and factual consistency, with no remaining blocking issues. Final adjudication: no blocking findings; review loop can stop.

The discovery stage used three disjoint read-only packets with the default fast profile. Initial outputs stopped at ordinary pagination and were not accepted as completed evidence. Two packets completed their continuations; one required a replacement `discovery_astra_low` reader after repeated incomplete returns. The primary consumed those corrected method/evidence assessments, owned all edits and evaluated adoption separately. This observed retrieval problem is already covered by the existing continuation rule; no duplicate policy was added. No timing or productivity comparison was performed.

Application builds, database/browser checks and cache lifecycle suites are not required: this change only affects reference guidance and its report; helper implementations and runtime are unchanged. The entire library, downstream application execution, human learning outcomes and general agent-performance improvement were not tested.
