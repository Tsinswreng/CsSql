using System.Data;

namespace Tsinswreng.CsSql;
using Tsinswreng.CsCtx;

public partial interface IDbFnCtx
	:IFnCtx
	,IAsyncDisposable
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
	async ValueTask IAsyncDisposable.DisposeAsync(){
		if(ObjsToDispose != null){
			foreach(var obj in ObjsToDispose){
				if(obj is IAsyncDisposable DispAsy){
					await DispAsy.DisposeAsync();
				}else if(obj is IDisposable Disp){
					Disp.Dispose();
				}
			}
			ObjsToDispose.Clear();
		}

		if(Txn is IDisposable txn){
			txn.Dispose();
			Txn = null;
		}

		if(DbConn is IDbConnection dbConn){
			try{
				dbConn.Close();
			}catch{
				if(dbConn is IDisposable disp){
					disp.Dispose();
				}
			}
			DbConn = null;
		}
	}
}

