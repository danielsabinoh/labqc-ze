using System.Data;
using System.Windows;
using System.Windows.Controls;
using LabQC.Application;
using LabQC.Domain;
using LabQC.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LabQC.Desktop;

internal static class LotDetailsDialog
{
    public static async Task ShowAsync(Window owner, DbContextOptions<LabDbContext> options, Guid lotId)
    {
        await using var db = new LabDbContext(options);
        var lot = await db.Lots.Include(x => x.Product).Include(x => x.Parameters).Include(x => x.Samples).ThenInclude(x => x.Results).SingleAsync(x => x.Id == lotId);
        var parameters = lot.Parameters.OrderBy(x => x.SortOrder).ToList();
        var currentResults = lot.Samples.SelectMany(x => x.Results).Where(x => x.IsCurrent && x.IsValid).ToList();

        var header = new StackPanel { Margin = new Thickness(20, 15, 20, 10) };
        header.Children.Add(new TextBlock { Text = $"Lote {lot.Number}", FontSize = 25, FontWeight = FontWeights.SemiBold });
        header.Children.Add(new TextBlock { Text = lot.Product.DisplayName, FontSize = 16, Margin = new Thickness(0, 3, 0, 3) });
        header.Children.Add(new TextBlock { Text = $"Fabricação: {lot.ManufactureDate:dd/MM/yyyy}   •   Situação: {PortugueseLabels.LotStatus(lot.Status)}   •   Amostras: {lot.Samples.Count(x => x.IsActive)}", Foreground = System.Windows.Media.Brushes.DimGray });

        var consolidated = parameters.Select(p =>
        {
            var result = ConsolidationEngine.Consolidate(p, currentResults.Where(x => x.LotParameterId == p.Id));
            var displayed = result.NumericValue.HasValue ? BrazilianDecimal.Format(result.NumericValue.Value, p.DecimalPlaces) : result.TextValue ?? "Pendente";
            var specification = !string.IsNullOrWhiteSpace(p.SpecificationText) ? p.SpecificationText : string.Join(" / ", new[] { p.Minimum.HasValue ? $"Mín. {BrazilianDecimal.Format(p.Minimum.Value, p.DecimalPlaces)}" : null, p.Maximum.HasValue ? $"Máx. {BrazilianDecimal.Format(p.Maximum.Value, p.DecimalPlaces)}" : null }.Where(x => x is not null));
            return new SummaryRow(p.ParameterName, displayed, p.Unit, specification, Conformity(result.Conformity));
        }).ToList();
        var summaryGrid = new DataGrid { ItemsSource = consolidated, IsReadOnly = true, AutoGenerateColumns = true, CanUserAddRows = false, Margin = new Thickness(12) };

        var individualTable = new DataTable(); individualTable.Columns.Add("Amostra");
        foreach (var p in parameters) individualTable.Columns.Add(p.ParameterName + (string.IsNullOrWhiteSpace(p.Unit) ? "" : $" ({p.Unit})"));
        foreach (var sample in lot.Samples.Where(x => x.IsActive).OrderBy(x => x.Code))
        {
            var row = individualTable.NewRow(); row[0] = sample.Code;
            for (var i = 0; i < parameters.Count; i++)
            {
                var result = sample.Results.SingleOrDefault(x => x.LotParameterId == parameters[i].Id && x.IsCurrent && x.IsValid);
                row[i + 1] = result?.NumericValue?.ToString(System.Globalization.CultureInfo.GetCultureInfo("pt-BR")) ?? result?.TextValue ?? (result?.ConformityValue is null ? "" : result.ConformityValue.Value ? "Conforme" : "Não conforme");
            }
            individualTable.Rows.Add(row);
        }
        var individualGrid = new DataGrid { ItemsSource = individualTable.DefaultView, IsReadOnly = true, AutoGenerateColumns = true, CanUserAddRows = false, Margin = new Thickness(12) };
        var tabs = new TabControl(); tabs.Items.Add(new TabItem { Header = "Resumo do lote", Content = summaryGrid }); tabs.Items.Add(new TabItem { Header = "Resultados por amostra", Content = individualGrid });
        var layout = new DockPanel(); DockPanel.SetDock(header, Dock.Top); layout.Children.Add(header); layout.Children.Add(tabs);
        new Window { Owner = owner, Title = $"Análises do lote {lot.Number}", Width = 950, Height = 650, MinWidth = 750, MinHeight = 480, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = layout }.ShowDialog();
    }

    private static string Conformity(ConformityStatus status) => status switch { ConformityStatus.Conforming => "Conforme", ConformityStatus.NonConforming => "Não conforme", ConformityStatus.Pending => "Pendente", _ => "Não aplicável" };
    private sealed record SummaryRow(string Parâmetro, string Resultado, string Unidade, string Especificação, string Situação);
}
