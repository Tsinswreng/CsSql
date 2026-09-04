namespace Tsinswreng.CsSql;

/// ISqlEtArg 的默認實現：SqlMkr.Build() 產出的「執行端入參材料」。
///
/// 純數據容器：SQL 文本裝好、參數表裝好就交出去，執行端只讀不寫。
public class SqlEtArg: ISqlEtArg{
	public str Sql{get;set;} = "";

	public IArgDict Args{get;set;} = ArgDict.Mk();
}