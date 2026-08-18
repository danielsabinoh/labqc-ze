using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using LabQC.Application;
using LabQC.Domain;
using LabQC.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
namespace LabQC.Desktop;
public partial class MainWindow : Window
{
 private readonly DbContextOptions<LabDbContext> _options; private readonly string _root; private User? _user; private DataTable? _table;
 public MainWindow(DbContextOptions<LabDbContext> options, string root) { InitializeComponent(); _options = options; _root = root; Loaded += MainWindow_Loaded; }
 private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
 {
  await using var db = new LabDbContext(_options);
  FirstAccessHint.Visibility = await db.Users.AnyAsync(x => x.Username == "admin" && x.MustChangePassword) ? Visibility.Visible : Visibility.Collapsed;
  UsernameBox.Focus();
 }
 private async void Login_Click(object s, RoutedEventArgs e)
 {
  try
  {
   await using var db = new LabDbContext(_options); _user = await new AuthenticationService(db).AuthenticateAsync(UsernameBox.Text, PasswordBox.Password);
   if (_user is null) { LoginError.Text = "Usuário ou senha inválidos."; return; }
   if (_user.MustChangePassword)
   {
    var updated = await CatalogDialogs.EditAccountAsync(this, _options, _user, true);
    if (updated is null) { _user = null; PasswordBox.Clear(); LoginError.Text = "Para continuar, altere a senha provisória."; return; }
    _user = updated; FirstAccessHint.Visibility = Visibility.Collapsed;
   }
   CurrentUserText.Text = $"{_user.FullName}\n{PortugueseLabels.UserRole(_user.Role)}"; Pages.SelectedIndex = 0; await RefreshAsync();
   LoginPanel.Visibility = Visibility.Collapsed; Shell.Visibility = Visibility.Visible;
  }
  catch (Exception error)
  {
   LoginPanel.Visibility = Visibility.Visible; Shell.Visibility = Visibility.Collapsed;
   LoginError.Text = $"Não foi possível abrir o sistema: {error.Message}";
  }
 }
 private void PasswordBox_KeyDown(object s, KeyEventArgs e) { if (e.Key == Key.Enter) Login_Click(s, e); }
 private void Nav_Click(object s, RoutedEventArgs e) => Pages.SelectedIndex = int.Parse((string)((Button)s).Tag);
 private async void MyAccount_Click(object s, RoutedEventArgs e)
 {
  if (_user is null) return; var updated = await CatalogDialogs.EditAccountAsync(this, _options, _user, false);
  if (updated is not null) { _user = updated; CurrentUserText.Text = $"{_user.FullName}\n{PortugueseLabels.UserRole(_user.Role)}"; MessageBox.Show("Conta atualizada.", "LabQC"); }
 }
 private void Grid_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
 {
  if (e.PropertyName is "Id" or "Pdf") e.Cancel = true;
 }
 private async Task RefreshAsync()
 {
  await using var db = new LabDbContext(_options);
  InAnalysisCount.Text = (await db.Lots.CountAsync(x => x.Status == LotStatus.InAnalysis)).ToString();
  AwaitingCount.Text = (await db.Lots.CountAsync(x => x.Status == LotStatus.AwaitingRelease)).ToString();
  NonConformityCount.Text = "0";

  // SQLite stores DateTimeOffset as TEXT and cannot translate ORDER BY for that CLR type.
  // Materialize first, then order in memory. PostgreSQL can keep using the same domain model.
  var lots = (await db.Lots.Include(x => x.Product).ToListAsync())
   .OrderByDescending(x => x.OpenedAt).ToList();
  var certificates = (await db.Certificates.ToListAsync())
   .OrderByDescending(x => x.IssuedAt).ToList();

  LotPicker.ItemsSource = lots.Select(x => new Picker(x.Id, $"{x.Number} — {x.Product.DisplayName}")).ToList();
  LotsGrid.ItemsSource = lots.Select(x => new LotRow(x.Id, x.Number, x.Product.DisplayName, x.ManufactureDate, PortugueseLabels.LotStatus(x.Status))).ToList();
  var products = await db.Products.OrderBy(x => x.Name).ThenBy(x => x.CommercialUnit).ToListAsync();
  ProductsGrid.ItemsSource = products.Select(x => new ProductRow(x.Id, x.Code, x.DisplayName, x.ShelfLifeMonths + " meses", PortugueseLabels.Active(x.IsActive))).ToList();
  var parameters = await db.AnalysisParameters.OrderBy(x => x.Name).ToListAsync();
  ParametersGrid.ItemsSource = parameters.Select(x => new ParameterRow(x.Id, x.Code, x.Name, PortugueseLabels.Category(x.Category), x.Unit, PortugueseLabels.ResultType(x.ResultType), PortugueseLabels.Active(x.IsActive))).ToList();
  CertificatesGrid.ItemsSource = certificates.Select(x => new CertificateRow(x.Id, x.Number, x.Version, x.ProductName, x.LotNumber, x.ClientName, x.IssuedAt, x.PdfPath)).ToList();
 }
 private bool RequireAdministrator()
 {
  if (_user?.Role == UserRole.Administrator) return true;
  MessageBox.Show("Esta operação exige o perfil Administrador.", "LabQC", MessageBoxButton.OK, MessageBoxImage.Warning); return false;
 }
 private async void NewProduct_Click(object s, RoutedEventArgs e) { if (_user is not null && RequireAdministrator() && await CatalogDialogs.NewProductAsync(this, _options, _user.Id)) await RefreshAsync(); }
 private async void NewParameter_Click(object s, RoutedEventArgs e) { if (RequireAdministrator() && await CatalogDialogs.NewParameterAsync(this, _options)) await RefreshAsync(); }
 private async void EditProduct_Click(object s, RoutedEventArgs e) { if (_user is null || !RequireAdministrator()) return; if (ProductsGrid.SelectedItem is not ProductRow row) { MessageBox.Show("Selecione um produto.", "LabQC"); return; } if (await CatalogDialogs.EditProductAsync(this, _options, row.Id, _user.Id)) await RefreshAsync(); }
 private async void DeleteProduct_Click(object s, RoutedEventArgs e) { if (_user is null || !RequireAdministrator()) return; if (ProductsGrid.SelectedItem is not ProductRow row) { MessageBox.Show("Selecione um produto.", "LabQC"); return; } if (await CatalogDialogs.DeleteProductAsync(this, _options, row.Id, _user.Id)) await RefreshAsync(); }
 private async void EditParameter_Click(object s, RoutedEventArgs e) { if (_user is null || !RequireAdministrator()) return; if (ParametersGrid.SelectedItem is not ParameterRow row) { MessageBox.Show("Selecione um parâmetro.", "LabQC"); return; } if (await CatalogDialogs.EditParameterAsync(this, _options, row.Id, _user.Id)) await RefreshAsync(); }
 private async void DeleteParameter_Click(object s, RoutedEventArgs e) { if (_user is null || !RequireAdministrator()) return; if (ParametersGrid.SelectedItem is not ParameterRow row) { MessageBox.Show("Selecione um parâmetro.", "LabQC"); return; } if (await CatalogDialogs.DeleteParameterAsync(this, _options, row.Id, _user.Id)) await RefreshAsync(); }
 private async void NewLot_Click(object s, RoutedEventArgs e) { if (_user is not null && await CatalogDialogs.NewLotAsync(this, _options, _user.Id)) { await RefreshAsync(); MessageBox.Show("Lote aberto com a especificação congelada.", "LabQC"); } }
 private async void Refresh_Click(object s, RoutedEventArgs e) => await RefreshAsync();
 private async Task ChangeLotStatusAsync(LotStatus target, string justification = "")
 {
  try
  {
   if (_user is null || LotsGrid.SelectedItem is not LotRow selected) { MessageBox.Show("Selecione um lote na lista.", "LabQC"); return; }
   await using var db = new LabDbContext(_options);
   var lot = await db.Lots.AsNoTracking().SingleAsync(x => x.Id == selected.Id); var old = lot.Status; var now = DateTimeOffset.Now;
   // Reuse the domain rules without keeping a stale tracked entity in the WPF session.
   var release = LotWorkflow.Transition(lot, target, _user, justification, now);
   await using var transaction = await db.Database.BeginTransactionAsync();
   var affected = await db.Lots.Where(x => x.Id == lot.Id && x.Status == old).ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, target));
   if (affected == 0)
   {
    await transaction.RollbackAsync(); await RefreshAsync();
    MessageBox.Show("O lote foi alterado por outra ação. A lista foi atualizada; selecione-o novamente.", "Lote atualizado", MessageBoxButton.OK, MessageBoxImage.Information); return;
   }
   db.LotReleases.Add(release);
   db.AuditEntries.Add(new AuditEntry { UserId = _user.Id, OccurredAt = now, EntityName = nameof(Lot), EntityId = lot.Id.ToString(), Action = "Situação alterada", OldValue = old.ToString(), NewValue = target.ToString(), Justification = justification });
   await db.SaveChangesAsync(); await transaction.CommitAsync(); await RefreshAsync();
  }
  catch (Exception error) { MessageBox.Show(error.Message, "Não foi possível alterar o lote", MessageBoxButton.OK, MessageBoxImage.Warning); }
 }
 private async void SubmitLot_Click(object s, RoutedEventArgs e) => await ChangeLotStatusAsync(LotStatus.AwaitingRelease);
 private async void ApproveLot_Click(object s, RoutedEventArgs e) => await ChangeLotStatusAsync(LotStatus.Approved, "Revisado e aprovado pelo responsável");
 private async void RejectLot_Click(object s, RoutedEventArgs e) { var reason = CatalogDialogs.AskText(this, "Reprovar lote", "Justificativa obrigatória"); if (reason is not null) await ChangeLotStatusAsync(LotStatus.Rejected, reason); }
 private async void IssueCertificate_Click(object s, RoutedEventArgs e)
 {
  if (_user is null || _user.Role == UserRole.Analyst) { MessageBox.Show("A emissão exige perfil Qualidade ou Administrador.", "LabQC"); return; }
  try { var certificate = await CatalogDialogs.IssueCertificateAsync(this, _options, _user.Id, _root); if (certificate is not null) { await RefreshAsync(); MessageBox.Show($"Certificado {certificate.Number} gerado em:\n{certificate.PdfPath}", "Laudo emitido"); } }
  catch (Exception error) { MessageBox.Show(error.Message, "Não foi possível gerar o laudo", MessageBoxButton.OK, MessageBoxImage.Error); }
 }
 private void OpenCertificate_Click(object s, RoutedEventArgs e)
 {
  if (CertificatesGrid.SelectedItem is not CertificateRow selected || string.IsNullOrWhiteSpace(selected.Pdf) || !File.Exists(selected.Pdf)) { MessageBox.Show("Selecione um certificado com PDF disponível.", "LabQC"); return; }
  Process.Start(new ProcessStartInfo(selected.Pdf) { UseShellExecute = true });
 }
 private async void LotPicker_SelectionChanged(object s, SelectionChangedEventArgs? e) { if (LotPicker.SelectedItem is not Picker p) return; await using var db = new LabDbContext(_options); var lot = await db.Lots.Include(x=>x.Parameters).Include(x=>x.Samples).ThenInclude(x=>x.Results).SingleAsync(x=>x.Id==p.Id); _table = new DataTable(); _table.Columns.Add("Amostra"); foreach(var lp in lot.Parameters.OrderBy(x=>x.SortOrder)) _table.Columns.Add(lp.ParameterName + (lp.Unit==""?"":$" ({lp.Unit})")); foreach(var sample in lot.Samples.Where(x=>x.IsActive).OrderBy(x=>x.Code)) { var row=_table.NewRow(); row[0]=sample.Code; var pars=lot.Parameters.OrderBy(x=>x.SortOrder).ToList(); for(int i=0;i<pars.Count;i++){var r=sample.Results.SingleOrDefault(x=>x.LotParameterId==pars[i].Id&&x.IsCurrent); row[i+1]=r?.NumericValue?.ToString(System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))??r?.TextValue??"";} _table.Rows.Add(row);} AnalysisGrid.ItemsSource=_table.DefaultView; }
 private void NewSample_Click(object s, RoutedEventArgs e) { if(_table is null)return; var r=_table.NewRow();r[0]=(_table.Rows.Count+1).ToString("00");_table.Rows.Add(r); }
 private async void SaveAnalysis_Click(object s, RoutedEventArgs e)
 {
  try
  {
   if (_table is null || LotPicker.SelectedItem is not Picker picker || _user is null) { MessageBox.Show("Selecione um lote.", "LabQC"); return; }
   AnalysisGrid.CommitEdit(); AnalysisGrid.CommitEdit();
   await using var db = new LabDbContext(_options);
   var lot = await db.Lots.Include(x => x.Parameters).Include(x => x.Samples).SingleAsync(x => x.Id == picker.Id);
   var parameters = lot.Parameters.OrderBy(x => x.SortOrder).ToList(); var service = new AnalysisEntryService(db); var saved = 0;
   foreach (DataRow row in _table.Rows)
   {
    var code = row[0]?.ToString()?.Trim(); if (string.IsNullOrWhiteSpace(code)) continue;
    var sample = lot.Samples.SingleOrDefault(x => x.Code == code);
    if (sample is null) { sample = new Sample { LotId = lot.Id, Code = code, CollectedAt = DateTimeOffset.Now }; db.Samples.Add(sample); lot.Samples.Add(sample); await db.SaveChangesAsync(); }
    for (var i = 0; i < parameters.Count; i++)
    {
     var raw = row[i + 1]?.ToString()?.Trim(); if (string.IsNullOrWhiteSpace(raw)) continue;
     if (await db.AnalysisResults.AnyAsync(x => x.SampleId == sample.Id && x.LotParameterId == parameters[i].Id && x.IsCurrent)) continue;
     decimal? numeric = null; string? text = null; bool? conformity = null;
     if (parameters[i].ResultType == ResultType.Numeric)
     {
      if (!BrazilianDecimal.TryParse(raw, out var value)) throw new InvalidOperationException($"Valor inválido: amostra {code}, {parameters[i].ParameterName} = {raw}");
      numeric = value;
     }
     else if (parameters[i].ResultType == ResultType.Conformity)
     {
      if (raw.Equals("Conforme", StringComparison.OrdinalIgnoreCase) || raw.Equals("C", StringComparison.OrdinalIgnoreCase)) conformity = true;
      else if (raw.Equals("Não conforme", StringComparison.OrdinalIgnoreCase) || raw.Equals("NC", StringComparison.OrdinalIgnoreCase)) conformity = false;
      else throw new InvalidOperationException($"Use Conforme ou Não conforme em {parameters[i].ParameterName}.");
     }
     else text = raw;
     await service.SaveCorrectionSafeAsync(sample.Id, parameters[i].Id, numeric, text, conformity, _user, null); saved++;
    }
   }
   MessageBox.Show($"{saved} resultado(s) salvo(s).", "LabQC", MessageBoxButton.OK, MessageBoxImage.Information);
   LotPicker_SelectionChanged(LotPicker, null); await RefreshAsync();
  }
  catch (Exception error) { MessageBox.Show(error.Message, "Não foi possível salvar", MessageBoxButton.OK, MessageBoxImage.Error); }
 }
 private void AnalysisGrid_PreviewKeyDown(object s, KeyEventArgs e) { if(e.Key==Key.Enter){e.Handled=true;AnalysisGrid.CommitEdit();var n=AnalysisGrid.Columns.IndexOf(AnalysisGrid.CurrentCell.Column)+1;if(n>=AnalysisGrid.Columns.Count){NewSample_Click(s,e);return;}AnalysisGrid.CurrentCell=new DataGridCellInfo(AnalysisGrid.CurrentItem,AnalysisGrid.Columns[n]);AnalysisGrid.BeginEdit();} if(e.Key==Key.N&&Keyboard.Modifiers==ModifierKeys.Control){e.Handled=true;NewSample_Click(s,e);} if(e.Key==Key.S&&Keyboard.Modifiers==ModifierKeys.Control){e.Handled=true;SaveAnalysis_Click(s,e);} }
 private async void Backup_Click(object s, RoutedEventArgs e) { var dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"LabQC Backups");var svc=new BackupService(Path.Combine(_root,"labqc.db"),Path.Combine(_root,"Certificados"));var p=await svc.CreateAsync(dir);BackupStatus.Text=$"Criado e verificado: {p}\n{await BackupService.VerifyAsync(p)}"; }
 private async void VerifyBackup_Click(object s, RoutedEventArgs e) { var d=new OpenFileDialog{Filter="Backup LabQC|*.labbackup"};if(d.ShowDialog()==true)BackupStatus.Text=await BackupService.VerifyAsync(d.FileName)?"Backup íntegro.":"Backup inválido."; }
 private sealed record Picker(Guid Id,string Display);
 private sealed record LotRow(Guid Id, string Lote, string Produto, DateOnly Fabricação, string Situação);
 private sealed record ProductRow(Guid Id, string Código, string Produto, string Validade, string Situação);
 private sealed record ParameterRow(Guid Id, string Código, string Parâmetro, string Categoria, string Unidade, string Tipo, string Situação);
 private sealed record CertificateRow(Guid Id, string Número, int Versão, string Produto, string Lote, string Cliente, DateTimeOffset Emissão, string Pdf);
}
