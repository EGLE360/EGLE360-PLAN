using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Egle360.Desktop.Services;
using Egle360.Pdf;
using Egle360.Pdf.Plans;
using Microsoft.Win32;

namespace Egle360.Desktop.Views;

/// <summary>
/// Planverwaltung: Planliste eines Projektordners mit Plannummer, Index und Angaben je Plan, dazu der Verlauf aller
/// Stände. Pläne werden in den Projektordner kopiert; neue Indizes können die Markierungen des Vorgängers übernehmen.
/// </summary>
public partial class PlansView : UserControl
{
    private const string AllDisciplines = "Alle Gewerke";

    private readonly WpfTextOutliner _outliner = new();
    private PlanProject? _project;
    private bool _loading;

    /// <summary>Zeile der Planliste.</summary>
    private sealed record PlanRow(PlanEntry Plan)
    {
        public string Number => Plan.Number;
        public string Title => Plan.Title;
        public string Index => Plan.Index;
        public DateTime Added => Plan.Current.Added.LocalDateTime;
        public string Discipline => Plan.Discipline;
        public string Floor => Plan.Floor;
        public string Status => Plan.Status;
        public int RevisionCount => Plan.Revisions.Count;
    }

    /// <summary>Eintrag im Verlauf.</summary>
    private sealed record RevisionRow(PlanRevision Revision, bool IsCurrent)
    {
        public string Title => $"Index {PlanProject.Display(Revision.Index)}{(IsCurrent ? " (aktuell)" : "")}";

        public string Detail
        {
            get
            {
                var text = $"{Revision.Added.LocalDateTime:dd.MM.yyyy HH:mm}{(Revision.AddedBy.Length > 0 ? ", " + Revision.AddedBy : "")}";
                return Revision.Note.Length > 0 ? $"{text}\n{Revision.Note}" : text;
            }
        }
    }

    public PlansView()
    {
        InitializeComponent();
        foreach (var d in PlanDiscipline.Defaults)
        {
            DisciplineBox.Items.Add(d.Code);
        }
        foreach (var s in PlanRegister.StatusNames)
        {
            StatusBox.Items.Add(s);
        }
        if (App.Theme.Settings.PlanProjectPath is { } path && PlanProject.Exists(path))
        {
            TryOpen(path, quiet: true);
        }
        Refresh();
    }

    private Window Owner => Window.GetWindow(this)!;

    private PlanEntry? SelectedPlan => (PlanGrid.SelectedItem as PlanRow)?.Plan is { } p ? _project?.Register.Find(p.Id) : null;

    /// <summary>Für den Screenshot-Modus: ein vorbereitetes Projekt anzeigen.</summary>
    internal void ShowProject(string root) => TryOpen(root, quiet: false);

    internal void SelectFirstForScreenshot() => PlanGrid.SelectedIndex = PlanGrid.Items.Count > 0 ? 0 : -1;

    // ---------- Projekt ----------

