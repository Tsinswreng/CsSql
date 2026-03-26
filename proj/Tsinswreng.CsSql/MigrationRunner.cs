namespace Tsinswreng.CsSql;

/// 通用遷移執行器：在單個事務中應用未執行的遷移。
public class MigrationRunner{
	/// 遷移清單與遷移編排器。
	public IMigrationMgr MigrationMgr;
	/// 建立 SQL 命令的工廠。
	public ISqlCmdMkr SqlCmdMkr;
	/// 生成可綁定事務的 Db 執行上下文。
	public IMkrTxn MkrTxn;
	/// 遷移歷史表倉儲。
	public IRepo<SchemaHistory, i64> RepoSchemaHistory;
	/// 通用事務包裝器。整個遷移過程必須在單事務內完成。
	public TxnWrapper TxnWrapper;

	/// 建立一個可供任意業務程序集復用的遷移執行器。
	///
	/// 業務側只需：
	/// - 配好自己的 `IMigrationMgr`
	/// - 再調 `UpAsy()`
	///
	/// 真正的執行流程都收斂在 `CsSql` 內。
	public MigrationRunner(
		IMigrationMgr MigrationMgr
		,ISqlCmdMkr SqlCmdMkr
		,IMkrTxn MkrTxn
		,IRepo<SchemaHistory, i64> RepoSchemaHistory
		,TxnWrapper TxnWrapper
	){
		this.RepoSchemaHistory = RepoSchemaHistory.UseSqlHelperStrAcc();
		this.MigrationMgr = MigrationMgr;
		this.SqlCmdMkr = SqlCmdMkr;
		this.MkrTxn = MkrTxn;
		this.TxnWrapper = TxnWrapper;
	}

	/// 在單個事務中執行所有未應用遷移。
	///
	/// 調用方通常只需要在部署腳本、測試工具或應用啓動初始化時調這一個入口。
	public async Task<nil> Up(CT Ct){
		return await TxnWrapper.Wrap<nil>(
			async(Ctx, Ct)=>{
				return async(Ct)=>{
					await MigrationMgr.RunPendingMigrations(
						Ctx: Ctx
						,SqlCmdMkr: SqlCmdMkr
						,MkrTxn: MkrTxn
						,RepoHistory: RepoSchemaHistory
						,Ct: Ct
					);
					return NIL;
				};
			}
			,Ct
		);
	}
}
