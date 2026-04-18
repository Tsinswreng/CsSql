using System.Collections;

namespace Tsinswreng.CsSql;
/// Bind values into <see cref="IArgDict"/> for one execution.
public interface IParamAutoBinder{
	[Doc(@$"
#Sum[Bind values into argument dictionary for current execution]
#Params([Argument dictionary])
#Rtn[Void]
")]
	public void Bind(IArgDict Args);
}

/// Binder for one fixed value that can be repeated for duplicated SQL batches.
public interface IParamAutoBinderOneBatch: IParamAutoBinder{
	[Doc(@$"
#Sum[Bind one fixed value for each duplicated SQL statement in current batch]
#Params([Argument dictionary],[Duplicated statement count])
#Rtn[Void]
")]
	public void BindBatch(IArgDict Args, u64 RepeatCnt);
}

/// Binder for "Many(values)" that can stream values by batch size.
public interface IParamAutoBinderMulti: IParamAutoBinder{
	[Doc(@$"
#Sum[Take next arguments batch from internal sequence]
#Params([Expected batch size],[Output batch list])
#Rtn[True if at least one value is available]
")]
	public bool TryTakeBatchArgs(u64 BatchSize, out IList Args);
	[Doc(@$"
#Sum[Bind a taken batch into arguments]
#Params([Argument dictionary],[Batch values])
#Rtn[Void]
")]
	public void BindBatch(IArgDict Args, IList Batch);
}

