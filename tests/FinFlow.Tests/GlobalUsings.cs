global using Xunit;

// テスト並列実行を無効化（WebApplicationFactoryのホスト構築競合を防ぐ）
[assembly: CollectionBehavior(DisableTestParallelization = true)]
