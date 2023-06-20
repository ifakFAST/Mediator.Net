// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;

namespace Ifak.Fast.Mediator.Publish
{
    public class LargePayloadReader
    {
        private readonly List<byte[]?> listBuckets;

        private bool hasInfo = false;
        private string hash = "";
        private int bytesLen = 0;
        private int bucketCount = 0;

        public string ContentHash => hash;

        public LargePayloadReader(int numberOfBuckets) {
            listBuckets = new List<byte[]?>(numberOfBuckets);
            for (int i = 0; i < numberOfBuckets; ++i) {
                listBuckets.Add(null);
            }
        }

        public void SetInfo(ArraySegment<byte> content) {

            string info = Encoding.UTF8.GetString(content);

            string[] arr = info.Split(';');
            if (arr.Length != 3) throw new Exception("Invalid format of info payload");
            string hash = arr[0];
            string bytesLen = arr[1];
            string buckets = arr[2];

            this.hasInfo = true;
            this.hash = hash;
            this.bytesLen = int.Parse(bytesLen);
            this.bucketCount = int.Parse(buckets);
        }

        public void SetBucket(int idx, byte[] content) {
            listBuckets[idx] = content;
        }

        public string? Content() {
            if (!this.hasInfo) return null;
            for (int i = 0; i < this.bucketCount; ++i) {
                if (this.listBuckets[i] == null) {
                    return null;
                }
            }
            int sum = 0;
            for (int i = 0; i < this.bucketCount; ++i) {
                sum += this.listBuckets[i]!.Length;
            }
            if (sum != this.bytesLen) return null;

            var byteStream = new MemoryStream(capacity: this.bytesLen);
            for (int i = 0; i < this.bucketCount; ++i) {
                byteStream.Write(this.listBuckets[i]);
            }
            byteStream.Position = 0;

            string hash = GetHash(byteStream);

            if (hash != this.hash) return null;

            byteStream.Position = 0;
            return Encoding.UTF8.GetString(byteStream.ToArray());
        }

        private static string GetHash(Stream stream) {
            using var sha1 = System.Security.Cryptography.SHA1.Create();
            return string.Concat(sha1.ComputeHash(stream).Select(x => x.ToString("X2")));
        }
    }
}
