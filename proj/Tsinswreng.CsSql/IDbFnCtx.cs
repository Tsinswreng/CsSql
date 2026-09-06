using System.Data;

namespace Tsinswreng.CsSql;
using Tsinswreng.CsCtx;

[Doc(@$"Database Function Context。
可 Dispose 能力繼承自 {nameof(IFnCtx)};釋放邏輯由 {nameof(DbFnCtx)} override Dispose 鏈實現:
先跑基類鉤子({nameof(FnCtx.FnDisposeAsy)}),再關自己的 Db 資源（ObjsToDispose → Txn → DbConn）。
")]
public partial interface IDbFnCtx
	:IFnCtx
{
	[Doc(@$"Transaction")]
	public ITxn? Txn{get;set;}
	
	[Doc(@$"don't need to set {nameof(DbConn)} manually
	when you pass DbFnCtx to {nameof(ISqlCmdMkr.MkCmd)}
	if {nameof(DbConn)} is null, it will be initialized by {nameof(IDbConnMgr)}
	")]
	public IDbConnection? DbConn{get;set;}
	
	//public IDictionary<obj, obj?>? Props{get;set;}
	[Doc(@$"Use {nameof(ExtnIDbFnCtx.AddToAsyDispose)} instead of directory operate on {nameof(ObjsToDispose)}")]
	public ICollection<obj?>? ObjsToDispose{get;set;}
#if Impl
	 = new List<obj?>();
#endif
	[Doc(@$"default is 1, which means non batch mode
	If set to > 1, then duplication of same sql with distinct parameters will be built and attach to SqlCmd.CommandText
	")]
	[Obsolete]
	public u64 BatchSize{get;set;}
#if Impl
	= 1;
#endif
}