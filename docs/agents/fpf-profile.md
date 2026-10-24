# OSAFW FPF Library Profile

Use with [fpf.md](fpf.md). These are entry points for actual unresolved questions, not a checklist or a complete catalog. Read the selected edition's applicability, conditions and needed body before relying on a method. Return its contribution to code, a contract, a check or existing evidence; adequate repository reasoning needs no lookup.

Routes apply to framework development and copied applications. Preserve deliberate app behavior and optional app-owned `docs/agents/fpf-app.md` domain routes, examples and exclusions. Established framework terms and contract owners come first.

| Unresolved question | Source starting point | Return to repository evidence |
| --- | --- | --- |
| What does the receiving action need, and which connection is missing? | FPF Core C.39 | Requested outcome, actual inputs, controller/model/template/test consumer and acceptance; reuse sufficient results. |
| Would another investigation or check change the result enough to justify its burden? | Core C.11.DUA | Attainable answer, delay and actual decision; binding checks and permissions still govern. |
| Which source passage can answer this question? | Core F.1 | Available publications, alternative queries, relevant conditions and honest access limits. |
| Does wording or a public name hide an action-changing meaning? | Core F.19 / F.8 | Actor, object, conditions, [naming.md](../naming.md), actual callers and public contracts; do not invent types or abstractions to imitate source terminology. |
| Which observation distinguishes plausible causes of a failure? | Systems Engineering SYSE.38 | Expected/observed route, model, ParsePage or provider behavior and a bounded discriminating observation. |
| Does an access statement identify the actor and permitted action? | Core F.19 for ambiguous wording; current authorization contracts supply permission | Owner-or-system predicates, direct-id reads, POST/token enforcement, parent authorization and preserved legitimate access. |
| Which component should own a function? | Systems Engineering SYSE.5 | Required effect, existing controller/model/FW boundaries and feasible alternatives; established ownership needs no new analysis. |
| Can a representation change preserve behavior? | Computational Thinking CMP.10 | Identity, duplicates, order, missing versus zero/empty, update and memory costs. |
| Can retries or concurrent operations interfere? | Computational Thinking CMP.14 | Adverse interleavings, request/task scope, actual DB provider, side effects, isolation and idempotency. |
| Does a mapping, join, conversion or default lose meaning? | Semantic Integration SIE.7; SIE.5 for identity and SIE.10 for receiving use | IDs, ownership, cardinality, units, dates and errors across actual API/template/config consumers. |
| Which upgrade or recovery states remain valid? | Systems Engineering SYSE.34 | Old/new readers and writers, persisted state, migration map, in-flight effects and copied-app consumers. |
| Does a passing or flaky check answer the requested question? | Systems Engineering SYSE.31 | Revision/configuration/provider, intended and rejected cases, consumer use and missing required evidence under [verification.md](verification.md). |
| How should agent procedures change and be evaluated? | Systems Engineering SYSE.47 / SYSE.46 | Concrete failed transition, smallest repair, baseline comparison and representative work outcomes; [workflow.md](workflow.md) retains authority. |
| Must a procedure, its performer and its support change together? | Core C.40.JD | Separate the revised procedure, demonstrated ability under stated support, and needed tooling, access or authority. A text change alone does not establish capability; adequate existing arrangements need no development exercise. |
| Which alternatives and consequences support a feature decision? | Decision Design PSDA.5, PSDA.7, PSDA.8, PSDA.9 | The same receiving decision and explicit comparison horizon; preserve alternative formulations, differing outcomes, uncertainty, actual users and approved intent. |

Publication carriers in the referenced edition:

- Core: `FPF-Spec.md`.
- Systems Engineering: `DPFs/Engineering DPF Suite/SYSTEMS-ENGINEERING-PRINCIPLES-FRAMEWORK.md`.
- Computational Thinking: `DPFs/Foundational Thinking DPF Suite/COMPUTATIONAL-THINKING-DPF.md`.
- Semantic Integration: `DPFs/Engineering DPF Suite/SEMANTIC-INTEGRATION-ENGINEERING-PRINCIPLES-FRAMEWORK.md`.
- Decision Design: `DPFs/DECISION-DESIGN-PRINCIPLES-FRAMEWORK.md`.

Recover changed carriers and other methods through the pinned Library Reference, DPF catalogue and `Read-Fpf.ps1 -Action List -Revision <full-sha>`. A filename, similar title or coherent description does not establish equivalence or applicability. Preserve full IDs/section references, Candidate status and source limits.

Human capability methods concern people learning for later work. Fluent agent output is not evidence of improved agent capability or human learning. Physical/embodied methods need their stated domain conditions. FPF terminology does not require new classes, mandatory diagrams, forms, a decision matrix or an ADR for ordinary work. Keep app extensions contributor-safe and preserve them during instruction-pack upgrades.

Navigation adapted from Anatoly Levenchuk's [FPF Library at a5ac87ac15e78517d12bc66155b9da3ca254b643](https://github.com/ailev/FPF/tree/a5ac87ac15e78517d12bc66155b9da3ca254b643), [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). Changes: selected software questions and .NET framework contract owners; source methods must be read at use.
