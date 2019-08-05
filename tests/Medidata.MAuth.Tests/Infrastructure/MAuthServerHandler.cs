﻿using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Medidata.MAuth.Core;
using Newtonsoft.Json;

namespace Medidata.MAuth.Tests.Infrastructure
{
    internal class MAuthServerHandler : HttpMessageHandler
    {
        private static readonly Guid clientUuid = new Guid("192cce84-8466-490e-b03e-074f82da3ee2");
        private int currentNumberOfAttempts = 0;

        public int SucceedAfterThisManyAttempts { get; set; } = 1;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            currentNumberOfAttempts += 1;
            var version = request.GetAuthHeaderValue().GetVersionFromAuthenticationHeader();
            var mAuthCore = MAuthCoreFactory.Instantiate(version);

            if (currentNumberOfAttempts < SucceedAfterThisManyAttempts)
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

            var authInfo = mAuthCore.GetAuthenticationInfo(request);

            if (!mAuthCore.Verify(authInfo.Payload, 
                await mAuthCore.GetSignature(request, authInfo),
                TestExtensions.ServerPublicKey
            ))
                return new HttpResponseMessage(HttpStatusCode.Unauthorized) { RequestMessage = request };

            var tokenRequestPath = mAuthCore.GetMAuthTokenRequestPath();

            if (!request.RequestUri.AbsolutePath.Equals(
                $"{tokenRequestPath}{clientUuid.ToHyphenString()}.json",
                StringComparison.OrdinalIgnoreCase))
                return new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent(
                    JsonConvert.SerializeObject(new
                    {
                        security_token = new ApplicationInfo()
                        {
                            Uuid = clientUuid,
                            Name = "Medidata.MAuth.Tests",
                            CreationDate = new DateTimeOffset(2016, 8, 1, 0, 0, 0, TimeSpan.Zero),
                            PublicKey = TestExtensions.ClientPublicKey
                        }
                    })
                )
            };
        }
    }
}
