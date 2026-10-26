# Contributing

Thanks for your interest. Bug reports, questions and pull requests are welcome. For security
problems, use [SECURITY.md](SECURITY.md) instead of a public issue.

Keep in mind that this repository holds **code only**: the art, scenes and prefabs are not
here (see the README), so changes that can be checked without them — rules, netcode, tests,
tooling — are the easiest to review.

## Workflow

1. **Fork** the repository and clone your fork.
2. Create a **branch** from `main` with a descriptive name, e.g. `fix/seat-release-on-timeout`.
3. Make your change, with tests when it touches game rules.
4. Run the checks below.
5. Open a **pull request** against `main` explaining what changed and why. CI builds the
   headless solution and runs its tests on every pull request that touches the core, the
   EditMode tests or `Tools/HeadlessTests`.

Code comments in this project are in Brazilian Portuguese; pull requests in English or
Portuguese are both fine.

## Checks before opening a pull request

Headless tests (needs the .NET SDK 8, no Unity):

```bash
dotnet build Tools/HeadlessTests/HeadlessTests.sln
dotnet test Tools/HeadlessTests/HeadlessTests.sln --no-build
```

Both must succeed — the explicit `build` also compiles `CompileCheck`, which catches test code
that Unity's NUnit 3.5 / .NET Standard 2.1 would not accept.

If you have Unity **6000.5.1f1**, also run the EditMode suite: *Window → General → Test Runner →
EditMode → Run All*.

## Code guidelines

- **C# 9** at most (no file-scoped namespaces, `record`, `init` or raw strings): it is what the
  Unity compiler accepts.
- **Respect the assembly layers.** Only `Net` (and `Editor`) may reference `Unity.Netcode`;
  gameplay reaches the network through the seams in `Core` and the `Network*` companions.
- **Put rules in pure classes** (the `*Math` pattern) and cover them with EditMode tests. A test
  that only depends on `Core` can also join the headless run — add it to both lists in
  `Tools/HeadlessTests` (`Tests/` and `CompileCheck/`).
- **Never trust the client.** A new `SendTo.Server` RPC must not carry an owner id: take
  `RpcParams` and resolve the sender with `NetSeat.TryOfSender`, and validate the request on the
  server.
- **Do not add third-party assets** (models, textures, sounds, packages from the Asset Store) to
  this repository.
- Do not commit generated folders (`Library/`, `Temp/`, `Logs/`, `bin/`, `obj/`); the
  `.gitignore` already covers them.
