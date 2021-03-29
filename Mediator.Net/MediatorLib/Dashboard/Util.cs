// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Util;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Dashboard
{
    public sealed class ReqResult : IDisposable
    {
        public ReqResult(int statusCode, MemoryStream bytes, string? contentType = null) {
            StatusCode = statusCode;
            Bytes = bytes ?? throw new ArgumentNullException("bytes");
            ContentType = contentType ?? "application/json";
        }

        public string ContentType { get; private set; } = "application/json";

        public MemoryStream Bytes { get; private set; }

        public int StatusCode { get; private set; }

        public string AsString() => Encoding.UTF8.GetString(Bytes.ToArray());

        public void Dispose() {
            Bytes.Dispose();
        }

        public static ReqResult Bad(string errMsg) {
            string js = "{ \"error\": " + StdJson.ValueToString(errMsg) + "}";
            byte[] bytes = Encoding.UTF8.GetBytes(js);
            return new ReqResult(400, new MemoryStream(bytes));
        }

        public static ReqResult OK() {
            return new ReqResult(200, new MemoryStream(0));
        }

        public static ReqResult OK(object obj, bool ignoreShouldSerializeMembers = false, string? contentType = null) {

            if (typeof(Task).IsAssignableFrom(obj.GetType())) {
                throw new Exception("ReqResult.OK: obj may not be a Task!");
            }

            var res = MemoryManager.GetMemoryStream("ReqResult.OK");
            try {
                StdJson.ObjectToStream(obj, res, indented: true, ignoreShouldSerializeMembers: ignoreShouldSerializeMembers);
                res.Seek(0, SeekOrigin.Begin);
            }
            catch (Exception) {
                res.Dispose();
                throw;
            }
            return new ReqResult(200, res, contentType: contentType);
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class Identify : Attribute
    {
        /// <summary>
        /// The view type id
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// The id of the view bundle
        /// </summary>
        public string Bundle { get; set; }

        /// <summary>
        /// The local URL path for the view, e.g. "index.html"
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// The optional name of the icon to use in the dashboard, e.g. "bubble_chart", "mdi-tilde"
        /// </summary>
        public string? Icon { get; set; }

        public Identify(string id, string bundle, string path, string? icon = null) {
            ID = id;
            Bundle = bundle;
            Path = path;
            Icon = icon;
        }
    }
}
