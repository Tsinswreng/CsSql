namespace Tsinswreng.CsSql.Scripts;

/// 發布 Tsinswreng.CsSql.Test.Sqlite 為 win-x64 NativeAOT 並在 AOT 環境下運行測試。
internal static partial class TestSqlite {
	/// 執行：publish -c Release -r win-x64 → 運行 publish 目錄下之測試 exe。
	internal static partial Task Main(string Root, CancellationToken Ct);
}
