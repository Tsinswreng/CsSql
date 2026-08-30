using Tsinswreng.CsTools;

namespace Tsinswreng.CsSql;
public class AutoBatch<TItem, TRet> : BatchCollector<TItem, TRet> {
	public AutoBatch() {

	}
	/// TODO pg旹 500>100>50; sqlite 單條循環>50>100>500 按數據庫選批大小
	public static new u64 DfltBatchSize { get; set; } = 100;
	public ISqlDuplicator SqlDuplicator { get; set; }
	//public u64 BatchSize;
	/// 當前批的命令(每批新建):reader 消費完會 Dispose 命令(AsyE2d 的 DisposableList),
	/// 跨批復用緩存命令在 pg 上會因已釋放而崩(sqlite 因 SqliteCommand 容錯未顯形)
	public ISqlCmd SqlCmd { get; set; }
	public IDbFnCtx Ctx { get; set; }
	public ISqlCmdMkr SqlCmdMkr { get; set; }
	public static AutoBatch<TItem, TRet> Mk(
		IDbFnCtx Ctx
		, ISqlCmdMkr SqlCmdMkr
		, ISqlDuplicator SqlDuplicator
		, Func<
			AutoBatch<TItem, TRet> //Self
			, IList<TItem>
			, CT
			, Task<TRet>
		> FnAsy
		, u64 BatchSize = 0
	) {
		if (BatchSize == 0) {
			BatchSize = DfltBatchSize;
		}
		var R = new AutoBatch<TItem, TRet>();
		R.Ctx = Ctx;
		R.SqlCmdMkr = SqlCmdMkr;
		R.SqlDuplicator = SqlDuplicator;
		var ArgFn = FnAsy;
		R.FnAsy = async (Items, Ct) => {
			var size = (u64)Items.Count;
			R.SqlCmd = await R.SqlCmdMkr.Prepare(R.Ctx, R.SqlDuplicator.DuplicateSql(size), Ct);
			R.Ctx.AddToDispose(R.SqlCmd); // 保險:執行前拋異常時也回收命令
			return await ArgFn(R, Items, Ct);
		};
		R.Init(R.FnAsy, BatchSize);
		return R;
	}
	


}
