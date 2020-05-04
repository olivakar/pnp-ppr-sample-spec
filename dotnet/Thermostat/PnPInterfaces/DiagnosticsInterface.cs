using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Thermostat.PnPInterfaces
{

    public class RebootCommandEventArgs : EventArgs
    {
        public int Delay { get; private set; }
        public RebootCommandEventArgs(int delay)
        {
            Delay = delay;
        }
    }

    class DiagnosticsInterface //: DigitalTwinInterfaceClient
    {
        //const string id = "dtmi:com:examples:Diagnostics;1";

        DeviceClient client;
        string componentName;

        public event EventHandler<RebootCommandEventArgs> OnRebootCommand;

        public DiagnosticsInterface(DeviceClient client, string componentName) //: base(id, instanceName)
        {
            this.client = client;
            this.componentName = "$iotin:" + componentName;

            this.client.SetMethodHandlerAsync(this.componentName + "*reboot", (MethodRequest req, object ctx) => {
                int delay = 0;
                var delayVal = JObject.Parse(req.DataAsJson).SelectToken("commandRequest.value");
                int.TryParse(delayVal.Value<string>(), out delay);
                OnRebootCommand?.Invoke(this, new RebootCommandEventArgs(delay));
                return Task.FromResult(new MethodResponse(200));
            }, this).Wait();

        }

        public async Task SendTelemetryValueAsync(double workingSet)
        {
            await this.client.SendEventAsync(
                new Message(
                    Encoding.UTF8.GetBytes(
                        JsonConvert.SerializeObject(
                            new { workingSet = Environment.WorkingSet}
                        )
                    )
                )
             );
        }
    }
}
