using System.Collections;

namespace Tsinswreng.CsSql;
/// Binder for prebuilt value sequence; supports incremental batch consumption.
public class ParamAutoBinderManyValues<TVal>: IParamAutoBinderMulti{
	[Doc(@$"Declared Parameter")]
	public IParam Param { get; set; }
	[Doc(@$"Received Arguments")]
	public IEnumerable<TVal> Args { get; set; }
	protected IEnumerator<TVal> ArgsItor{
		get{
			field ??= Args.GetEnumerator();
			return field;
		}
	}
	public ITable? Tbl { get; set; }
	/// 值所屬的 Code 列名:非空時按列 Upper→Raw(精確列轉換);空時退回按類型默認映射(單參重載)。
	public str? CodeCol { get; set; }
	
	
	public ParamAutoBinderManyValues(IParam Param, IEnumerable<TVal> Args){
		this.Param = Param;
		this.Args = Args;
	}

	[Doc(@$"
#Sum[Bind all values in sequence]
#Params([Argument dictionary])
#Rtn[Void]
")]
	public void Bind(IArgDict Args){
		foreach(var (i, value) in this.Args.Index()){
			var p = Param.ToOfst((u64)i);
			Args.AddRaw(p, ToRaw(value));
		}
	}



	[Doc(@$"
#Sum[Take next N values from sequence]
#Params([Maximum items to take],[Taken values])
#Rtn[True when at least one value is taken]
")]
	public bool TryTakeBatchArgs(u64 BatchSize, out IList Batch){
		var args = new List<TVal>();
		var argsItor = ArgsItor;
		// Pull values lazily; do not materialize full source sequence.
		for(u64 i = 0; i < BatchSize; i++){
			if(!argsItor.MoveNext()){
				break;
			}
			args.Add(argsItor.Current);
		}
		Batch = args;
		return args.Count > 0;
	}

	[Doc(@$"
#Sum[Bind a pre-taken value batch]
#Params([Argument dictionary],[Batch values])
#Rtn[Void]
#Throw[{nameof(InvalidCastException)}][When batch element type does not match {nameof(TVal)}]
")]
	public void BindBatch(IArgDict Args, IList Batch){
		var list = new List<TVal?>(Batch.Count);
		foreach(var item in Batch){
			if(item is null){
				// Many 值序列與 Bind / TryTakeBatchArgs 同語義:列值容 null(可空列、缺省列)。
				// null 對 TVal 是否合法由「該批是否能以 null 標識」決定:引用型 / 可空值型一律放行,
				// 僅非空值型(TVal=struct 且不可空)為上游拼 SQL 錯誤,保留抛錯兜底。
				if(!typeof(TVal).IsValueType || Nullable.GetUnderlyingType(typeof(TVal)) is not null){
					list.Add(default);
					continue;
				}
				throw new InvalidCastException($"Expected batch item type {typeof(TVal).Name}, got null.");
			}
			if(item is not TVal typed){
				throw new InvalidCastException($"Expected batch item type {typeof(TVal).Name}, got {item.GetType().Name}.");
			}
			list.Add(typed);
		}
		foreach(var (i, value) in list.Index()){
			var p = Param.ToOfst((u64)i);
			Args.AddRaw(p, value is null ? null : ToRaw(value));
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

