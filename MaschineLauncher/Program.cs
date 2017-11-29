using log4net.Config;
using System;
using System.Threading;

namespace MaschineLauncher
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool createdNew;
            var mutex = new Mutex(true, "MaschineLauncher", out createdNew);
            if (createdNew)
            {
                XmlConfigurator.Configure();

                var usbWatcher = new UsbWatcher();
                usbWatcher.Start();

                Thread.Sleep(Timeout.Infinite);
            }
        }
    }
}
