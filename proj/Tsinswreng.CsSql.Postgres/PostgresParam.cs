namespace Tsinswreng.CsSql.Postgres;

public class PostgresParamPrefix:I_AddParamPrefix{
	protected static PostgresParamPrefix? _Inst = null;
	public static PostgresParamPrefix Inst => _Inst??= new PostgresParamPrefix();
	public str AddParamPrefix(str Name){
		return "@"+Name;
	}
}
