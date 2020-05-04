using Microsoft.Azure.Devices.Client;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Thermostat.PnPInterfaces;

namespace Thermostat
{
    class ThermostatDevice
    {

        private readonly ILogger _logger;
        private CancellationToken _quitSignal;

        DeviceClient deviceClient;
        TemperatureSensor tempSensor;

        public ThermostatDevice(string connectionString, ILogger logger, CancellationToken cancellationToken)
        {
            _quitSignal = cancellationToken;
            _logger = logger;
            _logger.LogInformation(connectionString);
            deviceClient = DeviceClient.CreateFromConnectionString(connectionString, TransportType.Mqtt);
        }

        public async Task RunDeviceAsync()
        {
            var deviceInfo = new DeviceInformationInterface(deviceClient, "deviceInfo");
            var diag = new DiagnosticsInterface(deviceClient, "diag");
            tempSensor = new TemperatureSensor(deviceClient, "tempSensor1");

            diag.OnRebootCommand += Diag_OnRebootCommand;
            await deviceInfo.ReportDeviceInfoPropertiesAsync(ThisDeviceInfo);

            tempSensor.OnCurrentTempUpdated += TempSensor_OnCurrentTempUpdated;
            tempSensor.OnTargetTempReceived += TempSensor_OnTargetTempReceived;
            await tempSensor.ReadDesiredPropertiesAsync();

            await Task.Run(async () =>
            {
                while (!_quitSignal.IsCancellationRequested)
                {
                    await diag.SendTelemetryValueAsync(Environment.WorkingSet);
                    _logger.LogInformation("Sending Working Set");
                    await Task.Delay(5000);
                }
            });
        }

        private async Task ProcessTempUpdateAsync(double targetTemp)
        {
            // gradually increase current temp to target temp
            double step = (targetTemp - tempSensor.CurrentTemperature) / 10d;
            for (int i = 9; i >= 0; i--)
            {
                tempSensor.CurrentTemperature = targetTemp - step * (double)i;
                await tempSensor.SendTelemetryValueAsync(tempSensor.CurrentTemperature);
                await tempSensor.ReportCurrentTemperatureAsync(tempSensor.CurrentTemperature);
                await Task.Delay(1000);
            }
        }


        private void Diag_OnRebootCommand(object sender, RebootCommandEventArgs e)
        {
            tempSensor.CurrentTemperature = 0;
            for (int i = 0; i < e.Delay; i++)
            {
                _logger.LogWarning("================> REBOOT COMMAND RECEIVED <===================");
                Task.Delay(1000).Wait();
            }
            Task.Run(async () => { await tempSensor.ReadDesiredPropertiesAsync(); });
        }

        private void TempSensor_OnTargetTempReceived(object sender, TemperatureEventArgs ea)
        {
            _logger.LogWarning("TargetTempUpdated: " + ea.Temperature);
            Task.Run(async () => { await this.ProcessTempUpdateAsync(ea.Temperature);  });
        }

        private void TempSensor_OnCurrentTempUpdated(object sender, TemperatureEventArgs ea)
        {
            _logger.LogInformation("CurrentTempUpdated: " + ea.Temperature);
        }


        DeviceInfo ThisDeviceInfo
        {
            get
            {
                return new DeviceInfo
                {
                    Manufacturer = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"),
                    Model = Environment.OSVersion.Platform.ToString(),
                    SoftwareVersion = Environment.OSVersion.VersionString,
                    OperatingSystemName = Environment.GetEnvironmentVariable("OS"),
                    ProcessorArchitecture = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE"),
                    ProcessorManufacturer = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"),
                    TotalStorage = 123.45,// System.IO.DriveInfo.GetDrives()[0].TotalSize,
                    TotalMemory= Environment.WorkingSet
                };
            }
        }
    }
}
