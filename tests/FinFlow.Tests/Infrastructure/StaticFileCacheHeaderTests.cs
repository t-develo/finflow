using System.Net;
using FinFlow.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinFlow.Tests.Infrastructure;

/// <summary>
/// InMemory DB を使う静的ファイル配信テスト用の WebApplicationFactory。
/// </summary>
public class StaticFileTestFixture : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<FinFlowDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<FinFlowDbContext>(options =>
                options.UseInMemoryDatabase("FinFlowStaticFileTest"));
        });
    }
}

/// <summary>
/// 静的ファイル配信に Cache-Control が付くことを検証する。
///
/// 背景: 既定の <c>app.UseStaticFiles()</c> は ETag / Last-Modified は付けるが
/// Cache-Control を付けない。その場合ブラウザは「ヒューリスティックキャッシュ」に
/// 落ち、再検証せずにローカルのコピーを使い続ける（iOS Safari で特に顕著）。
/// 実際に、ログイン画面のタップ不能バグを修正して配信した後も端末が修正前の
/// CSS を使い続け、「直したはずの不具合が実機で再現し続ける」事象が発生した。
/// </summary>
[Trait("Category", "Integration")]
public class StaticFileCacheHeaderTests : IClassFixture<StaticFileTestFixture>
{
    private readonly HttpClient _client;

    public StaticFileCacheHeaderTests(StaticFileTestFixture fixture)
    {
        // 静的ファイルの配信そのものを検証するのでリダイレクトは追わない
        _client = fixture.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Theory]
    [InlineData("/css/main.css")]
    [InlineData("/css/components.css")]
    [InlineData("/css/pages.css")]
    [InlineData("/js/app.js")]
    public async Task GetStaticAsset_ReturnsCacheControlNoCache(string path)
    {
        // Act
        var response = await _client.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"{path} が配信されていません（wwwroot の配置を確認してください）");

        response.Headers.CacheControl.Should().NotBeNull(
            $"{path} に Cache-Control が付いていないと、ブラウザがヒューリスティック" +
            "キャッシュで古いファイルを使い続け、修正が実機に反映されません");

        response.Headers.CacheControl!.NoCache.Should().BeTrue(
            $"{path} は毎回 ETag で再検証させる必要があります");
    }

    [Fact]
    public async Task GetIndexHtml_ReturnsCacheControlNoCache()
    {
        // Act
        var response = await _client.GetAsync("/index.html");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl.Should().NotBeNull();
        response.Headers.CacheControl!.NoCache.Should().BeTrue();
    }

    [Fact]
    public async Task GetSpaRoute_FallsBackToIndexHtml_WithCacheControlNoCache()
    {
        // Act — SPA のディープリンク。MapFallbackToFile が index.html を返す。
        var response = await _client.GetAsync("/login");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");

        // MapFallbackToFile は UseStaticFiles とは別の StaticFileMiddleware を
        // 内部で実行するため、同じ StaticFileOptions を渡さないと index.html に
        // だけヘッダーが付かない。ここはその取りこぼしを防ぐためのテスト。
        response.Headers.CacheControl.Should().NotBeNull(
            "MapFallbackToFile にも StaticFileOptions を渡す必要があります");
        response.Headers.CacheControl!.NoCache.Should().BeTrue();
    }

    [Fact]
    public async Task GetIndexHtml_ReferencesAssetsWithCacheBustingVersion()
    {
        // Act
        var response = await _client.GetAsync("/index.html");
        var html = await response.Content.ReadAsStringAsync();

        // Assert — 端末に焼き付いた既存キャッシュに対抗できるのは URL の変更だけ。
        html.Should().Contain("/css/main.css?v=",
            "CSS の参照にバージョンクエリが無いと、既にキャッシュされた古い CSS が" +
            "使われ続けます（index.html の ?v= を更新し忘れていないか確認）");
        html.Should().Contain("/js/app.js?v=");
    }

    [Fact]
    public async Task GetIndexHtml_DoesNotBlockRenderingOnExternalCdn()
    {
        // Act
        var response = await _client.GetAsync("/index.html");
        var html = await response.Content.ReadAsStringAsync();

        // Assert — defer/async の無い外部 <script> は、インターネットに出られない
        // LAN 内（ラズパイ運用）で DNS/TCP タイムアウトまで <body> の解析を止め、
        // その間ページ全体がタップに反応しなくなる。
        html.Should().NotContain("cdn.jsdelivr.net",
            "外部 CDN の同期スクリプトはオフライン環境でページを操作不能にします。" +
            "必要なライブラリは使用するページから動的に読み込んでください。");
    }
}
