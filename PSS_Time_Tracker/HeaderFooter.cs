using iTextSharp.text.pdf;
using iTextSharp.text;

namespace TimeSheetRecorder
{
    public class HeaderFooter : PdfPageEventHelper
    {
        private Image _headerImage;
        private Image _footerImage;
        private float _headerHeight;
        private float _footerHeight;
        private readonly IWebHostEnvironment _hostingEnvironment;

        public HeaderFooter(IWebHostEnvironment hostingEnvironment, string headerImagePath, string footerImagePath)
        {
            _hostingEnvironment = hostingEnvironment;

            // Load header image
            string fullHeaderPath = Path.Combine(_hostingEnvironment.WebRootPath, headerImagePath);
            _headerImage = Image.GetInstance(fullHeaderPath);
            _headerImage.ScaleToFit(PageSize.A4.Width, 100); 
            _headerHeight = _headerImage.ScaledHeight;

            // Load footer image
            string fullFooterPath = Path.Combine(_hostingEnvironment.WebRootPath, footerImagePath);
            _footerImage = Image.GetInstance(fullFooterPath);
            _footerImage.ScaleToFit(PageSize.A4.Width, 100);
            _footerHeight = _footerImage.ScaledHeight;
        }

        public override void OnEndPage(PdfWriter writer, Document document)
        {
            base.OnEndPage(writer, document);

            
            _headerImage.SetAbsolutePosition(
                0, 
                document.PageSize.Height - _headerHeight);
            writer.DirectContent.AddImage(_headerImage);

            
            _footerImage.SetAbsolutePosition(
                0, 
                0); 
            writer.DirectContent.AddImage(_footerImage);
        }

        public override void OnStartPage(PdfWriter writer, Document document)
        {
            base.OnStartPage(writer, document);

           
            document.SetMargins(
                document.LeftMargin,
                document.RightMargin,
                _headerHeight + 30,  
                _footerHeight + 10);  
        }
    }
}