using System.Linq.Expressions;
using Tsinswreng.CsTools;

namespace Tsinswreng.CsSql;

[Doc(@$"
should be pure function, should return strings instead of directly modifying {nameof(ITable.OuterAdditionalSqls)}
#Rtn[strings to add in {nameof(ITable.OuterAdditionalSqls)} to create indexes]
")]
public delegate IList<str> FnSetIdx(
	IOptMkIdx? Opt
	,ITable Tbl
	,params IEnumerable<str>[] Cols
);

public interface ITblSetter<T>{
	public ITable<T> Tbl{get;set;}
	[Doc($"""
	define index for table using strings of field names in code.
	#Params([Options])
	#Examples([
		using E = MyEntity;
		fn(null, [nameof(E.Key)], [(nameof(E.Owner)), nameof(E.Head)]) ->
		
		means create two indexs, one is (E.Key), the other is (E.Owner, E.Head)

	])
	""")]
	public FnSetIdx FnSetIdx{get;set;}
	public ITable<T> Idx(
		IOptMkIdx? Opt,
		params IEnumerable<str>[] Cols
	);
	
	[Doc($$"""
	define index for table using member expressions.
	#Examples([
	fn(null
		,x=>x.Key
		,x=>new{x.Owner, x.Head}
	)
	means create two indexs, one is (E.Key), the other is (E.Owner, E.Head)
	])
	""")]
	public ITable<T> IdxExpr(
		IOptMkIdx? Opt,
		params Expression<Func<T,obj?>>[] Exprs
	);
	
}

public class TblSetter<T>:ITblSetter<T>{
	public TblSetter(ITable<T> Tbl){
		this.Tbl = Tbl;
		this.FnSetIdx = DefaultFnSetIdx;
	}
	public ITable<T> Tbl{get;set;}
	public FnSetIdx FnSetIdx{get;set;}
	
	/// Default implementation for FnSetIdx - pure function that only generates SQL
	public IList<str> DefaultFnSetIdx(
		IOptMkIdx? Opt
		, ITable Tbl
		, params IEnumerable<str>[] Cols
	) {
		var results = new List<str>();
		
		foreach (var colSet in Cols) {
			var colList = colSet.ToList();
			var isUnique = Opt?.Unique ?? false;
			var whereCondition = str.IsNullOrEmpty(Opt?.Where) ? null : new[] { Opt.Where };
			
			// Generate index name: Idx_{TableName}_{Col1}_{Col2}... or Ux_{TableName}_{Col1}_{Col2}...
			var prefix = isUnique ? "Ux" : "Idx";
			var colNamePart = string.Join("_", colList);
			var indexName = $"{prefix}_{Tbl.DbTblName}_{colNamePart}";
			
			var sql = MkIndexSqlByCodeCols(indexName, colList, isUnique, whereCondition);
			results.Add(sql);
		}
		
		return results;
	}
	
	/// Define index using member expressions
	public ITable<T> IdxExpr(
		IOptMkIdx? Opt,
		params Expression<Func<T, obj?>>[] Exprs
	) {
		var colSets = new List<IEnumerable<str>>();
		foreach (var expr in Exprs) {
			var colNames = ToolExpr.GetMemberNames(expr);
			colSets.Add(colNames);
		}
		return Idx(Opt, colSets.ToArray());
	}

	/// Define index using code column names
	public ITable<T> Idx(
		IOptMkIdx? Opt,
		params IEnumerable<str>[] Cols
	) {
		var sqls = FnSetIdx(Opt, Tbl, Cols);
		foreach (var sql in sqls) {
			Tbl.OuterAdditionalSqls.Add(sql);
		}
		return Tbl;
	}

	/// Build CREATE INDEX SQL using code column names
	public str MkIndexSqlByCodeCols(
		str IndexName
		, IEnumerable<str> CodeColNames
		, bool IsUnique = false
		, IEnumerable<str>? WhereAnds = null
	) {
		var flds = new List<str>();
		foreach (var codeCol in CodeColNames) {
			flds.Add(Tbl.QtCol(codeCol));
		}

		var unique = IsUnique ? "UNIQUE " : "";
		var sql =
		$"""
CREATE {unique}INDEX {Tbl.Qt(IndexName)}
ON {Tbl.Qt(Tbl.DbTblName)} ({str.Join(", ", flds)})
""";

		if (WhereAnds != null) {
			var conds = new List<str>();
			foreach (var cond in WhereAnds) {
				if (!str.IsNullOrWhiteSpace(cond)) {
					conds.Add(cond);
				}
			}
			if (conds.Count > 0) {
				sql += "\nWHERE " + str.Join("\nAND ", conds);
			}
		}

		return sql;
	}

	/// Build and append CREATE INDEX SQL to OuterAdditionalSqls
	public ITable<T> AddIndexByCodeCols(
		str IndexName
		, IEnumerable<str> CodeColNames
		, bool IsUnique = false
		, IEnumerable<str>? WhereAnds = null
	) {
		var sql = MkIndexSqlByCodeCols(IndexName, CodeColNames, IsUnique, WhereAnds);
		Tbl.OuterAdditionalSqls.Add(sql);
		return Tbl;
	}
}

public static class ExtnITblSetter {
	extension<TEntity>(ITblSetter<TEntity> z){
		public ColMkr<TEntity, obj?, obj?> Col(
			str NameInCode
		){
			var t = z.Tbl;
			var col = t.GetCol(NameInCode);
			var R = new ColMkr<TEntity, obj?, obj?>();
			R.TableT = t;
			R.Table = t;
			R.Column = col;
			return R;
		}

		public ColMkr<TEntity, obj?, obj?> Col(
			Expression<Func<TEntity, obj?>> ExprMemb
		){
			var memb = ToolExpr.GetMemberName(ExprMemb);
			return z.Col<TEntity>(memb);
		}

		// public ColMkr<TEntity, TRaw, TUpper> Col<TRaw, TUpper>(
		// 	str NameInCode
		// ){
		// 	var col = z.GetCol(NameInCode);
		// 	var R = new ColMkr<TEntity, TRaw, TUpper>();
		// 	R.TableT = z;
		// 	R.Column = col;
		// 	return R;
		// }

	}
}
