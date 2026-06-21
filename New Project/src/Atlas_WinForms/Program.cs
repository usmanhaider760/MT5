using Atlas_WinForms.Forms;

namespace Atlas_WinForms;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new Atlas_Dashboard());
    }
}