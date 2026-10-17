using Egle360.Pdf.Export;

namespace Egle360.Pdf.Plans;

/// <summary>Planliste als Excel-Mappe: ein Blatt mit dem aktuellen Stand je Plan, eines mit allen Ständen.</summary>
public static class PlanListExport
{
    public static byte[] ToXlsx(PlanRegister register, IReadOnlyList<PlanEntry> plans)
    {
        var list = new XlsxSheet("Planliste", [16, 40, 8, 14, 10, 12, 12, 14, 16, 18, 24, 8]);
        list.Rows.Add([.. new[] { "Plannummer", "Bezeichnung", "Index", "Stand vom", "Gewerk", "Geschoss", "Gebäude", "Bauabschnitt", "Status", "Verfasser", "Freigabe", "Stände" }
            .Select(XlsxCell.Header)]);
        foreach (var p in plans)
        {
            list.Rows.Add([
                XlsxCell.Text(p.Number), XlsxCell.Text(p.Title), XlsxCell.Text(p.Index), XlsxCell.Date(p.Current.Added.LocalDateTime),
                XlsxCell.Text(p.Discipline), XlsxCell.Text(p.Floor), XlsxCell.Text(p.Building), XlsxCell.Text(p.Section),
                XlsxCell.Text(p.Status), XlsxCell.Text(p.Author), XlsxCell.Text(p.Approval), XlsxCell.Integer(p.Revisions.Count),
            ]);
        }

        var history = new XlsxSheet("Verlauf", [16, 40, 8, 16, 18, 40, 50]);
        history.Rows.Add([.. new[] { "Plannummer", "Bezeichnung", "Index", "Hinzugefügt", "Von", "Vermerk", "Datei" }.Select(XlsxCell.Header)]);
        foreach (var p in plans)
        {
            foreach (var r in p.Revisions)
            {
                history.Rows.Add([
                    XlsxCell.Text(p.Number), XlsxCell.Text(p.Title), XlsxCell.Text(r.Index), XlsxCell.Date(r.Added.LocalDateTime),
                    XlsxCell.Text(r.AddedBy), XlsxCell.Text(r.Note), XlsxCell.Text(r.File),
                ]);
            }
        }
        return XlsxWriter.Write([list, history]);
    }
}
