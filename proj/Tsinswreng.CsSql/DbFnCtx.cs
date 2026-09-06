#define Impl
namespace Tsinswreng.CsSql;
using System.Data;
using Tsinswreng.CsCtx;

[Doc(@$"Database Function Context")]
public partial class DbFnCtx:
	FnCtx
	,IDbFnCtx
{
	// [BeaKona.AutoInterface(typeof(IFnCtx), IncludeBaseInterfaces = true)]
	// public IFnCtx IFnCtx{get;set;}
	
	public ITxn? Txn{get;set;}

	public IDbConnection? DbConn{get;set;}
	
	//public IDictionary<obj, obj?>? Props{get;set;}
	
	public ICollection<obj?>? ObjsToDispose{get;set;}
#if Impl
	 = new List<obj?>();
#endif
	[Obsolete]
	public u64 BatchSize{get;set;} = 1;

	// 繼承鏈:先跑基類鉤子(用戶掛的 FnDispose/FnDisposeAsy,此時 Txn/DbConn 仍可用),
	// 再關自己的 Db 資源(ObjsToDispose → Txn → DbConn,被依賴者最後關)。
	// 同步通道:對只能是 IAsyncDisposable 的對象盡力而為(無法 await,跳過)。
	public override void Dispose(){
		base.Dispose();
		DisposeCoreSync();
	}

	public override async ValueTask DisposeAsync(){
		await base.DisposeAsync();
		await DisposeCoreAsync();
	}

	private void DisposeCoreSync(){
		if(ObjsToDispose != null){
			foreach(var obj in ObjsToDispose){
				if(obj is IDisposable Disp){
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

	private async ValueTask DisposeCoreAsync(){
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