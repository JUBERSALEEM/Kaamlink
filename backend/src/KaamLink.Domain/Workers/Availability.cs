namespace KaamLink.Domain.Workers;

/// <summary>Worker availability state shown to employers. Stored as text.</summary>
public enum Availability
{
    Immediately,
    WithinTwoWeeks,
    WithinOneMonth,
    NotAvailable
}
