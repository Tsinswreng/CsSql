using System.Diagnostics;

namespace Tsinswreng.CsSql.Bench;

/// <summary>
/// 手寫基準測量工具。AOT 兼容(純 BCL,無反射/動態代碼)。
/// AOT 下無 JIT 預熱誤差,故不需要預熱輪;但 GC/緩存波動仍在,取多輪中位數抗噪聲。
/// </summary>
public static class BenchRunner {
	/// <summary>
	/// 測量一個操作的耗時,跑 N 輪取中位數(毫秒)。
	/// </summary>
	/// <param name="Fn">被測操作;返回的 long 會被累加,防止死代碼消除。</param>
	/// <param name="Rounds">輪數,取中位數。</param>
	public static double MeasureMs(Func<long> Fn, int Rounds = 5) {
		var samples = new List<double>(Rounds);
		for (var i = 0; i < Rounds; i++) {
			var sw = Stopwatch.StartNew();
			var sink = Fn();
			sw.Stop();
			GC.KeepAlive(sink); // 防止優化器把結果丟棄
			samples.Add(sw.Elapsed.TotalMilliseconds);
		}
		samples.Sort();
		return samples[samples.Count / 2]; // 中位數
	}

	/// <summary>
	/// 測量異步操作,跑 N 輪取中位數(毫秒)。
	/// </summary>
	public static async Task<double> MeasureMsAsync(Func<Task<long>> Fn, int Rounds = 5) {
		var samples = new List<double>(Rounds);
		for (var i = 0; i < Rounds; i++) {
			var sw = Stopwatch.StartNew();
			var sink = await Fn();
			sw.Stop();
			GC.KeepAlive(sink);
			samples.Add(sw.Elapsed.TotalMilliseconds);
		}
		samples.Sort();
		return samples[samples.Count / 2];
	}

	/// <summary>
	/// 打印對比表。Rows 每項:方案名 + 耗時毫秒。自動算每行均攤(基於 Rows/Total)。
	/// </summary>
	public static void PrintTable(string Title, IList<(string Name, double Ms)> Rows, long TotalItems) {
		Console.WriteLine();
		Console.WriteLine($"===== {Title} (總量 {TotalItems}) =====");
		Console.WriteLine($"{"方案",-28} {"總耗時(ms)",-14} {"每條均攤(ms)",-14}");
		foreach (var (name, ms) in Rows) {
			var perItem = TotalItems > 0 ? ms / TotalItems : 0;
			Console.WriteLine($"{name,-28} {ms,-14:F2} {perItem,-14:F4}");
		}
		Console.WriteLine();
	}
}
