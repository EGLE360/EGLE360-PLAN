using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Egle360.Desktop.Controls;
using Egle360.Pdf;
using Egle360.Pdf.Markups;

namespace Egle360.Desktop.Views;

/// <summary>Maßstab und Kalibrierung für die Mengenermittlung.</summary>
public partial class DocumentsView
{
    /// <summary>Einheit für neu festgelegte Maßstäbe.</summary>
    private MeasureUnit _unit = MeasureUnit.Meter;

    private void AttachMeasure(OpenDocument open)
    {
        var viewer = open.Viewer;
        var document = open.Document;
        viewer.ScaleProvider = page => page >= 0 && page < document.PageCount ? document.GetPageScale(page) : null;
        viewer.ScaleRequired += (_, page) => OnScaleRequired(open, page);
        viewer.CalibrationMeasured += (_, args) => OnCalibrationMeasured(open, args.Page, args.LengthPt);
        viewer.ToolChanged += (_, _) => { if (open == _active) { RefreshMarkupBar(); } };
        viewer.CurrentPageChanged += (_, _) => { if (open == _active) { RefreshScaleButton(document.CanAnnotate); } };
        document.PageScaleChanged += (_, _) => { if (open == _active) { RefreshScaleButton(document.CanAnnotate); } };
    }

    private void RefreshScaleButton(bool canAnnotate)
    {
        if (_scaleButton is null)
        {
            return;
        }
        var scale = _active is { } open && open.Viewer.CurrentPage >= 0 && open.Viewer.CurrentPage < open.Document.PageCount
            ? open.Document.GetPageScale(open.Viewer.CurrentPage)
            : null;
        _scaleButton.Content = scale is null ? "Kein Maßstab" : $"{scale.Label}, {scale.UnitSymbol}";
        _scaleButton.IsEnabled = canAnnotate;
        _scaleButton.ToolTip = scale is null
            ? "Maßstab der aktuellen Seite festlegen oder kalibrieren (für Messungen nötig)"
            : $"Maßstab der aktuellen Seite: {scale.Label}, Einheit {scale.UnitSymbol}";
    }

    /// <summary>Summen für mehrere ausgewählte Messungen derselben Art und Einheit.</summary>
    private static string SelectionTotals(IReadOnlyList<Markup> selected)
    {
        var values = selected.Select(m => (m, v: Measurement.Compute(m))).Where(x => x.v is not null && MarkupKinds.IsSummable(x.m.Kind)).ToList();
        if (values.Count < 2 || values.Count != selected.Count || values.Select(x => x.v!.Value.Unit).Distinct().Count() != 1)
        {
            return "";
        }
        var unit = values[0].v!.Value.Unit;
        var decimals = values[0].m.Scale is { } s ? Measurement.Decimals(s.Unit) : 0;
        return $", Summe {Measurement.Number(values.Sum(x => x.v!.Value.Value), decimals)} {unit}";
    }

    private void OnScaleMenu(object sender, RoutedEventArgs e)
    {
        if (_active is not { } open || open.Viewer.CurrentPage < 0)
        {
            return;
        }
        var page = open.Viewer.CurrentPage;
        var current = open.Document.GetPageScale(page);
        var menu = new ContextMenu { PlacementTarget = _scaleButton, Placement = PlacementMode.Bottom };
        menu.Items.Add(new MenuItem { Header = $"Maßstab für Seite {page + 1}", IsEnabled = false });
        foreach (var ratio in MeasureScale.CommonRatios)
        {
            var scale = MeasureScale.FromRatio(ratio, current?.Unit ?? _unit);
            var item = new MenuItem { Header = scale.Label, IsCheckable = true, IsChecked = current?.Label == scale.Label };
            item.Click += (_, _) => ApplyScale(open, [page], scale);
            menu.Items.Add(item);
        }
        menu.Items.Add(new Separator());
        var calibrate = new MenuItem { Header = "Kalibrieren …", ToolTip = "Zwei Punkte einer bekannten Strecke im Plan anklicken und das wirkliche Maß eingeben." };
        calibrate.Click += (_, _) => StartCalibration(open);
        menu.Items.Add(calibrate);

        var units = new MenuItem { Header = "Einheit" };
        foreach (var unit in Enum.GetValues<MeasureUnit>())
        {
            var item = new MenuItem { Header = Measurement.Symbol(unit), IsCheckable = true, IsChecked = (current?.Unit ?? _unit) == unit };
            item.Click += (_, _) =>
            {
                _unit = unit;
                if (current is not null)
                {
                    ApplyScale(open, [page], current with { Unit = unit });
                }
            };
            units.Items.Add(item);
        }
        menu.Items.Add(units);
        menu.Items.Add(new Separator());
        var all = new MenuItem { Header = "Auf alle Seiten übertragen", IsEnabled = current is not null && open.Document.PageCount > 1 };
        all.Click += (_, _) => ApplyScale(open, Enumerable.Range(0, open.Document.PageCount).ToList(), current!);
        menu.Items.Add(all);
        var remove = new MenuItem { Header = "Maßstab entfernen", IsEnabled = current is not null };
        remove.Click += (_, _) => ApplyScale(open, [page], null);
        menu.Items.Add(remove);
        menu.IsOpen = true;
    }

