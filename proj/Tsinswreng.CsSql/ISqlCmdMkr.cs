namespace Tsinswreng.CsSql;


public partial interface ISqlCmdMkr
	:I_DbSrcType
	,IMkrTxn
{

	[Doc(@$"初始化{nameof(DbFnCtx)} =: DbFnCtx
	
	傳入未初始化的 可能爲null的 DbFnCtx
	返回已初始化的 非null的 DbFnCtx。
	#Eg[
	```cs
	Ctx = await FnInitDbFnCtx(Ctx);
	```
	]
	")]
	public Func<IDbFnCtx?, Task<IDbFnCtx>> FnInitDbFnCtx{get;set;}
	
	/// 璫允 先Prepare後傳參數。㕥便 複用SqlCmd、每次傳不同參數。
	public Task<ISqlCmd> Prepare(ISqlCmd Cmd, CT Ct);

	public Task<ISqlCmd> Prepare(
		IDbFnCtx? DbFnCtx
		,str Sql
		,CT Ct
	);

	public Task<ISqlCmd> Prepare(
		IDbFnCtx? DbFnCtx
		,ISql Sql
		,CT Ct
	){
		return Prepare(DbFnCtx, Sql.RawStr, Ct);
	}

/// 無prepare、適用于CREATE TABLE等
	public Task<ISqlCmd> MkCmd(
		IDbFnCtx? DbFnCtx
		,str Sql
		,CT Ct
	);

}


