namespace Tsinswreng.CsSql;

[Doc(@$"Maker of Database Function Context")]
public partial interface IMkrDbFnCtx{
	public IMkrTxn TxnGetter{get;set;}
	[Doc(@$"Make Database Function Context with Transaction attached
	For Sqlite, Do not use for select statement, or it may cause nested transaction error.
	You just need `new {nameof(DbFnCtx)}` for select statement.
	")]
	public async Task<IDbFnCtx> MkTxnDbFnCtx(CT Ct){
		var R = new DbFnCtx();
		R.Txn = await TxnGetter.EnsureTxn(R, Ct);
		return R;
	}
}


/*
帶泛型版IMkrDbFnCtx<TDbFnCtx>潙舊設計、初慮及 自ʹ業務程序集 可自建DbFnCtx子類㕥做擴展
肰泛型大增繁。 如需擴展 則于業務程序集中 潙基ʹIDbFnCtx接口作擴展方法 㕥轉型作己ʹ擴展ʹ子類
 */
public partial class MkrDbFnCtx : IMkrDbFnCtx{
	public IMkrTxn TxnGetter{get;set;}
	public MkrDbFnCtx(IMkrTxn GetTxn){
		this.TxnGetter = GetTxn;
	}
}
