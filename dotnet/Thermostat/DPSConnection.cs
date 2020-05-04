using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Thermostat
{
    class DPSConnection
    {
        public static async Task<DeviceClient> Connect(string scopeId, string deviceId, string deviceKey, string dcmId)
        {
            
            using (var security = new SecurityProviderSymmetricKey(deviceId, deviceKey, deviceKey /*use secondary key?*/))
            using (var transport = new ProvisioningTransportHandlerMqtt(TransportFallbackType.TcpOnly))
            {
                // connect to Azure IoT DPS (device provisioning service)
                var provClient = ProvisioningDeviceClient.Create("global.azure-devices-provisioning.net", scopeId, security, transport);
                Console.Write("ProvisioningClient RegisterAsync . . . ");
                DeviceRegistrationResult result = await provClient.RegisterAsync(GetProvisionPayload(dcmId)).ConfigureAwait(false);

                Console.WriteLine($"{result.Status}");
                Console.WriteLine($"ProvisioningClient AssignedHub: {result.AssignedHub}; DeviceID: {result.DeviceId}");

                if (result.Status != ProvisioningRegistrationStatusType.Assigned)
                {
                    throw new ProvisioningTransportException("Error: Authentication has failed: " + result.Status);
                }

                IAuthenticationMethod auth;
                Console.WriteLine("Creating Symmetric Key DeviceClient authenication");
                auth = new DeviceAuthenticationWithRegistrySymmetricKey(result.DeviceId,
                  (security as SecurityProviderSymmetricKey).GetPrimaryKey());
                var deviceClient = DeviceClient.Create(result.AssignedHub, auth, TransportType.Mqtt);

                Console.WriteLine("DeviceClient OpenAsync.");
                await deviceClient.OpenAsync().ConfigureAwait(false);
                return deviceClient;
            }
        }

        static ProvisioningRegistrationAdditionalData GetProvisionPayload(string dcmId)
        {
            return new ProvisioningRegistrationAdditionalData
            {
                JsonData = $@"{{
                    ""__iot:interfaces"":
                    {{
                        ""CapabilityModelId"": ""{dcmId}""
                    }}
                }}",
            };
        }
    }
}
