namespace AutoTtsStudio.Forms;

/// <summary>
/// Custom DataGridView with DoubleBuffered enabled via constructor to prevent flicker.
/// </summary>
internal sealed class SmoothDataGridView : DataGridView
{
    public SmoothDataGridView()
    {
        DoubleBuffered = true;
    }
}
