namespace Tsinswreng.CsSql;

public partial interface IArgDict:IDictionary<str,obj>{
	public IDictionary<str, obj?> ParamName_RawValue{get;set;}
	[Doc(@$"
	#Params([],[Raw param name, without prefix(like `@` in sqlite's sql)])
	")]
	public IArgDict AddRaw(str ParamName, obj? Raw);
	public IArgDict AddRaw(IParam Param, obj? Raw);
	public IArgDict AddT<T>(IParam Param, T Raw, str? CodeColName=null);

	[Doc(@$"Batch bind multiple parameters.
	#Param[{nameof(Alt)}][if the length of {nameof(Uppers)} is not equal to {nameof(Params)}, use this value to replace missing values.]
	")]
	public IArgDict AddManyT<T>(
		IEnumerable<IParam> Params, IEnumerable<T> Uppers
		,str? CodeColName=null, obj? Alt=null
	);

	[Doc(@$"
	#See[{nameof(IParam.NumSuffixName)}]
	#See[{nameof(IParam)}.this[u64]]
	")]
	public IArgDict AddManyT<T>(
		IParam Param, IEnumerable<T> Uppers
		,str? CodeColName=null
	);
	public IDictionary<str, obj?> ToDict();
}

