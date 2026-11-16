## 1. Persistence and graph

- [x] 1.1 Spike `RequestPort` + `CheckpointManager` + `ResumeAsync` in-process to confirm how the pending request is resumed after restoring a checkpoint
- [x] 1.2 Add `PostgresCheckpointStore` (`JsonCheckpointStore`) and `AnalysisRunStore` with tables created on first use
- [x] 1.3 Add `review-cover-letter` port and `apply-review` node; `finalize-cover-letter` sends to the port
- [x] 1.4 Writer rewrite with reviewer feedback as untrusted data; cap of 3 feedback rounds
- [x] 1.5 Run, persist and resume in `JobAnalysisWorkflow`; `status` and `runId` in the response

## 2. Endpoints

- [x] 2.1 `POST /api/analyses` returns 202 + `Location` on strong fit, 200 `completed` on weak fit
- [x] 2.2 `GET /api/analyses/{runId}` (200 / 404)
- [x] 2.3 `POST /api/analyses/{runId}/approval` (200 / 202 / 400 / 404 / 409 / 502) with the atomic claim

## 3. Tests

- [x] 3.1 API tests: approval body validation and unknown run 404
- [x] 3.2 Integration: strong fit 202 awaiting approval, GET status, approve → approved with path
- [x] 3.3 Integration: reject without feedback; reject with feedback → new letter 202 with feedback in the prompt; rejection limit
- [x] 3.4 Integration: 409 for decided runs and for a second decision; resume after recreating the API host
- [x] 3.6 Integration: a resume interrupted for longer than the 15-minute lease can be claimed again
- [x] 3.7 Eval: a letter revised with reviewer feedback that asks for absent facts still mentions none of them
- [x] 3.5 Update existing analysis tests for the new strong-fit status code and fields

## 4. Docs

- [x] 4.1 README (approval flow, endpoints, roadmap status) and `.http` examples
- [x] 4.2 `openspec validate --all --strict` and `dotnet test JobFit.slnx`
