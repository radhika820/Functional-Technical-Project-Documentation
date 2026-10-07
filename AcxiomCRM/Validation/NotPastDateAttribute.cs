using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace AcxiomCRM.Validation;

/// <summary>
/// Date must not be earlier than today. Optionally exempt when another property holds one of the given values
/// (e.g. closed opportunities may have a past close date). Enforced on the server and emitted for client-side
/// (jquery-validate "notpast" adapter in wwwroot/js/site.js).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class NotPastDateAttribute : ValidationAttribute, IClientModelValidator
{
    public string? ExemptProperty { get; set; }
    public string? ExemptValues { get; set; }

    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is not DateTime date) return ValidationResult.Success; // [Required] reports missing values

        if (!string.IsNullOrEmpty(ExemptProperty) && !string.IsNullOrEmpty(ExemptValues))
        {
            var current = context.ObjectType.GetProperty(ExemptProperty)?.GetValue(context.ObjectInstance)?.ToString();
            if (current != null && ExemptValues.Split(',').Contains(current)) return ValidationResult.Success;
        }

        return date.Date < DateTime.Today
            ? new ValidationResult(ErrorMessage, new[] { context.MemberName ?? string.Empty })
            : ValidationResult.Success;
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        Merge(context.Attributes, "data-val", "true");
        Merge(context.Attributes, "data-val-notpast", ErrorMessage ?? "Date cannot be in the past.");
        if (!string.IsNullOrEmpty(ExemptProperty))
        {
            Merge(context.Attributes, "data-val-notpast-exempt", ExemptProperty!);
            Merge(context.Attributes, "data-val-notpast-values", ExemptValues ?? string.Empty);
        }
    }

    private static void Merge(IDictionary<string, string> attrs, string key, string value)
    {
        if (!attrs.ContainsKey(key)) attrs.Add(key, value);
    }
}
