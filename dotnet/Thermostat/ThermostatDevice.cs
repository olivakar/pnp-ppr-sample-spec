using Microsoft.Azure.Devices.Client;
using Microsoft.Extensions.Logging;
using System;
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
            //_logger.LogInformation(connectionString);
            //deviceClient = DeviceClient.CreateFromConnectionString(connectionString, TransportType.Mqtt);
        }

        public async Task RunDeviceAsync()
        {
            deviceClient = await DPSConnection.Connect("0ne000C660F", "themorstat-ppr", "XV9zQ+KPsfYcSECcKm45aLZhKKXbxtdSdjxMpzMqd3U=", "urn:com:examples:Thermostat:1");
            var deviceInfo = new DeviceInformationInterface(deviceClient, "deviceInfo");
            var diag = new DiagnosticsInterface(deviceClient, "diag");
            tempSensor = new TemperatureSensor(deviceClient, "tempSensor1");

            await deviceInfo.ReportDeviceInfoPropertiesAsync(ThisDeviceInfo);

            await tempSensor.ReadDesiredPropertiesAsync();
            tempSensor.OnCurrentTempUpdated += TempSensor_OnCurrentTempUpdated;
            tempSensor.OnTargetTempReceived += TempSensor_OnTargetTempReceived;
            await Task.Run(async () => { await tempSensor.ProcessTempUpdateAsync(21); });


            diag.OnRebootCommand += Diag_OnRebootCommand;
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

        private void Diag_OnRebootCommand(object sender, RebootCommandEventArgs e)
        {
            tempSensor.CurrentTemperature = 0;
            for (int i = 0; i < e.Delay+1; i++)
            {
                _logger.LogWarning("================> REBOOT COMMAND RECEIVED <===================");
            }
            Task.Run(async () => { await tempSensor.ReadDesiredPropertiesAsync(); });
        }

        private void TempSensor_OnTargetTempReceived(object sender, TargetTempUpdatedEventArgs ea)
        {
            _logger.LogWarning("TargetTempUpdated: " + ea.TargetTemperature);
        }

        private void TempSensor_OnCurrentTempUpdated(object sender, TargetTempUpdatedEventArgs ea)
        {
            _logger.LogInformation("CurrentTempUpdated: " + ea.TargetTemperature);
        }

        private Task<MethodResponse> RebootCommandHandler(MethodRequest req, object objContext)
        {
            _logger.LogWarning("================> REBOOT COMMAND RECEIVED <===================");
            return Task.FromResult(new MethodResponse(200));
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
