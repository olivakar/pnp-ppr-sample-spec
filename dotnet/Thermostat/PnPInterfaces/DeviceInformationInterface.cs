using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using System;
using System.Threading.Tasks;

namespace Thermostat.PnPInterfaces
{
    public class DeviceInfo
    {
        public string Manufacturer { get; set; }
        public string Model { get; set; }
        public string SoftwareVersion { get; set; }
        public string OperatingSystemName { get; set; }
        public string ProcessorArchitecture { get; set; }
        public string ProcessorManufacturer { get; set; }
        public double TotalMemory { get; set; }
        public double TotalStorage { get; set; }
    }


    public class DeviceInformationInterface //: DigitalTwinInterfaceClient
    {
        DeviceClient client;
        string componentName;
        public DeviceInformationInterface(DeviceClient client, string componentName)
        {
            this.client = client;
            this.componentName = "$iotin:" + componentName;
        }

        //private const string DeviceInformationInterfaceId = "dtmi:azure:DeviceManagement:DeviceInformation;1";

        public async Task ReportDeviceInfoPropertiesAsync(DeviceInfo di)
        {
           
            TwinCollection propertyCollection = new TwinCollection();
            propertyCollection[componentName] = new
            {
                manufacturer = new { value = di.Manufacturer },
                model = new { value = di.Model },
                swVersion = new { value = di.SoftwareVersion },
                osName = new { value = di.OperatingSystemName },
                processorArchitecture = new { value = di.ProcessorArchitecture },
                processorManufacturer = new { value = di.ProcessorManufacturer },
                totalMemory = new { value = di.TotalMemory },
                totalStorage = new { value = di.TotalStorage },
            };
            await this.client.UpdateReportedPropertiesAsync(propertyCollection);
            Console.WriteLine($"DeviceInformationInterface: sent {propertyCollection.Count} properties.");
        }
    }
}
