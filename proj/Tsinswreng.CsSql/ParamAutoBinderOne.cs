namespace Tsinswreng.CsSql;
/// Binder for one fixed value.
public class ParamAutoBinderOne<TVal>: IParamAutoBinderOneBatch{
	public IParam Param { get; set; }
	public TVal Value { get; set; }
	public ITable? Tbl { get; set; }
	/// 值所屬的 Code 列名:非空時按列 Upper→Raw(精確列轉換);空時退回按類型默認映射(單參重載)。
	public str? CodeCol { get; set; }

	public ParamAutoBinderOne(IParam Param, TVal Value){
		this.Param = Param;
		this.Value = Value;
	}

	[Doc(@$"
#Sum[Bind one fixed value]
#Params([Argument dictionary])
#Rtn[Void]
")]
	public void Bind(IArgDict Args){
		Args.AddRaw(Param, ToRaw(Value));
	}

	[Doc(@$"
#Sum[Bind one fixed value for each duplicated SQL statement]
#Params([Argument dictionary],[Duplicated statement count])
#Rtn[Void]
")]
	public void BindBatch(IArgDict Args, u64 RepeatCnt){
		for(u64 i = 0; i < RepeatCnt; i++){
			var p = Param.ToOfst(i);
			Args.AddRaw(p, ToRaw(Value));
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

