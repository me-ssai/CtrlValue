# CtrlValue — Claude Code Guide

## Project Overview

Full-stack personal finance platform.

- **Frontend:** Angular 19, TypeScript, Angular Material, SCSS — `frontend/project-z/`
- **Backend:** ASP.NET Core (.NET 8), Clean Architecture, PostgreSQL via EF Core — `backend/`
- **Auth:** JWT Bearer tokens (httpOnly cookies)
- **API Client:** NSwag-generated (`api.generated.ts`) — regenerate after schema changes with `nswag run`
- **Environments:** `development`, `demo`, `production`

---

## Architecture — Backend (Clean Architecture)

Strict layer dependency rule: outer layers depend on inner layers, never the reverse.

```
CtrlValue.Domain          ← Entities, value objects, domain logic. No framework dependencies.
CtrlValue.Application     ← Use cases, service interfaces, DTOs, business orchestration.
CtrlValue.Infrastructure  ← EF Core DbContext, repositories, email, external APIs.
CtrlValue.Api             ← Controllers, middleware, DI wiring, request/response mapping.
CtrlValue.Api.Tests       ← Integration & unit tests.
```

**Allowed dependencies:**
- `Api` → `Application` + `Infrastructure`
- `Application` → `Domain` + `Infrastructure`
- `Infrastructure` → `Domain`
- `Domain` → nothing

---

## SOLID Principles (C# .NET)

These are non-negotiable design rules for all backend code.

### S — Single Responsibility
Each class has one reason to change. Controllers handle HTTP only; services handle business logic only; repositories handle data access only. If a class is doing two jobs, split it.

### O — Open/Closed
Extend behaviour through new classes or interfaces, not by modifying existing ones. Use `abstract` base classes or strategy patterns for varying behaviour (e.g., different AI providers in `Agent` config).

### L — Liskov Substitution
Subtypes must be substitutable for their base types without breaking callers. Avoid overriding methods in ways that tighten preconditions or weaken postconditions.

### I — Interface Segregation
Define narrow, focused interfaces. Services in `Application` expose only what their callers need — no fat interfaces that force implementors to throw `NotImplementedException`.

### D — Dependency Inversion
`Application` layer defines interfaces (e.g., `IFinanceRepository`, `IEmailService`). `Infrastructure` implements them. `Api` wires them via DI in `Program.cs`. **Never `new` up infrastructure concerns inside domain or application code.**

**Example pattern:**
```csharp
// Application layer — defines the contract
public interface ITransactionRepository
{
    Task<IReadOnlyList<Transaction>> GetByAccountIdAsync(Guid accountId, CancellationToken ct);
}

// Infrastructure layer — implements it
public sealed class TransactionRepository : ITransactionRepository { ... }

// Api/Program.cs — wires it
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
```

---

## TDD Workflow — Red → Green → Refactor

TDD is mandatory. Do not write implementation code before a failing test exists.

### The cycle
1. **Red** — Write the smallest test that describes the intended behaviour. Run it; confirm it fails.
2. **Green** — Write the minimum production code to make it pass. No more.
3. **Refactor** — Clean up duplication and design issues while keeping all tests green.
4. **Repeat** — Move to the next behaviour slice.

### Rules
- Never skip the Red step. A test that was never red provides no confidence.
- One failing test at a time. Do not write multiple failing tests before making the first one pass.
- Tests must be fast, isolated, and deterministic.
- Test behaviour, not implementation. Tests should not break when you rename a private method.

---

## Backend Testing (.NET — xUnit + FluentAssertions)

### Test project: `backend/tests/CtrlValue.Api.Tests/`

**Packages available:**
- `xunit` — test runner
- `FluentAssertions` — readable assertions
- `Microsoft.AspNetCore.Mvc.Testing` — in-process integration tests
- Add `Moq` for unit test mocking when needed

### Unit tests (Application / Domain logic)

Mock all infrastructure at the interface boundary using Moq.

```csharp
public class TransactionServiceTests
{
    private readonly Mock<ITransactionRepository> _repo = new();
    private readonly TransactionService _sut;

    public TransactionServiceTests()
    {
        _sut = new TransactionService(_repo.Object);
    }

    [Fact]
    public async Task GetByAccount_WhenAccountExists_ReturnsTransactions()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        _repo.Setup(r => r.GetByAccountIdAsync(accountId, default))
             .ReturnsAsync([new Transaction { AccountId = accountId }]);

        // Act
        var result = await _sut.GetByAccountAsync(accountId);

        // Assert
        result.Should().HaveCount(1);
        result[0].AccountId.Should().Be(accountId);
    }
}
```

### Integration tests (API layer)

Use `WebApplicationFactory` to test the full HTTP pipeline in-process. Use a test database or an in-memory substitute at the EF Core level.

