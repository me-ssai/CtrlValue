# Contributing to CtrlValue

Thank you for your interest in contributing. Please read this guide before opening a PR or issue.

---

## Table of contents

- [Code of Conduct](#code-of-conduct)
- [How to report a bug](#how-to-report-a-bug)
- [How to request a feature](#how-to-request-a-feature)
- [Local setup](#local-setup)
- [Branch naming](#branch-naming)
- [Commit style](#commit-style)
- [Pull request checklist](#pull-request-checklist)
- [Code style](#code-style)

---

## Code of Conduct

This project follows the [Contributor Covenant](CODE_OF_CONDUCT.md). By participating you agree to abide by its terms.

---

## How to report a bug

Use the **Bug report** issue template. Include:

- Steps to reproduce
- Expected vs actual behaviour
- CtrlValue version / commit hash
- Browser + OS (for frontend issues)
- Redacted logs or screenshots where helpful

Do **not** open a public issue for security vulnerabilities — see [SECURITY.md](SECURITY.md).

---

## How to request a feature

Use the **Feature request** issue template. Describe the problem you are trying to solve, not just the solution you have in mind.

---

## Local setup

Prerequisites: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), [Node 20+](https://nodejs.org/), [Docker Desktop](https://www.docker.com/products/docker-desktop/), [Angular CLI](https://angular.dev/) (`npm i -g @angular/cli`), [dotnet-ef](https://learn.microsoft.com/en-us/ef/core/cli/dotnet) (`dotnet tool install --global dotnet-ef`).

```bash
# 1. Start supporting services
docker compose up postgres mailpit -d

# 2. Backend — create appsettings.Development.json (see README), then:
cd backend
dotnet restore && dotnet run --project src/CtrlValue.Api

# 3. Frontend
cd frontend/ctrlvalue
npm install && ng serve
```

Full setup details are in [README.md](README.md).

---

## Branch naming

| Type | Pattern | Example |
|------|---------|---------|
| Feature | `feature/<short-description>` | `feature/csv-import` |
| Bug fix | `fix/<short-description>` | `fix/login-redirect` |
| Docs | `docs/<short-description>` | `docs/api-reference` |
| Security | `security/<short-description>` | `security/rate-limit` |

Branch off `main`. All changes come in via PR — no direct pushes to `main`.

---

## Commit style

Use [Conventional Commits](https://www.conventionalcommits.org/):

```
feat: add OFX import support
fix: correct amortisation rounding on variable-rate loans
docs: update local setup prerequisites
chore: bump Angular to 19.3
```

Keep the subject line under 72 characters. Add a body if the why is not obvious.

---

## Pull request checklist

Before opening a PR, confirm:

- [ ] Branch is based on latest `main`
- [ ] CI passes locally (`ng build`, `ng lint`, `dotnet build`, `dotnet test`)
- [ ] New behaviour is covered by tests
- [ ] Database schema changes have a migration (`dotnet ef migrations add`)
- [ ] No secrets, credentials, or personal data in the diff
- [ ] Breaking changes are noted in the PR description
- [ ] Relevant docs updated if behaviour changes

---

## Code style

**Backend (C#)**
- Follow Clean Architecture layer boundaries — no references from Domain/Application into Infrastructure
- Use the existing service/repository patterns; do not introduce new abstractions unless the PR justifies them
- `dotnet format` before committing

**Frontend (Angular/TypeScript)**
- Standalone components only (no `NgModule`)
- API calls go through the generated `api.generated.ts` client — run `npm run refresh-api-dev` after changing backend contracts
- `ng lint` before committing
