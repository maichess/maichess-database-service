# Contract Notes

## Proposed: allow client-supplied ids on Insert (event-sourcing blocker)

**Status:** proposed — not implemented. Do not implement until explicitly approved (per the
root contract policy).

### Blocker

The event-driven redesign
([event-driven-architecture.md](../../maichess-knowledge-base/event-driven-architecture.md))
makes `match.events.v1` the source of truth, keyed by **matchId**. For commands and events to
share a stable partition key, the match aggregate id must be **minted by the producer** before
the match document exists.

The current `Database.Insert` contract
(`maichess-api-contracts/protos/database-service/v1/database.proto`) explicitly **ignores any
`id` in the record and assigns its own**:

> InsertRequest adds a new record. Any `id` field in `record` is ignored; the server assigns a
> new ID and returns the full record including it.

This prevents Match Manager from creating a match with a caller-supplied id, which in turn blocks
moving `Matches.CreateMatch` onto `match.commands.v1`
(see [match-maker `CONTRACT_NOTES.md`](../maichess-match-maker-service/CONTRACT_NOTES.md)).

### Proposed minimal change

Honor a caller-supplied `id` in `InsertRequest.record` **when present**: insert with that id and
fail with `ALREADY_EXISTS` on collision; keep server-assignment when `id` is absent. This is
backward compatible — existing callers omit `id` and are unaffected.

Alternative considered: a separate `InsertWithId` RPC. Rejected as more surface area for the same
capability; the conditional-honor approach keeps one insert path.

### Scope

Needed for `match-db` (MongoDB) to support event-sourced match creation. `user-db` (Postgres) is
unaffected (no event-sourced aggregates there yet).
