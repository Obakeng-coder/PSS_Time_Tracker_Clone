using iTextSharp.text.pdf;
using iTextSharp.text;

namespace PSS_Time_Tracker
{
    public class AnnotatedCellEvent : IPdfPCellEvent
    {
        private readonly PdfWriter _writer;
        private readonly PdfAnnotation _annotation;

        public AnnotatedCellEvent(PdfWriter writer, PdfAnnotation annotation)
        {
            _writer = writer;
            _annotation = annotation;
        }

        public void CellLayout(PdfPCell cell, Rectangle position, PdfContentByte[] canvases)
        {
           
            float iconSize = 20f;

          
            var annotationRect = new Rectangle(
                position.Right - iconSize, 
                position.Bottom,           
                position.Right,           
                position.Bottom + iconSize 
            );

            _annotation.Put(PdfName.RECT, new PdfRectangle(annotationRect));
            _writer.AddAnnotation(_annotation);
        }

    }

}