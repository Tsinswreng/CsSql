//批原語(2026-09-01 自 Ngan.Dict.Backend.Domains.Word.Svc.SqlFlow 下沉至庫,命名保留)。
//設計:函數邊界 = 批邊界——吃 IList 的批量 API 內部零業務分批,傳多少元素就執行多少;
//「流」只出現在調用方的來源側,由本類的切批方法(Batches / BatchesInOnly)在調用點把流切成一塊一塊
//後逐批調用 IList 版(見 IRepo 的 ImList 重載與 SqlRepo 中同名 IAsyncEnumerable 版的實現)。
//
//批大小唯一出處 = 本類的 DfltBatchSize / DfltInBatchSize(按 DB 執行策略),
//業務代碼與任何調用點不出現批大小數字(執行層只把 DbSrcType 傳進來)。
//
//與被遷移版本的差異:
//- 原 Ngan.Dict.Backend 版本無 Db 感知,Batches 單一(批滿執行+尾批);
//- 本版把「批大小」收歸為按 Db 的策略函數,並拆成有輸出(Batches)與無輸出(BatchesInOnly)兩個入口,
//  寫操作不需要為「消化流」而造一個假的輸出元素。
using System.Runtime.CompilerServices;

namespace Tsinswreng.CsSql;

/// 批切分原語:把調用方手上的 IAsyncEnumerable 按批喂給吃 IList 的批量函數。
/// 這是「函數邊界 = 批邊界」的唯一切批點,本身不落 SQL、不知道任何表,
/// 只負責:湊滿一批 → 交出去執行 → 把結果按批順序攤平(或寫操作直接消費)。
public static class SqlFlow{
	/// 寫入批大小(按 DB 執行策略):
	/// sqlite 事務內單條最快(基準:多行 VALUES 隨批變大反而變慢)、pg 同構批量拼 SQL 一次 N 組。
	/// db 參數從執行層的 injected ITblMgr(實例 TblMgr)拿,數字只存在本函數。
	/// <param name="Db">目標數據庫種類,決定沿用哪套基準策略。</param>
	public static u64 DfltBatchSize(EDbSrcType Db){
		return Db == EDbSrcType.Sqlite ? 1ul : 500ul;
	}

	/// 查(IN 子句)批大小:IN 段的參數上限策略(執行層分段的兜底規模)。
	/// 注意與寫入批大小是兩種策略:IN 段的規模由參數上限倒推,與一個事務的條數無關。
	/// <param name="Db">目標數據庫種類(兩端 IN 參數上限不同)。</param>
	public static u64 DfltInBatchSize(EDbSrcType Db){
		return Db == EDbSrcType.Sqlite ? 50ul : 500ul;
	}

	/// 未傳 BatchSize 時的兜底規模,僅供確實拿不到 DbSrcType 的調用方;
	/// 執行層(有 injected TblMgr)應顯式傳 DfltBatchSize / DfltInBatchSize。
	public const u64 FallbackBatchSize = 500ul;

	/// 流切批器(有輸出版,查法用):消費 Src、按批調 FnBatch。
	/// 回調結果(TOut 集合)按批的執行順序攤平成出參流,因此出參流與入參流位置一一對應
	/// (前提:每個回調實現都保持入參列表內的位置語義,如 Ord 系的「查無補 null」)。
	/// 空間複雜度 O(批大小),與總元素數無關:只緩衝當前一批,不會把整條流物化。
	/// <param name="Src">調用方的來源流,會被本方法完整消費(惰性,隨取隨切)。</param>
	/// <param name="FnBatch">吃一『批』IList 的批量函數(通常是 IRepo 的 IList 版或提升出的批內核心);執行順序 = 批的順序。</param>
	/// <param name="Ct">整個流的取用與每批執行共用同一個取消令牌。</param>
	/// <param name="BatchSize">批元素數;null 或 0 時用 FallbackBatchSize(執行層請顯式傳 Dflt*BatchSize)。</param>
	public static async IAsyncEnumerable<TOut> Batches<TIn, TOut>(
		IAsyncEnumerable<TIn> Src
		,Func<IList<TIn>, CT, Task<IEnumerable<TOut>>> FnBatch
		,[EnumeratorCancellation] CT Ct
		,u64? BatchSize = null
	){
		var size = (BatchSize is null || BatchSize == 0) ? FallbackBatchSize : BatchSize.Value;
		var buf = new List<TIn>((i32)size);

		// 隨取隨切:滿一批才交出去執行(第一批之前的元素停留在緩衝裏,不觸發任何 SQL)
		await foreach(var item in Src.WithCancellation(Ct)){
			buf.Add(item);
			if((u64)buf.Count < size){
				continue;
			}
			// 先換新緩衝再執行:回調若持有傳入列表的引用(如併發續跑),不會污染下一批的收集
			var outs = await FnBatch(buf, Ct);
			buf = new List<TIn>((i32)size);
			foreach(var o in outs){
				yield return o;
			}
		}

		// 尾批(不足一整批的殘餘)也要執行,保證流被完整消費、元素不丟
		if(buf.Count > 0){
			var outs = await FnBatch(buf, Ct);
			foreach(var o in outs){
				yield return o;
			}
		}
	}

	/// 流切批器(無輸出版,寫法用):寫操作只消費流、不產出元素,本方法替調用方把流跑完。
	/// 與 Batches 的區別:每批回調返回 nil(執行完生效),不需要為了攤平輸出而造假的輸出元素。
	/// 批的執行順序即流的消費順序;任一批拋異常則整個流提前中止(可由調用方包裹事務保證原子性)。
	/// <param name="Src">待消費的來源流。</param>
	/// <param name="FnBatch">吃一『批』IList 的寫入函數,逐批調用、批間順序執行。</param>
	/// <param name="Ct">消費與執行共用取消令牌。</param>
	/// <param name="BatchSize">批元素數;null 或 0 時用 FallbackBatchSize(執行層請顯式傳 Dflt*BatchSize)。</param>
	/// <returns>nil;流的完整消費與最後一批的執行都已在返回前完成。</returns>
	public static async Task<nil> BatchesInOnly<TIn>(
		IAsyncEnumerable<TIn> Src
		,Func<IList<TIn>, CT, Task<nil>> FnBatch
		,CT Ct
		,u64? BatchSize = null
	){
		var size = (BatchSize is null || BatchSize == 0) ? FallbackBatchSize : BatchSize.Value;
		var buf = new List<TIn>((i32)size);

		// 逐元素收集,滿一批即執行(與 Batches 相同的「先換緩衝再執行」,防並發引用污染)
		await foreach(var item in Src.WithCancellation(Ct)){
			buf.Add(item);
			if((u64)buf.Count < size){
				continue;
			}
			await FnBatch(buf, Ct);
			buf = new List<TIn>((i32)size);
		}

		// 尾批收尾,保證流的完整消費
		if(buf.Count > 0){
			await FnBatch(buf, Ct);
		}
		return NIL;
	}
}