using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Dtos;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using AcxiomCRM.ViewModels.Reports;

int pass = 0, fail = 0;
void Check(string name, bool ok, string extra = "")
{
    if (ok) pass++; else fail++;
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name} {extra}");
}
List<ValidationResult> Validate(object o)
{
    var res = new List<ValidationResult>();
    Validator.TryValidateObject(o, new ValidationContext(o), res, validateAllProperties: true);
    return res;
}
string Msgs(List<ValidationResult> r) => string.Join(" | ", r.Select(x => x.ErrorMessage));

// ---- formatting ----
Check("Money 100000", AppFormat.Money(100000) == "₹1,00,000", AppFormat.Money(100000));
Check("Money 1234567", AppFormat.Money(1234567) == "₹12,34,567", AppFormat.Money(1234567));
Check("Money 999", AppFormat.Money(999) == "₹999");
Check("Money 0", AppFormat.Money(0) == "₹0");

// ---- opportunity rules ----
var okOpp = new OpportunityInput { OpportunityName = "Deal", CustomerId = 1, Amount = 1000, Stage = "Proposal", Probability = 50, ExpectedCloseDate = DateTime.Today.AddDays(5) };
Check("Opportunity valid passes", Validate(okOpp).Count == 0, Msgs(Validate(okOpp)));

var o0 = new OpportunityInput { OpportunityName = "Deal", CustomerId = 1, Amount = 0, Stage = "Proposal", Probability = 50, ExpectedCloseDate = DateTime.Today.AddDays(5) };
Check("Amount = 0 rejected with exact message", Msgs(Validate(o0)).Contains("Opportunity Amount must be greater than 0."), Msgs(Validate(o0)));
var oNeg = new OpportunityInput { OpportunityName = "Deal", CustomerId = 1, Amount = -5, Stage = "Proposal", Probability = 50, ExpectedCloseDate = DateTime.Today.AddDays(5) };
Check("Amount < 0 rejected", Msgs(Validate(oNeg)).Contains("greater than 0"));
var p101 = new OpportunityInput { OpportunityName = "Deal", CustomerId = 1, Amount = 10, Stage = "Proposal", Probability = 101, ExpectedCloseDate = DateTime.Today.AddDays(5) };
Check("Probability 101 rejected with exact message", Msgs(Validate(p101)).Contains("Probability must be between 0 and 100."), Msgs(Validate(p101)));
var pNeg = new OpportunityInput { OpportunityName = "Deal", CustomerId = 1, Amount = 10, Stage = "Proposal", Probability = -1, ExpectedCloseDate = DateTime.Today.AddDays(5) };
Check("Probability -1 rejected", Msgs(Validate(pNeg)).Contains("between 0 and 100"));
var p0 = new OpportunityInput { OpportunityName = "Deal", CustomerId = 1, Amount = 10, Stage = "Proposal", Probability = 0, ExpectedCloseDate = DateTime.Today.AddDays(5) };
var p100 = new OpportunityInput { OpportunityName = "Deal", CustomerId = 1, Amount = 10, Stage = "Proposal", Probability = 100, ExpectedCloseDate = DateTime.Today.AddDays(5) };
Check("Probability 0 and 100 accepted (inclusive)", Validate(p0).Count == 0 && Validate(p100).Count == 0);
var past = new OpportunityInput { OpportunityName = "Deal", CustomerId = 1, Amount = 10, Stage = "Negotiation", Probability = 50, ExpectedCloseDate = DateTime.Today.AddDays(-1) };
Check("Active opp, close date yesterday rejected with exact message", Msgs(Validate(past)).Contains("Expected Close Date cannot be in the past."), Msgs(Validate(past)));
var today = new OpportunityInput { OpportunityName = "Deal", CustomerId = 1, Amount = 10, Stage = "Negotiation", Probability = 50, ExpectedCloseDate = DateTime.Today };
Check("Close date today accepted", Validate(today).Count == 0);
var wonPast = new OpportunityInput { OpportunityName = "Deal", CustomerId = 1, Amount = 10, Stage = "Won", Probability = 100, ExpectedCloseDate = DateTime.Today.AddDays(-30) };
Check("Won opp with past close date accepted", Validate(wonPast).Count == 0, Msgs(Validate(wonPast)));
var lostPast = new OpportunityInput { OpportunityName = "Deal", CustomerId = 1, Amount = 10, Stage = "Lost", Probability = 0, ExpectedCloseDate = DateTime.Today.AddDays(-30) };
Check("Lost opp with past close date accepted", Validate(lostPast).Count == 0);
var noName = new OpportunityInput { OpportunityName = "", CustomerId = 1, Amount = 10, Stage = "Proposal", Probability = 5, ExpectedCloseDate = DateTime.Today };
Check("Blank opportunity name -> field-specific required", Msgs(Validate(noName)).Contains("Opportunity Name is required."));
var noCust = new OpportunityInput { OpportunityName = "x", CustomerId = 0, Amount = 10, Stage = "Proposal", Probability = 5, ExpectedCloseDate = DateTime.Today };
Check("Missing customer rejected", Msgs(Validate(noCust)).Contains("Customer is required."));
var longName = new OpportunityInput { OpportunityName = new string('a', 151), CustomerId = 1, Amount = 10, Stage = "Proposal", Probability = 5, ExpectedCloseDate = DateTime.Today };
Check("Over-length text rejected before save", Msgs(Validate(longName)).Contains("cannot exceed 150"));

