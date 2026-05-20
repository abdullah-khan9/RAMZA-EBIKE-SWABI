using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QRCoder;
using Ramza_EBike_Swabi.Models;
using System.Globalization;

namespace Ramza_EBike_Swabi.Views.Pages
{
    public partial class QRCodeWindow : Window
    {
        private BitmapSource _qrBitmap;

        public QRCodeWindow(Bike bike)
        {
            InitializeComponent();

            TitleText.Text = $"{bike.Brand} - {bike.Model}";

            var priceText = bike.Price.ToString("N2", CultureInfo.InvariantCulture);
            var addedDate = bike.AddedDate.ToString("yyyy-MM-dd");

            // QR code payload
            var payload =
$@"Model: {bike.Model}{Environment.NewLine}
Brand: {bike.Brand}{Environment.NewLine}
Motor Number: {bike.MotorNumber}{Environment.NewLine}
Chassis Number: {bike.ChassisNumber}{Environment.NewLine}
Motor Power: {bike.MotorPower}{Environment.NewLine}
Battery: {bike.BatteryCapacity}{Environment.NewLine}
Color: {bike.Color}{Environment.NewLine}
Warranty: {bike.Warranty}{Environment.NewLine}
Price: PKR {priceText}";

            // Generate QR code
            using var qrGenerator = new QRCodeGenerator();
            using var data = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(data);
            var qrBytes = qrCode.GetGraphic(20, System.Drawing.Color.Black, System.Drawing.Color.White, true);
            _qrBitmap = LoadImage(qrBytes);

            QrImage.Source = _qrBitmap;
        }

        private BitmapSource LoadImage(byte[] imageData)
        {
            var bi = new BitmapImage();
            using var ms = new MemoryStream(imageData);
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.StreamSource = ms;
            bi.EndInit();
            bi.Freeze();
            return bi;
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            if (_qrBitmap == null) return;

            // Page size
            double pageWidth = 827, pageHeight = 1169; // A4 default in 1/96 inch
            var selectedItem = (PageSizeCombo.SelectedItem as ComboBoxItem)?.Content.ToString();
            switch (selectedItem)
            {
                case "A4": pageWidth = 827; pageHeight = 1169; break;
                case "A5": pageWidth = 583; pageHeight = 827; break;
            }

            // Rows & columns
            if (!int.TryParse(RowsBox.Text, out int rows) || rows < 1) rows = 1;
            if (!int.TryParse(ColsBox.Text, out int cols) || cols < 1) cols = 1;

            // Create FlowDocument for preview
            var doc = new FlowDocument
            {
                PageWidth = pageWidth,
                PageHeight = pageHeight,
                ColumnWidth = pageWidth,
                Background = Brushes.White,
                PagePadding = new Thickness(20)
            };

            // Table for QR code grid
            var table = new Table();
            for (int c = 0; c < cols; c++) table.Columns.Add(new TableColumn());

            for (int r = 0; r < rows; r++)
            {
                var row = new TableRow();
                for (int c = 0; c < cols; c++)
                {
                    var cell = new TableCell();
                    var image = new Image
                    {
                        Source = _qrBitmap,
                        Width = (pageWidth - 40) / cols * 0.8,
                        Height = (pageWidth - 40) / cols * 0.8,
                        Stretch = Stretch.Uniform
                    };
                    cell.Blocks.Add(new BlockUIContainer(image));
                    row.Cells.Add(cell);
                }
                var tableRowGroup = new TableRowGroup();
                tableRowGroup.Rows.Add(row);
                table.RowGroups.Add(tableRowGroup);
            }

            doc.Blocks.Add(table);

            // Print dialog
            var pd = new PrintDialog();
            if (pd.ShowDialog() == true)
            {
                // Wrap document for preview + printing
                IDocumentPaginatorSource idpSource = doc;
                pd.PrintDocument(idpSource.DocumentPaginator, $"QR Code - {TitleText.Text}");
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
