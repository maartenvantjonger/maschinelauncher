using log4net;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading;
using System.Windows.Forms;

namespace MaschineLauncher
{
    class UsbWatcher
    {
        private static readonly string GUID_DEVCLASS_USB = "{36fc9e60-c465-11cf-8056-444553540000}";
        private ManagementEventWatcher _watcher;
        private ILog _log = LogManager.GetLogger("UsbWatcher");

        public void Start()
        {
            var query = new WqlEventQuery
            {
                QueryString = string.Format(@"SELECT * FROM __InstanceCreationEvent WITHIN 1
                    WHERE TargetInstance ISA 'Win32_PnPEntity'
                    AND TargetInstance.ClassGuid = '{0}'", GUID_DEVCLASS_USB)
            };

            _watcher = new ManagementEventWatcher();
            _watcher.EventArrived += watcher_EventArrived;
            _watcher.Query = query;
            _watcher.Start();

            _log.Info("Started watching for USB devices");
        }

        public void Stop()
        {
            if (_watcher != null)
            {
                _watcher.Stop();
                _watcher.Dispose();
            }
        }

        private void watcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            var targetInstance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            var deviceId = (string)targetInstance.Properties["DeviceID"].Value;

            var targetDeviceIds = ConfigurationManager.AppSettings["DeviceIds"].Split(',').ToList();
            var match = targetDeviceIds.Exists(targetId => deviceId.StartsWith(targetId, StringComparison.InvariantCultureIgnoreCase));
            if (match)
            {
                _log.InfoFormat("Device matched: {0}", deviceId);

                var thread = new Thread(RunApplication);
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
            }
        }

        private void RunApplication()
        {
            var applicationPath = ConfigurationManager.AppSettings["ApplicationPath"];
            if (!File.Exists(applicationPath))
            {
                applicationPath = PromptApplicationPath(applicationPath);
            }

            if (File.Exists(applicationPath))
            {
                if (!IsProcessOpen(applicationPath))
                {
                    StartProcess(applicationPath);
                }
                else
                {
                    _log.InfoFormat("Application process already exists: {0}", applicationPath);
                }
            }
            else {
                _log.InfoFormat("No valid application path given: {0}", applicationPath);
            }
        }

        private string PromptApplicationPath(string applicationPath)
        {
            using (var fileDialog = new OpenFileDialog())
            {
                fileDialog.Title = "Choose an application to be launched";
                fileDialog.Filter = "Executable Files|*.exe";
                fileDialog.InitialDirectory = Path.GetDirectoryName(applicationPath);
                fileDialog.Multiselect = false;
                fileDialog.CheckFileExists = true;

                var result = fileDialog.ShowDialog();
                if (result == DialogResult.OK)
                {
                    applicationPath = fileDialog.FileName;

                    var config = ConfigurationManager.OpenExeConfiguration(Application.ExecutablePath);
                    config.AppSettings.Settings["ApplicationPath"].Value = applicationPath;
                    config.Save();
                }
            }

            return applicationPath;
        }

        private void StartProcess(string applicationPath)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = applicationPath,
                    ErrorDialog = true,
                    WorkingDirectory = Path.GetDirectoryName(applicationPath)
                };

                Process.Start(startInfo);
                _log.InfoFormat("Launched application: {0}", startInfo.FileName);
            }
            catch (Exception e)
            {
                _log.Error(String.Format("Error launching application: {0}", applicationPath), e);
            }
        }

        public bool IsProcessOpen(string applicationPath)
        {
            var query = "SELECT ExecutablePath FROM Win32_Process WHERE ExecutablePath IS NOT NULL";
            using (var searcher = new ManagementObjectSearcher(query))
            {
                using (var results = searcher.Get())
                {
                    return results.Cast<ManagementObject>()
                        .Any(p => (string)p.Properties["ExecutablePath"].Value == applicationPath);
                }
            }
        }
    }
}
