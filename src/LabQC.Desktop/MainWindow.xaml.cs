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
 private List<Picker> _openLots = []; private List<LotRow> _lots = []; private List<ProductRow> _products = []; private List<ParameterRow> _parameters = []; private List<ProductChoice> _productChoices = [];
 public MainWindow(DbContextOptions<LabDbContext> options, string root) { InitializeComponent(); _options = options; _root = root; Loaded += MainWindow_Loaded; }
 private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
 {
  FitWindowToWorkArea();
  await using var db = new LabDbContext(_options);
  FirstAccessHint.Visibility = await db.Users.AnyAsync(x => x.Username == "admin" && x.MustChangePassword) ? Visibility.Visible : Visibility.Collapsed;
  UsernameBox.Focus();
 }
 private void FitWindowToWorkArea()
 {
  var area = SystemParameters.WorkArea;
  MinWidth = Math.Min(1080, area.Width);
  MinHeight = Math.Min(700, area.Height);
  var targetWidth = Math.Min(1320, area.Width * 0.94);
  var targetHeight = Math.Min(840, area.Height * 0.92);
  WindowState = WindowState.Normal;
  Width = Math.Max(MinWidth, targetWidth);
  Height = Math.Max(MinHeight, targetHeight);
  if (Width > area.Width) Width = area.Width;
  if (Height > area.Height) Height = area.Height;
  Left = area.Left + Math.Max(0, (area.Width - Width) / 2);
  Top = area.Top + Math.Max(0, (area.Height - Height) / 2);
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
   CurrentUserText.Text = $"{_user.FullName}\n{PortugueseLabels.UserRole(_user.Role)}"; AccessNavButton.Visibility = _user.Role == UserRole.Administrator ? Visibility.Visible : Visibility.Collapsed; Pages.SelectedIndex = 0; await RefreshAsync();
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
  if (e.PropertyName is "Id" or "ProductId" or "Pdf") e.Cancel = true;
 }
 private async Task RefreshAsync(Guid? preferredProductId = null, Guid? preferredLotId = null)
 {
  await using var db = new LabDbContext(_options);
  var previousAnalysisProduct = preferredProductId ?? (AnalysisProductPicker.SelectedItem as ProductChoice)?.Id;
  var previousLotsProduct = preferredProductId ?? (LotsProductPicker.SelectedItem as ProductChoice)?.Id;
  var previousLot = preferredLotId ?? (LotPicker.SelectedItem as Picker)?.Id;
  var lots = (await db.Lots.Include(x => x.Product).Include(x => x.Parameters).Include(x => x.Samples).ThenInclude(x => x.Results).ToListAsync()).OrderByDescending(x => x.OpenedAt).ToList();
  var certificates = (await db.Certificates.ToListAsync())
   .OrderByDescending(x => x.IssuedAt).ToList();
  var products = await db.Products.OrderBy(x => x.Name).ThenBy(x => x.CommercialUnit).ToListAsync();
  var parameters = await db.AnalysisParameters.OrderBy(x => x.Name).ToListAsync();
  var users = await db.Users.OrderBy(x => x.FullName).ToListAsync();

  var openLots = lots.Where(x => x.Status == LotStatus.InAnalysis).ToList();
  var pending = 0; var nonConforming = 0;
  foreach (var lot in openLots)
  {
   var consolidated = lot.Parameters.Select(p => ConsolidationEngine.Consolidate(p, lot.Samples.SelectMany(x => x.Results).Where(x => x.LotParameterId == p.Id))).ToList();
   if (consolidated.Any(x => x.Conformity == ConformityStatus.Pending)) pending++;
   if (consolidated.Any(x => x.Conformity == ConformityStatus.NonConforming)) nonConforming++;
  }
  InAnalysisCount.Text = openLots.Count.ToString(); PendingCount.Text = pending.ToString(); NonConformityCount.Text = nonConforming.ToString(); AwaitingCount.Text = lots.Count(x => x.Status == LotStatus.Closed).ToString();
  DashboardAlert.Text = nonConforming > 0 ? $"Atenção: {nonConforming} lote(s) com resultado fora da especificação." : pending > 0 ? $"{pending} lote(s) ainda possuem análises pendentes." : "Tudo em dia nos lotes abertos.";

  _openLots = openLots.Select(x => new Picker(x.Id, x.ProductId, $"Lote {x.Number} — {Progress(x)} preenchido")).ToList();
  _lots = lots.Select(x => new LotRow(x.Id, x.ProductId, x.Number, x.Product.DisplayName, x.ManufactureDate, Progress(x), PortugueseLabels.LotStatus(x.Status))).ToList();
  _products = products.Select(x => new ProductRow(x.Id, x.Code, x.Family, x.DisplayName, x.ShelfLifeMonths + " meses", PortugueseLabels.Active(x.IsActive))).ToList();
  _parameters = parameters.Select(x => new ParameterRow(x.Id, x.Code, x.Name, PortugueseLabels.Category(x.Category), x.Unit, PortugueseLabels.ResultType(x.ResultType), PortugueseLabels.Active(x.IsActive))).ToList();

  _productChoices = products.Where(x => x.IsActive || lots.Any(l => l.ProductId == x.Id)).Select(x => new ProductChoice(x.Id, x.Family, x.DisplayName, openLots.Count(l => l.ProductId == x.Id))).ToList();
  var families = _productChoices.GroupBy(x => x.Family).OrderBy(x => ProductFamilies.Standard.ToList().IndexOf(x.Key)).Select(x => new FamilyChoice(x.Key, x.Count())).ToList();
  AnalysisFamilyPicker.ItemsSource = families; LotsFamilyPicker.ItemsSource = families;
  var analysisFamily = _productChoices.FirstOrDefault(x => x.Id == previousAnalysisProduct)?.Family ?? (AnalysisFamilyPicker.SelectedItem as FamilyChoice)?.Name;
  var lotsFamily = _productChoices.FirstOrDefault(x => x.Id == previousLotsProduct)?.Family ?? (LotsFamilyPicker.SelectedItem as FamilyChoice)?.Name;
  AnalysisFamilyPicker.SelectedItem = families.FirstOrDefault(x => x.Name == analysisFamily) ?? families.FirstOrDefault();
  LotsFamilyPicker.SelectedItem = families.FirstOrDefault(x => x.Name == lotsFamily) ?? families.FirstOrDefault();
  ApplyAnalysisFamilyFilter(previousAnalysisProduct); ApplyLotsFamilyFilter(previousLotsProduct);
  ApplyProductFilters(); ApplyParameterFilters(); ApplyAnalysisProductFilter(previousLot); ApplyLotsProductFilter();
  RecentLotsGrid.ItemsSource = _lots.Take(8).ToList();
  CertificatesGrid.ItemsSource = certificates.Select(x => new CertificateRow(x.Id, x.Number, x.Version, x.ProductName, x.LotNumber, x.ClientName, x.IssuedAt, x.PdfPath)).ToList();
  UsersGrid.ItemsSource = users.Select(x => new UserRow(x.Id, x.FullName, x.Username, PortugueseLabels.UserRole(x.Role), x.IsActive ? "Ativo" : "Desativado", x.MustChangePassword ? "Troca pendente" : "Definida")).ToList();
 }

 private static string Progress(Lot lot)
 {
  var completed = lot.Parameters.Count(p => ConsolidationEngine.Consolidate(p, lot.Samples.SelectMany(x => x.Results).Where(x => x.LotParameterId == p.Id)).Conformity != ConformityStatus.Pending);
  return $"{completed}/{lot.Parameters.Count}";
 }

 private void ApplyAnalysisProductFilter(Guid? preferredLotId = null)
 {
  if (AnalysisProductPicker.SelectedItem is not ProductChoice product) { LotPicker.ItemsSource = null; AnalysisGrid.ItemsSource = null; _table = null; return; }
  var filtered = _openLots.Where(x => x.ProductId == product.Id).ToList(); LotPicker.ItemsSource = filtered;
  LotPicker.SelectedItem = filtered.FirstOrDefault(x => x.Id == preferredLotId) ?? filtered.FirstOrDefault();
  if (filtered.Count == 0) { AnalysisGrid.ItemsSource = null; _table = null; }
 }

 private void ApplyAnalysisFamilyFilter(Guid? preferredProductId = null)
 {
  if (AnalysisFamilyPicker.SelectedItem is not FamilyChoice family) { AnalysisProductPicker.ItemsSource = null; ApplyAnalysisProductFilter(); return; }
  var products = _productChoices.Where(x => x.Family == family.Name).ToList(); AnalysisProductPicker.ItemsSource = products;
  AnalysisProductPicker.SelectedItem = products.FirstOrDefault(x => x.Id == preferredProductId) ?? products.FirstOrDefault();
 }

 private void ApplyLotsFamilyFilter(Guid? preferredProductId = null)
 {
  if (LotsFamilyPicker.SelectedItem is not FamilyChoice family) { LotsProductPicker.ItemsSource = null; ApplyLotsProductFilter(); return; }
  var products = _productChoices.Where(x => x.Family == family.Name).ToList(); LotsProductPicker.ItemsSource = products;
  LotsProductPicker.SelectedItem = products.FirstOrDefault(x => x.Id == preferredProductId) ?? products.FirstOrDefault();
 }

 private void ApplyLotsProductFilter()
 {
  LotsGrid.ItemsSource = LotsProductPicker.SelectedItem is ProductChoice product ? _lots.Where(x => x.ProductId == product.Id).ToList() : _lots;
 }

 private void ApplyProductFilters()
 {
  var search = ProductSearchBox?.Text.Trim() ?? "";
  ProductsGrid.ItemsSource = string.IsNullOrWhiteSpace(search) ? _products : _products.Where(x => $"{x.Código} {x.Família} {x.Produto} {x.Situação}".Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
 }

 private void ApplyParameterFilters()
 {
  var search = ParameterSearchBox?.Text.Trim() ?? "";
  ParametersGrid.ItemsSource = string.IsNullOrWhiteSpace(search) ? _parameters : _parameters.Where(x => $"{x.Código} {x.Parâmetro} {x.Categoria} {x.Unidade}".Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
 }
 private bool RequireAdministrator()
 {
  if (_user?.Role == UserRole.Administrator) return true;
  MessageBox.Show("Esta operação exige o perfil Administrador.", "LabQC", MessageBoxButton.OK, MessageBoxImage.Warning); return false;
 }
 private async void NewProduct_Click(object s, RoutedEventArgs e) { if (_user is not null && RequireAdministrator() && await CatalogDialogs.NewProductAsync(this, _options, _user.Id)) await RefreshAsync(); }
 private async void NewParameter_Click(object s, RoutedEventArgs e) { if (RequireAdministrator() && await CatalogDialogs.NewParameterAsync(this, _options)) await RefreshAsync(); }
 private async void EditProduct_Click(object s, RoutedEventArgs e) { if (_user is null || !RequireAdministrator()) return; if (ProductsGrid.SelectedItem is not ProductRow row) { MessageBox.Show("Selecione um produto.", "LabQC"); return; } if (await CatalogDialogs.EditProductAsync(this, _options, row.Id, _user.Id)) await RefreshAsync(); }
 private async void DuplicateProduct_Click(object s, RoutedEventArgs e) { if (_user is null || !RequireAdministrator()) return; if (ProductsGrid.SelectedItem is not ProductRow row) { MessageBox.Show("Selecione o produto que será usado como modelo.", "LabQC"); return; } if (await CatalogDialogs.DuplicateProductAsync(this, _options, row.Id, _user.Id)) await RefreshAsync(); }
 private async void DeleteProduct_Click(object s, RoutedEventArgs e) { if (_user is null || !RequireAdministrator()) return; if (ProductsGrid.SelectedItem is not ProductRow row) { MessageBox.Show("Selecione um produto.", "LabQC"); return; } if (await CatalogDialogs.DeleteProductAsync(this, _options, row.Id, _user.Id)) await RefreshAsync(); }
 private async void EditParameter_Click(object s, RoutedEventArgs e) { if (_user is null || !RequireAdministrator()) return; if (ParametersGrid.SelectedItem is not ParameterRow row) { MessageBox.Show("Selecione um parâmetro.", "LabQC"); return; } if (await CatalogDialogs.EditParameterAsync(this, _options, row.Id, _user.Id)) await RefreshAsync(); }
 private async void DeleteParameter_Click(object s, RoutedEventArgs e) { if (_user is null || !RequireAdministrator()) return; if (ParametersGrid.SelectedItem is not ParameterRow row) { MessageBox.Show("Selecione um parâmetro.", "LabQC"); return; } if (await CatalogDialogs.DeleteParameterAsync(this, _options, row.Id, _user.Id)) await RefreshAsync(); }
 private async void NewUser_Click(object s, RoutedEventArgs e) { if (_user is null || !RequireAdministrator()) return; if (await CatalogDialogs.NewUserAsync(this, _options, _user.Id)) { await RefreshAsync(); MessageBox.Show("Acesso criado. A senha deverá ser alterada no primeiro login.", "LabQC", MessageBoxButton.OK, MessageBoxImage.Information); } }
 private async void ToggleUser_Click(object s, RoutedEventArgs e) { if (_user is null || !RequireAdministrator()) return; if (UsersGrid.SelectedItem is not UserRow row) { MessageBox.Show("Selecione um usuário.", "LabQC"); return; } if (await CatalogDialogs.ToggleUserAsync(this, _options, row.Id, _user.Id)) await RefreshAsync(); }
 private async void NewLot_Click(object s, RoutedEventArgs e) => await OpenNewLotAsync((LotsProductPicker.SelectedItem as ProductChoice)?.Id, false);
 private async void NewLotFromAnalysis_Click(object s, RoutedEventArgs e) => await OpenNewLotAsync((AnalysisProductPicker.SelectedItem as ProductChoice)?.Id, true);
 private async void DashboardNewLot_Click(object s, RoutedEventArgs e) => await OpenNewLotAsync(null, true);
 private void DashboardAnalysis_Click(object s, RoutedEventArgs e) => Pages.SelectedIndex = 1;
 private async Task OpenNewLotAsync(Guid? productId, bool goToAnalysis)
 {
  if (_user is null) return;
  var lotId = await CatalogDialogs.NewLotAsync(this, _options, _user.Id, productId); if (!lotId.HasValue) return;
  await using var db = new LabDbContext(_options); var selectedProductId = await db.Lots.Where(x => x.Id == lotId.Value).Select(x => x.ProductId).SingleAsync();
  await RefreshAsync(selectedProductId, lotId); if (goToAnalysis) Pages.SelectedIndex = 1;
  MessageBox.Show("Lote aberto e pronto para receber análises.", "LabQC", MessageBoxButton.OK, MessageBoxImage.Information);
 }
 private void AnalysisFamilyPicker_SelectionChanged(object s, SelectionChangedEventArgs e) => ApplyAnalysisFamilyFilter();
 private void LotsFamilyPicker_SelectionChanged(object s, SelectionChangedEventArgs e) => ApplyLotsFamilyFilter();
 private void AnalysisProductPicker_SelectionChanged(object s, SelectionChangedEventArgs e) => ApplyAnalysisProductFilter();
 private void LotsProductPicker_SelectionChanged(object s, SelectionChangedEventArgs e) => ApplyLotsProductFilter();
 private void ProductSearch_TextChanged(object s, TextChangedEventArgs e) => ApplyProductFilters();
 private void ParameterSearch_TextChanged(object s, TextChangedEventArgs e) => ApplyParameterFilters();
 private void ProductsGrid_MouseDoubleClick(object s, MouseButtonEventArgs e) => EditProduct_Click(s, e);
 private void ParametersGrid_MouseDoubleClick(object s, MouseButtonEventArgs e) => EditParameter_Click(s, e);
 private async void Refresh_Click(object s, RoutedEventArgs e) => await RefreshAsync();
 private async void CloseLot_Click(object s, RoutedEventArgs e)
 {
  if (_user is null || LotsGrid.SelectedItem is not LotRow selected) { MessageBox.Show("Selecione um lote.", "LabQC"); return; }
  if (MessageBox.Show($"Fechar o lote {selected.Lote}?\n\nDepois disso não será possível lançar nem corrigir análises, mas o histórico e a emissão do laudo continuarão disponíveis.", "Fechar lote", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
  await using var db = new LabDbContext(_options); var lot = await db.Lots.SingleAsync(x => x.Id == selected.Id);
  if (lot.Status == LotStatus.Closed) { MessageBox.Show("Este lote já está fechado.", "LabQC"); return; }
  var old = lot.Status; lot.Status = LotStatus.Closed;
  db.AuditEntries.Add(new AuditEntry { UserId = _user.Id, OccurredAt = DateTimeOffset.Now, EntityName = nameof(Lot), EntityId = lot.Id.ToString(), Action = "Lote fechado", OldValue = old.ToString(), NewValue = LotStatus.Closed.ToString() });
  await db.SaveChangesAsync(); await RefreshAsync(); MessageBox.Show("Lote fechado. Ele já está disponível para emissão do laudo.", "LabQC");
 }
 private async void LotsGrid_MouseDoubleClick(object s, MouseButtonEventArgs e)
 {
  if (LotsGrid.SelectedItem is LotRow selected) await LotDetailsDialog.ShowAsync(this, _options, selected.Id);
 }
 private async void RecentLotsGrid_MouseDoubleClick(object s, MouseButtonEventArgs e)
 {
  if (RecentLotsGrid.SelectedItem is LotRow selected) await LotDetailsDialog.ShowAsync(this, _options, selected.Id);
 }
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
   if (lot.Status == LotStatus.Closed) { MessageBox.Show("Este lote está fechado e não aceita novos lançamentos ou correções.", "Lote fechado", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
   await using var transaction = await db.Database.BeginTransactionAsync();
   var parameters = lot.Parameters.OrderBy(x => x.SortOrder).ToList(); var service = new AnalysisEntryService(db); var saved = 0;
   string? correctionReason = null;
   foreach (DataRow row in _table.Rows)
   {
    var code = row[0]?.ToString()?.Trim(); if (string.IsNullOrWhiteSpace(code)) continue;
    var sample = lot.Samples.SingleOrDefault(x => x.Code == code);
    if (sample is null) { sample = new Sample { LotId = lot.Id, Code = code, CollectedAt = DateTimeOffset.Now }; db.Samples.Add(sample); lot.Samples.Add(sample); await db.SaveChangesAsync(); }
    for (var i = 0; i < parameters.Count; i++)
    {
     var raw = row[i + 1]?.ToString()?.Trim(); if (string.IsNullOrWhiteSpace(raw)) continue;
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
     var previous = await db.AnalysisResults.SingleOrDefaultAsync(x => x.SampleId == sample.Id && x.LotParameterId == parameters[i].Id && x.IsCurrent);
     if (previous is not null)
     {
      var unchanged = previous.NumericValue == numeric && string.Equals(previous.TextValue?.Trim(), text?.Trim(), StringComparison.Ordinal) && previous.ConformityValue == conformity;
      if (unchanged) continue;
      correctionReason ??= CatalogDialogs.AskText(this, "Corrigir resultado", "Motivo da alteração (obrigatório)");
      if (correctionReason is null) { await transaction.RollbackAsync(); return; }
     }
     await service.SaveCorrectionSafeAsync(sample.Id, parameters[i].Id, numeric, text, conformity, _user, previous is null ? null : correctionReason); saved++;
    }
   }
   await transaction.CommitAsync();
   MessageBox.Show($"{saved} resultado(s) salvo(s).", "LabQC", MessageBoxButton.OK, MessageBoxImage.Information);
   await RefreshAsync(picker.ProductId, picker.Id);
  }
  catch (Exception error) { MessageBox.Show(error.Message, "Não foi possível salvar", MessageBoxButton.OK, MessageBoxImage.Error); }
 }
 private void AnalysisGrid_PreviewKeyDown(object s, KeyEventArgs e)
 {
  if (e.Key == Key.Enter)
  {
   e.Handled = true; var column = AnalysisGrid.CurrentCell.Column; var rowIndex = AnalysisGrid.Items.IndexOf(AnalysisGrid.CurrentItem);
   AnalysisGrid.CommitEdit(); AnalysisGrid.CommitEdit();
   if (column is not null && rowIndex >= 0 && rowIndex + 1 < AnalysisGrid.Items.Count)
   {
    var nextRow = AnalysisGrid.Items[rowIndex + 1]; AnalysisGrid.SelectedItem = nextRow; AnalysisGrid.CurrentCell = new DataGridCellInfo(nextRow, column); AnalysisGrid.ScrollIntoView(nextRow, column); AnalysisGrid.BeginEdit();
   }
  }
  if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; NewSample_Click(s, e); }
  if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; SaveAnalysis_Click(s, e); }
 }
 private async void Backup_Click(object s, RoutedEventArgs e) { var dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"LabQC Backups");var svc=new BackupService(Path.Combine(_root,"labqc.db"),Path.Combine(_root,"Certificados"));var p=await svc.CreateAsync(dir);BackupStatus.Text=$"Criado e verificado: {p}\n{await BackupService.VerifyAsync(p)}"; }
 private async void VerifyBackup_Click(object s, RoutedEventArgs e) { var d=new OpenFileDialog{Filter="Backup LabQC|*.labbackup"};if(d.ShowDialog()==true)BackupStatus.Text=await BackupService.VerifyAsync(d.FileName)?"Backup íntegro.":"Backup inválido."; }
 private sealed record FamilyChoice(string Name, int Products) { public string Display => Name; public string Summary => Products == 1 ? "1 produto" : $"{Products} produtos"; }
 private sealed record ProductChoice(Guid Id, string Family, string Display, int OpenLots) { public string Summary => OpenLots == 0 ? "Nenhum lote aberto" : OpenLots == 1 ? "1 lote aberto" : $"{OpenLots} lotes abertos"; }
 private sealed record Picker(Guid Id, Guid ProductId, string Display);
 private sealed record LotRow(Guid Id, Guid ProductId, string Lote, string Produto, DateOnly Fabricação, string Análises, string Situação);
 private sealed record ProductRow(Guid Id, string Código, string Família, string Produto, string Validade, string Situação);
 private sealed record ParameterRow(Guid Id, string Código, string Parâmetro, string Categoria, string Unidade, string Tipo, string Situação);
 private sealed record CertificateRow(Guid Id, string Número, int Versão, string Produto, string Lote, string Cliente, DateTimeOffset Emissão, string Pdf);
 private sealed record UserRow(Guid Id, string Nome, string Usuário, string Perfil, string Situação, string Senha);
}