    /// <summary>
    /// Setzt den Maßstab der Seiten. Vorhandene Messungen auf diesen Seiten übernehmen ihn, damit Plan und Werte
    /// zusammenpassen; das lässt sich mit Rückgängig zurücknehmen.
    /// </summary>
    private void ApplyScale(OpenDocument open, IReadOnlyList<int> pages, MeasureScale? scale)
    {
        try
        {
            foreach (var page in pages)
            {
                open.Document.SetPageScale(page, scale);
                if (scale is null)
                {
                    continue;
                }
                var changed = open.Document.GetMarkups(page)
                    .Where(m => MarkupKinds.NeedsScale(m.Kind) && m.Scale != scale)
                    .Select(m => m.WithScale(scale))
                    .ToList();
                if (changed.Count > 0)
                {
                    open.History.ReplaceMany(page, changed, "Maßstab der Messungen ändern");
                }
            }
            UpdateTab(open);
            RefreshMarkupBar();
        }
        catch (PdfException ex)
        {
            ShowError(ex.Message);
        }
    }

    private void StartCalibration(OpenDocument open)
    {
        open.Viewer.SetTool(ViewerTool.Calibrate);
        StatusMarkup.Text = "Kalibrieren: Anfang und Ende einer bekannten Strecke ziehen, zum Beispiel eine Bemaßung. Esc bricht ab.";
        open.Viewer.FocusView();
    }

    private void OnScaleRequired(OpenDocument open, int page)
    {
        var answer = MessageBox.Show(Owner,
            $"Seite {page + 1} hat noch keinen Maßstab. Ohne Maßstab lassen sich Längen und Flächen nicht berechnen.\n\n"
            + "Ja: Maßstab jetzt kalibrieren (zwei Punkte einer bekannten Strecke anklicken).\n"
            + "Nein: Maßstab aus der Liste wählen (Schaltfläche „Kein Maßstab“ in der Markierungsleiste).",
            "Maßstab fehlt", MessageBoxButton.YesNoCancel, MessageBoxImage.Information);
        if (answer == MessageBoxResult.Yes)
        {
            StartCalibration(open);
        }
        else if (answer == MessageBoxResult.No)
        {
            open.Viewer.GoToPageIfHidden(page);
            OnScaleMenu(this, new RoutedEventArgs());
        }
    }

    private void OnCalibrationMeasured(OpenDocument open, int page, double lengthPt)
    {
        RefreshMarkupBar();
        if (lengthPt < 1)
        {
            ShowError("Die Strecke ist zu kurz. Bitte zwei weiter entfernte Punkte wählen.");
            return;
        }
        var result = Dialogs.AskCalibration(Owner, _unit, open.Document.PageCount > 1);
        if (result is not { } r)
        {
            return;
        }
        _unit = r.Unit;
        var meters = r.Value / Measurement.Factor(r.Unit);
        var scale = MeasureScale.Calibrated(lengthPt, meters, r.Unit);
        ApplyScale(open, r.AllPages ? Enumerable.Range(0, open.Document.PageCount).ToList() : [page], scale);
    }

