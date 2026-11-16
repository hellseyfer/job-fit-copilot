# Design

## Context

Stage 5 ends the strong-fit route at `finalize-cover-letter`, inside one synchronous in-process run. Agent
Framework supports human-in-the-loop with a `RequestPort<TRequest, TResponse>`: a message sent to the port is
surfaced as a `RequestInfoEvent` with an `ExternalRequest`, the run halts with `RunStatus.PendingRequests`, and
`ExternalRequest.CreateResponse(...)` resumes it. With a `CheckpointManager` the runtime checkpoints at the end
of every superstep, and `InProcessExecution.ResumeAsync(workflow, checkpointInfo, manager)` restores a run.

## Goals / Non-goals

Goals: a real pause of the same run, durable across restarts, with one decision per pause.
Non-goals: auth, run expiry, notifications.

## Decisions

- **Graph.**
  ```
  … finalize-cover-letter ──► review-cover-letter (RequestPort<CoverLetterReview, ReviewDecision>) ──► apply-review
                                                                                   ├──► output (approved | rejected)
                                                                                   └──► write-cover-letter (rejected with feedback)
  ```
  `finalize-cover-letter` now sends a `CoverLetterReview` (the outcome so far) to the port instead of yielding the
  final output; `apply-review` yields the final `AnalysisOutcome` or sends a `LetterDraft` with the feedback back
  to the writer. `plan-study` still yields directly.
- **Checkpoint store.** `PostgresCheckpointStore : JsonCheckpointStore` over the existing `NpgsqlDataSource`, table
  `workflow_checkpoints (session_id, checkpoint_id, parent_id, data json, created_at)`, plugged with
  `CheckpointManager.CreateJson(store)`. Plain SQL with Npgsql: the data is opaque JSON keyed by session, not
  vectors, so VectorData does not fit. The column is `json`, not `jsonb`: `jsonb` reorders keys and the
  checkpoint relies on System.Text.Json metadata order.
- **Run store.** `analysis_runs (run_id, checkpoint_id, request jsonb, status, result jsonb, feedback_rounds,
  updated_at)`. `runId` is the workflow session id. The pending `ExternalRequest` is not stored: the spike showed
  that `ResumeAsync` from a checkpoint re-emits the pending `RequestInfoEvent` with its data, and the response is
  created from it. Both tables are created on first use (`CREATE TABLE IF NOT EXISTS`),
  like the vector collection.
- **One decision per pause.** The approval endpoint claims the run with
  `UPDATE analysis_runs SET status = 'resuming' WHERE run_id = @id AND status = 'awaiting-approval' RETURNING …`;
  zero rows → 409 (or 404 when the run does not exist). On a model failure while resuming the status goes back
  to `awaiting-approval` and the response is 502, so the reviewer can retry. A claim older than a 15-minute
  lease (the API stopped mid-resume) can be claimed again, so an interrupted run is not stuck in `resuming`.
- **Feedback** goes to the writer as `<reviewer_feedback>` untrusted data in the rewrite prompt; the verification
  loop restarts at attempt 1 for the new letter. `feedback_rounds` caps rejections with feedback at 3.
- **Status values:** `awaiting-approval`, `approved`, `rejected`, `completed` (weak fit), `resuming` (internal).

## Risks / Trade-offs

- Agent Framework's checkpoint format is internal; checkpoints from one package version may not resume on
  another. Acceptable for a demo; noted in the README.
- Resuming rebuilds the workflow from current configuration (threshold) — a changed threshold does not affect a
  paused run because routing already happened.
- Old runs and checkpoints accumulate; cleanup is out of scope.
