namespace Arisoul.T212Ext.Web.Components.Shared;

public enum PaymentKind
{
    Paid,
    Estimated,
    Due,
    Late,
}

/// <summary>One instrument's payment on one day, paid or expected, in the calendar's currency.</summary>
public sealed record PaymentCalendarEntry(DateOnly Date, string Ticker, string? Isin, string? Name, decimal Amount, PaymentKind Kind);
