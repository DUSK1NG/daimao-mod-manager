using System.Reflection;
using HunterModManager.App;
using HunterModManager.Core;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        ApplicationConfiguration.Initialize();
        var temporary = Path.Combine(Path.GetTempPath(), "HunterModManager.LayoutTests", Guid.NewGuid().ToString("N"));
        try
        {
            var sevenZip = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7z.exe");
            var archives = new ArchiveService(sevenZip);
            var manager = new ModManager(temporary, archives);
            using var form = new MainForm(manager, archives) { ShowInTaskbar = false, Opacity = 0 };
            form.Show();
            Application.DoEvents();

            var split = Descendants(form).OfType<SplitContainer>().Single();
            var scale = form.DeviceDpi / 96f;
            form.Size = new Size((int)(780 * scale), (int)(650 * scale));
            Application.DoEvents();
            Check(split.Orientation == Orientation.Horizontal, "窄窗体应改为上下分栏");
            Check(split.Width <= split.Parent!.ClientSize.Width, "分栏不能超出窗体宽度");
            var import = Descendants(split.Panel2).OfType<TableLayoutPanel>().Single();
            var chooseArchive = Descendants(import).OfType<Button>().Single(button => button.Text == "选择…");
            Check(chooseArchive.Right <= import.ClientSize.Width, "选择压缩包按钮不能被右侧裁掉");

            form.Size = new Size((int)(1200 * scale), (int)(760 * scale));
            Application.DoEvents();
            Check(split.Orientation == Orientation.Vertical, "宽窗体应恢复左右分栏");

            form.Size = new Size(3000, 2000);
            typeof(MainForm).GetMethod("FitToWorkingArea", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
            var area = Screen.FromControl(form).WorkingArea;
            Check(area.Contains(form.Bounds), "启动时窗体应保持在屏幕可用区域内");
            Console.WriteLine($"布局测试通过：窄窗控件可见、宽窗左右分栏、启动尺寸限制；DPI={form.DeviceDpi}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
        }
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
