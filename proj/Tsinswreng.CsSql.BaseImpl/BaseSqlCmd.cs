using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;
using Tsinswreng.CsCore;
using Tsinswreng.CsSql;
using Tsinswreng.CsTools;

namespace Tsinswreng.CsSql.BaseImpl;

public abstract partial class BaseSqlCmd<
	TRawCmd ,TRawTxn
>
	:ISqlCmd
	,IAsyncDisposable
	where TRawCmd : DbCommand
	where TRawTxn : DbTransaction
{
	public abstract EDbSrcType DbSrcType{get;}
	public TRawCmd RawCmd{get;set;}
	public IList<Func<Task<nil>>> FnsOnDispose{get;set;} = new List<Func<Task<nil>>>();
	public str? Sql{get;set;}
	public abstract IDbValConvtr DbValConvtr{get;protected set;}
	protected bool _Disposed = false;
	public BaseSqlCmd(TRawCmd DbCmd){
		RawCmd = DbCmd;
	}

    // 实现 ExeReader，返回 IResultReader
    public virtual async Task<IResultReader> ExeReader(CT Ct){
		var RawReader = await RawCmd.ExecuteReaderAsync(Ct);
		return new BaseResultReader(
			this
			,RawCmd
			,RawReader
			,DbValConvtr
		);
	}

	public virtual ISqlCmd AttachCtxTxn(ITxn Txn){
		if(Txn.RawTxn is not TRawTxn RawTxn){
			throw new ArgumentException("Txn.RawTxn is not TRawTxn RawTxn");
		}
		RawCmd.Transaction = RawTxn;
		return this;
	}

	public virtual ISqlCmd AttachCtxTxn(IDbFnCtx Ctx){
		if(Ctx?.Txn is not null){
			if(Ctx.Txn.RawTxn is DbTransaction rawTxn){
				if(!ReferenceEquals(rawTxn.Connection, RawCmd.Connection)){
					return this;
				}
			}
			AttachCtxTxn(Ctx.Txn);
		}
		return this;
	}

	public abstract nil ParamAddWithValue(
		DbParameterCollection Params, string? parameterName, object? value
	);

	public abstract str ToResolvedArg(str RawArg);
	
	
	//e.g return "@"+RawArg
	[Impl]
	public ISqlCmd ResolvedArgs(IDictionary<str, obj?> Args){
		RawCmd.Parameters.Clear();//不清空舊參數 續ˣ珩DbCmd蜮報錯
		foreach(var (k,v) in Args){
			this.ParamAddWithValue(
				RawCmd.Parameters
				,k, CodeValToDbVal(v)
			);
		}
		return this;
	}

	/// 傳入之字典不帶@名稱 佔位
	[Impl]
	public ISqlCmd RawArgs(IDictionary<str, obj?> Args){
		RawCmd.Parameters.Clear();//不清空舊參數 續ˣ珩DbCmd蜮報錯
		foreach(var (k,v) in Args){
			this.ParamAddWithValue(
				RawCmd.Parameters
				,ToResolvedArg(k), CodeValToDbVal(v)
			);
		}
		return this;
	}

/// @0, @1, @2 ...
	public ISqlCmd Args(IEnumerable<obj?> Params){
		RawCmd.Parameters.Clear();
		var i = 0;
		foreach(var v in Params){
			//RawCmd.Parameters.AddWithValue("@"+i, CodeValToDbVal(v));
			this.ParamAddWithValue(
				RawCmd.Parameters
				,ToResolvedArg(i+""), CodeValToDbVal(v)
			);
		i++;}
		return this;
	}


	/// 若含null則做DBNull與null之轉、否則原樣返
	public virtual obj? DbValToCodeVal(obj? DbVal){
		return DbValConvtr.ToCodeVal(DbVal);
	}

	public virtual obj? CodeValToDbVal(obj? CodeVal){
		return DbValConvtr.ToDbVal(CodeVal);
	}

	protected async Task<nil> RunFnsOnDispose(){
		foreach(var fn in FnsOnDispose){
			await fn();
		}
		FnsOnDispose.Clear();
		return NIL;
	}

	public void Dispose(){
		if(_Disposed){
			return;
		}
		_Disposed = true;
		try{
			RunFnsOnDispose().GetAwaiter().GetResult();
		}finally{
			RawCmd.Dispose();
		}
	}

	public async ValueTask DisposeAsync() {
		if(_Disposed){
			return;
		}
		_Disposed = true;
		try{
			await RunFnsOnDispose();
		}finally{
			RawCmd.Dispose();
		}
	}
}
