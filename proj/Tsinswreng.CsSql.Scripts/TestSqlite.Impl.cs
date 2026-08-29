using static Tsinswreng.CsSh.ShGlobal;

namespace Tsinswreng.CsSql.Scripts;

/// Implements the sqlite AOT test entry point.
internal static partial class TestSqlite {
	internal static async partial Task Main(string Root, CancellationToken Ct) {
		var ProjectDir = Root/"proj/Tsinswreng.CsSql.Test.Sqlite";
		var PublishExe = ProjectDir/"bin/Release/net10.0/win-x64/publish/Tsinswreng.CsSql.Test.Sqlite.exe";

		// 先以 NativeAOT 發布（csproj 已設 PublishAot=true），再運行發布物，
		// 驗證 AOT 剪裁後測試仍全部通過。
		// -m:1：本機 nuget.org 不可達時，多節點並行 restore 會靜默失敗（0 警告 0 錯誤 exit 1）。
		Cd(ProjectDir);
		await Exe("dotnet", ["publish", "-c", "Release", "-r", "win-x64", "-m:1"], Ct);
		// 測試輸出必須轉送終端，否則看不到結果。
		await Cmd(PublishExe, [], Ct).Out(Ct);
	}
}
