using System.IO;

namespace TakeOutMyBloatware.Operations
{
    public class LicensePrinter : IActionHandler
    {
        private readonly IUI ui;
        public LicensePrinter(IUI ui) => this.ui = ui;

        public void Run()
        {
            Stream licenseFile = GetType().Assembly.GetManifestResourceStream("TakeOutMyBloatware.License.txt")!;
            string licenseText = new StreamReader(licenseFile).ReadToEnd();
            ui.PrintNotice(licenseText);
        }
    }
}
