# AcxiomCRM

Role-based CRM web application (customers, leads, opportunities, follow-ups, activities, dashboard, audit log, reports and a secured REST API) built for the **Acxiom Consulting** project assignment.

**Author:** Radhika Goyal &nbsp;|&nbsp; ID 2023002095 &nbsp;|&nbsp; B.Tech CSE, GSCSE, GITAM
**Stack:** ASP.NET Core 8 MVC (Razor) · ASP.NET Core Identity · Entity Framework Core (SQL Server) · Bootstrap 5 · jQuery Validation (unobtrusive) · Chart.js · JWT bearer for the API

---

## 1. Run it

Prerequisites: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and SQL Server (Windows: LocalDB ships with Visual Studio; macOS/Linux: SQL Server in Docker - then change the connection string).

```bash
cd AcxiomCRM
dotnet run            # https://localhost:7180  (http://localhost:5180)
```

On first start (Development) the app creates the database `AcxiomCRM`, the three roles, four demo users and demo CRM data. Open the site and sign in with:

| Role | Email | Password |
|---|---|---|
| Admin | admin@acxiomcrm.local | `Admin@12345` |
| Manager | manager@acxiomcrm.local | `Manager@12345` |
| Sales Executive | sales1@acxiomcrm.local | `Sales@12345` |
| Sales Executive | sales2@acxiomcrm.local | `Sales@12345` |

sales1 and sales2 report to the manager, so the Manager sees both of their records ("team"). You can also use **Register** (creates a Sales Executive with no manager).

* Swagger UI (Development only): `https://localhost:7180/swagger` - use *Authorize* and paste the token from `POST /api/auth/login`.
* Ready-made API calls: `AcxiomCRM/AcxiomCRM.http` (VS / VS Code REST Client).
* Rule checks (no database needed): `dotnet run --project AcxiomCRM.RuleChecks` runs 59 checks of the validation messages, lead workflow, weighted pipeline, CSV-injection protection, audit redaction and dashboard date ranges.
* The schema is created with `EnsureCreated()`. If you pull a newer version with model changes, drop the `AcxiomCRM` database first. To use migrations instead: `dotnet ef migrations add Initial` and replace `EnsureCreatedAsync()` in `Data/DbSeeder.cs` with `MigrateAsync()`.

Demo credentials live only in `appsettings.Development.json`. For production supply `ConnectionStrings__DefaultConnection`, `Jwt__Key` (>= 32 chars) and the seed user settings through environment variables or a secret store; the app refuses to start in Production without a JWT key.

---

## 2. What is implemented (mapped to the assignment)

| Spec section | Implementation |
|---|---|
| 2 / 17.2 Module tree | Authentication (Login/Register/Logout/Access control), Dashboard, Customers, Leads (status + conversion), Opportunities (+ pipeline board), Follow-Ups (schedule / complete / pending), Activities (Call/Meeting/Email/Task), User & Role management (Users/Roles/Permissions), Audit Log, REST API, 8 Reports |
| 3 Architecture | `Controllers` -> `Services` (business rules, workflows, scoping) -> EF Core `Data` -> SQL Server; `Models`, `Dtos`, `ViewModels`, `Views`, `Validation`, `Helpers`; audit/validation/security are cross-cutting |
| 4.2, 6, 17.4 Authentication | ASP.NET Core Identity (`ApplicationUser : IdentityUser`) - framework password hashing, no custom password table; policy (min 8, upper, lower, digit, symbol - configurable); lockout (5 attempts / 15 min - configurable); lockout/unlock audited; admin unlock |
| 7, 17.10 Authorization | Roles Admin / Manager / SalesExecutive. Enforced **on the server** via `[Authorize(Roles)]` plus record-level scoping in `ScopeService` (Admin = all, Manager = self + reporting Sales Executives, Sales Executive = own/assigned). Out-of-scope record -> access-denied (MVC) / 403 (API); manipulated assignee ids are rejected |
| 5, 17.5-17.8 Validation | **Client side** (jquery-validate + custom `notpast` adapter): required, email, phone, length, date, numeric/range. **Server side** (DataAnnotations + service re-checks): same rules, status workflow, FK/ownership checks, uniqueness, duplicate customer. Exact messages from section 5.4 |
| 8 Workflows | Lead -> Contacted -> Qualified -> **Convert** (creates/links customer, creates opportunity, audited); opportunity stage moves with Won=100% / Lost=0%; follow-up complete / missed / cancel / reschedule (auto-moves a New lead to Contacted and logs an activity); user administration |
| 9, 17.3 Data model | Customer, Lead, Opportunity, FollowUp, Activity, AuditLog (all required fields) + Identity tables |
| 10, 17.14 REST API | `/api/auth/login|logout`, `/api/customers` (GET, GET id, POST, PUT, DELETE = deactivate), `/api/leads`, `/api/opportunities`, `/api/followups` (+ complete), `/api/reports/pipeline`. DTOs only, validated payloads, 200/201/204/400/401/403/404/409/423/429, JWT bearer, login rate-limited, no stack traces |
| 11 Reports | Customer, Lead, Follow-Up, Opportunity, Pipeline (by stage / by owner), Sales/Conversion, User Activity, Audit - with filters, sorting, paging, CSV export, role-scoped data |
| 17.11-17.12 Dashboard | Cards: Total Customers, Total Leads, Open Leads, Total Opportunities, Open / Won / Lost, Total Pipeline Value (+ weighted, pending/overdue follow-ups); Chart.js charts: Lead Status, Opportunity Pipeline, Monthly Sales; Today / This week / This month / Custom range; admin security stats. All computed server-side from the user's authorized scope |
| 17.13 Search | Search + filter + sort + pagination on every list page; global search box in the header |
| 17.16 Audit | Login, failed login, lockout, unlock, logout, register, create, update, delete/deactivate, role change, status change, lead conversion, follow-up complete / reschedule, password change/reset (no secrets), report export. Captures user, action, entity, record id, old/new JSON, timestamp, IP, result. Append-only: the DbContext rejects updates/deletes of audit rows; there is no edit/delete UI or API |
| 12 UI/UX | Responsive Bootstrap layout, role-aware navigation, inline validation messages, success/error alerts, confirmation dialogs for destructive actions, accessible labels, no technical error text shown to users |
| 14 Security checklist | See below |

