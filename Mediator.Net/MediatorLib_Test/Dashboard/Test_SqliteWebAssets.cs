#nullable enable

using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Xunit;
using DashboardModule = Ifak.Fast.Mediator.Dashboard.Module;

namespace MediatorLib_Test.Dashboard;

public sealed class Test_SqliteWebAssets
{
    private const long Id = 2026052808300101;
    private static readonly byte[] FileBytes = Encoding.UTF8.GetBytes("II\x2a\0SQLite web asset test payload");
    private static readonly DateTimeOffset Modified = new(2026, 5, 28, 8, 30, 12, 345, TimeSpan.Zero);

    [Fact]
    public async Task BrotliResponseUsesImporterMetadataAndStoredBytes() {
        using var fixture = new Fixture();
        byte[] stored = Compress(FileBytes);
        fixture.Insert(stored);
        var context = await fixture.Get(encoding: "gzip, br");
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal(stored, Body(context));
        Assert.Equal("image/tiff", context.Response.ContentType);
        Assert.Equal("br", context.Response.Headers.ContentEncoding.ToString());
        Assert.Equal(stored.Length, context.Response.ContentLength);
        Assert.Equal("Thu, 28 May 2026 08:30:12 GMT", context.Response.Headers.LastModified.ToString());
        Assert.Equal("Accept-Encoding", context.Response.Headers.Vary.ToString());
        Assert.Equal("public, max-age=172800", context.Response.Headers.CacheControl.ToString());
        Assert.Equal("*", context.Response.Headers.AccessControlAllowOrigin.ToString());
        Assert.Equal("*", context.Response.Headers.AccessControlAllowMethods.ToString());
        Assert.Equal("*", context.Response.Headers.AccessControlAllowHeaders.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("gzip")]
    [InlineData("br;q=0")]
    [InlineData("br;q=0, *;q=1")]
    [InlineData("br;q=0.2, identity;q=1")]
    [InlineData("xbr")]
    public async Task ClientsWithoutBrotliReceiveOriginalFile(string encoding) {
        using var fixture = new Fixture();
        fixture.Insert(Compress(FileBytes));
        var context = await fixture.Get(encoding: encoding);
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal(FileBytes, Body(context));
        Assert.False(context.Response.Headers.ContainsKey("Content-Encoding"));
        Assert.Equal(FileBytes.Length, context.Response.ContentLength);
    }

    [Theory]
    [InlineData("BR")]
    [InlineData("*;q=0.5")]
    [InlineData("br;q=0.5, identity;q=0")]
    public async Task BrotliNegotiationSupportsCasingWildcardsAndQuality(string encoding) {
        using var fixture = new Fixture();
        byte[] compressed = Compress(FileBytes);
        fixture.Insert(compressed);
        var context = await fixture.Get(encoding: encoding);
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal(compressed, Body(context));
        Assert.Equal("br", context.Response.Headers.ContentEncoding.ToString());
    }

    [Theory]
    [InlineData("br;q=0, identity;q=0")]
    [InlineData("*;q=0")]
    public async Task NoAcceptableRepresentationReturns406(string encoding) {
        using var fixture = new Fixture();
        fixture.Insert(Compress(FileBytes));
        var context = await fixture.Get(encoding: encoding);
        Assert.Equal(406, context.Response.StatusCode);
        Assert.Empty(Body(context));
    }

    [Theory]
    [InlineData("", "tif", "image/tiff")]
    [InlineData("identity", "json", "application/json")]
    [InlineData("identity", "unrecognizedextension", "application/octet-stream")]
    public async Task UncompressedEntriesUseExtensionMimeType(string compression, string ext, string mime) {
        using var fixture = new Fixture();
        fixture.Insert(FileBytes, compression, ext);
        var context = await fixture.Get(encoding: "br");
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal(FileBytes, Body(context));
        Assert.Equal(mime, context.Response.ContentType);
        Assert.False(context.Response.Headers.ContainsKey("Content-Encoding"));
        var rejected = await fixture.Get(encoding: "br, identity;q=0");
        Assert.Equal(406, rejected.Response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?id=")]
    [InlineData("?id=1&id=2")]
    [InlineData("?id=hello")]
    [InlineData("?id=9223372036854775808")]
    [InlineData("?id=1%20OR%201=1")]
    public async Task InvalidIdReturns400WithoutServingDatabase(string query) {
        using var fixture = new Fixture();
        var context = await fixture.Get(query: query);
        Assert.Equal(400, context.Response.StatusCode);
        Assert.Empty(Body(context));
        Assert.False(fixture.NextCalled);
    }

    [Fact]
    public async Task MissingDatabaseOrRowReturns404WithoutCreatingDatabase() {
        using var fixture = new Fixture();
        Assert.Equal(404, (await fixture.Get()).Response.StatusCode);
        Assert.Equal(404, (await fixture.Get(path: "/WebAssets/missing.sqlite")).Response.StatusCode);
        Assert.False(File.Exists(Path.Combine(fixture.Root, "missing.sqlite")));
    }

    [Theory]
    [InlineData("/WebAssets/../outside.sqlite")]
    [InlineData("/WebAssets/..\\outside.sqlite")]
    [InlineData("/WebAssets/../WebAssetsOther/outside.sqlite")]
    public async Task PathsOutsideRootAreRejected(string path) {
        using var fixture = new Fixture();
        fixture.Insert(Compress(FileBytes));
        File.Copy(fixture.DatabasePath, Path.Combine(fixture.Folder, "outside.sqlite"));
        string otherRoot = Path.Combine(fixture.Folder, "WebAssetsOther");
        Directory.CreateDirectory(otherRoot);
        File.Copy(fixture.DatabasePath, Path.Combine(otherRoot, "outside.sqlite"));
        var context = await fixture.Get(path: path);
        Assert.Equal(404, context.Response.StatusCode);
        Assert.False(fixture.NextCalled);
        Assert.Empty(Body(context));
    }

    [Fact]
    public async Task AccessControlRunsBeforeSqliteHandling() {
        using var fixture = new Fixture();
        fixture.Insert(Compress(FileBytes));
        var context = await fixture.Get(authorized: false, query: "?id=invalid");
        Assert.Equal(401, context.Response.StatusCode);
        Assert.Empty(Body(context));
        Assert.False(fixture.NextCalled);
        Assert.Equal(401, (await fixture.Get(authorized: false, path: "/WebAssets/file.tif")).Response.StatusCode);
    }

    [Theory]
    [InlineData("/WebAssets/file.tiff", "GET")]
    [InlineData("/WebAssets", "GET")]
    [InlineData("/ctx/files/file.tiff", "GET")]
    [InlineData("/WebAssetsOther/2605.sqlite", "GET")]
    [InlineData("/WebAssets/2605.sqlite", "POST")]
    public async Task UnrelatedRequestsPassThrough(string path, string method) {
        using var fixture = new Fixture();
        await fixture.Get(path: path, method: method);
        Assert.True(fixture.NextCalled);
    }

    [Fact]
    public async Task ConditionalRequestsUseVariantSpecificContentEtags() {
        using var fixture = new Fixture();
        fixture.Insert(Compress(FileBytes));
        var br = await fixture.Get(encoding: "br");
        string etag = br.Response.Headers.ETag.ToString();
        var identity = await fixture.Get();
        Assert.NotEqual(etag, identity.Response.Headers.ETag.ToString());
        foreach (string value in new[] { etag, "W/" + etag, "\"other\", " + etag, "*" }) {
            var cached = await fixture.Get(encoding: "br", etag: value);
            Assert.Equal(304, cached.Response.StatusCode);
            Assert.Empty(Body(cached));
            Assert.Null(cached.Response.ContentLength);
            Assert.Equal(etag, cached.Response.Headers.ETag.ToString());
        }
        Assert.Equal(200, (await fixture.Get(etag: etag)).Response.StatusCode);
        string modified = br.Response.Headers.LastModified.ToString();
        Assert.Equal(304, (await fixture.Get(since: modified)).Response.StatusCode);
        Assert.Equal(200, (await fixture.Get(since: Modified.AddSeconds(-1).ToString("r"))).Response.StatusCode);
        Assert.Equal(200, (await fixture.Get(etag: "\"different\"", since: modified)).Response.StatusCode);
        Assert.Equal(200, (await fixture.Get(since: "invalid")).Response.StatusCode);
        // An upsert can preserve last_modified and byte length; content must still change the ETag.
        fixture.Insert(Compress(Encoding.UTF8.GetBytes("Different content")));
        var updated = await fixture.Get(encoding: "br", etag: etag);
        Assert.Equal(200, updated.Response.StatusCode);
        Assert.NotEqual(etag, updated.Response.Headers.ETag.ToString());
    }

    [Theory]
    [InlineData("DROP TABLE files;")]
    [InlineData("ALTER TABLE files RENAME COLUMN data TO invalid_column;")]
    public async Task InvalidSchemaReturns500WithoutInternalDetails(string sql) {
        using var fixture = new Fixture();
        fixture.Execute(sql);
        var context = await fixture.Get();
        Assert.Equal(500, context.Response.StatusCode);
        Assert.Empty(Body(context));
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
    }

    [Theory]
    [InlineData("gzip")]
    [InlineData("br")]
    public async Task UnsupportedCompressionOrCorruptBrotliReturns500(string compression) {
        using var fixture = new Fixture();
        fixture.Insert(new byte[] { 255, 255, 255, 255 }, compression);
        var context = await fixture.Get();
        Assert.Equal(500, context.Response.StatusCode);
        Assert.Empty(Body(context));
        Assert.False(context.Response.Headers.ContainsKey("Content-Encoding"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("br")]
    public async Task TruncatedBrotliIsRejectedForEveryRepresentation(string encoding) {
        using var fixture = new Fixture();
        byte[] compressed = Compress(FileBytes);
        fixture.Insert(compressed[..^1]);
        var context = await fixture.Get(encoding: encoding);
        Assert.Equal(500, context.Response.StatusCode);
        Assert.Empty(Body(context));
    }

    [Theory]
    [InlineData("")]
    [InlineData("br")]
    public async Task LargeBrotliFilesAreDecodedAcrossMultipleBuffers(string encoding) {
        using var fixture = new Fixture();
        byte[] data = new byte[300000];
        new Random(1234).NextBytes(data);
        byte[] compressed = Compress(data);
        fixture.Insert(compressed);
        var context = await fixture.Get(encoding: encoding);
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal(encoding == "br" ? compressed : data, Body(context));
    }

    [Fact]
    public async Task ExclusiveDatabaseLockReturns503() {
        using var fixture = new Fixture();
        fixture.Insert(Compress(FileBytes));
        fixture.Execute("PRAGMA journal_mode=DELETE;");
        using var writer = fixture.Open();
        using var command = writer.CreateCommand();
        command.CommandText = "BEGIN EXCLUSIVE;";
        command.ExecuteNonQuery();
        var context = await fixture.Get();
        Assert.Equal(503, context.Response.StatusCode);
        Assert.Empty(Body(context));
    }

    [Fact]
    public async Task WalReaderSeesCommittedRowsWhileImporterWrites() {
        using var fixture = new Fixture();
        fixture.Insert(FileBytes, "identity");
        using var writer = fixture.Open();
        using var transaction = writer.BeginTransaction();
        using var command = writer.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE files SET data=$data WHERE id=$id;";
        byte[] updated = Encoding.UTF8.GetBytes("new forecast");
        command.Parameters.Add("$data", SqliteType.Blob).Value = updated;
        command.Parameters.AddWithValue("$id", Id);
        command.ExecuteNonQuery();
        var beforeCommit = await fixture.Get();
        Assert.Equal(200, beforeCommit.Response.StatusCode);
        Assert.Equal(FileBytes, Body(beforeCommit));
        transaction.Commit();
        var afterCommit = await fixture.Get();
        Assert.Equal(200, afterCommit.Response.StatusCode);
        Assert.Equal(updated, Body(afterCommit));
    }

    [Theory]
    [InlineData("gzip", true)]
    [InlineData("br, gzip", true)]
    [InlineData("GZIP", true)]
    [InlineData("*;q=0.5", true)]
    [InlineData("gzip;q=0.5, identity;q=0", true)]
    [InlineData("", false)]
    [InlineData("br", false)]
    [InlineData("gz", false)]
    [InlineData("gzip;q=0", false)]
    [InlineData("gzip;q=0, *;q=1", false)]
    [InlineData("gzip;q=0.2, identity;q=1", false)]
    public async Task GzipNegotiationUsesHttpEncodingAndOriginalBytes(string encoding, bool compressed) {
        using var fixture = new Fixture();
        byte[] data = CompressGzip(FileBytes);
        fixture.Insert(data, "gz");
        var context = await fixture.Get(encoding: encoding);
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal(compressed ? data : FileBytes, Body(context));
        Assert.Equal(compressed ? "gzip" : "", context.Response.Headers.ContentEncoding.ToString());
        Assert.Equal("image/tiff", context.Response.ContentType);
        Assert.Equal("Thu, 28 May 2026 08:30:12 GMT", context.Response.Headers.LastModified.ToString());
        Assert.Equal((compressed ? data : FileBytes).Length, context.Response.ContentLength);
        Assert.Equal("Accept-Encoding", context.Response.Headers.Vary.ToString());
    }

    [Theory]
    [InlineData("gzip;q=0, identity;q=0")]
    [InlineData("br, identity;q=0")]
    [InlineData("*;q=0")]
    public async Task GzipWithNoAcceptableRepresentationReturns406(string encoding) {
        using var fixture = new Fixture();
        fixture.Insert(CompressGzip(FileBytes), "gz");
        var context = await fixture.Get(encoding: encoding);
        Assert.Equal(406, context.Response.StatusCode);
        Assert.Empty(Body(context));
    }

    [Fact]
    public async Task GzipCacheValidationDistinguishesRepresentations() {
        using var fixture = new Fixture();
        fixture.Insert(CompressGzip(FileBytes), "gz");
        var compressed = await fixture.Get(encoding: "gzip");
        var identity = await fixture.Get();
        string etag = compressed.Response.Headers.ETag.ToString();
        Assert.NotEqual(etag, identity.Response.Headers.ETag.ToString());
        var cached = await fixture.Get(encoding: "gzip", etag: etag);
        Assert.Equal(304, cached.Response.StatusCode);
        Assert.Equal("gzip", cached.Response.Headers.ContentEncoding.ToString());
        Assert.Empty(Body(cached));
        Assert.Equal(200, (await fixture.Get(etag: etag)).Response.StatusCode);
        fixture.Insert(Compress(FileBytes), "br");
        var brotli = await fixture.Get(encoding: "br");
        Assert.NotEqual(etag, brotli.Response.Headers.ETag.ToString());
        Assert.Equal(identity.Response.Headers.ETag.ToString(), (await fixture.Get()).Response.Headers.ETag.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("gzip")]
    public async Task CorruptGzipIsRejectedForEveryRepresentation(string encoding) {
        using var fixture = new Fixture();
        byte[] corrupt = CompressGzip(FileBytes);
        corrupt[^8] ^= 0xff; // Break the CRC32 in the GZip trailer.
        fixture.Insert(corrupt, "gz");
        var context = await fixture.Get(encoding: encoding);
        Assert.Equal(500, context.Response.StatusCode);
        Assert.Empty(Body(context));
        Assert.False(context.Response.Headers.ContainsKey("Content-Encoding"));
    }

    private static byte[] CompressGzip(byte[] data) {
        using var result = new MemoryStream();
        using (var stream = new GZipStream(result, CompressionLevel.SmallestSize, leaveOpen: true)) {
            stream.Write(data);
        }
        return result.ToArray();
    }


    private static byte[] Body(HttpContext context) => ((MemoryStream)context.Response.Body).ToArray();

    private static byte[] Compress(byte[] data) {
        using var result = new MemoryStream();
        using (var stream = new BrotliStream(result, CompressionLevel.SmallestSize, leaveOpen: true)) {
            stream.Write(data);
        }
        return result.ToArray();
    }

    private sealed class Fixture : IDisposable {
        public string Folder { get; } = Path.Combine(Path.GetTempPath(), "MediatorNet-SqliteWebAssets-" + Guid.NewGuid().ToString("N"));
        public string Root => Path.Combine(Folder, "WebAssets");
        public string DatabasePath => Path.Combine(Root, "2605.sqlite");
        public bool NextCalled { get; private set; }
        private readonly DashboardModule module = new();
        private static readonly MethodInfo Handler = typeof(DashboardModule).GetMethod(
            "HandleWebAssetsRequest", BindingFlags.Instance | BindingFlags.NonPublic)!;

        public Fixture() {
            Directory.CreateDirectory(Root);
            typeof(DashboardModule).GetField("getRequestAccessToken", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(module, "test-access-token");
            Execute("PRAGMA page_size=8192; PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; " +
                "CREATE TABLE files (id INTEGER PRIMARY KEY, ext TEXT NOT NULL, " +
                "last_modified INTEGER NOT NULL, compression TEXT NOT NULL DEFAULT 'br', data BLOB NOT NULL);");
        }

        public SqliteConnection Open() {
            var connection = new SqliteConnection(new SqliteConnectionStringBuilder {
                DataSource = DatabasePath, Pooling = false, DefaultTimeout = 1,
            }.ToString());
            connection.Open();
            return connection;
        }

        public void Execute(string sql) {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        public void Insert(byte[] data, string compression = "br", string ext = "tiff") {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT OR REPLACE INTO files (id, ext, last_modified, compression, data) " +
                "VALUES ($id, $ext, $last_modified, $compression, $data);";
            command.Parameters.AddWithValue("$id", Id);
            command.Parameters.Add("$data", SqliteType.Blob).Value = data;
            command.Parameters.AddWithValue("$ext", ext);
            command.Parameters.AddWithValue("$last_modified", Modified.ToUnixTimeSeconds());
            command.Parameters.AddWithValue("$compression", compression);
            command.ExecuteNonQuery();
        }

        public async Task<DefaultHttpContext> Get(string path = "/WebAssets/2605.sqlite", string? query = null,
            string encoding = "", bool authorized = true, string? etag = null, string? since = null, string method = "GET") {
            NextCalled = false;
            var context = new DefaultHttpContext();
            context.Request.Method = method;
            context.Request.Host = new HostString("localhost", 8080);
            context.Request.Path = path;
            context.Request.QueryString = new QueryString(query ?? "?id=" + Id);
            context.Request.Headers.AcceptEncoding = encoding;
            if (authorized) context.Request.Headers.Cookie = "dashboard_get_auth_8080=test-access-token";
            if (etag != null) context.Request.Headers.IfNoneMatch = etag;
            if (since != null) context.Request.Headers.IfModifiedSince = since;
            context.Response.Body = new MemoryStream();
            RequestDelegate next = _ => { NextCalled = true; return Task.CompletedTask; };
            await (Task)Handler.Invoke(module, new object[] { context, next, Root })!;
            return context;
        }

        public void Dispose() => Directory.Delete(Folder, recursive: true);
    }
}
