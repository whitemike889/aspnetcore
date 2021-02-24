// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Http;
using Microsoft.AspNetCore.Connections;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http3
{
    internal interface IHttp3Stream
    {
        long StreamId { get; }

        long StartExpirationTicks { get; set; }

        bool HasStarted { get; }

        bool IsRequestStream { get; }

        void Abort(ConnectionAbortedException abortReason, Http3ErrorCode errorCode);
    }
}
