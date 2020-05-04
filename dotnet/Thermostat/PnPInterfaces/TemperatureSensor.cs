using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;

namespace Thermostat.PnPInterfaces
{
    public class TargetTempUpdatedEventArgs : EventArgs
    {
        public TargetTempUpdatedEventArgs(double t, bool completed = false)
        {
            TargetTemperature = t;
            TargetTemperatureCompleted = completed;
        }
        public double TargetTemperature { get;}
        public bool TargetTemperatureCompleted { get; }
    }

    class TemperatureSensor 
    {
        //const string id = "dtmi:com:examples:TemperatureSensor;1";
        DeviceClient client;
        string componentName;
        
        public event EventHandler<TargetTempUpdatedEventArgs> OnTargetTempReceived;
        public event EventHandler<TargetTempUpdatedEventArgs> OnCurrentTempUpdated;

        public double CurrentTemperature = 0d;

        public TemperatureSensor(DeviceClient client, string componentName)
        {
            this.client = client;
            this.componentName = "$iotin:" + componentName;
#pragma warning disable CS0618 // Type or member is obsolete
            _ = client.SetDesiredPropertyUpdateCallback(this.OnDesiredPropertyChanged, null);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        public async Task ReadDesiredPropertiesAsync()
        {
            var twin = await client.GetTwinAsync();
            var targetValue = GetPropertyValueIfFound(twin.Properties.Desired, "targetTemperature");
            if (double.TryParse(targetValue, out double targetTemp))
            {
                OnTargetTempReceived?.Invoke(this, new TargetTempUpdatedEventArgs(targetTemp));
                await ProcessTempUpdateAsync(targetTemp);
            }
        }

        public async Task ReportTargetTemperatureAsync(double target)
        {
            TwinCollection twin = new TwinCollection();
            twin[this.componentName] = new 
            {
                targetTemperature =  new { value = JsonConvert.SerializeObject(target) }
            };
            await this.client.UpdateReportedPropertiesAsync(twin);
        }

        public async Task ReportCurrentTemperatureAsync(double target)
        {
            TwinCollection twin = new TwinCollection();
            twin[this.componentName] = new
            {
                currentTemperature = new { value = JsonConvert.SerializeObject(target) }
            };
            await this.client.UpdateReportedPropertiesAsync(twin);
        }

        public async Task SendTelemetryValueAsync(double currentTemp)
        {
            await this.client.SendEventAsync(
              new Message(
                  Encoding.UTF8.GetBytes(
                      JsonConvert.SerializeObject(
                          new { temperature = currentTemp }
                      )
                  )
              )
           );
        }


        string GetPropertyValueIfFound(TwinCollection properties, string propertyName)
        {
            string result = string.Empty;
            if (properties.Contains(this.componentName))
            {
                var prop = properties[this.componentName][propertyName];
                var propVal = prop["value"];
                result = Convert.ToString(propVal);
            }
            return result;
        }

        async Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object ctx)
        {
            Console.WriteLine($"Received desired updates [{desiredProperties.ToJson()}]");
            string desiredPropertyValue = GetPropertyValueIfFound(desiredProperties, "targetTemperature");
            if (double.TryParse(desiredPropertyValue, out double targetTemperature))
            {
                OnTargetTempReceived?.Invoke(this, new TargetTempUpdatedEventArgs(targetTemperature));
                await ProcessTempUpdateAsync(targetTemperature);
            }
            await Task.FromResult("done");
        }

        public async Task ProcessTempUpdateAsync(double targetTemp)
        {
            // gradually increase current temp to target temp
            double step = (targetTemp - CurrentTemperature) / 10d;
            for (int i = 9; i >= 0; i--)
            {
                CurrentTemperature = targetTemp - step * (double)i;
                await SendTelemetryValueAsync(CurrentTemperature);
                await ReportCurrentTemperatureAsync(CurrentTemperature);
                OnCurrentTempUpdated?.Invoke(this, new TargetTempUpdatedEventArgs(CurrentTemperature, i==0));
                await Task.Delay(1000);
            }
        }
    }
}
