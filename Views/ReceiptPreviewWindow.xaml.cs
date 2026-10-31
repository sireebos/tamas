using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RestaurantManager.Views;

public partial class ReceiptPreviewWindow : Window
{
    private readonly FlowDocument _receiptDocument;

    public ReceiptPreviewWindow(FlowDocument receiptDocument, string orderNumber)
    {
        InitializeComponent();
        _receiptDocument = receiptDocument;
        OrderNumberLabel.Text = orderNumber;

        // Clone the document for display
        var displayDoc = CloneDocument(receiptDocument);
        ReceiptViewer.Document = displayDoc;
    }

    private async void Print_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var printDialog = new PrintDialog();

            // Print directly to the default printer without showing the dialog
            var printDoc = CloneDocument(_receiptDocument);
            printDoc.ColumnWidth = double.PositiveInfinity;
            printDoc.TextAlignment = TextAlignment.Left;

            var pageWidth = printDialog.PrintableAreaWidth;
            var pageHeight = printDialog.PrintableAreaHeight;

            // Detect receipt/thermal printers (narrow paper: 58mm ≈ 220px, 80mm ≈ 302px at 96dpi)
            if (pageWidth < 350)
            {
                // Thermal receipt printer — minimal padding, smaller font
                printDoc.PagePadding = new Thickness(4, 2, 4, 2);
                printDoc.FontSize = pageWidth < 250 ? 8 : 9; // smaller for 58mm
            }
            else
            {
                // Standard printer / PDF — normal padding
                printDoc.PagePadding = new Thickness(20, 15, 20, 15);
            }

            var paginator = ((IDocumentPaginatorSource)printDoc).DocumentPaginator;
            paginator.PageSize = new Size(pageWidth, pageHeight);
            printDialog.PrintDocument(paginator, "Receipt");
        }
        catch (System.Exception ex)
        {
            App.Logger?.LogError(ex, "Print error");
            await App.Notifications!.ShowErrorAsync("Print Error", $"Print error: {ex.Message}");
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.P && System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
        {
            Print_Click(sender, e);
            e.Handled = true;
        }
    }

    private static FlowDocument CloneDocument(FlowDocument source)
    {
        var doc = new FlowDocument
        {
            FontFamily = source.FontFamily,
            FontSize = source.FontSize,
            PagePadding = source.PagePadding,
            TextAlignment = TextAlignment.Left,
            ColumnWidth = double.PositiveInfinity
        };

        foreach (var block in source.Blocks)
        {
            if (block is BlockUIContainer buc && buc.Child is Image srcImg && srcImg.Source is BitmapImage bmp)
            {
                var newImg = new Image
                {
                    Source = bmp,
                    Width = srcImg.Width,
                    Height = srcImg.Height,
                    Stretch = srcImg.Stretch
                };
                var newBuc = new BlockUIContainer(newImg)
                {
                    TextAlignment = buc.TextAlignment,
                    Margin = buc.Margin
                };
                doc.Blocks.Add(newBuc);
            }
            else if (block is Paragraph para)
            {
                var newPara = new Paragraph
                {
                    Margin = para.Margin,
                    TextAlignment = para.TextAlignment,
                    FontWeight = para.FontWeight,
                    FontSize = para.FontSize,
                    Foreground = para.Foreground
                };
                foreach (var inline in para.Inlines)
                {
                    if (inline is Run run)
                    {
                        var newRun = new Run(run.Text)
                        {
                            FontWeight = run.FontWeight
                        };
                        newPara.Inlines.Add(newRun);
                    }
                    else if (inline is InlineUIContainer iuc && iuc.Child is Image inlineImg && inlineImg.Source is BitmapImage inlineBmp)
                    {
                        var newImg = new Image
                        {
                            Source = inlineBmp,
                            Width = inlineImg.Width,
                            Height = inlineImg.Height,
                            Stretch = inlineImg.Stretch
                        };
                        newPara.Inlines.Add(new InlineUIContainer(newImg));
                    }
                    else if (inline is LineBreak)
                    {
                        newPara.Inlines.Add(new LineBreak());
                    }
                }
                doc.Blocks.Add(newPara);
            }
        }

        return doc;
    }
}
