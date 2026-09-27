using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace HunterModManager.Wpf.Views;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);

    private bool? wasCompact;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            var dark = 1;
            _ = DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref dark, sizeof(int));
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var workArea = SystemParameters.WorkArea;
        Width = Math.Min(Width, workArea.Width - 24);
        Height = Math.Min(Height, workArea.Height - 24);
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Top + (workArea.Height - Height) / 2;
        UpdateWorkspaceLayout();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (WorkspaceGrid is not null) UpdateWorkspaceLayout();
    }

    private void UpdateWorkspaceLayout()
    {
        var compact = ActualWidth < 900;
        if (compact == wasCompact) return;
        wasCompact = compact;
        WorkspaceGrid.ColumnDefinitions[0].Width = new GridLength(compact ? 216 : 292);
    }

    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length == 1)
        {
            if (DataContext is ViewModels.MainViewModel vm && vm.DropArchiveCommand.CanExecute(files[0]))
                vm.DropArchiveCommand.Execute(files[0]);
        }
        else
        {
            MessageBox.Show("一次请拖入一个 ZIP、RAR 或 7Z 文件。", "呆猫mod manager",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        e.Handled = true;
    }
}
