using System.Collections;

namespace Tsinswreng.CsSql;
/// Binder for async value sequence; supports incremental batch consumption from async source.
public class ParamAutoBinderManyAsy<TVal>: IParamAutoBinderMultiAsy{
	[Doc(@$"Declared Parameter")]
	public IParam Param { get; set; }
	[Doc(@$"Async source sequence")]
	public IAsyncEnumerable<TVal> Args { get; set; }
	protected IAsyncEnumerator<TVal>? ArgsItor;
	public ITable? Tbl { get; set; }
	/// 值所屬的 Code 列名:非空時按列 Upper→Raw(精確列轉換);空時退回按類型默認映射(單參重載)。
	public str? CodeCol { get; set; }
	
	
	public ParamAutoBinderManyAsy(IParam Param, IAsyncEnumerable<TVal> Args){
		this.Param = Param;
		this.Args = Args;
	}

	[Doc(@$"
#Sum[Bind all values in async sequence]
#Params([Argument dictionary])
#Rtn[Void]
#Note[此方法不应在 IParamAutoBinderMultiAsync 场景中调用]
")]
	public void Bind(IArgDict Args){
		throw new NotSupportedException("Use TryTakeBatchArgsAsync for async streaming");
	}

	[Doc(@$"
#Sum[Take next N values from async sequence]
#Params([Maximum items to take],[Cancellation token])
#Rtn[(True when at least one value is taken, Taken values)]
")]
	public async ValueTask<(bool HasAny, IList Batch)> TryTakeBatchArgsAsync(u64 BatchSize, CT Ct){
		var args = new List<TVal>();
		ArgsItor ??= Args.GetAsyncEnumerator(Ct);
		// 异步消费异步枚举，真正的流式处理
		for(u64 i = 0; i < BatchSize; i++){
			if(!await ArgsItor.MoveNextAsync()){
				break;
			}
			args.Add(ArgsItor.Current);
		}
		return (args.Count > 0, args);
	}

	[Doc(@$"
#Sum[Bind a pre-taken value batch]
#Params([Argument dictionary],[Batch values])
#Rtn[Void]
#Throw[{nameof(InvalidCastException)}][When batch element type does not match {nameof(TVal)}]
")]
	public void BindBatch(IArgDict Args, IList Batch){
		var list = new List<TVal>(Batch.Count);
		foreach(var item in Batch){
			if(item is not TVal typed){
				throw new InvalidCastException($"Expected batch item type {typeof(TVal).Name}, got {item?.GetType().Name ?? "null"}.");
			}
			list.Add(typed);
		}
		foreach(var (i, value) in list.Index()){
			var p = Param.ToOfst((u64)i);
			Args.AddRaw(p, ToRaw(value));
		}
	}

	/// 值 → 原始層:帶 Tbl 時執行 Upper→Raw;有 CodeCol 按列精確轉換,否則按類型默認映射。
	private obj? ToRaw(TVal Value){
		if(Tbl == null){
			return Value;
		}
		if(CodeCol != null){
			return Tbl.UpperToRaw(Value, CodeCol);
		}
		return Tbl.UpperToRaw(Value);
	}
}
