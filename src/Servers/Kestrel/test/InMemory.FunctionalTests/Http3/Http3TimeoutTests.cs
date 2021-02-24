// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Testing;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Tests
{
    public class Http3TimeoutTests : Http3TestBase
    {
        [Fact]
        public async Task HEADERS_IncompleteFrameReceivedWithinRequestHeadersTimeout_StreamError()
        {
            var now = _serviceContext.MockSystemClock.UtcNow;
            var limits = _serviceContext.ServerOptions.Limits;

            var requestStream = await InitializeConnectionAndStreamsAsync(_noopApplication).DefaultTimeout();

            var controlStream = await GetInboundControlStream().DefaultTimeout();
            await controlStream.ExpectSettingsAsync().DefaultTimeout();

            await AssertIsTrueRetryAsync(
                () => Connection._startingStreams.Count == 2,
                "Wait until streams have been created.").DefaultTimeout();

            await requestStream.SendHeadersPartialAsync().DefaultTimeout();

            TriggerTick(now);
            TriggerTick(now + limits.RequestHeadersTimeout);

            // The control stream was removed because it has started.
            Assert.Single(Connection._startingStreams);

            TriggerTick(now + limits.RequestHeadersTimeout + TimeSpan.FromTicks(1));

            await requestStream.WaitForStreamErrorAsync(Http3ErrorCode.RequestRejected, CoreStrings.BadRequest_RequestHeadersTimeout);
        }

        [Fact]
        public async Task HEADERS_HeaderFrameReceivedWithinRequestHeadersTimeout_Success()
        {
            var now = _serviceContext.MockSystemClock.UtcNow;
            var limits = _serviceContext.ServerOptions.Limits;
            var headers = new[]
            {
                new KeyValuePair<string, string>(HeaderNames.Method, "Custom"),
                new KeyValuePair<string, string>(HeaderNames.Path, "/"),
                new KeyValuePair<string, string>(HeaderNames.Scheme, "http"),
                new KeyValuePair<string, string>(HeaderNames.Authority, "localhost:80"),
            };

            var requestStream = await InitializeConnectionAndStreamsAsync(_noopApplication).DefaultTimeout();

            var controlStream = await GetInboundControlStream().DefaultTimeout();
            await controlStream.ExpectSettingsAsync().DefaultTimeout();

            await AssertIsTrueRetryAsync(
                () => Connection._startingStreams.Count == 2,
                "Wait until streams have been created.").DefaultTimeout();

            TriggerTick(now);
            TriggerTick(now + limits.RequestHeadersTimeout);

            // The control stream was removed because it has started.
            Assert.Single(Connection._startingStreams);
            Connection._startingStreams.TryPeek(out var serverRequestStream);

            await requestStream.SendHeadersAsync(headers).DefaultTimeout();

            await AssertIsTrueRetryAsync(
                () => serverRequestStream.HasStarted,
                "Request stream has read headers.").DefaultTimeout();

            TriggerTick(now + limits.RequestHeadersTimeout + TimeSpan.FromTicks(1));

            Assert.Empty(Connection._startingStreams);
        }

        [Fact]
        public async Task ControlStream_HeaderNotReceivedWithinRequestHeadersTimeout_StreamError()
        {
            var now = _serviceContext.MockSystemClock.UtcNow;
            var limits = _serviceContext.ServerOptions.Limits;
            var headers = new[]
            {
                new KeyValuePair<string, string>(HeaderNames.Method, "Custom"),
                new KeyValuePair<string, string>(HeaderNames.Path, "/"),
                new KeyValuePair<string, string>(HeaderNames.Scheme, "http"),
                new KeyValuePair<string, string>(HeaderNames.Authority, "localhost:80"),
            };

            await InitializeConnectionAsync(_noopApplication).DefaultTimeout();

            var controlStream = await GetInboundControlStream().DefaultTimeout();
            await controlStream.ExpectSettingsAsync().DefaultTimeout();

            var outboundControlStream = await CreateControlStream(id: null);

            await AssertIsTrueRetryAsync(
                () => Connection._startingStreams.Count == 1,
                "Wait until streams have been created.").DefaultTimeout();

            TriggerTick(now);
            TriggerTick(now + limits.RequestHeadersTimeout);

            Assert.Single(Connection._startingStreams);

            TriggerTick(now + limits.RequestHeadersTimeout + TimeSpan.FromTicks(1));

            await outboundControlStream.WaitForStreamErrorAsync(Http3ErrorCode.StreamCreationError, CoreStrings.Http3ControlStreamHeaderTimeout);
        }

        [Fact]
        public async Task ControlStream_HeaderReceivedWithinRequestHeadersTimeout_StreamError()
        {
            var now = _serviceContext.MockSystemClock.UtcNow;
            var limits = _serviceContext.ServerOptions.Limits;
            var headers = new[]
            {
                new KeyValuePair<string, string>(HeaderNames.Method, "Custom"),
                new KeyValuePair<string, string>(HeaderNames.Path, "/"),
                new KeyValuePair<string, string>(HeaderNames.Scheme, "http"),
                new KeyValuePair<string, string>(HeaderNames.Authority, "localhost:80"),
            };

            await InitializeConnectionAsync(_noopApplication).DefaultTimeout();

            var controlStream = await GetInboundControlStream().DefaultTimeout();
            await controlStream.ExpectSettingsAsync().DefaultTimeout();

            var outboundControlStream = await CreateControlStream(id: null);

            await AssertIsTrueRetryAsync(
                () => Connection._startingStreams.Count == 1,
                "Wait until streams have been created.").DefaultTimeout();

            TriggerTick(now);
            TriggerTick(now + limits.RequestHeadersTimeout);

            Assert.Single(Connection._startingStreams);
            Connection._startingStreams.TryPeek(out var serverControlStream);

            await outboundControlStream.WriteStreamIdAsync(id: 0);

            await AssertIsTrueRetryAsync(
                () => serverControlStream.HasStarted,
                "Control stream has read header.").DefaultTimeout();

            TriggerTick(now + limits.RequestHeadersTimeout + TimeSpan.FromTicks(1));

            Assert.Empty(Connection._startingStreams);
        }

        private static async Task AssertIsTrueRetryAsync(Func<bool> assert, string message)
        {
            const int Retries = 10;

            for (var i = 0; i < Retries; i++)
            {
                if (i > 0)
                {
                    await Task.Delay((i + 1) * 10);
                }

                if (assert())
                {
                    return;
                }
            }

            throw new Exception($"Assert failed after {Retries} retries: {message}");
        }
    }
}
