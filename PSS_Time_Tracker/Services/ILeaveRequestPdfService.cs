namespace PSS_Time_Tracker.Services
{
    /// <summary>
    /// Builds the branded "Employee Leave Application" PDF - a field-for-field replica of the real
    /// Providence Managed Services paper form, pre-filled from a <see cref="LeaveRequestPdfRequest"/>.
    /// </summary>
    public interface ILeaveRequestPdfService
    {
        /// <returns>The rendered PDF as a byte array, ready to return via <c>File(bytes, "application/pdf", fileName)</c>.</returns>
        byte[] GenerateLeaveForm(LeaveRequestPdfRequest request);
    }
}
