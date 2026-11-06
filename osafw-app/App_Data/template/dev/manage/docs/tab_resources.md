### Maintaining the application source

Get repository access from the project owner. Start with the README, deployment profile and `docs/deploy.md`. Controllers coordinate requests, models handle data, and ParsePage templates render the interface.

Work on a development branch with a disposable database. Build, run `pwsh -NoProfile -File scripts/test.ps1` and try the affected workflow. Have a maintainer review the change, including AI-assisted edits, before deployment.
