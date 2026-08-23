# CI (pending activation)

Two ready-to-run GitHub Actions workflows live in `workflows/`:

- `backend-ci.yml` — restore → build (Release) → unit tests → integration tests
  (PostgreSQL via Testcontainers) → secret scan (gitleaks). Triggered by `backend/**` changes.
- `android-ci.yml` — .NET 10 + MAUI Android workload → Release build of `KaamLink.csproj`.
  Triggered by client changes. **This is also the compile gate for the Phase 01 app code.**

## Why they are not in `.github/workflows/` yet

The GitHub App token used by this workspace was **rejected when pushing files under
`.github/workflows/`** ("refusing to allow a GitHub App to create or update workflow
... without `workflows` permission"). The files are kept here so the repository stays
pushable until the permission is granted.

## Activate (one command, once permission exists)

```bash
mkdir -p .github && git mv ci/workflows .github/workflows && git rm ci/README.md \
  && git commit -m "Activate CI workflows" && git push
```

Either reconnect GitHub in Arena with the **workflows** permission, or a maintainer
with write access can run the command above / place the files themselves.

Path filters inside the workflows already match their final locations, and the job
steps do not depend on the workflow files' location — no edits are needed when moving.
