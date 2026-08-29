namespace Tsinswreng.CsSql.Scripts;

using System.Runtime.CompilerServices;

/// CsSql 維護腳本的命令列入口。
/// 第一個引數選擇具體腳本；其餘引數保留給該腳本未來的專用選項。
internal static partial class Program {
	/// 將命令列入口分派至各個具名的 CsSql 工作。
	internal static async Task Main(string[] Args) {
		using var CtSource = new CancellationTokenSource();
		Console.CancelKeyPress += (_, Event) => {
			Event.Cancel = true;
			CtSource.Cancel();
		};

		var Ct = CtSource.Token;
		var CallerPath = OwnPath();
		var CallerDir = System.IO.Path.GetDirectoryName(CallerPath)!;
		// Program.cs 實際位於 <CsSql根>/proj/Tsinswreng.CsSql.Scripts；
		// 上推 2 級得到 CsSql 倉庫根（獨立項目，非 Ngan 工作區），各腳本以 Root/"proj/..." 定位目標。
		var Root = System.IO.Path.GetFullPath(CallerDir/"../..")/"";

		if (Args.Length == 0) {
			PrintUsage();
			return;
		}

		switch (Args[0]) {
			case nameof(TestSqlite):
				await TestSqlite.Main(Root, Ct);
				break;
			default:
				throw new ArgumentException($"Unknown CsSql script: {Args[0]}.", nameof(Args));
		}
	}

	/// 列出可由 dotnet run -- <入口> 呼叫的腳本名稱。
	private static void PrintUsage() {
		Console.Error.WriteLine("Usage: dotnet run --project Tsinswreng.CsSql/proj/Tsinswreng.CsSql.Scripts -- <entry>");
		Console.Error.WriteLine("Entries: TestSqlite");
	}

	/// 讓編譯器把這個 dispatcher 原始檔路徑填入；CLR 的 Main 本身不會填 CallerFilePath。
	private static string OwnPath([CallerFilePath] string CallerPath = "") {
		return CallerPath;
	}
}