// ---- follow-up rules ----
var fu = new FollowUpInput { Subject = "Call", CustomerId = 1, FollowUpDate = DateTime.Today.AddDays(-1), Status = "Planned", FollowUpType = "Call" };
Check("Follow-up yesterday (planned) rejected with exact message", Msgs(Validate(fu)).Contains("Follow-up date cannot be earlier than today."), Msgs(Validate(fu)));
var fuToday = new FollowUpInput { Subject = "Call", CustomerId = 1, FollowUpDate = DateTime.Today, Status = "Planned", FollowUpType = "Call" };
Check("Follow-up today accepted", Validate(fuToday).Count == 0);
var fuDone = new FollowUpInput { Subject = "Call", CustomerId = 1, FollowUpDate = DateTime.Today.AddDays(-3), Status = "Completed", FollowUpType = "Call" };
Check("Completed follow-up keeps its past date (edit of history)", Validate(fuDone).Count == 0);
var fuNone = new FollowUpInput { Subject = "Call", FollowUpDate = DateTime.Today, Status = "Planned", FollowUpType = "Call" };
Check("Follow-up with no related record rejected", Msgs(Validate(fuNone)).Contains("Select a customer, lead or opportunity"));
var fuBadType = new FollowUpInput { Subject = "Call", CustomerId = 1, FollowUpDate = DateTime.Today, Status = "Planned", FollowUpType = "Telepathy" };
Check("Invalid follow-up type rejected", Msgs(Validate(fuBadType)).Contains("valid follow-up type"));

// ---- customer / lead rules ----
var c = new CustomerInput { CustomerName = "Anita Rao", Email = "anita@example.com", Phone = "9876543210" };
Check("Customer valid passes", Validate(c).Count == 0, Msgs(Validate(c)));
var cBadMail = new CustomerInput { CustomerName = "Anita", Email = "not-an-email", Phone = "9876543210" };
Check("Invalid email message", Msgs(Validate(cBadMail)).Contains("Enter a valid email address."));
foreach (var bad in new[] { "12345", "98765abcde", "5876543210", "98765432101", "+919876543210", "" })
{
    var cp = new CustomerInput { CustomerName = "Anita", Email = "a@b.co", Phone = bad };
    var m = Msgs(Validate(cp));
    Check($"Invalid phone '{bad}' rejected", m.Contains("Enter a valid phone number.") || m.Contains("Phone is required."), m);
}
var cBlank = new CustomerInput { CustomerName = "", Email = "", Phone = "" };
var blankMsgs = Msgs(Validate(cBlank));
Check("Blank required fields -> field-specific messages", blankMsgs.Contains("Customer Name is required.") && blankMsgs.Contains("Email is required.") && blankMsgs.Contains("Phone is required."), blankMsgs);
var cLong = new CustomerInput { CustomerName = new string('x', 151), Email = "a@b.co", Phone = "9876543210" };
Check("Customer name max length enforced", Msgs(Validate(cLong)).Contains("between 2 and 150"));
var l = new LeadInput { LeadName = "Lead", Email = "l@x.com", Phone = "9123456789", Source = "Website", Status = "New", Priority = "High", ExpectedValue = 5000 };
Check("Lead valid passes", Validate(l).Count == 0, Msgs(Validate(l)));
var lBig = new LeadInput { LeadName = "Lead", Email = "l@x.com", Phone = "9123456789", ExpectedValue = 100_000_001 };
Check("Lead expected value range enforced", Msgs(Validate(lBig)).Contains("Expected Value must be between"));
var lNeg = new LeadInput { LeadName = "Lead", Email = "l@x.com", Phone = "9123456789", ExpectedValue = -1 };
Check("Lead negative value rejected", Msgs(Validate(lNeg)).Contains("Expected Value must be between"));

// ---- lead workflow ----
Check("Lead New->Contacted allowed", LeadStatuses.CanTransition("New", "Contacted"));
Check("Lead New->Converted blocked", !LeadStatuses.CanTransition("New", "Converted"));
Check("Lead New->Qualified blocked (must be contacted first)", !LeadStatuses.CanTransition("New", "Qualified"));
Check("Lead Contacted->Qualified allowed", LeadStatuses.CanTransition("Contacted", "Qualified"));
Check("Lead Qualified->Converted allowed", LeadStatuses.CanTransition("Qualified", "Converted"));
Check("Lead Converted is terminal", !LeadStatuses.CanTransition("Converted", "New") && !LeadStatuses.CanTransition("Converted", "Lost"));
Check("Lead Lost->Qualified blocked", !LeadStatuses.CanTransition("Lost", "Qualified"));
Check("Lead same-status is a no-op", LeadStatuses.CanTransition("New", "New"));
Check("Edit form never offers Converted", !LeadStatuses.SelectableFrom("Qualified").Contains("Converted"));
Check("Invalid status string rejected", !LeadStatuses.IsValid("Hacked") && LeadStatuses.IsValid("Qualified"));

