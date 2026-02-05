using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Client.ViewModels;
using System.IO;
using System.Text;

namespace Client.Views;

public partial class ReportView : UserControl
{
    public ReportView()
    {
        InitializeComponent();

        this.AttachedToVisualTree += (_, _) =>
        {
            if (DataContext is ReportViewModel vm)
            {
                vm.ExportCSVRequested -= OnExportCsvRequested;
                vm.ExportCSVRequested += OnExportCsvRequested;
            }
        };
    }

    private async void OnExportCsvRequested(string suggestedFileName, string csvContent)
    {
        var window = this.VisualRoot as Window;
        if (window == null) return;

        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedFileName,
            FileTypeChoices = new[]
            { new FilePickerFileType("CSV") { Patterns = new[] { "*.csv" } } }
        });

        if (file == null) return;

        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        await writer.WriteAsync(csvContent);
    }
}