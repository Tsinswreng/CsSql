namespace Tsinswreng.CsSql;

/// Factory that creates auto-binders bound to one SQL parameter.

public class SqlArgBinderFactory{
	public IParam Param { get; set; }
	public ITable? Tbl{get;set;}
	//TODO 處理CodeCol 後可用于精細UpperToRaw類型轉換
	public str? CodeCol{get;set;}
	public SqlArgBinderFactory(
		IParam Param
		,ITable? Tbl=null
		,str? CodeCol = null
	){
		this.Param = Param;
		this.Tbl = Tbl;
		this.CodeCol = CodeCol;
	}
	[Doc(@$"
#Sum[Create binder for one fixed value]
#Params([Value bound to parameter])
#TParams([Value type])
#Rtn[Auto binder instance]
")]
	public IParamAutoBinder One<TVal>(TVal Value){
		return new ParamAutoBinderOne<TVal>(Param, Value){Tbl=Tbl};
	}
	[Doc(@$"
#Sum[Create binder for a value sequence]
#Params([Sequence to bind as numbered parameters])
#TParams([Element type])
#Rtn[Auto binder instance]
")]
	public IParamAutoBinder Many<TVal>(IEnumerable<TVal> Values){
		// 同步版本委托给异步版本，通过 ToAsyncEnumerable 转换
		return Many(Values.ToAsyncEnumerable());
	}
	
	[Doc(@$"
#Sum[Create binder for an async value sequence]
#Params([Async sequence to bind as numbered parameters])
#TParams([Element type])
#Rtn[Auto binder instance]
")]
	public IParamAutoBinder Many<TVal>(IAsyncEnumerable<TVal> Values){
		return new ParamAutoBinderManyAsy<TVal>(Param, Values){Tbl=Tbl};
	}

}