// ---- opportunity helpers ----
Check("Weighted = Amount x Probability / 100", new Opportunity { Amount = 200000, Probability = 25 }.WeightedAmount == 50000m);
Check("Stage->status mapping", OpportunityStatuses.FromStage("Won") == "Won" && OpportunityStatuses.FromStage("Lost") == "Lost" && OpportunityStatuses.FromStage("Proposal") == "Open");
Check("Overdue = planned & past date", new FollowUp { Status = "Planned", FollowUpDate = DateTime.Today.AddDays(-1) }.IsOverdue && !new FollowUp { Status = "Completed", FollowUpDate = DateTime.Today.AddDays(-1) }.IsOverdue && !new FollowUp { Status = "Planned", FollowUpDate = DateTime.Today }.IsOverdue);

// ---- audit snapshot never leaks secrets ----
var u = new ApplicationUser { UserName = "x@y.z", Email = "x@y.z", FullName = "X", PasswordHash = "SECRETHASH", SecurityStamp = "STAMP" };
var snap = AuditSnapshot.Of(u);
Check("Audit snapshot excludes password hash / stamp", !snap.Contains("SECRETHASH") && !snap.Contains("STAMP") && snap.Contains("FullName"), snap);
Check("Audit snapshot of Opportunity has scalar fields only", AuditSnapshot.Of(new Opportunity { OpportunityName = "n", Customer = new Customer() }).Contains("OpportunityName"));

// ---- report CSV: formula injection neutralised ----
var rt = new ReportTable { Columns = new[] { "A", "B" }, Rows = new List<object?[]> { new object?[] { "=HYPERLINK(\"http://x\")", "ok" }, new object?[] { "+1", "-2" }, new object?[] { "@cmd", DateTime.Today } } };
var csv = System.Text.Encoding.UTF8.GetString(rt.ToCsv());
Check("CSV neutralises =,+,-,@ prefixes", csv.Contains("\"'=HYPERLINK") && csv.Contains("\"'+1\"") && csv.Contains("\"'-2\"") && csv.Contains("\"'@cmd\""), csv.Replace("\n", " ~ "));
rt.Apply(new ReportFilter { Sort = 0, Dir = "desc", PageSize = 5, Page = 1 });
Check("Report paging/sorting runs", rt.TotalCount == 3 && rt.PageRows.Count == 3);

// ---- dashboard date ranges ----
var (f1, t1, lab1) = DashboardService.Resolve(new DashboardFilter { Range = "today" });
Check("Dashboard 'today' spans exactly 24h", f1 != null && t1 != null && (t1.Value - f1.Value).TotalHours is >= 23 and <= 25 && lab1 == "Today");
var (f2, t2, _) = DashboardService.Resolve(new DashboardFilter { Range = "week" });
Check("Dashboard 'week' spans 7 days starting Monday", f2 != null && (t2!.Value - f2.Value).TotalDays is >= 6.9 and <= 7.1 && f2.Value.ToLocalTime().DayOfWeek == DayOfWeek.Monday, f2?.ToLocalTime().DayOfWeek.ToString() ?? "");
var (f3, t3, _) = DashboardService.Resolve(new DashboardFilter { Range = "month" });
Check("Dashboard 'month' starts on the 1st", f3 != null && f3.Value.ToLocalTime().Day == 1 && t3 != null);
var (f4, t4, _) = DashboardService.Resolve(new DashboardFilter { Range = "custom", From = new DateTime(2026, 1, 1), To = new DateTime(2026, 1, 31) });
Check("Dashboard custom range inclusive of end date", f4 != null && t4 != null && (t4.Value - f4.Value).TotalDays is >= 30.9 and <= 32.1);
var (f5, t5, _) = DashboardService.Resolve(new DashboardFilter());
Check("Dashboard default = all time", f5 == null && t5 == null);

// ---- ServiceResult mapping ----
var sr = ServiceResult<string>.Invalid("Email", "dup");
Check("ServiceResult invalid carries field errors", !sr.Succeeded && sr.Errors.Count == 1 && sr.As<int>().Status == ResultStatus.Invalid);
Check("ServiceResult NotFound/Forbidden convert", ServiceResult<string>.NotFound().As<int>().Status == ResultStatus.NotFound && ServiceResult<string>.Forbidden().As<int>().Status == ResultStatus.Forbidden);

Console.WriteLine($"\n{pass} passed, {fail} failed");
return fail == 0 ? 0 : 1;
