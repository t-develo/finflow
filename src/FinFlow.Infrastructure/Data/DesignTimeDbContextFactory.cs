using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FinFlow.Infrastructure.Data;

/// <summary>
/// dotnet ef 用のデザインタイムファクトリ。
/// 同一 DbContext に対して IDesignTimeDbContextFactory を複数置くと dotnet ef が失敗するため、
/// プロバイダは環境変数 FINFLOW_MIGRATIONS_PROVIDER で切り替える（既定: SqlServer）。
///
/// SQLite 用マイグレーションの作成例:
///   FINFLOW_MIGRATIONS_PROVIDER=Sqlite dotnet ef migrations add &lt;Name&gt; \
///     --project src/FinFlow.Infrastructure.Sqlite \
///     --startup-project src/FinFlow.Api
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FinFlowDbContext>
{
    private const string SqliteMigrationsAssembly = "FinFlow.Infrastructure.Sqlite";

    public FinFlowDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../FinFlow.Api"))
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<FinFlowDbContext>();
        var provider = Environment.GetEnvironmentVariable("FINFLOW_MIGRATIONS_PROVIDER") ?? "SqlServer";

        if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            // 実際には接続しない（マイグレーションの生成にのみ使う）ダミーの接続文字列
            optionsBuilder.UseSqlite(
                "Data Source=finflow-designtime.db",
                sqlite => sqlite.MigrationsAssembly(SqliteMigrationsAssembly));
        }
        else
        {
            optionsBuilder.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"));
        }

        return new FinFlowDbContext(optionsBuilder.Options);
    }
}
