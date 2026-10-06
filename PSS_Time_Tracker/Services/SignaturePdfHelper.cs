using iTextSharp.text;
using iTextSharp.text.pdf;

namespace PSS_Time_Tracker.Services
{
    public static class SignaturePdfHelper
    {
        /// <summary>
        /// A drawn/uploaded signature prints as its image, scaled to fit. Anything else - an older typed
        /// signature, or one of the system labels like "(Awaiting Manager Confirmation)" - prints as text
        /// in <paramref name="textFont"/>, exactly as before.
        /// </summary>
        public static PdfPCell BuildCell(
            string? signature, Font textFont, float maxImageWidth, float maxImageHeight,
            int padding, float minHeight, int horizontalAlignment = Element.ALIGN_CENTER)
        {
            PdfPCell cell;

            if (SignatureHelper.TryDecodeImage(signature, out var bytes))
            {
                try
                {
                    var image = Image.GetInstance(bytes);
                    image.ScaleToFit(maxImageWidth, maxImageHeight);
                    cell = new PdfPCell(image, false);
                }
                catch (Exception)
                {
                    cell = new PdfPCell(new Phrase("", textFont));
                }
            }
            else
            {
                cell = new PdfPCell(new Phrase(signature ?? "", textFont));
            }

            cell.BackgroundColor = BaseColor.WHITE;
            cell.HorizontalAlignment = horizontalAlignment;
            cell.VerticalAlignment = Element.ALIGN_MIDDLE;
            cell.Padding = padding;
            cell.MinimumHeight = minHeight;
            return cell;
        }
    }
}
