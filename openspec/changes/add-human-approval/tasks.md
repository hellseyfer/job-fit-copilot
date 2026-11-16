## 1. Persistence and graph

- [ ] 1.1 Spike `RequestPort` + `CheckpointManager` + `ResumeAsync` in-process to confirm how the pending request is resumed after restoring a checkpoint
- [ ] 1.2 Add `PostgresCheckpointStore` (`JsonCheckpointStore`) and `AnalysisRunStore` with tables created on first use
- [ ] 1.3 Add `review-cover-letter` port and `apply-review` node; `finalize-cover-letter` sends to the port
- [ ] 1.4 Writer rewrite with reviewer feedback as untrusted data; cap of 3 feedback rounds
- [ ] 1.5 Run, persist and resume in `JobAnalysisWorkflow`; `status` and `runId` in the response

## 2. Endpoints

- [ ] 2.1 `POST /api/analyses` returns 202 + `Location` on strong fit, 200 `completed` on weak fit
- [ ] 2.2 `GET /api/analyses/{runId}` (200 / 404)
- [ ] 2.3 `POST /api/analyses/{runId}/approval` (200 / 202 / 400 / 404 / 409 / 502) with the atomic claim

## 3. Tests

- [ ] 3.1 API tests: approval body validation and unknown run 404
- [ ] 3.2 Integration: strong fit 202 awaiting approval, GET status, approve → approved with path
- [ ] 3.3 Integration: reject without feedback; reject with feedback → new letter 202 with feedback in the prompt; rejection limit
- [ ] 3.4 Integration: 409 for decided runs and for a second decision; resume after recreating the API host
- [ ] 3.5 Update existing analysis tests for the new strong-fit status code and fields

## 4. Docs

- [ ] 4.1 README (approval flow, endpoints, roadmap status) and `.http` examples
- [ ] 4.2 `openspec validate --all --strict` and `dotnet test JobFit.slnx`
