using System.IO;
using System.Windows;
using LabQC.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace LabQC.Desktop;
public partial class App : System.Windows.Application
{
 protected override async void OnStartup(StartupEventArgs e) { base.OnStartup(e); var root = Environment.GetEnvironmentVariable("LABQC_DATA_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LabQC"); Directory.CreateDirectory(root); var options = new DbContextOptionsBuilder<LabDbContext>().UseSqlite($"Data Source={Path.Combine(root, "labqc.db")}").Options; await using (var db = new LabDbContext(options)) await DatabaseSeeder.SeedAsync(db); MainWindow = new MainWindow(options, root); MainWindow.Show(); }
}
