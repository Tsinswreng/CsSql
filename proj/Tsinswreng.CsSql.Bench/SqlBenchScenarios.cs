using System.Diagnostics;
using Tsinswreng.CsSql;
using Tsinswreng.CsSql.Test.Domains;

namespace Tsinswreng.CsSql.Bench;

/// <summary>
/// 基準場景:對比「原始 ADO.NET 實現」與「CsSql IRepo API」的批量寫入/讀取性能。
/// 原始實現直接操作 DbCommand,不走 CsSql——這是「批量執行層調查」中候選策略的雛形,
/// 用於為執行引擎改造出對比數據,不改庫。
/// </summary>
public static class SqlBenchScenarios {
	/// <summary>
	/// 統一計時規範:每種策略先跑一輪不計時的 warm-up(讓 sqlite page cache / OS 文件緩存 / 連接熱起來;
	/// AOT 下無 JIT 預熱誤差,但緩存仍有冷熱差),再跑 Rounds 輪計時、每輪前執行
	/// BeforeEachRound(預設清表,清表不計入耗時),取中位數抗噪聲。
	/// </summary>
	/// <param name="Db">原始執行器,用於每輪前的清表。</param>
	/// <param name="Title">對比表標題。</param>
	/// <param name="Strategies">每項:方案名 + 被測操作。</param>
	/// <param name="Total">總行數,僅用於打印每條均攤。</param>
	/// <param name="Rounds">計時輪數,取中位數。</param>
	/// <param name="BeforeEachRound">每輪(含 warm-up)前執行的操作;null 表示不清表(讀場景)。</param>
	public static async Task MeasureStrategies(IRawDbExecutor Db, str Title, IReadOnlyList<(str Name, Func<Task> Run)> Strategies, int Total, int Rounds = 5, Func<Task>? BeforeEachRound = null) {
		// 策略失敗記錄:某策略拋異常(如 pg OrdAdd 執行層未驗證的 bug)時,
		// 記下原因並跳過其後續輪次,不中斷其他策略的對比
		var Failures = new str?[Strategies.Count];

		// warm-up:每種策略不計時跑一遍,熱緩存
		for (var I = 0; I < Strategies.Count; I++) {
			if (BeforeEachRound != null) {
				await BeforeEachRound();
			}
			try {
				await Strategies[I].Run();
			} catch (Exception E) {
				Failures[I] = E.Message;
			}
		}

		// 計時輪
		var Samples = new List<double>[Strategies.Count];
		for (var i = 0; i < Samples.Length; i++) {
			Samples[i] = new List<double>(Rounds);
		}
		for (var r = 0; r < Rounds; r++) {
			for (var i = 0; i < Strategies.Count; i++) {
				if (Failures[i] != null) {
					continue; // 已失敗的策略不再重跑
				}
				if (BeforeEachRound != null) {
					await BeforeEachRound();
				}
				var Sw = Stopwatch.StartNew();
				try {
					await Strategies[i].Run();
				} catch (Exception E) {
					Failures[i] = E.Message;
				} finally {
					Sw.Stop();
				}
				Samples[i].Add(Sw.Elapsed.TotalMilliseconds);
			}
		}

		// 中位數 + 打印;失敗的策略以 FAIL 標出,原因列在表下方
		var Rows = new List<(str, double)>(Strategies.Count);
		for (var i = 0; i < Strategies.Count; i++) {
			if (Failures[i] != null) {
				Rows.Add((Strategies[i].Name + " [FAIL]", 0));
			} else {
				var Sorted = Samples[i].OrderBy(x => x).ToList();
				Rows.Add((Strategies[i].Name, Sorted[Sorted.Count / 2]));
			}
		}
		BenchRunner.PrintTable(Title, Rows, Total);
		for (var i = 0; i < Strategies.Count; i++) {
			if (Failures[i] != null) {
				Console.WriteLine($"  ⚠ {Strategies[i].Name} 失敗: {Failures[i]}");
			}
		}
	}

	/// <summary>
	/// 批量插入 Total 行 TestKv,對比五種寫法。
	/// A/B/C 是原始 ADO.NET 候選策略;D 是 CsSql IRepo 現狀(無事務);
	/// D2 是「IRepo 現狀 + 事務」,用於隔離「CsSql 封裝開銷」與「無事務代價」。
	/// </summary>
	/// <param name="Db">原始執行器。</param>
	/// <param name="Repo">CsSql IRepo(測試域 TestKv)。</param>
	/// <param name="CmdMkr">CsSql 命令建立器,供 D2 開事務。</param>
	/// <param name="Total">總行數。</param>
	/// <param name="BatchSize">批大小(對 C 多行 VALUES 生效)。</param>
	public static async Task RunInsertComparison(IRawDbExecutor Db, IRepo<TestKv, IdTestKv> Repo, ISqlCmdMkr CmdMkr, int Total, int BatchSize, int Rounds = 5) {
		var Rows = MkRows(Total);
		var Strategies = new (str, Func<Task>)[] {
			("A.單條循環(無事務)", () => Db.InsertSingleNoTxn(Rows)),
			("B.事務內循環(prepare復用)", () => Db.InsertTxnLoop(Rows)),
			("C.多行VALUES", () => Db.InsertMultiValues(Rows, BatchSize)),
			("D.IRepo.OrdAdd(現狀,無事務)", () => RunRepoAdd(Db, Repo, Rows)),
			("D2.IRepo.OrdAdd(事務內)", () => RunRepoAddInTxn(CmdMkr, Repo, Rows)),
		};
		// 每輪前清表:各寫法插入同一批數據,保證公平
		await MeasureStrategies(Db, $"Insert {Total} rows, batch={BatchSize}", Strategies, Total, Rounds, BeforeEachRound: () => Db.ClearTable("TestKv"));
	}

