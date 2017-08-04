﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.IoTSolutions.IotHubManager.WebService.v1.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebService.Test.helpers;
using WebService.Test.helpers.Http;
using Xunit;
using Xunit.Abstractions;

namespace WebService.Test.IntegrationTests
{
    public class DevicesStatusTest
    {
        private readonly HttpClient httpClient;

        // Pull Request don't have access to secret credentials, which are
        // required to run tests interacting with Azure IoT Hub.
        // The tests should run when working locally and when merging branches.
        private readonly bool credentialsAvailable;

        public DevicesStatusTest(ITestOutputHelper log)
        {
            this.httpClient = new HttpClient(log);
            this.credentialsAvailable = !CIVariableHelper.IsPullRequest(log);
        }

        [SkippableFact, Trait(Constants.Type, Constants.IntegrationTest)]
        public void GetDeviceIsHealthy()
        {
            Skip.IfNot(this.credentialsAvailable, "Skipping this test for Travis pull request as credentials are not available");

            var request = new HttpRequest();
            request.SetUriFromString(AssemblyInitialize.Current.WsHostname + "/v1/devices");

            // Act
            var response = this.httpClient.GetAsync(request).Result;

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [SkippableFact, Trait(Constants.Type, Constants.IntegrationTest)]
        public void GetDeviceByIdIsHealthy()
        {
            Skip.IfNot(this.credentialsAvailable, "Skipping this test for Travis pull request as credentials are not available");

            var request = new HttpRequest();
            request.SetUriFromString(AssemblyInitialize.Current.WsHostname + "/v1/devices/foobar");

            // Act
            var response = this.httpClient.GetAsync(request).Result;

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [SkippableFact, Trait(Constants.Type, Constants.IntegrationTest)]
        public void CreateDeleteDeviceIsHealthy()
        {
            Skip.IfNot(this.credentialsAvailable, "Skipping this test for Travis pull request as credentials are not available");

            var deviceId = "testDevice123";
            this.DeleteDeviceIfExists(deviceId);

            // create device
            var device = new DeviceRegistryApiModel()
            {
                Id = deviceId
            };

            var request = new HttpRequest();
            request.SetUriFromString(AssemblyInitialize.Current.WsHostname + $"/v1/devices");
            request.SetContent(device);

            var response = this.httpClient.PostAsync(request).Result;
            var azureDevice = JsonConvert.DeserializeObject<DeviceRegistryApiModel>(response.Content);

            Assert.Equal(deviceId, azureDevice.Id);

            // clean it up
            this.DeleteDeviceIfExists(deviceId);
        }

        [SkippableFact, Trait(Constants.Type, Constants.IntegrationTest)]
        public void UpdateTwinIsHealthy()
        {
            Skip.IfNot(this.credentialsAvailable, "Skipping this test for Travis pull request as credentials are not available");

            var deviceId = "testDevice1";
            var device = this.CreateDeviceIfNotExists(deviceId);

            try
            {
                var newTagValue = System.Guid.NewGuid().ToString();

                // update twin by adding/editing a tag
                if (device.Tags.ContainsKey("newTag"))
                {
                    device.Tags["newTag"] = newTagValue;
                }
                else
                {
                    device.Tags.Add("newTag", newTagValue);
                }

                var newConfig = new NewConfig
                {
                    TelemetryInterval = 10,
                    TelemetryType = "Type1;Type2"
                };

                // update twin by adding config 
                var configValue = JToken.Parse(JsonConvert.SerializeObject(newConfig));
                if (device.DesiredProperties.ContainsKey("config"))
                {
                    device.DesiredProperties["config"] = configValue;
                }
                else
                {
                    device.DesiredProperties.Add("config", configValue);
                }

                var request = new HttpRequest();
                request.SetUriFromString(AssemblyInitialize.Current.WsHostname + $"/v1/devices/{deviceId}");
                request.SetContent(device);

                var response = this.httpClient.PutAsync(request).Result;

                device = JsonConvert.DeserializeObject<DeviceRegistryApiModel>(response.Content);
                Assert.Equal(newTagValue, device.Tags["newTag"]);

                // get device again
                device = this.GetDevice(deviceId);
                Assert.Equal(newTagValue, device.Tags["newTag"]);

                var configInTwin = JsonConvert.DeserializeObject<NewConfig>(device.DesiredProperties["config"].ToString());
                Assert.Equal(10, configInTwin.TelemetryInterval);

            }
            finally
            {
                // clean it up
                this.DeleteDeviceIfExists(deviceId);
            }
        }

        public class NewConfig
        {
            public int TelemetryInterval { get; set; }
            public string TelemetryType { get; set; }
        }

        [SkippableFact, Trait(Constants.Type, Constants.IntegrationTest)]
        public void GetDeviceListTest()
        {
            Skip.IfNot(this.credentialsAvailable, "Skipping this test for Travis pull request as credentials are not available");

            var deviceId = "testDevice-getdeviceList";
            var device = this.CreateDeviceIfNotExists(deviceId);

            try
            {
                var request = new HttpRequest();
                request.SetUriFromString(AssemblyInitialize.Current.WsHostname + $"/v1/devices");
                var response = this.httpClient.GetAsync(request).Result;
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                var deviceList = JsonConvert.DeserializeObject<DeviceListApiModel>(response.Content);
                Assert.True(deviceList.Items.Count >= 1);
                Assert.True(deviceList.Items.Any(d => d.Id == deviceId));
            }
            finally
            {
                // clean it up
                this.DeleteDeviceIfExists(deviceId);
            }
        }

        [SkippableFact, Trait(Constants.Type, Constants.IntegrationTest)]
        public void GetDeviceListWithQueryTest()
        {
            Skip.IfNot(this.credentialsAvailable, "Skipping this test for Travis pull request as credentials are not available");

            var deviceId = "testDevice-getdeviceListWithReportedQuery";
            var device = this.CreateDeviceIfNotExists(deviceId);

            try
            {
                var newTagValue = $"mynewTag{DateTime.Now.Ticks}";

                // update newTag/desired property
                {
                    if (device.Tags.ContainsKey("newTag"))
                    {
                        device.Tags["newTag"] = newTagValue;
                    }
                    else
                    {
                        device.Tags.Add("newTag", newTagValue);
                    }

                    var newConfig = new NewConfig
                    {
                        TelemetryInterval = 10
                    };

                    // update desired value
                    var configValue = JToken.Parse(JsonConvert.SerializeObject(newConfig));
                    if (device.DesiredProperties.ContainsKey("config"))
                    {
                        device.DesiredProperties["config"] = configValue;
                    }
                    else
                    {
                        device.DesiredProperties.Add("config", configValue);
                    }

                    var newReport = new NewConfig
                    {
                        TelemetryInterval = 15
                    };

                    var request = new HttpRequest();
                    request.SetUriFromString(AssemblyInitialize.Current.WsHostname + $"/v1/devices/{deviceId}");
                    request.SetContent(device);

                    var response = this.httpClient.PutAsync(request).Result;
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                }

                // verify
                var queries = new List<string>();
                queries.Add(WebUtility.UrlEncode($"tags.newTag = '{newTagValue}'"));
                queries.Add(WebUtility.UrlEncode($"properties.desired.config.TelemetryInterval = 10"));
                queries.Add(WebUtility.UrlEncode($"properties.desired.config.TelemetryInterval > 9"));
                queries.Add(WebUtility.UrlEncode($"properties.desired.config.TelemetryInterval >= 10"));
                queries.Add(WebUtility.UrlEncode($"properties.desired.config.TelemetryInterval < 11"));
                queries.Add(WebUtility.UrlEncode($"properties.desired.config.TelemetryInterval <= 10"));
                queries.Add(WebUtility.UrlEncode($"tags.newTag = '{newTagValue}' and properties.desired.config.TelemetryInterval = 10"));
                queries.Add(WebUtility.UrlEncode($"properties.desired.config.TelemetryInterval = 10"));

                foreach (var query in queries)
                {
                    var request = new HttpRequest();
                    request.SetUriFromString(AssemblyInitialize.Current.WsHostname + $"/v1/devices?query={query}");
                    var response = this.httpClient.GetAsync(request).Result;
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                    var deviceList = JsonConvert.DeserializeObject<DeviceListApiModel>(response.Content);
                    Assert.True(deviceList.Items.Count >= 1, WebUtility.UrlDecode(query));
                    Assert.True(deviceList.Items.Any(d => d.Id == deviceId), WebUtility.UrlDecode(query));
                }

                queries.Clear();
                queries.Add(WebUtility.UrlEncode($"properties.desired.config.TelemetryInterval != 10"));
                queries.Add(WebUtility.UrlEncode($"properties.desired.config.TelemetryInterval < 9"));
                queries.Add(WebUtility.UrlEncode($"properties.desired.config.TelemetryInterval > 10 and properties.desired.config.TelemetryInterval = 10"));
                foreach (var query in queries)
                {
                    var request = new HttpRequest();
                    request.SetUriFromString(AssemblyInitialize.Current.WsHostname + $"/v1/devices?query={query}");
                    var response = this.httpClient.GetAsync(request).Result;
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                    var deviceList = JsonConvert.DeserializeObject<DeviceListApiModel>(response.Content);
                    Assert.True(!deviceList.Items.Any(d => d.Id == deviceId), WebUtility.UrlDecode(query));
                }
            }
            finally
            {
                // clean it up
                this.DeleteDeviceIfExists(deviceId);
            }
        }

        [SkippableFact, Trait(Constants.Type, Constants.IntegrationTest)]
        public async Task PostDeviceQueryTest()
        {
            Skip.IfNot(this.credentialsAvailable, "Skipping this test for Travis pull request as credentials are not available");

            var request = new HttpRequest();
            request.SetUriFromString(AssemblyInitialize.Current.WsHostname + $"/v1/devices/query");
            request.SetContent("tags.Floor =\"10F\"");
            var response = await this.httpClient.PostAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var deviceList = JsonConvert.DeserializeObject<DeviceListApiModel>(response.Content);
            Assert.NotNull(deviceList.Items);
        }

        [SkippableFact, Trait(Constants.Type, Constants.IntegrationTest)]
        public async Task GetAllDevicesByClausesTest()
        {
            Skip.IfNot(this.credentialsAvailable, "Skipping this test for Travis pull request as credentials are not available");

            var request = new HttpRequest();
            request.SetUriFromString(AssemblyInitialize.Current.WsHostname + $"/v1/devices?query=[]");
            var response = await this.httpClient.GetAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var deviceList = JsonConvert.DeserializeObject<DeviceListApiModel>(response.Content);
            Assert.NotNull(deviceList.Items);
        }

        [SkippableFact, Trait(Constants.Type, Constants.IntegrationTest)]
        public async Task GetDevicesByBadQueryTest()
        {
            Skip.IfNot(this.credentialsAvailable, "Skipping this test for Travis pull request as credentials are not available");

            var request = new HttpRequest();
            request.SetUriFromString(AssemblyInitialize.Current.WsHostname + $"/v1/devices?query=abc");
            var response = await this.httpClient.GetAsync(request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [SkippableFact, Trait(Constants.Type, Constants.IntegrationTest)]
        public async Task QueryDevicesByClausesTest()
        {
            Skip.IfNot(this.credentialsAvailable, "Skipping this test for Travis pull request as credentials are not available");

            var query = JsonConvert.SerializeObject(new object[]
            {
                new
                {
                    Key = "tags.Floor",
                    Operator = "EQ",
                    Value = "10F"
                },
                new
                {
                    Key = "properties.reported.Device.Location.Latitude",
                    Operator = "GE",
                    Value = "30"
                },
                new
                {
                    Key = "properties.desired.Config.TelemetryInterval",
                    Operator = "GE",
                    Value = 3
                }
            });

            var request = new HttpRequest();
            request.SetUriFromString(AssemblyInitialize.Current.WsHostname + $"/v1/devices?query={query}");
            var response = await this.httpClient.GetAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var deviceList = JsonConvert.DeserializeObject<DeviceListApiModel>(response.Content);
            Assert.NotNull(deviceList.Items);
        }

        private DeviceRegistryApiModel GetDevice(string deviceId)
        {
            var request = new HttpRequest();
            request.SetUriFromString(AssemblyInitialize.Current.WsHostname + $"/v1/devices/{deviceId}");
            var response = this.httpClient.GetAsync(request).Result;

            if (response.StatusCode != HttpStatusCode.OK)
            {
                return null;
            }

            return JsonConvert.DeserializeObject<DeviceRegistryApiModel>(response.Content);
        }

        private DeviceRegistryApiModel CreateDeviceIfNotExists(string deviceId)
        {
            var device = GetDevice(deviceId);
            if (device != null)
            {
                return device;
            }

            device = new DeviceRegistryApiModel()
            {
                Id = deviceId
            };

            var request = new HttpRequest();
            request.SetUriFromString(AssemblyInitialize.Current.WsHostname + $"/v1/devices/{deviceId}");
            request.SetContent(device);

            var response = this.httpClient.PutAsync(request).Result;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            return JsonConvert.DeserializeObject<DeviceRegistryApiModel>(response.Content);
        }

        private void DeleteDeviceIfExists(string deviceId)
        {
            var device = GetDevice(deviceId);
            if (device != null)
            {
                var request = new HttpRequest();
                request.SetUriFromString(AssemblyInitialize.Current.WsHostname + $"/v1/devices/{deviceId}");
                var response = this.httpClient.DeleteAsync(request).Result;

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        }
    }
}