    /// <summary>Höhe (Wandfläche) oder Tiefe (Volumen) einer Messung festlegen oder entfernen.</summary>
    private void OnEditDepth(OpenDocument open, int page, Markup markup)
    {
        if (!open.Document.CanAnnotate || !Measurement.SupportsDepth(markup.Kind) || markup.Locked)
        {
            return;
        }
        var area = markup.Kind == MarkupKind.Area;
        var value = Dialogs.AskDepth(Owner,
            area ? "Tiefe für Volumen" : "Höhe für Wandfläche",
            area ? "Tiefe oder Stärke in Metern, zum Beispiel 0,25 für eine Bodenplatte. Das Volumen ist Fläche mal Tiefe."
                 : "Wandhöhe in Metern, zum Beispiel 2,80. Die Wandfläche ist Länge mal Höhe.",
            markup.Depth);
        if (value is null)
        {
            return;
        }
        double? depth = value > 0 ? value : null;
        var current = open.Document.GetMarkups(page).FirstOrDefault(m => m.Id == markup.Id);
        if (current is null || current.Depth == depth)
        {
            return;
        }
        try
        {
            open.History.Replace(page, current with { Depth = depth, Modified = DateTimeOffset.Now }, area ? "Tiefe ändern" : "Höhe ändern");
        }
        catch (PdfException ex)
        {
            ShowError(ex.Message);
        }
    }

    /// <summary>Neigung oder Gefälle einer Länge oder Fläche in Prozent festlegen oder entfernen.</summary>
    private void OnEditSlope(OpenDocument open, int page, Markup markup)
    {
        if (!open.Document.CanAnnotate || !Measurement.SupportsSlope(markup.Kind) || markup.Locked)
        {
            return;
        }
        var area = markup.Kind == MarkupKind.Area;
        var value = Dialogs.AskPositiveNumber(Owner, "Neigung oder Gefälle",
            area ? "Neigung der Fläche in Prozent, zum Beispiel 58 für ein Dach mit etwa 30°. Gemessen wird im Grundriss, dazu kommt die geneigte Fläche."
                 : "Neigung in Prozent, zum Beispiel 2 für ein Gefälle von 2 cm je Meter. Gemessen wird im Grundriss, dazu kommen die geneigte Länge und der Höhenunterschied.",
            markup.Slope, "%", "2,5");
        if (value is null)
        {
            return;
        }
        if (value > Measurement.MaxSlope)
        {
            ShowError($"Die Neigung darf höchstens {Measurement.Number(Measurement.MaxSlope, 0)} % betragen.");
            return;
        }
        double? slope = value > 0 ? value : null;
        var current = open.Document.GetMarkups(page).FirstOrDefault(m => m.Id == markup.Id);
        if (current is null || current.Slope == slope)
        {
            return;
        }
        try
        {
            open.History.Replace(page, current with { Slope = slope, Modified = DateTimeOffset.Now }, "Neigung ändern");
        }
        catch (PdfException ex)
        {
            ShowError(ex.Message);
        }
    }

    /// <summary>Einheitspreis einer Messung festlegen oder entfernen.</summary>
    private void OnEditPrice(OpenDocument open, int page, Markup markup)
    {
        if (!open.Document.CanAnnotate || markup.Locked || Egle360.Pdf.Export.MarkupReport.PricedQuantity(markup) is not { Quantity: not null } priced)
        {
            return;
        }
        var value = Dialogs.AskPositiveNumber(Owner, "Einheitspreis",
            $"Preis in Euro je {priced.Unit}. Der Gesamtpreis erscheint in der Mengenliste und im Excel-Export.",
            markup.UnitPrice, $"€ je {priced.Unit}", "45,50");
        if (value is null)
        {
            return;
        }
        double? price = value > 0 ? value : null;
        var current = open.Document.GetMarkups(page).FirstOrDefault(m => m.Id == markup.Id);
        if (current is null || current.UnitPrice == price)
        {
            return;
        }
        try
        {
            open.History.Replace(page, current with { UnitPrice = price, Modified = DateTimeOffset.Now }, "Einheitspreis ändern");
        }
        catch (PdfException ex)
        {
            ShowError(ex.Message);
        }
    }

    /// <summary>Drehwinkel abfragen: positiv gegen den Uhrzeigersinn, negativ im Uhrzeigersinn.</summary>
    private void OnRotateAngle(PdfViewer viewer)
    {
        var text = Dialogs.AskText(Owner, "Um Winkel drehen",
            "Winkel in Grad. Positive Werte drehen gegen den Uhrzeigersinn, negative im Uhrzeigersinn (z. B. 30 oder -12,5).", "15");
        if (text is null)
        {
            return;
        }
        var t = text.Trim().Replace("°", "").Replace(" ", "");
        if (!double.TryParse(t, System.Globalization.NumberStyles.Float, t.Contains(',') ? System.Globalization.CultureInfo.GetCultureInfo("de-DE") : System.Globalization.CultureInfo.InvariantCulture, out var degrees)
            || !double.IsFinite(degrees))
        {
            ShowError($"„{text}“ ist kein Winkel.");
            return;
        }
        viewer.RotateSelectionFree(degrees);
    }
}
