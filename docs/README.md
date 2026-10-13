# T212Ext docs

| File | What it is |
|---|---|
| [CONVENTIONS.md](CONVENTIONS.md) | The rules every change follows: workflow, the API client, data and sync, the app. |
| [T212-API.md](T212-API.md) | What we know about the Trading 212 API: authentication, endpoints, rate limits, quirks, terms of use. |
| [BACKLOG.md](BACKLOG.md) | The roadmap by phase, and ideas not yet planned. |
| [RELEASE-NOTES.md](RELEASE-NOTES.md) | What changed in each version, for the person using the app. |
| [UPGRADING.md](UPGRADING.md) | Deploy steps of the versions not yet in production. |

## Versions

[Semantic versioning](https://semver.org): `MAJOR.MINOR.PATCH`. The current version is in the `VERSION` file at the root, which every project reads (`Directory.Build.props`). The app shows it at the bottom of the menu.

The branch name says which number a piece of work bumps:

| Branch | Bumps | For | Example |
|---|---|---|---|
| `major-<name>` | major (`2.4.1` → `3.0.0`) | A rewrite, or a change that needs data migrated by hand or breaks the configuration. | `major-blazor-rewrite` |
| `feature-<name>` | minor (`2.0.3` → `2.1.0`) | New functionality: a page, a report, a data source. | `feature-dividend-charts` |
| `hotfix-<name>` | patch (`2.1.0` → `2.1.1`) | A fix or a small improvement with no new functionality. | `hotfix-csv-old-layout` |

Docs-only changes don't bump the version; their branch is a `hotfix-` without a release.

Versions 1.x were the .NET MAUI app (never tagged). 2.0.0 is the rewrite.

## Releasing

1. The branch bumps `VERSION`, adds the version's section to `RELEASE-NOTES.md` and, once there is a server, its deploy steps to `UPGRADING.md`.
2. After it merges, the merge commit gets the tag (`git tag v2.1.0 <commit>`) and a GitHub release with the version's notes.
3. Once production runs the version, its section leaves `UPGRADING.md`.

## Conventions

- Docs are written in English.
- The rules for code changes are in [CONVENTIONS.md](CONVENTIONS.md); a PR that sets a new rule adds it there.
- A finding about the Trading 212 API (a quirk, a limit, a field) goes to [T212-API.md](T212-API.md) in the PR that relies on it.
