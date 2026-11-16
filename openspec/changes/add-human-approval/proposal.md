# Proposal

## Why

The cover letter is the only output the candidate sends to someone else, and an LLM wrote it. The roadmap
promises a human approves it before it is used. Approval can take minutes or days, so the workflow must
really pause, survive API restarts, and resume the same run where it stopped.

## What Changes

- After `finalize-cover-letter`, the strong-fit route stops at a `review-cover-letter` request port
  (Agent Framework `RequestPort`) and the run is checkpointed in PostgreSQL.
- `POST /api/analyses` returns **202** with a `runId`, `status` `awaiting-approval` and the result so far
  (including the letter) on the strong-fit route; the weak-fit route still returns 200 with `status` `completed`.
  **BREAKING** for strong-fit clients that expected 200.
- New `GET /api/analyses/{runId}` returns the run's current status and result.
- New `POST /api/analyses/{runId}/approval` with `{ approved, feedback? }` resumes the run from its checkpoint:
  - approved → 200, `status` `approved`;
  - rejected without feedback → 200, `status` `rejected`;
  - rejected with feedback → the writer rewrites with the reviewer's feedback, the new letter is verified,
    and the run pauses again (202 `awaiting-approval`), at most 3 human rejections with feedback.
- Unknown run → 404; a run that is not awaiting approval, or a concurrent second approval → 409; invalid body → 400.

## Capabilities

### New Capabilities
- `cover-letter-approval`: pausing a strong-fit analysis for human approval, persisting the run, and resuming
  it with an approval, a rejection, or a rejection with feedback.

### Modified Capabilities
- `job-analysis-workflow`: the strong-fit response becomes 202 with a run id and status, and the graph gains
  the `review-cover-letter` port and the `apply-review` node.

## Impact

- Code: new `src/JobFit.Api/Approvals/` (Postgres checkpoint store, run store, endpoints); `JobAnalysisWorkflow`
  graph, run and resume; `CoverLetterWriter` accepts reviewer feedback.
- Data: two tables created on first use, `workflow_checkpoints` and `analysis_runs`, in the existing database.
- Tests: API tests for validation and 404; integration tests for approve, reject, reject with feedback, 409 and
  resuming after the API restarts.

## Out of scope

- Authentication and who may approve (single-user demo).
- Expiring or cleaning up old runs and checkpoints.
- Notifications when a letter is waiting.
- Approval of the weak-fit study plan.
