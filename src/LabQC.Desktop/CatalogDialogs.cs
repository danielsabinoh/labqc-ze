using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Text.Json;
using LabQC.Application;
using LabQC.Domain;
using LabQC.Infrastructure;
using LabQC.Reports;
using Microsoft.EntityFrameworkCore;

namespace LabQC.Desktop;

internal static class CatalogDialogs
{
    public static async Task<bool> NewUserAsync(Window owner, DbContextOptions<LabDbContext> options, Guid administratorId)
    {
        var username = Box(); var fullName = Box(); var role = RoleBox();
        var password = new PasswordBox { Margin = new Thickness(4), Padding = new Thickness(7) };
        var confirmation = new PasswordBox { Margin = new Thickness(4), Padding = new Thickness(7) };
        var guidance = new TextBlock { Text = "A pessoa deverá trocar esta senha no primeiro acesso.", Foreground = System.Windows.Media.Brushes.DimGray, Margin = new Thickness(4, 8, 4, 4), TextWrapping = TextWrapping.Wrap };
        var dialog = Form(owner, "Criar novo acesso", ("Usuário", username), ("Nome completo", fullName), ("Perfil de acesso", role), ("Senha provisória", password), ("Confirme a senha", confirmation), ("", guidance));
        if (dialog.ShowDialog() != true) return false;
        if (string.IsNullOrWhiteSpace(username.Text) || string.IsNullOrWhiteSpace(fullName.Text)) { Alert("Informe usuário e nome completo."); return false; }
        if (password.Password.Length < 8) { Alert("A senha provisória precisa ter pelo menos 8 caracteres."); return false; }
        if (password.Password != confirmation.Password) { Alert("A confirmação da senha não confere."); return false; }
        await using var db = new LabDbContext(options);
        var normalized = username.Text.Trim();
        if (await db.Users.AnyAsync(x => x.Username == normalized)) { Alert("Esse nome de usuário já está em uso."); return false; }
        var user = new User { Username = normalized, FullName = fullName.Text.Trim(), Role = ((EnumOption<UserRole>)role.SelectedItem).Value, PasswordHash = PasswordHasher.Hash(password.Password), IsActive = true, MustChangePassword = true };
        db.Users.Add(user);
        db.AuditEntries.Add(new AuditEntry { UserId = administratorId, OccurredAt = DateTimeOffset.Now, EntityName = nameof(User), EntityId = user.Id.ToString(), Action = "Acesso criado", NewValue = $"{user.Username}|{user.Role}" });
        await db.SaveChangesAsync(); return true;
    }

