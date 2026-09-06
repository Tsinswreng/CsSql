namespace Tsinswreng.CsSql;

/// Factory that creates auto-binders bound to one SQL parameter.

public class SqlArgBinderFactory{
	public IParam Param { get; set; }
	public ITable? Tbl{get;set;}
	//TODO 铏曠悊CodeCol 寰屽彲鐢ㄤ簬绮剧窗UpperToRaw椤炲瀷杞夋彌
	public str? CodeCol{get;set;}
	public IDictionary<object, object>? SharedManyCtx { get; set; }
	public SqlArgBinderFactory(
		IParam Param
		,ITable? Tbl=null
		,str? CodeCol = null
		,IDictionary<object, object>? SharedManyCtx = null
	){
		this.Param = Param;
		this.Tbl = Tbl;
		this.CodeCol = CodeCol;
		this.SharedManyCtx = SharedManyCtx;
	}
	[Doc(@$"
#Sum[Create binder for one fixed value]
#Params([Value bound to parameter])
#TParams([Value type])
#Rtn[Auto binder instance]
")]
	public IParamAutoBinder One<TVal>(TVal Value){
		return new ParamAutoBinderOne<TVal>(Param, Value){Tbl=Tbl, CodeCol=CodeCol};
	}
	[Doc(@$"
#Sum[Create binder for a value sequence]
#Params([Sequence to bind as numbered parameters])
#TParams([Element type])
#Rtn[Auto binder instance]
")]
	public IParamAutoBinder Many<TVal>(IEnumerable<TVal> Values){
		// 同步值序列直接用同步 binder：供 Build() 一次性全量展开（异步流式才走 Many(IAsyncEnumerable)）
		return new ParamAutoBinderManyValues<TVal>(Param, Values){Tbl=Tbl, CodeCol=CodeCol};
	}
	
	[Doc(@$"
#Sum[Create binder for an async value sequence]
#Params([Async sequence to bind as numbered parameters])
#TParams([Element type])
#Rtn[Auto binder instance]
")]
	public IParamAutoBinder Many<TVal>(IAsyncEnumerable<TVal> Values){
		return new ParamAutoBinderManyAsy<TVal>(Param, Values){Tbl=Tbl, CodeCol=CodeCol};
	}

	[Doc(@$"
#Sum[Create binder from shared async source with per-item projection]
#Params([Async source],[Projection selector])
#TParams([Source item type],[Projected value type])
#Rtn[Auto binder instance]
#Note[Binders created from the same source in one splicer share single-pass consumption]
")]
	public IParamAutoBinder Many<TItem, TVal>(
		IAsyncEnumerable<TItem> Values,
		Func<TItem, TVal> FnMap
	){
		if(SharedManyCtx is null){
			return Many(Values.Select(FnMap));
		}

		if(!SharedManyCtx.TryGetValue(Values, out var groupObj)){
			groupObj = new SharedManyAsyMapBatchSource<TItem>(Values);
			SharedManyCtx.Add(Values, groupObj);
		}
		if(groupObj is not SharedManyAsyMapBatchSource<TItem> group){
			throw new InvalidOperationException("Many(source, selector) source type mismatch in shared binder context.");
		}

		var idx = group.RegisterBinder();
		return new ParamAutoBinderManyAsyMap<TItem, TVal>(
			Param,
			group,
			idx,
			FnMap
		){Tbl = Tbl, CodeCol = CodeCol};
	}

	[Doc(@$"
#Sum[Create binder from shared sync source with per-item projection]
#Params([Source],[Projection selector])
#TParams([Source item type],[Projected value type])
#Rtn[Auto binder instance]
")]
	public IParamAutoBinder Many<TItem, TVal>(
		IEnumerable<TItem> Values,
		Func<TItem, TVal> FnMap
	){
		return Many(Values.ToAsyncEnumerable(), FnMap);
	}

}
