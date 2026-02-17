using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Client.ViewModels;
using System.IO;

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
                vm.ExportRequested -= OnExportRequested;
                vm.ExportRequested += OnExportRequested;
            }
        };
    }

    private async void OnExportRequested(string suggestedFileName, byte[] content)
    {
        var window = this.VisualRoot as Window;
        if (window == null) return;

        var ext = Path.GetExtension(suggestedFileName).TrimStart('.');
        var filterName = ext.ToUpperInvariant();
        var pattern = $"*.{ext}";

        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedFileName,
            FileTypeChoices = new[]
            {
                new FilePickerFileType(filterName) { Patterns = new[] { pattern } }
            }
        });

        if (file == null) return;

        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(content);
    }
}