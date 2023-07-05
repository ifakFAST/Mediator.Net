// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Ifak.Fast.Mediator.Publish.MQTT;

public class LargePayloadWriter
{
    public static List<ReadOnlyMemory<byte>> GetPayloadAndBuckets(string payload, int maxLenPerBucket) {

        byte[] data = Encoding.UTF8.GetBytes(payload);

        int bucketCount = (data.Length / maxLenPerBucket) + (data.Length % maxLenPerBucket == 0 ? 0 : 1);

        string hash = GetHash(data);
        string bytesLen = data.Length.ToString();
        string buckets = bucketCount.ToString();

        string info = $"{hash};{bytesLen};{buckets}";
        byte[] infoBytes = Encoding.UTF8.GetBytes(info);

        var res = new List<ReadOnlyMemory<byte>>(bucketCount + 1);
        res.Add(infoBytes);

        var mem = new ReadOnlyMemory<byte>(data);

        var offset = 0;

        for (int i = 0; i < bucketCount; ++i) {
            int len = Math.Min(maxLenPerBucket, mem.Length - offset);
            res.Add(mem.Slice(offset, len));
            offset += len;
        }

        return res;
    }

    private static string GetHash(byte[] data) {
        using var sha1 = System.Security.Cryptography.SHA1.Create();
        return string.Concat(sha1.ComputeHash(data).Select(x => x.ToString("X2")));
    }
}
