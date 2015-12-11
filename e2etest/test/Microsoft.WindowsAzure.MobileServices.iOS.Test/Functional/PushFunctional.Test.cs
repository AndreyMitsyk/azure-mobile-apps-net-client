// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System.Linq;

using Microsoft.WindowsAzure.MobileServices.TestFramework;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using Foundation;
using System.Globalization;
using Newtonsoft.Json;

namespace Microsoft.WindowsAzure.MobileServices.Test
{
    [Tag("push")]
    public class PushFunctional : FunctionalTestBase
    {
        readonly IPushTestUtility pushTestUtility;

        public PushFunctional()
        {
            this.pushTestUtility = TestPlatform.Instance.PushTestUtility;
        }

        [AsyncTestMethod]
        public async Task InitialDeleteRegistrationsAsync()
        {
            NSData channelUri = NSDataFromDescription(this.pushTestUtility.GetPushHandle());
            Dictionary<string, string> channelUriParam = new Dictionary<string, string>()
            {
                {"channelUri", TrimDeviceToken(channelUri.Description)}
            };
            await this.GetClient().InvokeApiAsync("deleteRegistrationsForChannel", HttpMethod.Delete, channelUriParam);
        }

        [AsyncTestMethod]
        public async Task RegisterAsync()
        {
            NSData channelUri = NSDataFromDescription(this.pushTestUtility.GetPushHandle());
            var push = this.GetClient().GetPush();
            await push.RegisterAsync(channelUri);
            Dictionary<string, string> parameters = new Dictionary<string, string>()
            {
                {"channelUri", TrimDeviceToken(channelUri.Description)}
            };
            await VerifyRegistration(parameters, push);
        }

        [AsyncTestMethod]
        public async Task LoginRegisterAsync()
        {
            MobileServiceUser user = await GetDummyUser();
            this.GetClient().CurrentUser = user;
            NSData channelUri = NSDataFromDescription(this.pushTestUtility.GetPushHandle());
            Dictionary<string, string> parameters = new Dictionary<string, string>()
            {
                {"channelUri", TrimDeviceToken(channelUri.Description)}
            };
            var push = this.GetClient().GetPush();
            await push.RegisterAsync(channelUri);
            await VerifyRegistration(parameters, push);
        }

        [AsyncTestMethod]
        public async Task UnregisterAsync()
        {
            var channelUri = this.pushTestUtility.GetPushHandle();
            var push = this.GetClient().GetPush();
            await push.UnregisterAsync();
            try
            {
                await this.GetClient().InvokeApiAsync("verifyUnregisterInstallationResult", HttpMethod.Get, null);
            }
            catch (MobileServiceInvalidOperationException)
            {
                throw;
            }
        }

        [AsyncTestMethod]
        public async Task RegisterAsyncTemplatesAndOverride()
        {
            NSData channelUri = NSDataFromDescription(this.pushTestUtility.GetPushHandle());
            JObject templates = GetTemplates("foo");
            JObject expectedTemplates = GetTemplates(null);
            var push = this.GetClient().GetPush();
            try
            {
                await push.RegisterAsync(channelUri);
                Dictionary<string, string> parameters = new Dictionary<string, string>()
                {
                    {"channelUri", TrimDeviceToken(channelUri.Description)},
                };
                await this.GetClient().InvokeApiAsync("verifyRegisterInstallationResult", HttpMethod.Get, parameters);

                await push.RegisterAsync(channelUri, templates);
                parameters = new Dictionary<string, string>()
                {
                    {"channelUri", TrimDeviceToken(channelUri.Description)},
                    {"templates", JsonConvert.SerializeObject(expectedTemplates)}
                };
                await this.GetClient().InvokeApiAsync("verifyRegisterInstallationResult", HttpMethod.Get, parameters);

                await push.RegisterAsync(channelUri);
                parameters = new Dictionary<string, string>()
                {
                    {"channelUri", TrimDeviceToken(channelUri.Description)},
                };
                await this.GetClient().InvokeApiAsync("verifyRegisterInstallationResult", HttpMethod.Get, parameters);
            }
            catch (MobileServiceInvalidOperationException)
            {
                throw;
            }
            finally
            {
                push.UnregisterAsync().Wait();
                this.GetClient().CurrentUser = null;
            }
        }

        private async Task VerifyRegistration(Dictionary<string, string> parameters, Push push)
        {
            try
            {
                //Verifies templates are removed from the installation registration
                await this.GetClient().InvokeApiAsync("verifyRegisterInstallationResult", HttpMethod.Get, parameters);
            }
            catch (MobileServiceInvalidOperationException)
            {
                throw;
            }
            finally
            {
                push.UnregisterAsync().Wait();
                this.GetClient().CurrentUser = null;
            }
        }

        private static JObject GetTemplates(string tag)
        {
            var toastTemplate = "{\"aps\": {\"alert\":\"boo!\"}, \"extraprop\":\"($message)\"}";
            JObject templateBody = new JObject();
            templateBody["body"] = toastTemplate;

            if (tag != null)
            {
                JArray tags = new JArray();
                tags.Add("foo");
                templateBody["tags"] = tags;
            }

            JObject templates = new JObject();
            templates["testApnsTemplate"] = templateBody;
            return templates;
        }

        private async Task<MobileServiceUser> GetDummyUser()
        {
            var dummyUser = await this.GetClient().InvokeApiAsync("JwtTokenGenerator", HttpMethod.Get, null);

            MobileServiceUser user = new MobileServiceUser((string)dummyUser["userId"])
            {
                MobileServiceAuthenticationToken = (string)dummyUser["autheticationToken"]
            };
            return user;
        }

        internal static string TrimDeviceToken(string deviceToken)
        {
            if (deviceToken == null)
            {
                throw new ArgumentNullException("deviceToken");
            }

            return deviceToken.Trim('<', '>').Replace(" ", string.Empty).ToUpperInvariant();
        }

        internal static NSData NSDataFromDescription(string hexString)
        {
            hexString = hexString.Trim('<', '>').Replace(" ", string.Empty);
            NSMutableData data = new NSMutableData();
            byte[] hexAsBytes = new byte[hexString.Length / 2];
            for (int index = 0; index < hexAsBytes.Length; index++)
            {
                string byteValue = hexString.Substring(index * 2, 2);
                hexAsBytes[index] = byte.Parse(byteValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            data.AppendBytes(hexAsBytes);
            return data;
        }
    }
}