# Security policy

This is an online co-op game: a client talks to a host machine owned by another player, so
problems in the network layer — a client acting on behalf of another player, crashing the
host, or reading data it should not — are taken seriously.

## Reporting a vulnerability

**Please do not open a public issue for security problems.**

Report it privately through GitHub: open the **Security** tab of this repository and click
**Report a vulnerability** (GitHub's *Private vulnerability reporting*). Only the maintainer
sees the report.

Please include:

- the affected script or RPC (most of the network code is in `Assets/_Project/Scripts/Net/`);
- steps to reproduce, ideally with two local instances (for example Multiplayer Play Mode);
- the impact you expect (acting as another player, host crash, desynchronization…).

You can expect an acknowledgement within a few days. Fixes are made in the main codebase first
and then reach this repository with the next public snapshot.

## Scope and rules

- Test only against sessions you host yourself. Never test against someone else's machine.
- In scope: anything in this repository — the RPCs and server-side executors, connection
  approval, the replicated state, save/load, and the CI workflow.
- Already known and documented (see the README, *Known limits*): owner-authoritative player
  movement, damage amounts reported by the client, connection approval without a room code or
  password, and direct-IP connections without a relay. Reports that go beyond these are welcome.
- Out of scope: Unity, Netcode for GameObjects and other packages themselves (report those to
  their maintainers), and findings that require a compromised host machine.

## Supported versions

Only the latest snapshot on the default branch is supported.
