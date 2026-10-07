using System.Globalization;
using System.Text;
using AcxiomCRM.Services;

namespace AcxiomCRM.ViewModels.Reports;

public class ReportFilter
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Status { get; set; }
    public string? OwnerId { get; set; }
    public string? Module { get; set; }
    public string? Q { get; set; }
    public string? GroupBy { get; set; }
    public int Sort { get; set; } = -1;
    public string? Dir { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public bool Desc => string.Equals(Dir, "desc", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Generic tabular report with in-memory sorting, paging and CSV export.</summary>
public class ReportTable : IPagedInfo
{
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string[] Columns { get; set; } = Array.Empty<string>();
    public List<object?[]> Rows { get; set; } = new();
    public List<(string Label, string Value)> Summary { get; set; } = new();

    public List<object?[]> PageRows { get; private set; } = new();
    public int Page { get; private set; } = 1;
    public int PageSize { get; private set; } = 20;
    public int TotalCount => Rows.Count;
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public int SortColumn { get; private set; } = -1;
    public bool Desc { get; private set; }

    public ReportTable Apply(ReportFilter f)
    {
        SortColumn = f.Sort;
        Desc = f.Desc;
        if (f.Sort >= 0 && f.Sort < Columns.Length)
        {
            var sorted = Rows.OrderBy(r => r[f.Sort], CellComparer.Instance);
            Rows = (Desc ? Rows.OrderByDescending(r => r[f.Sort], CellComparer.Instance) : sorted).ToList();
        }
        PageSize = Math.Clamp(f.PageSize, 5, 100);
        Page = Math.Clamp(f.Page, 1, TotalPages);
        PageRows = Rows.Skip((Page - 1) * PageSize).Take(PageSize).ToList();
        return this;
    }

    public byte[] ToCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", Columns.Select(Csv)));
        foreach (var r in Rows) sb.AppendLine(string.Join(",", r.Select(c => Csv(Format(c)))));
        return new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string Format(object? c) => c switch
    {
        null => "",
        DateTime d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        decimal m => m.ToString("0.00", CultureInfo.InvariantCulture),
        double x => x.ToString("0.0", CultureInfo.InvariantCulture),
        bool b => b ? "Yes" : "No",
        _ => c.ToString() ?? ""
    };

    /// <summary>Quotes values and neutralises spreadsheet formula injection (=, +, -, @ prefixes).</summary>
    private static string Csv(string s)
    {
        if (s.Length > 0 && "=+-@\t\r".Contains(s[0])) s = "'" + s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    private sealed class CellComparer : IComparer<object?>
    {
        public static readonly CellComparer Instance = new();
        public int Compare(object? a, object? b)
        {
            if (a == null && b == null) return 0;
            if (a == null) return -1;
            if (b == null) return 1;
            if (a.GetType() == b.GetType() && a is IComparable c) return c.CompareTo(b);
            return string.Compare(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
