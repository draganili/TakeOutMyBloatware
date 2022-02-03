using System.Diagnostics;

namespace TakeOutMyBloatware.Operations
{
    public class WebBrowserAction : IActionHandler
    {
        private readonly string url;

        public WebBrowserAction(string url)
        {
            this.url = url;
        }

        public void Run()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }
    }
}
