namespace PSS_Time_Tracker.Models
{
    /// <summary>Settings for the shared draw/upload signature pad (Views/Shared/_SignaturePad.cshtml).</summary>
    public class SignaturePadModel
    {
        /// <summary>Form field name the signature image (a PNG data URI) posts under.</summary>
        public string Name { get; set; } = "Signature";

        /// <summary>Unique per pad on a page - several pads can share one page (e.g. the leave approval lists).</summary>
        public string Id { get; set; } = "signaturePad";

        public bool Compact { get; set; }
    }
}
