namespace Tsinswreng.CsSql;

using Tsinswreng.CsRefl;


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



/// `SchemaHistory` 的型別元資料來源：它是 CsSql 內建輔助表，用反射來源即可，不必登記。
public partial class CsSqlTypeInfo{
	protected static CsSqlTypeInfo? _Inst = null;
	/// 單例；`SchemaHistory` 的型別元資料靠它取。
	public static CsSqlTypeInfo Inst => _Inst??= new CsSqlTypeInfo();
	/// 反射來源；用庫內單例，元資料會按型別緩存，故不必每次重建。
	public ITypeInfoSrc TypeInfoSrc{get;} = ReflTypeInfoSrc.Inst;
}

public partial class SchemaHistoryTblMkr{
	public str TblName = "__TsinswrengSchemaHistory";
	public ITable MkTbl(){
		ITable R = Table.Mk<SchemaHistory>(CsSqlTypeInfo.Inst.TypeInfoSrc, TblName);
		R.Col(nameof(SchemaHistory.Id)).AdditionalSqls(["PRIMARY KEY"]);
		return R;
	}

}