    private void OnNewProject(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Ordner für das neue Planprojekt wählen" };
        if (dialog.ShowDialog(Owner) != true)
        {
            return;
        }
        if (PlanProject.Exists(dialog.FolderName))
        {
            TryOpen(dialog.FolderName, quiet: false);
            return;
        }
        var name = Dialogs.AskText(Owner, "Neues Planprojekt", "Name des Projekts:", Path.GetFileName(dialog.FolderName.TrimEnd('\\', '/')));
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }
        try
        {
            SetProject(PlanProject.Create(dialog.FolderName, name.Trim()));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError(ex.Message);
        }
    }

    private void OnOpenProject(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Ordner des Planprojekts wählen" };
        if (dialog.ShowDialog(Owner) != true)
        {
            return;
        }
        if (!PlanProject.Exists(dialog.FolderName))
        {
            ShowError($"In diesem Ordner gibt es keine Planliste ({PlanRegister.FileName}). Mit „Neues Planprojekt“ lässt sich eine anlegen.");
            return;
        }
        TryOpen(dialog.FolderName, quiet: false);
    }

    private void TryOpen(string root, bool quiet)
    {
        try
        {
            SetProject(PlanProject.Open(root));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            if (!quiet)
            {
                ShowError($"Die Planliste konnte nicht gelesen werden: {ex.Message}");
            }
        }
    }

    private void SetProject(PlanProject project)
    {
        _project = project;
        App.Theme.Settings.PlanProjectPath = project.Root;
        App.Theme.SaveSettings();
        Refresh();
    }

    // ---------- Anzeige ----------

    private void Refresh(string? selectId = null)
    {
        selectId ??= SelectedPlan?.Id;
        var has = _project is not null;
        ProjectTitle.Text = has && _project!.Register.Name.Length > 0 ? _project.Register.Name : "Pläne";
        ProjectInfo.Text = has ? $"{_project!.Register.Plans.Count} Pläne in {_project.Root}" : "";
        AddButton.IsEnabled = ExportButton.IsEnabled = has;

        _loading = true;
        var current = DisciplineFilter.SelectedItem as string ?? AllDisciplines;
        DisciplineFilter.Items.Clear();
        DisciplineFilter.Items.Add(AllDisciplines);
        foreach (var d in _project?.Register.Plans.Select(p => p.Discipline).Where(d => d.Length > 0).Distinct().Order() ?? Enumerable.Empty<string>())
        {
            DisciplineFilter.Items.Add(d);
        }
        DisciplineFilter.SelectedItem = DisciplineFilter.Items.Contains(current) ? current : AllDisciplines;
        _loading = false;

        var plans = FilteredPlans();
        PlanGrid.ItemsSource = plans.Select(p => new PlanRow(p)).ToList();
        PlanGrid.SelectedItem = PlanGrid.Items.Cast<PlanRow>().FirstOrDefault(r => r.Plan.Id == selectId);
        EmptyText.Visibility = plans.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Text = !has
            ? "Noch kein Planprojekt geöffnet. Ein Planprojekt ist ein Ordner mit einer Planliste. Mit „Neues Planprojekt“ wählst du einen Ordner, danach fügst du Pläne hinzu oder ziehst PDF-Dateien hierher."
            : _project!.Register.Plans.Count == 0
                ? "Die Planliste ist leer. Pläne hinzufügen oder PDF-Dateien hierher ziehen. Plannummer und Index werden aus dem Plankopf oder dem Dateinamen erkannt."
                : "Kein Plan passt zum Filter.";
        ShowDetails();
    }

    private List<PlanEntry> FilteredPlans()
    {
        if (_project is null)
        {
            return [];
        }
        var discipline = DisciplineFilter.SelectedItem as string is { } d && d != AllDisciplines ? d : "";
        return [.. _project.Register.Filter(FilterBox.Text, discipline)];
    }

    private void OnFilterChanged(object sender, EventArgs e)
    {
        if (!_loading && IsLoaded)
        {
            Refresh();
        }
    }

    private void OnPlanSelected(object sender, SelectionChangedEventArgs e) => ShowDetails();

    private void ShowDetails()
    {
        var plan = SelectedPlan;
        DetailsPanel.IsEnabled = plan is not null;
        RevisionButton.IsEnabled = OpenPlanButton.IsEnabled = RemoveButton.IsEnabled = plan is not null;
        _loading = true;
        NumberBox.Text = plan?.Number ?? "";
        TitleBox.Text = plan?.Title ?? "";
        DisciplineBox.Text = plan?.Discipline ?? "";
        StatusBox.Text = plan?.Status ?? "";
        FloorBox.Text = plan?.Floor ?? "";
        BuildingBox.Text = plan?.Building ?? "";
        SectionBox.Text = plan?.Section ?? "";
        AuthorBox.Text = plan?.Author ?? "";
        ApprovalBox.Text = plan?.Approval ?? "";
        RevisionList.ItemsSource = plan?.Revisions.Select((r, i) => new RevisionRow(r, i == plan.Revisions.Count - 1)).Reverse().ToList();
        _loading = false;
    }

    /// <summary>Übernimmt geänderte Angaben aus dem Formular, sobald ein Feld verlassen wird.</summary>
    private void OnDetailChanged(object sender, RoutedEventArgs e)
    {
        if (_loading || _project is null || SelectedPlan is not { } plan)
        {
            return;
        }
        var number = NumberBox.Text.Trim();
        var updated = plan with
        {
            Number = number.Length > 0 ? number : plan.Number,
            Title = TitleBox.Text.Trim(),
            Discipline = DisciplineBox.Text.Trim(),
            Status = StatusBox.Text.Trim(),
            Floor = FloorBox.Text.Trim(),
            Building = BuildingBox.Text.Trim(),
            Section = SectionBox.Text.Trim(),
            Author = AuthorBox.Text.Trim(),
            Approval = ApprovalBox.Text.Trim(),
        };
        if (updated == plan)
        {
            return;
        }
        if (PlanRegister.NumberKey(updated.Number) != PlanRegister.NumberKey(plan.Number) && _project.Register.FindByNumber(updated.Number) is not null)
        {
            ShowError($"Die Plannummer {updated.Number} gibt es schon.");
            NumberBox.Text = plan.Number;
            return;
        }
        if (Save(() => _project.Update(_project.Register.Replace(updated))))
        {
            Dispatcher.BeginInvoke(() => Refresh(plan.Id));
        }
    }

    // ---------- Pläne ----------

    private void OnAddPlans(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "PDF-Dateien (*.pdf)|*.pdf", Multiselect = true, Title = "Pläne hinzufügen" };
        if (dialog.ShowDialog(Owner) == true)
        {
            Import(dialog.FileNames);
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = _project is not null && e.Data.GetData(DataFormats.FileDrop) is string[] files
            && files.Any(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (_project is not null && e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            Import(files.Where(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)).ToList());
        }
    }

    private void Import(IReadOnlyList<string> files)
    {
        if (_project is null || files.Count == 0)
        {
            return;
        }
        IReadOnlyList<PlanImportResult> results;
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            results = _project.Import(files, Environment.UserName, TakeOverBox.IsChecked == true, _outliner);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PdfException)
        {
            Mouse.OverrideCursor = null;
            ShowError($"Beim Hinzufügen ist ein Fehler aufgetreten: {ex.Message}");
            Refresh();
            return;
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
        Refresh(results.LastOrDefault(r => r.PlanId.Length > 0)?.PlanId);
        var lines = new List<string>();
        var added = results.Count(r => r.Action == PlanImportAction.NewPlan);
        var revised = results.Where(r => r.Action == PlanImportAction.NewRevision).ToList();
        if (added > 0)
        {
            lines.Add(added == 1 ? "1 neuer Plan angelegt." : $"{added} neue Pläne angelegt.");
        }
        foreach (var r in revised)
        {
            var plan = _project.Register.Find(r.PlanId)!;
            lines.Add($"Plan {plan.Number}: neuer Index {PlanProject.Display(plan.Index)}"
                + (TakeOverBox.IsChecked == true ? $", {r.MarkupsTakenOver} Markierung(en) übernommen." : "."));
        }
        lines.AddRange(results.Where(r => r.Action is PlanImportAction.Duplicate or PlanImportAction.Failed)
            .Select(r => $"{Path.GetFileName(r.Source)}: {r.Message}"));
        if (revised.Count > 0 || results.Any(r => r.Action is PlanImportAction.Duplicate or PlanImportAction.Failed))
        {
            MessageBox.Show(Owner, string.Join("\n", lines), "Pläne hinzugefügt", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OnNewRevision(object sender, RoutedEventArgs e)
    {
        if (_project is null || SelectedPlan is not { } plan)
        {
            return;
        }
        var dialog = new OpenFileDialog { Filter = "PDF-Dateien (*.pdf)|*.pdf", Title = $"Neuer Index für Plan {plan.Number}" };
        if (dialog.ShowDialog(Owner) != true)
        {
            return;
        }
        var suggested = PlanProject.ReadInfo(dialog.FileName)?.Index is { Length: > 0 } read ? read : NextIndex(plan.Index);
        if (Dialogs.AskText(Owner, "Neuer Index", $"Index des neuen Stands von Plan {plan.Number} (bisher {PlanProject.Display(plan.Index)}):", suggested) is not { } index)
        {
            return;
        }
        index = index.Trim();
        if (plan.Revisions.Any(r => string.Equals(r.Index, index, StringComparison.OrdinalIgnoreCase)))
        {
            ShowError($"Plan {plan.Number} hat den Index {PlanProject.Display(index)} schon.");
            return;
        }
        var note = Dialogs.AskText(Owner, "Neuer Index", "Änderungsvermerk (optional):", "") ?? "";
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            var (_, taken) = _project.AddRevision(plan.Id, dialog.FileName, index, Environment.UserName, note.Trim(), TakeOverBox.IsChecked == true, _outliner);
            Mouse.OverrideCursor = null;
            Refresh(plan.Id);
            if (TakeOverBox.IsChecked == true)
            {
                MessageBox.Show(Owner, $"Index {PlanProject.Display(index)} eingetragen, {taken} Markierung(en) übernommen.", "Neuer Index",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PdfException)
        {
            Mouse.OverrideCursor = null;
            ShowError($"Der neue Index konnte nicht eingetragen werden: {ex.Message}");
        }
    }

    /// <summary>Nächster Index als Vorschlag: A wird B, 03 wird 04; sonst leer.</summary>
    internal static string NextIndex(string index)
    {
        if (index.Length == 1 && char.IsAsciiLetterUpper(index[0]) && index[0] < 'Z')
        {
            return ((char)(index[0] + 1)).ToString();
        }
        if (index.Length > 0 && index.All(char.IsAsciiDigit) && int.TryParse(index, out var n))
        {
            return (n + 1).ToString(new string('0', index.Length));
        }
        return index.Length == 0 ? "A" : "";
    }

    private void OnOpenPlan(object sender, RoutedEventArgs e)
    {
        if (SelectedPlan is { } plan)
        {
            OpenFile(plan.Current);
        }
    }

    private void OnPlanDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if ((e.OriginalSource as FrameworkElement)?.DataContext is PlanRow row && _project?.Register.Find(row.Plan.Id) is { } plan)
        {
            OpenFile(plan.Current);
        }
    }

    private void OnRevisionDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (RevisionList.SelectedItem is RevisionRow row)
        {
            OpenFile(row.Revision);
        }
    }

    private void OpenFile(PlanRevision revision)
    {
        if (_project is null)
        {
            return;
        }
        var path = _project.FullPath(revision);
        if (!File.Exists(path))
        {
            ShowError($"Die Datei {revision.File} fehlt im Projektordner.");
            return;
        }
        ((MainWindow)Owner).OpenDocuments([path]);
    }

    private void OnRemovePlan(object sender, RoutedEventArgs e)
    {
        if (_project is null || SelectedPlan is not { } plan)
        {
            return;
        }
        if (MessageBox.Show(Owner, $"Plan {plan.Number} aus der Planliste entfernen? Die Dateien bleiben im Ordner {PlanProject.PlanFolder}.",
                "EGLE360 PLAN", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes
            && Save(() => _project.Update(_project.Register.Remove(plan.Id))))
        {
            Refresh();
        }
    }

    private void OnExport(object sender, RoutedEventArgs e)
    {
        if (_project is null)
        {
            return;
        }
        var dialog = new SaveFileDialog
        {
            Filter = "Excel-Arbeitsmappe (*.xlsx)|*.xlsx",
            FileName = $"Planliste {(_project.Register.Name.Length > 0 ? _project.Register.Name : "Projekt")}.xlsx",
        };
        if (dialog.ShowDialog(Owner) == true)
        {
            Save(() => File.WriteAllBytes(dialog.FileName, PlanListExport.ToXlsx(_project.Register, FilteredPlans())));
        }
    }

    private bool Save(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError(ex.Message);
            return false;
        }
    }

    private void ShowError(string message) =>
        MessageBox.Show(Owner, message, "EGLE360 PLAN", MessageBoxButton.OK, MessageBoxImage.Warning);
}