	/// <summary>
	/// pg 批量插入對比:在 RunInsertComparison 五種寫法(A/B/C/D/D2)基礎上,
	/// 追加 pg 專用候選策略 E(NpgsqlBatch)與 F(COPY)——這是 pg 執行層調查的核心對比。
	/// </summary>
	public static async Task RunPgInsertComparison(IPgBatchExecutor Db, IRepo<TestKv, IdTestKv> Repo, ISqlCmdMkr CmdMkr, int Total, int BatchSize, int Rounds = 5) {
		var Rows = MkRows(Total);
		var Strategies = new (str, Func<Task>)[] {
			("A.單條循環(無事務)", () => Db.InsertSingleNoTxn(Rows)),
			("B.事務內循環", () => Db.InsertTxnLoop(Rows)),
			("C.多行VALUES", () => Db.InsertMultiValues(Rows, BatchSize)),
			("D.IRepo.OrdAdd(現狀,無事務)", () => RunRepoAdd(Db, Repo, Rows)),
			("D2.IRepo.OrdAdd(事務內)", () => RunRepoAddInTxn(CmdMkr, Repo, Rows)),
			("E.NpgsqlBatch", () => Db.InsertNpgsqlBatch(Rows, BatchSize)),
			("F.COPY(BinaryImporter)", () => Db.InsertCopy(Rows)),
		};
		// 每輪前清表:各寫法插入同一批數據,保證公平
		await MeasureStrategies(Db, $"Insert {Total} rows, batch={BatchSize}", Strategies, Total, Rounds, BeforeEachRound: () => Db.ClearTable("TestKv"));
	}

	/// <summary>
	/// 測 IRepo 讀 API:OrdGetById / GetInId / GetAll 對比。
	/// 先一次性寫入 Total 行種子數據(不計時),再對同一份數據測三種讀法。
	/// </summary>
	public static async Task RunRepoReadComparison(IRawDbExecutor Db, IRepo<TestKv, IdTestKv> Repo, int Total, int Rounds = 5) {
		var Rows = MkRows(Total);

		// 種子數據:清表 + 原始多行 VALUES 批量寫入,不計時。
		// 不用 IRepo.OrdAdd 種數據:pg 上 OrdAdd 執行層有緩存命令復用 bug(見 pg 基準 D 策略 FAIL),會崩。
		await Db.ClearTable("TestKv");
		await Db.InsertMultiValues(Rows, 500);

		var Ids = Rows.Select(x => x.Id).ToArray();
		// 讀場景共用一個 Ctx(讀不修改數據),避免每輪重開連接的噪音
		var ReadCtx = await Db.NewCtx();
		try {
			var Strategies = new (str, Func<Task>)[] {
				("OrdGetById", () => CountRows(Repo.OrdGetById(ReadCtx, ToAsyE(Ids), default))),
				("GetInId", () => CountRows(Repo.GetInId(ReadCtx, ToAsyE(Ids), default))),
				("GetAll", () => CountRows(Repo.GetAll(ReadCtx, default))),
			};
			// 讀不改變數據,每輪前不需要清表
			await MeasureStrategies(Db, $"Read {Total} rows", Strategies, Total, Rounds, BeforeEachRound: null);
		} finally {
			await ((IAsyncDisposable)ReadCtx).DisposeAsync();
		}
	}

	/// <summary>以 CsSql IRepo 現狀執行批量插入(無事務),用完釋放 Ctx。</summary>
	private static async Task RunRepoAdd(IRawDbExecutor Db, IRepo<TestKv, IdTestKv> Repo, IList<TestKv> Rows) {
		var Ctx = await Db.NewCtx();
		try {
			await Repo.OrdAdd(Ctx, ToAsyE(Rows), default);
		} finally {
			await ((IAsyncDisposable)Ctx).DisposeAsync();
		}
	}

	/// <summary>在事務內以 CsSql IRepo 執行批量插入,隔離「CsSql 開銷」與「無事務代價」。</summary>
	private static async Task RunRepoAddInTxn(ISqlCmdMkr CmdMkr, IRepo<TestKv, IdTestKv> Repo, IList<TestKv> Rows) {
		await CmdMkr.RunInTxn(default, async Ctx => {
			await Repo.OrdAdd(Ctx, ToAsyE(Rows), default);
			return NIL;
		});
	}

	/// <summary>完整消費一個異步序列並計數(防止讀結果被死代碼消除)。</summary>
	private static async Task<long> CountRows<T>(IAsyncEnumerable<T> Rows) {
		var N = 0L;
		await foreach (var _ in Rows) {
			N++;
		}
		return N;
	}

	/// <summary>構造 Total 行測試資料(強類型 Id + 字段)。</summary>
	public static List<TestKv> MkRows(int Total) {
		var Rows = new List<TestKv>(Total);
		for (var i = 0; i < Total; i++) {
			Rows.Add(new TestKv {
				Id = new IdTestKv(),
				Owner = default,
				KStr = "bench_k_" + i,
				VStr = "bench_v_" + i,
			});
		}
		return Rows;
	}

	/// <summary>List → IAsyncEnumerable(測試域/IRepo 入參形狀)。</summary>
	public static async IAsyncEnumerable<T> ToAsyE<T>(IEnumerable<T> Items) {
		foreach (var It in Items) {
			yield return It;
		}
	}
}
