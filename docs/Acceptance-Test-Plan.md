# AcxiomCRM - Acceptance Test Plan

Run after `dotnet run` (Development). Demo accounts are listed in the README. "API" steps use `AcxiomCRM.http` or Swagger.

## A. Final acceptance scenario (assignment 17.19)

| # | Scenario | Steps | Expected |
|---|---|---|---|
| 1 | Anonymous access blocked | Signed out, open `/Customers`, `/Dashboard`, `/Reports` | Redirected to Login (return URL kept) |
| 2 | Register / login | Register a new user, then log out and log in | Lands on Dashboard; new user is a Sales Executive |
| 3 | Client-side validation | New customer: email `abc`, phone `123`, blank name; click Save | Inline messages ("Enter a valid email address.", "Enter a valid phone number.", "Customer Name is required."); form not submitted |
| 4 | Server-side validation | Disable JS (or send `POST /api/customers` with `{"customerName":"","email":"bad","phone":"1"}`) | Rejected with the same messages (400 / form redisplayed) |
| 5 | Opportunity amount | Create opportunity with Amount 0 or -5 | "Opportunity Amount must be greater than 0." |
| 6 | Opportunity probability | Probability 101 (and -1) | "Probability must be between 0 and 100." (0 and 100 accepted) |
| 7 | Close date | Active stage, Expected Close Date = yesterday | "Expected Close Date cannot be in the past." (Won/Lost may keep a past date) |
| 8 | Follow-up date | Schedule follow-up dated yesterday | "Follow-up date cannot be earlier than today." |
| 9 | Sales Executive scope | Log in as sales1 | Only own customers/leads/opportunities/follow-ups; no Administration menu; `/Users`, `/AuditLogs`, `/Reports/UserActivity` -> Access denied; opening another user's record URL -> Access denied |
| 10 | Manager scope | Log in as manager | Sees sales1 + sales2 data; pipeline board and reports show team totals; Users is read-only; Audit log denied (unless `ManagerCanViewAudit` = true) |
| 11 | Admin | Log in as admin | Users (create, edit role, deactivate, unlock, reset password), Roles, Permissions, Audit log, all reports |
| 12 | Audit entries | Create, edit and delete a lead (and deactivate a customer) | Audit log shows Create / Update / Delete / Deactivate with user, time, IP and old/new values |
| 13 | API | `POST /api/auth/login`, then `GET /api/customers` with the token | 200 JSON, only authorized records; without token 401; other user's record 403; duplicate email 409 |
| 14 | Dashboard | Open Dashboard; change the period | KPI cards and the 3 Chart.js charts show authorized data; period filter changes counts |

## B. Module checklist (assignment 13)

| Area | Check | Expected |
|---|---|---|
| Authentication | Valid login / wrong password / logout | Works / generic "Invalid email or password." / session ends |
| Password security | Inspect `AspNetUsers.PasswordHash` | Hashed (Identity v3 format); policy rejects `abc123` |
| Lockout | 5 wrong passwords for sales2 | Account locked; admin sees "Locked" on Users and can Unlock; Lockout/Unlock audited |
| Authorization | Change IDs/URLs manually | Denied or 404; assignee tampering rejected |
| Client validation | Required, email, phone, length, date, numeric | Messages shown before submit |
| Server validation | Tampered requests | Rejected |
| Opportunity / Follow-up rules | See A5-A8 | Enforced |
| Lead workflow | New -> Contacted -> Qualified -> Convert | Invalid jumps (e.g. New -> Qualified) refused; conversion creates customer + opportunity, links existing customer instead of duplicating |
| Duplicates | Customer with an existing email, phone, or name+company | Refused with message |
| Audit | Login success/failure, role change, password reset | Logged without secrets |
| Reports | Filters, sort, paging, CSV | Role-scoped; CSV opens without formula execution |

## C. Security checklist (assignment 14)
Password hashing - Identity (verified in DB) · Password policy - `Security:*` · Lockout - 5 attempts / 15 min · Role-based authorization - attributes + `ScopeService` · Server-side validation - services + DataAnnotations · Anti-forgery - global `AutoValidateAntiforgeryToken` (remove a form token in dev tools -> 400) · Sensitive info out of logs - audit redaction, no password logging · HTTPS - redirection + HSTS in non-Development · API authorization / validation - bearer-only, per-endpoint scope checks, ProblemDetails errors · Audit - see A12 · Least privilege - Sales Executive minimum scope, Manager no security admin · Credentials not in source - JWT key and production DB credentials from configuration.
