namespace PSS_Time_Tracker.Services
{
    /// <summary>
    /// Signatures are stored as image data URIs ("data:image/png;base64,...") in the existing signature
    /// columns - drawn on a canvas or uploaded by the user. Older rows still hold the previously typed
    /// name, which renders as text, so both shapes have to keep working.
    /// </summary>
    public static class SignatureHelper
    {
        private const int MaxDataUriLength = 400_000;
        private const string PngPrefix = "data:image/png;base64,";
        private const string JpegPrefix = "data:image/jpeg;base64,";

        public static bool TryDecodeImage(string? value, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();
            if (string.IsNullOrWhiteSpace(value) || value.Length > MaxDataUriLength)
            {
                return false;
            }

            bool isPng = value.StartsWith(PngPrefix, StringComparison.Ordinal);
            bool isJpeg = value.StartsWith(JpegPrefix, StringComparison.Ordinal);
            if (!isPng && !isJpeg)
            {
                return false;
            }

            try
            {
                var decoded = Convert.FromBase64String(value[(isPng ? PngPrefix.Length : JpegPrefix.Length)..]);

                // Checks the real file header rather than trusting the declared MIME type.
                bool magicOk = isPng
                    ? decoded.Length > 8 && decoded[0] == 0x89 && decoded[1] == 0x50 && decoded[2] == 0x4E && decoded[3] == 0x47
                    : decoded.Length > 3 && decoded[0] == 0xFF && decoded[1] == 0xD8;
                if (!magicOk)
                {
                    return false;
                }

                bytes = decoded;
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        /// <summary>True only for a real drawn/uploaded signature image - what every new submission must carry.</summary>
        public static bool IsSignatureImage(string? value) => TryDecodeImage(value, out _);
    }
}
