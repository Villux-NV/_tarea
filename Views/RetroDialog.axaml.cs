using System.Threading.Tasks;
using Avalonia.Controls;

namespace Tarea.Views;

public partial class RetroDialog : Window
{
    public bool Confirmed { get; private set; }

    public RetroDialog()
    {
        InitializeComponent();
    }

    private void Ok_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Confirmed = true;
        Close();
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }

    public static async Task<bool> Confirm(Window owner, string title, string message)
    {
        var dialog = new RetroDialog();
        dialog.TitleText.Text = "> " + title;
        dialog.MessageText.Text = message;
        dialog.BtnOk.Content = "[ confirm ]";
        dialog.BtnCancel.IsVisible = true;
        await dialog.ShowDialog(owner);
        return dialog.Confirmed;
    }

    public static async Task Alert(Window owner, string title, string message)
    {
        var dialog = new RetroDialog();
        dialog.TitleText.Text = "> " + title;
        dialog.MessageText.Text = message;
        await dialog.ShowDialog(owner);
    }
}