```csharp
public class AccountsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AccountsEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAccounts_Unauthenticated_Returns401()
    {
        var response = await _client.GetAsync("/api/accounts");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

### Naming convention

`MethodName_StateUnderTest_ExpectedBehaviour`

### Run backend tests

```bash
cd backend
dotnet test
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
```

---

## Frontend Testing (Angular — Karma + Jasmine)

### Test file convention

Every component and service gets a co-located `.spec.ts` file.

```
src/app/pages/accounts/accounts.component.ts
src/app/pages/accounts/accounts.component.spec.ts

src/app/services/finance.service.ts
src/app/services/finance.service.spec.ts
```

### Service unit test pattern

Mock HTTP via `HttpClientTestingModule`. Never hit a real API in unit tests.

```typescript
describe('FinanceService', () => {
  let service: FinanceService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [FinanceService],
    });
    service = TestBed.inject(FinanceService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should fetch accounts', () => {
    service.getAccounts().subscribe(accounts => {
      expect(accounts.length).toBe(1);
    });

    const req = httpMock.expectOne('/api/accounts');
    expect(req.request.method).toBe('GET');
    req.flush([{ id: '1', name: 'Savings' }]);
  });
});
```

### Component unit test pattern

Use `NO_ERRORS_SCHEMA` to isolate the component under test from child components.

```typescript
describe('AccountsComponent', () => {
  let component: AccountsComponent;
  let fixture: ComponentFixture<AccountsComponent>;
  let financeServiceSpy: jasmine.SpyObj<FinanceService>;

  beforeEach(() => {
    financeServiceSpy = jasmine.createSpyObj('FinanceService', ['getAccounts']);
    financeServiceSpy.getAccounts.and.returnValue(of([]));

    TestBed.configureTestingModule({
      declarations: [AccountsComponent],
      providers: [{ provide: FinanceService, useValue: financeServiceSpy }],
      schemas: [NO_ERRORS_SCHEMA],
    });

    fixture = TestBed.createComponent(AccountsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should load accounts on init', () => {
    expect(financeServiceSpy.getAccounts).toHaveBeenCalled();
  });
});
```

### Run frontend tests

```bash
cd frontend/project-z
ng test                    # watch mode
ng test --watch=false      # single run (CI)
ng test --code-coverage    # with coverage report
```

---

## Key Commands

### Backend

```bash
cd backend
dotnet run --project src/CtrlValue.Api          # start API (http://localhost:5000)
dotnet test                                     # run all tests
dotnet ef migrations add <Name> --project src/CtrlValue.Infrastructure --startup-project src/CtrlValue.Api
dotnet ef database update --project src/CtrlValue.Infrastructure --startup-project src/CtrlValue.Api
```

### Frontend

```bash
cd frontend/project-z
ng serve                   # dev server (http://localhost:4200), proxies API to :5000
ng build --configuration production
ng build --configuration demo
ng test --watch=false
ng lint
nswag run                  # regenerate API client from OpenAPI spec
```

---

## Directory Structure

```
backend/
  src/
    CtrlValue.Domain/         ← Entities, value objects, domain interfaces
    CtrlValue.Application/    ← Use cases, service interfaces, DTOs
    CtrlValue.Infrastructure/ ← EF Core, repositories, email, AI providers
    CtrlValue.Api/            ← Controllers, middleware, Program.cs
      appsettings.json        ← Base config (no secrets)
      appsettings.Development.json
  tests/
    CtrlValue.Api.Tests/      ← xUnit integration + unit tests

frontend/project-z/
  src/
    app/
      pages/                 ← Feature components (each with .spec.ts)
      services/              ← Angular services (each with .spec.ts)
      shared/                ← Reusable components
      guards/                ← Route guards
      interceptors/          ← HTTP interceptors (JWT, demo)
      models/                ← TypeScript interfaces / API models
      services/api.generated.ts  ← NSwag-generated — DO NOT edit manually
    environments/            ← environment.ts, environment.prod.ts, environment.demo.ts
```

---

## Gotchas

- **Never edit `api.generated.ts` by hand.** It is fully regenerated by `nswag run`. Add wrappers in separate service files.
- **Database calls must be mocked in unit tests.** Real DB calls belong only in integration tests.
- **JWT is required on all API endpoints** except `/api/auth/*` and the demo session endpoint. Always test the 401 case.
- **Demo mode** is a special read-only tenant (`Demo.EntityId` in appsettings). The `DemoInterceptor` blocks mutating requests. Account for this in E2E tests.
- **EF Core migrations** go in `CtrlValue.Infrastructure`. Always pair a migration with a corresponding test for the new schema.
- **AI provider config** (`Agent.DefaultProvider`) is switchable between `OpenAI` and `Anthropic` at runtime via appsettings — do not hardcode provider names in business logic.
- **NSwag** reads the running API's OpenAPI spec. The API must be running before regenerating the client.
- **CORS** is locked to `localhost:4200` and `demo.ctrlvalue.com`. Do not add broad wildcards.
