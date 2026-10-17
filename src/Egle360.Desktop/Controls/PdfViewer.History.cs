using System.Windows.Input;

namespace Egle360.Desktop.Controls;

/// <summary>Vorherige und nächste Ansicht: Sprünge (Seite, Fundstelle, Listeneintrag) lassen sich zurückverfolgen.</summary>
public sealed partial class PdfViewer
{
    private const int MaxViewHistory = 50;

    private readonly record struct ViewState(double Zoom, double X, double Y);

    private readonly List<ViewState> _backViews = [];
    private readonly List<ViewState> _forwardViews = [];
    private bool _restoringView;

    public bool CanGoBack => _backViews.Count > 0;

    public bool CanGoForward => _forwardViews.Count > 0;

    /// <summary>Die Möglichkeit, zurück oder vorwärts zu gehen, hat sich geändert.</summary>
    public event EventHandler? ViewHistoryChanged;

    private ViewState CurrentView => new(_layout.Zoom, _scroll.HorizontalOffset, _scroll.VerticalOffset);

    /// <summary>Merkt sich die aktuelle Ansicht vor einem Sprung. Kleine Bewegungen zählen nicht als eigener Schritt.</summary>
    private void RememberView()
    {
        if (_restoringView || _layout.PageCount == 0)
        {
            return;
        }
        var current = CurrentView;
        if (_backViews.Count > 0 && IsSameView(_backViews[^1], current))
        {
            return;
        }
        _backViews.Add(current);
        if (_backViews.Count > MaxViewHistory)
        {
            _backViews.RemoveAt(0);
        }
        _forwardViews.Clear();
        ViewHistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool IsSameView(ViewState a, ViewState b) =>
        Math.Abs(a.Zoom - b.Zoom) < 1e-6 && Math.Abs(a.X - b.X) < _scroll.ViewportWidth / 4 && Math.Abs(a.Y - b.Y) < _scroll.ViewportHeight / 4;

    public void GoBack() => StepView(_backViews, _forwardViews);

    public void GoForward() => StepView(_forwardViews, _backViews);

    private void StepView(List<ViewState> from, List<ViewState> to)
    {
        if (from.Count == 0)
        {
            return;
        }
        var target = from[^1];
        from.RemoveAt(from.Count - 1);
        to.Add(CurrentView);
        _restoringView = true;
        try
        {
            if (Math.Abs(target.Zoom - _layout.Zoom) > 1e-6)
            {
                SetZoom(target.Zoom);
            }
            _scroll.UpdateLayout();
            _scroll.ScrollToHorizontalOffset(target.X);
            _scroll.ScrollToVerticalOffset(target.Y);
        }
        finally
        {
            _restoringView = false;
        }
        ViewHistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Seitentasten der Maus (zurück, vorwärts).</summary>
    private bool HandleHistoryButton(MouseButtonEventArgs e)
    {
        switch (e.ChangedButton)
        {
            case MouseButton.XButton1:
                GoBack();
                return true;
            case MouseButton.XButton2:
                GoForward();
                return true;
            default:
                return false;
        }
    }
}
