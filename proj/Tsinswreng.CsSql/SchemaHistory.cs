namespace Tsinswreng.CsSql;

using Tsinswreng.Srefl;


/// 遷移表實體類
public partial class SchemaHistory{
	public static SchemaHistory Sample = new();
	/// 主鍵。用插入旹之毫秒時間戳
	public i64 Id{get;set;} = DateTimeOffset.Now.ToUnixTimeMilliseconds();
	/// 非 被應用之時
	public i64 CreatedMs{get;set;} = DateTimeOffset.Now.ToUnixTimeMilliseconds();
	public str? Name{get;set;}
	public str? Descr{get;set;}
	public i64 ProductVersionTime{get;set;} = LibVersion.Time;

}



[StrAccType(typeof(SchemaHistory))]
public partial class SqlHelperStrAcc{
	protected static SqlHelperStrAcc? _Inst = null;
	public static SqlHelperStrAcc Inst => _Inst??= new SqlHelperStrAcc();
}

public partial class SchemaHistoryTblMkr{
	public str TblName = "__TsinswrengSchemaHistory";
	public ITable MkTbl(){
		ITable R = Table.Mk<SchemaHistory>(SqlHelperStrAcc.Inst, TblName);
		R.Col(nameof(SchemaHistory.Id)).AdditionalSqls(["PRIMARY KEY"]);
		return R;
	}

}
