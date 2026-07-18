using System.Linq.Expressions;
using System.Collections;
using Tsinswreng.CsTools;
using Tsinswreng.CsPage;
namespace Tsinswreng.CsSql;

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

	public ISqlSplicer<E> From(){
		AddSeg($"FROM {Qt(Tbl.DbTblName)}");
		return this;
	}

	public ISqlSplicer<E> From(str Raw){
		AddSeg($"FROM {Raw}");
		return this;
	}

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
		var seg = Tbl.SqlMkr.LimOfst(Limit+"", null);
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

	///UPDATE {Qt(Tbl.DbTblName)} SET
	public ISqlSplicer<E> UpdateSet(){
		return AddSeg($"UPDATE {Qt(Tbl.DbTblName)} SET");
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
