namespace Tsinswreng.CsSql;
/// Binder for one fixed value.
public class ParamAutoBinderOne<TVal>: IParamAutoBinderOneBatch{
	public IParam Param { get; set; }
	public TVal Value { get; set; }
	public ITable? Tbl { get; set; }

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
		if(Tbl != null){
			Args.AddRaw(Param, Tbl.UpperToRaw(Value));
			return;
		}
		Args.AddRaw(Param, Value);
	}

	[Doc(@$"
#Sum[Bind one fixed value for each duplicated SQL statement]
#Params([Argument dictionary],[Duplicated statement count])
#Rtn[Void]
")]
	public void BindBatch(IArgDict Args, u64 RepeatCnt){
		for(u64 i = 0; i < RepeatCnt; i++){
			var p = Param.ToOfst(i);
			if(Tbl != null){
				Args.AddRaw(p, Tbl.UpperToRaw(Value));
			}else{
				Args.AddRaw(p, Value);
			}
		}
	}
}