### Security measures
* Passwords hashed by Identity; never stored, shown or logged. Audit snapshots strip hashes/stamps.
* Anti-forgery token validated on every state-changing MVC request (global `AutoValidateAntiforgeryToken`); the API uses bearer tokens only (no cookie auth, so no CSRF surface).
* Cookie: HttpOnly, SameSite, Secure in non-Development, sliding 30-min expiry. Security stamp is validated on every request, so deactivating a user, changing a role or resetting a password takes effect immediately; API tokens are rejected for inactive/locked users or after logout.
* SQL injection: only EF Core LINQ/parameterized queries.
* Self-registration can only create a Sales Executive; elevated roles are assigned by an Admin. The last active Admin cannot be demoted or deactivated; admins cannot deactivate themselves.
* HTTPS redirection + HSTS (non-Development), `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`; login/register/API-login rate limited.
* CSV export neutralises spreadsheet formula injection. Open-redirect-safe `returnUrl`.
* No secrets in source: JWT key and production credentials come from configuration; Development demo credentials are only in `appsettings.Development.json`.

### Configuration flags (`appsettings.json`)
`Features:AllowSelfRegistration` (true) · `Features:ManagerCanViewUsers` (true, read-only) · `Features:ManagerCanViewAudit` (false; when true a manager sees team entries only) · `Security:*` (password policy, lockout, session length, auth rate limit).

### Design decisions worth knowing
* "Team" = Sales Executives whose *Reporting Manager* is that Manager (set in User administration).
* "Delete" on a customer deactivates it (history is kept); it is blocked while the customer has open opportunities and is Admin/Manager only. Leads/follow-ups/activities are deleted outright (confirmation + audit); opportunities are deleted by Admin/Manager only.
* New follow-ups are always created as **Planned** and cannot be back-dated (no historical-entry process is offered). Overdue = Planned and dated before today.
* Phone rule: 10-digit Indian mobile number starting 6-9 (one constant in `Models/Constants.cs`).
* Dates use the server's local "today".

---

## 3. Project layout
```
AcxiomCRM.sln
AcxiomCRM/                    web application
  Controllers/ (+ Api/)       MVC + REST controllers (thin)
  Services/                   customer, lead, opportunity, follow-up, activity, dashboard, report, audit, scope, JWT
  Data/                       ApplicationDbContext, DbSeeder
  Models/ Dtos/ ViewModels/   entities + constants, request/response DTOs, form/list models
  Validation/ Helpers/        NotPastDate attribute (server + client), HTML helpers, notification view component
  Views/ wwwroot/             Razor views, site.css, site.js, validation.js
AcxiomCRM.RuleChecks/         console project: 59 rule checks (no DB)
docs/Acceptance-Test-Plan.md  manual test script for sections 13, 14, 17.19
```

---

## 4. Verification status (please read)
The code was written and checked in a sandbox that could not reach NuGet, so:
* **Verified:** the whole project (all C# and every Razor view) compiles with 0 errors / 0 warnings against the .NET 8 ASP.NET reference assemblies, with thin API stubs standing in for the four NuGet packages (EF Core, Identity EF store, JwtBearer, Swashbuckle). The 59 rule checks in `AcxiomCRM.RuleChecks` pass.
* **Not run here:** the app against a real SQL Server (EF query translation, the generated schema, seeding), and in a browser (client-side validation, Chart.js). Run through `docs/Acceptance-Test-Plan.md` once on your machine; if a package version resolves differently, `dotnet build` will say so.
