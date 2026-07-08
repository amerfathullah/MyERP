namespace MyERP.Sales.Entities;

/// <summary>
/// Domain event raised when a Delivery Note is submitted.
/// Used to trigger stock ledger updates (decrease warehouse stock).
/// </summary>
public class DeliveryNoteSubmittedEvent
{
    public DeliveryNote DeliveryNote { get; }

    public DeliveryNoteSubmittedEvent(DeliveryNote deliveryNote)
    {
        DeliveryNote = deliveryNote;
    }
}
