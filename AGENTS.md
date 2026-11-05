# Repository maintenance

Read DEVELOPMENT.md before changing the application or packaging.

Keep the root limited to the explicit allowlist in scripts/test_repository_layout.py.
Put new guides and handoffs in docs/, automation in scripts/, and regression checks
in Source/tests/. Preserve the existing Source/ project paths.
Update all consumers and tests together for intentional path moves; preserve archive
entry names, complete editable source, licenses and historical release notes.
Do not commit build output, private configuration backups or client reports.

Run python3 -m unittest discover -s scripts -p "test_*.py" -v and the complete
existing CI checks before promotion. New root entries require an intentional review
of the allowlist and a reason in the change description; never relax it to a glob.
Rebase older feature branches on current main and rerun checks after integration.
Organization changes do not authorize version bumps, releases, tag/asset replacement
or repository access changes. Preserve the polished README layout.

## Repository standards (all contributors and AI tools)

This repository follows the shared TechSavage1989 standard in [`docs/REPOSITORY_STANDARDS.md`](docs/REPOSITORY_STANDARDS.md), the same file in every app repository. Read it before changing anything. In short:

- **README:** centered header (real icon, one `<h1>`, tagline, quick links Download · Get started · Features · Screenshots · Updates · Development, hero image), then the standard section order. Update it in the same pull request as any user-visible change.
- **Screenshots:** real captures with fictional data in `docs/images/` (`hero.png` + kebab-case names). Recapture any screenshot a UI change makes stale, in the same pull request.
- **Layout:** compact root; respect the root allowlist and checks above. Never commit build output, binaries, archives, logs, caches or secrets.
- **Branches:** short-lived `feat/` `fix/` `docs/` `chore/` `release/` branches, one focused change per pull request, merge only on green checks, delete the branch after merging.
- **Instructions:** this file is the single source of project rules for every tool; `CLAUDE.md` imports it. Add new rules and gotchas here.

Where this file's project-specific rules differ from the shared standard, this file wins; an enforced check script wins over both.

## Supplementary network evidence import

Supplementary Auvik and CLI data is non-authoritative. Never infer or override physical port counts, installed modules, VLAN/native/voice assignments, LAGs, administrative config or actual links from this evidence. Require exact device identity matching before attaching Auvik reports, and preserve source attribution. Support read-only file imports without executing formulas, scripts, embedded code or external references. Keep only fictional fixtures in CI. Preserve workspace version-1 backward compatibility and the existing 8-file report baseline. Read `docs/SUPPLEMENTARY_IMPORT.md` and run SupplementaryChecks plus the complete CI suite when editing this feature.

For drag-and-drop imports, preserve the existing configuration/RouterOS backup paths. Treat every Auvik workbook and recognized operational command capture as evidence, not a switch config. Resolve device identity from configuration hostnames, reject ambiguous assignments and import primary configs before supplementary files in mixed drops. Run the supplementary drop UI regression and in-memory workbook batch tests before merging.
