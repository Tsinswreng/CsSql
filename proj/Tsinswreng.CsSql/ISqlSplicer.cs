namespace Tsinswreng.CsSql;

using System.Linq.Expressions;
using System.Collections;
using Tsinswreng.CsTools;
using Tsinswreng.CsPage;
using IStr_Any = System.Collections.Generic.IDictionary<str, obj?>;

[Doc(@$"when you call the splicer apis,
you must keep the order the same as the sql syntax.
e.g you must keep `select` before `from`
")]
public partial class ISqlSplicer<E>: IAutoBindSqlDuplicator{
	public ITable Tbl{get;set;}
	public IList<obj> Segs{get;set;} = [];
	public IList<IParamAutoBinder> ParamAutoBinders { get; set; } = [];
	public IDictionary<object, object> SharedManyCtx { get; set; }
		= new Dictionary<object, object>(new RefEqComparer());


	//變 實體
	public ISqlSplicer<T2> T<T2>(ITable<T2> Tbl2){
		return new SqlSplicer<T2>(){Tbl=Tbl2};
	}

	public ISqlSplicer<T2> T<T2>(ITable Tbl2){
		return new SqlSplicer<T2>(){Tbl=Tbl2};
	}

	ISqlSplicer<E> AddSeg(str Seg){
		Seg = " "+Seg+" ";
		Segs.Add(Seg);
		return this;
	}

	ISqlSplicer<E> AddSeg(IParam P){
		Segs.Add(" ");
		Segs.Add(P);
		Segs.Add(" ");
		return this;
	}

	/// s -> "s"
	str Qt(str s){
		return Tbl.Qt(s);
	}

	// CodeCol -> "DbCol"
	str QtCol(str s){
		return Tbl.QtCol(s);
		//return Tbl.Qt(Tbl.DbTblName+"."+Tbl.ColNameToDb(s));
	}
	// CodeCol -> "Tbl"."DbCol"
	str QtTblCol(str s){
		return Tbl.Qt(Tbl.DbTblName)+"."+QtCol(s);
	}

	/// p -> @p
	IParam Prm(str s){
		//TODO 若褈則改名併返改後者
		return Tbl.Prm(s);
	}

	/// x=>x.Memb -> Memb (string)
	str Memb<T2>(Expression<Func<T2, obj?>> Memb){
		return ToolExpr.GetMemberName(Memb);
	}

	/// x=>x.Memb -> Tbl.Memb
	str TblWithMemb<T2>(Expression<Func<T2, obj?>> ExprMemb){
		return Tbl.DbTblName+"."+Memb(ExprMemb);
	}

	/// x=>x.Memb -> "Tbl"."Memb"
	str QtTblWithMemb<T2>(Expression<Func<T2, obj?>> ExprMemb){
		//Tbl.Qt(TblWithMemb(ExprMemb)); 此謬。如"Tbl.Field"實示 完整ʹ字段。璫用"Tbl"."Field"
		return Tbl.Qt(Tbl.DbTblName)+"."+Tbl.Qt(Memb(ExprMemb));
	}

	public ISqlSplicer<E> Raw(str Raw){
		return AddSeg(Raw);
	}
	public ISqlSplicer<E> Select(str Raw){
		return AddSeg($"SELECT {Raw}");
	}

	public ISqlSplicer<E> Select(Expression<Func<E, obj?>> GetMember){
		var memb = Memb(GetMember);
		//return AddSeg($"SELECT {Qt(Tbl.DbTblName+"."+Tbl.ColNameToDb(memb))} AS {memb}");
		var seg = "SELECT"
		+Qt(Tbl.DbTblName)
		+"."
		+Qt(Tbl.DbColName(memb))
		+" AS "
		+Qt(memb);
		return AddSeg(seg);
	}

	public ISqlSplicer<E> FromT(){
		AddSeg($"FROM {Qt(Tbl.DbTblName)}");
		return this;
	}

	// 注:填其他表名的帶參版(From(str Raw))暫不提供,等真實跨表/schema/別名場景出現再給;
	// 過渡期用 Raw($"FROM {x}") 兜底。

	public ISqlSplicer<E> Where1(){
		return AddSeg($"WHERE 1=1");
	}
	public ISqlSplicer<E> WhereNonDel(){
		ITable t = Tbl;
		return AddSeg("WHERE "+Tbl.SqlIsNonDel());
	}
	public ISqlSplicer<E> Where(str Raw){
		return AddSeg($"WHERE {Raw}");
	}

	public ISqlSplicer<E> And(){
		return AddSeg("AND");

	}

	public ISqlSplicer<E> Or(){
		return AddSeg("OR");
	}

	public ISqlSplicer<E> And(str Raw){
		return AddSeg($"AND {Raw}");
	}
	public ISqlSplicer<E> Or(str Raw){
		return AddSeg($"OR {Raw}");
	}
	public ISqlSplicer<E> Not(str Raw){
		return AddSeg($"NOT ({Raw})");

	}

	public ISqlSplicer<E> Paren(str Raw){
		return AddSeg($"({Raw})");
	}

	public ISqlSplicer<E> Paren(Func<ISqlSplicer<E>, obj?> FnBlock){
		AddSeg($"\n(\n");
		FnBlock(this);
		AddSeg($"\n)\n");
		return this;
	}


	#region BindedParam
	
	public ISqlSplicer<E> Bool(
		Expression<Func<E, obj?>> GetMember
		,str Op
		,Func<SqlArgBinderFactory, IParamAutoBinder> Bind
	){
		var CodeCol = Memb(GetMember);
		return Bool(CodeCol, Op, Bind);
	}
	
	[Doc($"""
	#Examples([
	fn(x=>x.MyField, "LIKE", x=>x.One(MyArg))
	])
	""")]
	public ISqlSplicer<E> Bool(
		str CodeCol
		,str Op
		,Func<SqlArgBinderFactory, IParamAutoBinder> Bind
	){
		Bool(CodeCol, Op, out var param);
		var binder = Bind(new SqlArgBinderFactory(param, Tbl, CodeCol, SharedManyCtx));
		ParamAutoBinders.Add(binder);
		return this;
	}
	public ISqlSplicer<E> AndEq(
		Expression<Func<E, obj?>> GetMember,
		Func<SqlArgBinderFactory, IParamAutoBinder> Bind
	){
		var memb = Memb(GetMember);
		AndEq(GetMember, out var param);
		var binder = Bind(new SqlArgBinderFactory(param, Tbl, memb, SharedManyCtx));
		ParamAutoBinders.Add(binder);
		return this;
	}
	
	public ISqlSplicer<E> AndEq(
		string CodeCol,
		Func<SqlArgBinderFactory, IParamAutoBinder> Bind
	){
		var r = And().Bool(CodeCol, "=", out var param);
		var binder = Bind(new SqlArgBinderFactory(param, Tbl, CodeCol, SharedManyCtx));
		ParamAutoBinders.Add(binder);
		return r;
	}

	#endregion BindedParam

	// ================================================================
	// 通用拼接出口（2026-09-03）：
	// splicer 的一切都往 Segs 拼可見 SQL 片段（Select/FromT/Where1/And/Bool/Raw/UpdateT/Eq/PL/PR/C...），
	// 順序即語法；批量值是 binder（One/Many）的職責。這裡只補兩個不綁語義的通用件：
	// AddRaw(IParam) 把參數佔位放進片段（Upper→Raw 由 binder 做），Build 把「Segs 模板 + ParamAutoBinders」
	// 按 Many binder 公共長度展開成執行端吃的 ISqlEtArg（Sql = N 份語句 ';' 拼、Args 對序後綴全程唯一）。
	// ================================================================

	/// 把 IParam 參數佔位按序拼進片段（SET 值位 / IN 列表 / VALUES 值位...）。Upper→Raw 由對應 binder 完成。
	public ISqlSplicer<E> AddRaw(IParam P){
		return AddSeg(P);
	}

	/// 語句頭片段:INSERT INTO {Tbl 表名}(T 表示自動填表名,與 UpdateT/DelFromT/FromT 同規則)。
	public ISqlSplicer<E> InsertIntoT(){
		return AddSeg($"INSERT INTO {Qt(Tbl.DbTblName)}");
	}

	/// 語句頭片段:DELETE FROM {Tbl 表名}(T 表示自動填表名;跨表/別名等填其他表名場景用到再給帶參版)。
	public ISqlSplicer<E> DelFromT(){
		return AddSeg($"DELETE FROM {Qt(Tbl.DbTblName)}");
	}

	// ================================================================
	// 語句部 Decl（與 SELECT 系同族的語塊）：
	// 每個方法 = 一段可見 SQL 片段，按語法順序拼；值收整批（列枚舉/循環/binder 全在庫內），
	// 批量 = Build 按各 Many binder 公共長度展開 N 份、參數對序後綴。
	// 同構（Vals/Set）收 CodeDicts（code 層，binder 帶 Tbl 自動 Upper→Raw）；
	// 異構（UpdEach）收 DbDicts（已 raw，binder 不帶 Tbl 避免二次轉換）。
	// ================================================================

	[Doc(@$"
	#Sum[INSERT 值部片段（同構批量）]
	#Params([Code 列名集合],[整批 CodeDicts（鍵=Code 列名、值=Code 層）])
	#Rtn[this]
	#Note[拼出 `(a, b) VALUES (@a, @b)` 並為每列註冊 Many binder（值序列=整批該列）；Build 按行數展開 N 份]
	")]
	public partial ISqlSplicer<E> Vals(
		IList<str> CodeCols
		,IList<IStr_Any> CodeDicts
	);

	[Doc(@$"
	#Sum[UPDATE SET 子句片段（同構批量）]
	#Params([Code 列名集合（SET 列，通常排除主鍵）],[整批 CodeDicts（鍵=Code 列名、值=Code 層）])
	#Rtn[this]
	#Note[拼出 `SET a = @a, b = @b`（含 SET 詞頭）並為每列註冊 Many binder（值序列=整批該列）；配合 UpdateT()/Where1()/And().Bool(...) 使用]
	")]
	public partial ISqlSplicer<E> Set(
		IList<str> CodeCols
		,IList<IStr_Any> CodeDicts
	);

	[Doc(@$"
	#Sum[UPDATE SET 單列單值（語句部）]
	#Params([Code 列名],[Db 層 raw 值（不帶 Tbl、不再 Upper→Raw；如軟刪值 = SoftDelCol.FnDelete(null) 的結果）])
	#Rtn[this]
	#Note[拼出 `SET {{col}} = @{{col}}` 並註冊 One binder（值視為已 raw）；配合 UpdateT()/WhereIn(...) 使用]
	")]
	public partial ISqlSplicer<E> Set(
		str CodeCol
		,obj? RawVal
	);

	[Doc(@$"
	#Sum[異構字典 UPDATE（每對 SET 列集可不同）]
	#Params([主鍵 Code 列名],[主鍵值列表（code 層，庫內 Upper→Raw）],[整批 DbDicts（鍵=Db 列名、值=已 raw）])
	#Rtn[this]
	#Note[逐對生成 `UPDATE t SET 列=@u_{{i}}_{{j}}... WHERE CodeId=@id_{{i}}`，對間 ';' 拼一命令（原 BatOrdUpdByDbDictCore 形態）；空對跳過、主鍵列永不出現在 SET]
	")]
	public partial ISqlSplicer<E> UpdEach(
		str CodeIdName
		,IList<obj?> Ids
		,IList<IStr_Any> DbDicts
	);

	[Doc(@$"
	#Sum[WHERE 列 IN 值列表（語句部）]
	#Params([Code 列名],[值列表（code 層，庫內 Upper→Raw）])
	#Rtn[this]
	#Note[拼出 `WHERE {{col}} IN (@_0,@_1...)` 並為每個值註冊 One binder（帶 Tbl+CodeCol 轉換）；語詞對應裸 SQL——軟刪=UpdateT().Set(軟刪列,raw).WhereIn(...)、硬刪=DelFromT().WhereIn(...)]
	")]
	public partial ISqlSplicer<E> WhereIn(
		str CodeCol
		,IList<obj?> UpperVals
	);

	/// 產出執行端入參：Sql = 模板按各 Many binder 公共長度展開 N 份 ';' 拼（無 Many 則 1 份），
	/// Args = One 綁無後綴名一次 + Many 綁 @name__0..@name__N-1（對序後綴）。
	public partial ISqlEtArg Build();

	public ISqlSplicer<E> OrderBy(str Raw){
		AddSeg($"ORDER BY {Raw}");
		return this;
	}
	
	public ISqlSplicer<E> OrderBy(IList<str> Raws){
		var Raw = string.Join(", ", Raws);
		AddSeg($"ORDER BY {Raw}");
		return this;
	}

	public ISqlSplicer<E> OrderByDesc(str Raw){
		AddSeg($"ORDER BY {Raw} DESC");
		return this;
	}
	
	[Doc(@$"Only support one column for order by.")]
	public ISqlSplicer<E> OrderByDesc(Expression<Func<E, obj?>> GetMember){
		OrderByDesc(QtCol(Memb(GetMember)));
		return this;
	}

	[Doc(@$"Bind")]
	public ISqlSplicer<E> LimOfst(IPageQry Qry){
		var r = LimOfst(out var lim, out var ofst);
		ParamAutoBinders.Add(new SqlArgBinderFactory(lim, Tbl).One(Qry.PageSize));
		ParamAutoBinders.Add(new SqlArgBinderFactory(ofst, Tbl).One(Qry.Offset_()));
		return r;
	}
	
	public ISqlSplicer<E> Lim(u64 Limit){
		var seg = Tbl.SqlDialect.LimOfst(Limit+"", null);
		return AddSeg(seg);
	}

	public ISqlSplicer<E> Set(){
		return AddSeg("SET");
	}


	public ISqlSplicer<E> Eq(str Left, str Right){
		return AddSeg(Left).AddSeg("=").AddSeg(Right);
	}

	public ISqlSplicer<E> Eq(Expression<Func<E, obj?>> ExprMemb, str Right){
		return Eq(QtTblWithMemb(ExprMemb), Right);
	}

	///UPDATE {Qt(Tbl.DbTblName)}(T 表示自動填表名;SET 詞歸 Set 語部:UpdateT().Set(...))
	public ISqlSplicer<E> UpdateT(){
		return AddSeg($"UPDATE {Qt(Tbl.DbTblName)}");
	}

	public ISqlSplicer<E> With(str Raw){
		return AddSeg("WITH").AddSeg(Raw);
	}
	
	[Doc(@$"`AS`")]
	public ISqlSplicer<E> As(){
		return AddSeg("AS");
	}

	[Doc(@$"`,`")]
	public ISqlSplicer<E> C(){
		return AddSeg(", ");
	}
	[Doc(@$"`(`")]
	public ISqlSplicer<E> PL(){
		return AddSeg("(");
	}
	[Doc(@$"`)`")]
	public ISqlSplicer<E> PR(){
		return AddSeg(")");
	}

	[Impl]
	[Doc($@"
#Sum[Generate repeated SQL statements]
#Params([Repeat count])
#Rtn[String containing multiple SQL statements, each ending with a semicolon]
#See([{nameof(ToSqlStrAtOfst)}],[{nameof(IParam.ToOfst)}])
")]
	public str DuplicateSql(u64 Cnt){
		var R = new List<str>();
		for(u64 i=0;i<Cnt;i++){
			R.Add(ToSqlStrAtOfst(i));
			R.Add(";");
		}
		return string.Join("", R);
	}

	[Impl]
	[Doc($@"
#Sum[Convert SQL segments to SQL string with offset]
#Params([Parameter offset])
#Rtn[Complete SQL string where all parameters are adjusted to the specified offset]
#See([{nameof(DuplicateSql)}],[{nameof(IParam.ToOfst)}])
")]
	public str ToSqlStrAtOfst(u64 Ofst){
		var L = new List<str>();
		foreach(var seg in Segs){
			if(seg is IParam p){
				L.Add(p.ToOfst(Ofst)+"");
			}else if(seg is str s){
				L.Add(s);
			}
		}
		return string.Join("", L);
	}


	public str ToSqlStr(u64 RepeatCnt = 1){
		return DuplicateSql(RepeatCnt);
	}

	// public str ToSqlStr(IDbFnCtx Ctx){
	// 	return DuplicateSql(Ctx.BatchSize);
	// }

}
public class ISqlSplicer: ISqlSplicer<obj>{}
public class SqlSplicer<T>:ISqlSplicer<T>{}

public class SqlSplicer:ISqlSplicer{

}