    public static async Task<bool> ToggleUserAsync(Window owner, DbContextOptions<LabDbContext> options, Guid userId, Guid administratorId)
    {
        if (userId == administratorId) { Alert("Você não pode desativar o próprio acesso."); return false; }
        await using var db = new LabDbContext(options); var user = await db.Users.SingleAsync(x => x.Id == userId);
        var newStatus = !user.IsActive; var action = newStatus ? "reativar" : "desativar";
        if (MessageBox.Show($"Deseja {action} o acesso de {user.FullName}?", "Acessos", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return false;
        user.IsActive = newStatus;
        db.AuditEntries.Add(new AuditEntry { UserId = administratorId, OccurredAt = DateTimeOffset.Now, EntityName = nameof(User), EntityId = user.Id.ToString(), Action = newStatus ? "Acesso reativado" : "Acesso desativado", OldValue = (!newStatus).ToString(), NewValue = newStatus.ToString() });
        await db.SaveChangesAsync(); return true;
    }

    public static async Task<User?> EditAccountAsync(Window owner, DbContextOptions<LabDbContext> options, User sessionUser, bool firstAccess)
    {
        var username = Box(sessionUser.Username); var fullName = Box(sessionUser.FullName);
        var currentPassword = new PasswordBox { Margin = new Thickness(4), Padding = new Thickness(7) };
        var newPassword = new PasswordBox { Margin = new Thickness(4), Padding = new Thickness(7) };
        var confirmation = new PasswordBox { Margin = new Thickness(4), Padding = new Thickness(7) };
        Window dialog;
        if (firstAccess)
            dialog = Form(owner, "Troca obrigatória da senha provisória", ("Usuário", username), ("Nome completo", fullName), ("Nova senha", newPassword), ("Confirme a nova senha", confirmation));
        else
            dialog = Form(owner, "Minha conta", ("Usuário", username), ("Nome completo", fullName), ("Senha atual (obrigatória para salvar)", currentPassword), ("Nova senha (deixe vazia para manter)", newPassword), ("Confirme a nova senha", confirmation));
        if (dialog.ShowDialog() != true) return null;
        await using var db = new LabDbContext(options); var user = await db.Users.SingleAsync(x => x.Id == sessionUser.Id);
        if (!firstAccess && !PasswordHasher.Verify(currentPassword.Password, user.PasswordHash)) { Alert("A senha atual está incorreta."); return null; }
        if (string.IsNullOrWhiteSpace(username.Text) || string.IsNullOrWhiteSpace(fullName.Text)) { Alert("Informe usuário e nome completo."); return null; }
        if (await db.Users.AnyAsync(x => x.Username == username.Text.Trim() && x.Id != user.Id)) { Alert("Esse nome de usuário já está em uso."); return null; }
        var changingPassword = !string.IsNullOrEmpty(newPassword.Password);
        if (firstAccess && !changingPassword) { Alert("Você precisa criar uma nova senha."); return null; }
        if (changingPassword)
        {
            if (newPassword.Password.Length < 8) { Alert("A nova senha precisa ter pelo menos 8 caracteres."); return null; }
            if (newPassword.Password != confirmation.Password) { Alert("A confirmação da senha não confere."); return null; }
            if (newPassword.Password == "Admin@123") { Alert("Escolha uma senha diferente da senha provisória."); return null; }
            user.PasswordHash = PasswordHasher.Hash(newPassword.Password); user.MustChangePassword = false; user.PasswordChangedAt = DateTimeOffset.Now;
        }
        var oldUsername = user.Username; user.Username = username.Text.Trim(); user.FullName = fullName.Text.Trim();
        db.AuditEntries.Add(new AuditEntry { UserId = user.Id, OccurredAt = DateTimeOffset.Now, EntityName = nameof(User), EntityId = user.Id.ToString(), Action = firstAccess ? "Primeiro acesso concluído" : "Conta alterada", OldValue = oldUsername, NewValue = user.Username });
        await db.SaveChangesAsync(); return user;
    }

    public static async Task<bool> NewProductAsync(Window owner, DbContextOptions<LabDbContext> options, Guid userId)
    {
        var code = Box(); var family = FamilyBox(); var name = Box(); var unit = Box("saco 50 kg"); var shelf = Box("12");
        var dialog = Form(owner, "Cadastrar produto", ("Código", code), ("Família", family), ("Nome do produto", name), ("Unidade de comercialização", unit), ("Validade (meses)", shelf));
        if (dialog.ShowDialog() != true) return false;
        if (string.IsNullOrWhiteSpace(code.Text) || string.IsNullOrWhiteSpace(name.Text) || !int.TryParse(shelf.Text, out var months) || months <= 0) { Alert("Preencha código, nome e validade corretamente."); return false; }
        await using var db = new LabDbContext(options);
        if (await db.Products.AnyAsync(x => x.Code == code.Text.Trim())) { Alert("Já existe um produto com esse código."); return false; }
        var product = new Product { Code = code.Text.Trim(), Family = (string)family.SelectedItem, Name = name.Text.Trim(), CommercialUnit = unit.Text.Trim(), ShelfLifeMonths = months };
        db.Products.Add(product); await db.SaveChangesAsync();
        if (MessageBox.Show("Produto cadastrado. Deseja configurar agora as análises e os limites?", "Configuração do produto", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            await ConfigureSpecificationAsync(owner, options, userId, product.Id);
        return true;
    }

    public static async Task<bool> EditProductAsync(Window owner, DbContextOptions<LabDbContext> options, Guid productId, Guid userId)
    {
        await using var db = new LabDbContext(options); var product = await db.Products.SingleAsync(x => x.Id == productId);
        var code = Box(product.Code); var family = FamilyBox(product.Family); var name = Box(product.Name); var unit = Box(product.CommercialUnit); var shelf = Box(product.ShelfLifeMonths.ToString());
        var active = new CheckBox { Content = "Produto ativo", IsChecked = product.IsActive, Margin = new Thickness(4, 10, 4, 4) };
        var dialog = Form(owner, "Alterar produto", ("Código", code), ("Família", family), ("Nome do produto", name), ("Unidade de comercialização", unit), ("Validade (meses)", shelf), ("Situação", active));
        if (dialog.ShowDialog() != true) return false;
        if (string.IsNullOrWhiteSpace(code.Text) || string.IsNullOrWhiteSpace(name.Text) || string.IsNullOrWhiteSpace(unit.Text) || !int.TryParse(shelf.Text, out var months) || months <= 0) { Alert("Preencha código, nome, unidade e validade corretamente."); return false; }
        if (await db.Products.AnyAsync(x => x.Code == code.Text.Trim() && x.Id != product.Id)) { Alert("Já existe outro produto com esse código."); return false; }
        var old = product.DisplayName; product.Code = code.Text.Trim(); product.Family = (string)family.SelectedItem; product.Name = name.Text.Trim(); product.Description = ""; product.CommercialUnit = unit.Text.Trim(); product.ShelfLifeMonths = months; product.IsActive = active.IsChecked == true;
        db.AuditEntries.Add(new AuditEntry { UserId = userId, OccurredAt = DateTimeOffset.Now, EntityName = nameof(Product), EntityId = product.Id.ToString(), Action = "Alterado", OldValue = old, NewValue = product.DisplayName });
        await db.SaveChangesAsync();
        if (product.IsActive && MessageBox.Show("Deseja configurar agora as análises e os limites deste produto?", "Configuração do produto", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            await ConfigureSpecificationAsync(owner, options, userId, product.Id);
        return true;
    }

    public static async Task<bool> DeleteProductAsync(Window owner, DbContextOptions<LabDbContext> options, Guid productId, Guid userId)
    {
        await using var db = new LabDbContext(options); var product = await db.Products.Include(x => x.Specifications).SingleAsync(x => x.Id == productId);
        var hasHistory = await db.Lots.AnyAsync(x => x.ProductId == productId);
        var action = hasHistory ? "arquivar" : "excluir definitivamente";
        if (MessageBox.Show($"Deseja {action} o produto “{product.DisplayName}”?\n\n{(hasHistory ? "Os lotes, resultados e laudos antigos serão preservados." : "O produto ainda não possui lotes e será removido do cadastro.")}", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return false;
        if (hasHistory)
        {
            product.IsActive = false;
            db.AuditEntries.Add(new AuditEntry { UserId = userId, OccurredAt = DateTimeOffset.Now, EntityName = nameof(Product), EntityId = product.Id.ToString(), Action = "Arquivado", OldValue = "Ativo", NewValue = "Arquivado" });
        }
        else
        {
            db.ProductSpecifications.RemoveRange(product.Specifications);
            db.Products.Remove(product);
            db.AuditEntries.Add(new AuditEntry { UserId = userId, OccurredAt = DateTimeOffset.Now, EntityName = nameof(Product), EntityId = product.Id.ToString(), Action = "Excluído", OldValue = product.DisplayName });
        }
        await db.SaveChangesAsync(); return true;
    }

    public static async Task<bool> DuplicateProductAsync(Window owner, DbContextOptions<LabDbContext> options, Guid productId, Guid userId)
    {
        await using var db = new LabDbContext(options);
        var source = await db.Products.Include(x => x.Specifications).ThenInclude(x => x.Parameters).SingleAsync(x => x.Id == productId);
        var code = Box(source.Code + "-COPIA"); var family = FamilyBox(source.Family); var name = Box(source.Name); var unit = Box(source.CommercialUnit); var shelf = Box(source.ShelfLifeMonths.ToString());
        var dialog = Form(owner, "Duplicar produto", ("Novo código", code), ("Família", family), ("Nome do produto", name), ("Unidade de comercialização", unit), ("Validade (meses)", shelf));
        if (dialog.ShowDialog() != true) return false;
        if (string.IsNullOrWhiteSpace(code.Text) || string.IsNullOrWhiteSpace(name.Text) || string.IsNullOrWhiteSpace(unit.Text) || !int.TryParse(shelf.Text, out var months) || months <= 0) { Alert("Preencha código, nome, unidade e validade corretamente."); return false; }
        if (await db.Products.AnyAsync(x => x.Code == code.Text.Trim())) { Alert("Já existe um produto com esse código."); return false; }
        var copy = new Product { Code = code.Text.Trim(), Family = (string)family.SelectedItem, Name = name.Text.Trim(), CommercialUnit = unit.Text.Trim(), ShelfLifeMonths = months, IsActive = true };
        var sourceSpec = source.Specifications.SingleOrDefault(x => x.IsActive);
        if (sourceSpec is not null)
        {
            var spec = new ProductSpecification { Product = copy, ProductId = copy.Id, Version = 1, EffectiveFrom = DateTimeOffset.Now, IsActive = true, ChangeReason = $"Copiado de {source.DisplayName}" };
            foreach (var item in sourceSpec.Parameters.OrderBy(x => x.SortOrder))
                spec.Parameters.Add(new ProductSpecificationParameter { AnalysisParameterId = item.AnalysisParameterId, Minimum = item.Minimum, Maximum = item.Maximum, SpecificationText = item.SpecificationText, StandardText = item.StandardText, ConsolidationMethod = item.ConsolidationMethod, SortOrder = item.SortOrder });
            copy.Specifications.Add(spec);
        }
        db.Products.Add(copy);
        db.AuditEntries.Add(new AuditEntry { UserId = userId, OccurredAt = DateTimeOffset.Now, EntityName = nameof(Product), EntityId = copy.Id.ToString(), Action = "Duplicado", OldValue = source.DisplayName, NewValue = copy.DisplayName });
        await db.SaveChangesAsync(); return true;
    }

    public static async Task<bool> NewParameterAsync(Window owner, DbContextOptions<LabDbContext> options)
    {
        var code = Box(); var name = Box(); var unit = Box();
        var category = CategoryBox(); var type = ResultTypeBox();
        var dialog = Form(owner, "Cadastrar parâmetro de análise", ("Código", code), ("Nome", name), ("Categoria", category), ("Unidade", unit), ("Tipo de resultado", type));
        if (dialog.ShowDialog() != true) return false;
        if (string.IsNullOrWhiteSpace(code.Text) || string.IsNullOrWhiteSpace(name.Text)) { Alert("Preencha código e nome corretamente."); return false; }
        await using var db = new LabDbContext(options);
        if (await db.AnalysisParameters.AnyAsync(x => x.Code == code.Text.Trim())) { Alert("Já existe um parâmetro com esse código."); return false; }
        var resultType = ((EnumOption<ResultType>)type.SelectedItem).Value;
        db.AnalysisParameters.Add(new AnalysisParameter { Code = code.Text.Trim(), Name = name.Text.Trim(), Unit = unit.Text.Trim(), Category = ((EnumOption<ParameterCategory>)category.SelectedItem).Value, ResultType = resultType, DecimalPlaces = resultType == ResultType.Numeric ? 2 : 0 });
        await db.SaveChangesAsync(); return true;
    }

    public static async Task<bool> EditParameterAsync(Window owner, DbContextOptions<LabDbContext> options, Guid parameterId, Guid userId)
    {
        await using var db = new LabDbContext(options); var parameter = await db.AnalysisParameters.SingleAsync(x => x.Id == parameterId);
        var code = Box(parameter.Code); var name = Box(parameter.Name); var unit = Box(parameter.Unit); var category = CategoryBox(parameter.Category); var type = ResultTypeBox(parameter.ResultType);
        var active = new CheckBox { Content = "Parâmetro ativo", IsChecked = parameter.IsActive, Margin = new Thickness(4, 10, 4, 4) };
        var dialog = Form(owner, "Alterar parâmetro", ("Código", code), ("Nome", name), ("Categoria", category), ("Unidade", unit), ("Tipo de resultado", type), ("Situação", active));
        if (dialog.ShowDialog() != true) return false;
        if (string.IsNullOrWhiteSpace(code.Text) || string.IsNullOrWhiteSpace(name.Text)) { Alert("Preencha código e nome."); return false; }
        if (await db.AnalysisParameters.AnyAsync(x => x.Code == code.Text.Trim() && x.Id != parameter.Id)) { Alert("Já existe outro parâmetro com esse código."); return false; }
        var old = parameter.Name; var resultType = ((EnumOption<ResultType>)type.SelectedItem).Value;
        parameter.Code = code.Text.Trim(); parameter.Name = name.Text.Trim(); parameter.Unit = unit.Text.Trim(); parameter.Category = ((EnumOption<ParameterCategory>)category.SelectedItem).Value; parameter.ResultType = resultType; parameter.DecimalPlaces = resultType == ResultType.Numeric ? 2 : 0; parameter.IsActive = active.IsChecked == true;
        db.AuditEntries.Add(new AuditEntry { UserId = userId, OccurredAt = DateTimeOffset.Now, EntityName = nameof(AnalysisParameter), EntityId = parameter.Id.ToString(), Action = "Alterado", OldValue = old, NewValue = parameter.Name }); await db.SaveChangesAsync(); return true;
    }

    public static async Task<bool> DeleteParameterAsync(Window owner, DbContextOptions<LabDbContext> options, Guid parameterId, Guid userId)
    {
        await using var db = new LabDbContext(options); var parameter = await db.AnalysisParameters.SingleAsync(x => x.Id == parameterId);
        var hasHistory = await db.ProductSpecificationParameters.AnyAsync(x => x.AnalysisParameterId == parameterId) || await db.LotParameters.AnyAsync(x => x.SourceParameterId == parameterId);
        var action = hasHistory ? "arquivar" : "excluir definitivamente";
        if (MessageBox.Show($"Deseja {action} o parâmetro “{parameter.Name}”?\n\n{(hasHistory ? "Resultados, especificações e laudos antigos serão preservados." : "O parâmetro ainda não foi utilizado e será removido do cadastro.")}", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return false;
        if (hasHistory)
        {
            parameter.IsActive = false;
            db.AuditEntries.Add(new AuditEntry { UserId = userId, OccurredAt = DateTimeOffset.Now, EntityName = nameof(AnalysisParameter), EntityId = parameter.Id.ToString(), Action = "Arquivado", OldValue = "Ativo", NewValue = "Arquivado" });
        }
        else
        {
            db.AnalysisParameters.Remove(parameter);
            db.AuditEntries.Add(new AuditEntry { UserId = userId, OccurredAt = DateTimeOffset.Now, EntityName = nameof(AnalysisParameter), EntityId = parameter.Id.ToString(), Action = "Excluído", OldValue = parameter.Name });
        }
        await db.SaveChangesAsync(); return true;
    }

    public static async Task<bool> ConfigureSpecificationAsync(Window owner, DbContextOptions<LabDbContext> options, Guid userId, Guid productId)
    {
        await using var db = new LabDbContext(options);
        var products = await db.Products.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
        var parameters = await db.AnalysisParameters.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
        if (products.Count == 0 || parameters.Count == 0) { Alert("Cadastre ao menos um produto e um parâmetro primeiro."); return false; }
        var product = new ComboBox { ItemsSource = products, DisplayMemberPath = "DisplayName", SelectedItem = products.Single(x => x.Id == productId), IsEnabled = false, Margin = new Thickness(4) };
        var reason = Box("Ajuste das análises e limites");
        var rows = new ObservableCollection<SpecRow>(parameters.Select((p, i) => new SpecRow(p, i + 1)));
        var currentSpec = await db.ProductSpecifications.Include(x => x.Parameters).Where(x => x.ProductId == productId && x.IsActive).SingleOrDefaultAsync();
        if (currentSpec is not null)
            foreach (var row in rows) if (currentSpec.Parameters.SingleOrDefault(x => x.AnalysisParameterId == row.Id) is { } configured) { row.Use = true; row.Minimum = configured.Minimum?.ToString(System.Globalization.CultureInfo.GetCultureInfo("pt-BR")) ?? ""; row.Maximum = configured.Maximum?.ToString(System.Globalization.CultureInfo.GetCultureInfo("pt-BR")) ?? ""; row.Text = configured.SpecificationText; row.Method = configured.ConsolidationMethod; }
        var grid = new DataGrid { ItemsSource = rows, AutoGenerateColumns = false, CanUserAddRows = false, Height = 350, Margin = new Thickness(4) };
        grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Usar", Binding = new Binding("Use") });
        grid.Columns.Add(new DataGridTextColumn { Header = "Parâmetro", Binding = new Binding("Name"), IsReadOnly = true, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Mínimo", Binding = new Binding("Minimum"), Width = 90 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Máximo", Binding = new Binding("Maximum"), Width = 90 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Especificação/texto padrão", Binding = new Binding("Text"), Width = 190 });
        var consolidationOptions = Enum.GetValues<ConsolidationMethod>().Select(x => new EnumOption<ConsolidationMethod>(x, PortugueseLabels.Consolidation(x))).ToList();
        grid.Columns.Add(new DataGridComboBoxColumn { Header = "Como calcular", ItemsSource = consolidationOptions, DisplayMemberPath = "Label", SelectedValuePath = "Value", SelectedValueBinding = new Binding("Method"), Width = 150 });
        var dialog = Form(owner, "Configurar análises e limites do produto", ("Produto", product), ("Motivo da alteração", reason), ("Marque as análises exigidas e informe os limites", grid));
        dialog.Width = 900; dialog.Height = 620;
        if (dialog.ShowDialog() != true) return false;
        grid.CommitEdit(); grid.CommitEdit();
        var selected = rows.Where(x => x.Use).ToList(); if (selected.Count == 0) { Alert("Selecione pelo menos um parâmetro."); return false; }
        var selectedProduct = (Product)product.SelectedItem;
        var current = await db.ProductSpecifications.Where(x => x.ProductId == selectedProduct.Id).ToListAsync();
        foreach (var old in current.Where(x => x.IsActive)) old.IsActive = false;
        var spec = new ProductSpecification { ProductId = selectedProduct.Id, Version = current.Count == 0 ? 1 : current.Max(x => x.Version) + 1, EffectiveFrom = DateTimeOffset.Now, IsActive = true, ChangeReason = reason.Text.Trim() };
        foreach (var row in selected)
        {
            decimal? min = ParseOptional(row.Minimum); decimal? max = ParseOptional(row.Maximum);
            spec.Parameters.Add(new ProductSpecificationParameter { AnalysisParameterId = row.Id, Minimum = min, Maximum = max, SpecificationText = row.Text.Trim(), StandardText = row.Text.Trim(), ConsolidationMethod = row.Method, SortOrder = row.SortOrder });
        }
        db.ProductSpecifications.Add(spec);
        db.AuditEntries.Add(new AuditEntry { UserId = userId, OccurredAt = DateTimeOffset.Now, EntityName = nameof(ProductSpecification), EntityId = spec.Id.ToString(), Action = "Versão criada", NewValue = $"V{spec.Version}", Justification = spec.ChangeReason });
        await db.SaveChangesAsync(); return true;
    }

    public static async Task<Guid?> NewLotAsync(Window owner, DbContextOptions<LabDbContext> options, Guid userId, Guid? preferredProductId = null)
    {
        await using var db = new LabDbContext(options);
        var products = await db.Products.Include(x => x.Specifications).ThenInclude(x => x.Parameters).ThenInclude(x => x.AnalysisParameter).Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
        products = products.Where(x => x.Specifications.Any(s => s.IsActive)).ToList();
        if (products.Count == 0) { Alert("Nenhum produto possui análises configuradas. Configure o produto primeiro."); return null; }
        var product = new ComboBox { ItemsSource = products, DisplayMemberPath = "DisplayName", SelectedItem = products.FirstOrDefault(x => x.Id == preferredProductId) ?? products[0], Margin = new Thickness(4), Padding = new Thickness(7), FontSize = 16 };
        var number = Box(); var manufacture = new DatePicker { SelectedDate = DateTime.Today, Margin = new Thickness(4), Padding = new Thickness(5) }; var notes = Box();
        var expiry = new TextBlock { Margin = new Thickness(4, 7, 4, 10), FontWeight = FontWeights.SemiBold, Foreground = System.Windows.Media.Brushes.DarkSlateGray };
        var analyses = new TextBlock { Margin = new Thickness(4, 7, 4, 7), TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.DimGray };
        void UpdateSummary()
        {
            if (product.SelectedItem is not Product selected || manufacture.SelectedDate is not DateTime date) return;
            expiry.Text = $"Validade calculada: {DateOnly.FromDateTime(date).AddMonths(selected.ShelfLifeMonths):dd/MM/yyyy}";
            var active = selected.Specifications.Single(x => x.IsActive);
            analyses.Text = "Análises deste produto: " + string.Join(" • ", active.Parameters.OrderBy(x => x.SortOrder).Select(x => x.AnalysisParameter.Name));
        }
        product.SelectionChanged += (_, _) => UpdateSummary(); manufacture.SelectedDateChanged += (_, _) => UpdateSummary(); UpdateSummary();
        var dialog = Form(owner, "Abrir novo lote", ("Produto", product), ("Número do lote", number), ("Data de fabricação", manufacture), ("", expiry), ("Observação (opcional)", notes), ("", analyses));
        dialog.Width = 620;
        if (dialog.ShowDialog() != true) return null;
        if (manufacture.SelectedDate is not DateTime manufactureDate || string.IsNullOrWhiteSpace(number.Text)) { Alert("Informe o número do lote e a data corretamente."); return null; }
        var date = DateOnly.FromDateTime(manufactureDate);
        var selected = (Product)product.SelectedItem; var spec = selected.Specifications.Single(x => x.IsActive);
        if (await db.Lots.AnyAsync(x => x.ProductId == selected.Id && x.Number == number.Text.Trim())) { Alert("Esse lote já existe para o produto."); return null; }
        var lot = LotFactory.Create(number.Text, selected, spec, date, 0m, "Produção", DateTimeOffset.Now); lot.Notes = notes.Text.Trim();
        db.Lots.Add(lot); db.AuditEntries.Add(new AuditEntry { UserId = userId, OccurredAt = lot.OpenedAt, EntityName = nameof(Lot), EntityId = lot.Id.ToString(), Action = "Aberto", NewValue = lot.Number });
        await db.SaveChangesAsync(); return lot.Id;
    }

    public static async Task<Certificate?> IssueCertificateAsync(Window owner, DbContextOptions<LabDbContext> options, Guid userId, string dataRoot)
    {
        await using var db = new LabDbContext(options);
        var lots = await db.Lots.Include(x => x.Product).Include(x => x.Parameters).Where(x => x.Status == LotStatus.Closed).ToListAsync();
        lots = lots.OrderByDescending(x => x.OpenedAt).ToList();
        if (lots.Count == 0) { Alert("Não há lote fechado disponível. Feche o lote antes de emitir o laudo."); return null; }
        var lotBox = new ComboBox { ItemsSource = lots, DisplayMemberPath = "Number", SelectedIndex = 0, Margin = new Thickness(4) };
        var client = Box(); var city = Box(); var state = Box(); var invoice = Box(); var quantity = Box(); var unit = Box();
        lotBox.SelectionChanged += (_, _) => { if (lotBox.SelectedItem is Lot l) { quantity.Text = l.QuantityProduced.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")); unit.Text = l.Unit; } };
        if (lots[0] is { } initial) { quantity.Text = initial.QuantityProduced.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")); unit.Text = initial.Unit; }
        var dialog = Form(owner, "Emitir certificado de análises", ("Lote fechado", lotBox), ("Cliente", client), ("Cidade", city), ("UF", state), ("Nota fiscal", invoice), ("Quantidade certificada", quantity), ("Unidade", unit));
        if (dialog.ShowDialog() != true) return null;
        if (string.IsNullOrWhiteSpace(client.Text) || !BrazilianDecimal.TryParse(quantity.Text, out var amount) || amount <= 0) { Alert("Informe cliente e quantidade corretamente."); return null; }
        var lot = (Lot)lotBox.SelectedItem;
        var allResults = await db.AnalysisResults.Where(x => x.Sample.LotId == lot.Id && x.IsCurrent && x.IsValid).ToListAsync();
        var consolidated = lot.Parameters.OrderBy(x => x.SortOrder).Select(p => (Parameter: p, Result: ConsolidationEngine.Consolidate(p, allResults.Where(x => x.LotParameterId == p.Id)))).ToList();
        var pending = consolidated.Where(x => x.Result.Conformity == ConformityStatus.Pending).Select(x => x.Parameter.ParameterName).ToList();
        if (pending.Count > 0) { Alert("Existem resultados pendentes: " + string.Join(", ", pending)); return null; }
        var year = DateTime.Now.Year; var issuedThisYear = (await db.Certificates.Select(x => x.IssuedAt).ToListAsync()).Count(x => x.Year == year);
        var certificate = new Certificate
        {
            Number = $"{year}-{issuedThisYear + 1:000000}", Version = 1, LotId = lot.Id, IssuedAt = DateTimeOffset.Now, IssuedByUserId = userId,
            ProductName = lot.Product.DisplayName, LotNumber = lot.Number, ClientName = client.Text.Trim(), City = city.Text.Trim(), State = state.Text.Trim().ToUpperInvariant(), InvoiceNumber = invoice.Text.Trim(),
            CertifiedQuantity = amount, QuantityUnit = unit.Text.Trim(), ManufactureDate = lot.ManufactureDate, ExpiryDate = lot.ExpiryDate
        };
        foreach (var item in consolidated)
        {
            var p = item.Parameter; var result = item.Result;
            var formatted = result.NumericValue.HasValue ? BrazilianDecimal.Format(result.NumericValue.Value, p.DecimalPlaces) : result.TextValue ?? "";
            var specification = !string.IsNullOrWhiteSpace(p.SpecificationText) ? p.SpecificationText : string.Join(" / ", new[] { p.Minimum.HasValue ? $"Mín. {BrazilianDecimal.Format(p.Minimum.Value, p.DecimalPlaces)}" : null, p.Maximum.HasValue ? $"Máx. {BrazilianDecimal.Format(p.Maximum.Value, p.DecimalPlaces)}" : null }.Where(x => x is not null));
            certificate.Results.Add(new CertificateResult { ParameterName = p.ParameterName, Category = p.Category, Result = formatted, Unit = p.Unit, Specification = specification, Conformity = result.Conformity, SortOrder = p.SortOrder });
        }
        certificate.SnapshotJson = JsonSerializer.Serialize(new { certificate.Number, certificate.Version, certificate.ProductName, certificate.LotNumber, certificate.ClientName, certificate.City, certificate.State, certificate.InvoiceNumber, certificate.CertifiedQuantity, certificate.QuantityUnit, certificate.ManufactureDate, certificate.ExpiryDate, Results = certificate.Results.Select(x => new { x.ParameterName, x.Result, x.Unit, x.Specification, x.Conformity }) });
        db.Certificates.Add(certificate); db.Clients.Add(new Client { Name = certificate.ClientName, City = certificate.City, State = certificate.State });
        await db.SaveChangesAsync();
        new CertificatePdfService().Generate(certificate, "J. C. Oliveira & Filhos Ltda.", Path.Combine(dataRoot, "Certificados"));
        db.AuditEntries.Add(new AuditEntry { UserId = userId, OccurredAt = certificate.IssuedAt, EntityName = nameof(Certificate), EntityId = certificate.Id.ToString(), Action = "Emitido", NewValue = $"{certificate.Number} v{certificate.Version}" });
        await db.SaveChangesAsync(); return certificate;
    }

    public static string? AskText(Window owner, string title, string label)
    {
        var value = Box(); var dialog = Form(owner, title, (label, value)); return dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(value.Text) ? value.Text.Trim() : null;
    }

    private static Window Form(Window owner, string title, params (string Label, FrameworkElement Input)[] fields)
    {
        var stack = new StackPanel { Margin = new Thickness(20) };
        foreach (var (label, input) in fields) { stack.Children.Add(new TextBlock { Text = label, Margin = new Thickness(4, 8, 4, 0) }); stack.Children.Add(input); }
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 15, 0, 0) };
        var cancel = new Button { Content = "Cancelar", MinWidth = 90 }; var save = new Button { Content = "Salvar", MinWidth = 90, IsDefault = true };
        buttons.Children.Add(cancel); buttons.Children.Add(save); stack.Children.Add(buttons);
        var window = new Window { Owner = owner, Title = title, Width = 520, SizeToContent = SizeToContent.Height, MaxHeight = 720, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
        cancel.Click += (_, _) => window.DialogResult = false; save.Click += (_, _) => window.DialogResult = true; return window;
    }
    private static TextBox Box(string value = "") => new() { Text = value, Margin = new Thickness(4), Padding = new Thickness(7) };
    private static ComboBox FamilyBox(string? selected = null)
    {
        var value = ProductFamilies.Standard.Contains(selected ?? "") ? selected! : ProductFamilies.Infer(selected);
        return new ComboBox { ItemsSource = ProductFamilies.Standard, SelectedItem = value, Margin = new Thickness(4), Padding = new Thickness(7) };
    }
    private static ComboBox CategoryBox(ParameterCategory? selected = null)
    {
        var items = Enum.GetValues<ParameterCategory>().Select(x => new EnumOption<ParameterCategory>(x, PortugueseLabels.Category(x))).ToList();
        return new ComboBox { ItemsSource = items, DisplayMemberPath = "Label", SelectedItem = items.Single(x => x.Value == (selected ?? ParameterCategory.Physicochemical)), Margin = new Thickness(4), Padding = new Thickness(5) };
    }
    private static ComboBox ResultTypeBox(ResultType? selected = null)
    {
        var items = Enum.GetValues<ResultType>().Select(x => new EnumOption<ResultType>(x, PortugueseLabels.ResultType(x))).ToList();
        return new ComboBox { ItemsSource = items, DisplayMemberPath = "Label", SelectedItem = items.Single(x => x.Value == (selected ?? ResultType.Numeric)), Margin = new Thickness(4), Padding = new Thickness(5) };
    }
    private static ComboBox RoleBox(UserRole selected = UserRole.Analyst)
    {
        var items = Enum.GetValues<UserRole>().Select(x => new EnumOption<UserRole>(x, PortugueseLabels.UserRole(x))).ToList();
        return new ComboBox { ItemsSource = items, DisplayMemberPath = "Label", SelectedItem = items.Single(x => x.Value == selected), Margin = new Thickness(4), Padding = new Thickness(5) };
    }
    private static decimal? ParseOptional(string text) { if (string.IsNullOrWhiteSpace(text)) return null; if (!BrazilianDecimal.TryParse(text, out var value)) throw new InvalidOperationException($"Número inválido: {text}"); return value; }
    private static void Alert(string message) => MessageBox.Show(message, "LabQC", MessageBoxButton.OK, MessageBoxImage.Warning);

    private sealed class SpecRow(AnalysisParameter p, int order)
    {
        public Guid Id { get; } = p.Id; public string Name { get; } = $"{p.Name} ({p.Unit})"; public bool Use { get; set; }
        public string Minimum { get; set; } = ""; public string Maximum { get; set; } = ""; public string Text { get; set; } = "";
        public ConsolidationMethod Method { get; set; } = p.ResultType == ResultType.Numeric ? ConsolidationMethod.Average : p.ResultType == ResultType.Conformity ? ConsolidationMethod.Conformity : ConsolidationMethod.StandardText;
        public int SortOrder { get; } = order;
    }
    private sealed record EnumOption<T>(T Value, string Label) where T : struct, Enum;
}
