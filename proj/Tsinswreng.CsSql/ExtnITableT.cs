namespace Tsinswreng.CsSql;

using System.Linq.Expressions;
using Tsinswreng.CsCore;
using Tsinswreng.CsPage;
using Tsinswreng.CsTools;
using IStr_Any = System.Collections.Generic.IDictionary<str, obj?>;
using Str_Any = System.Collections.Generic.Dictionary<str, obj?>;
public static partial class ExtnITableT{
	extension<T>(ITable<T> z)
		where T:new()
	{
		//"db_col_name"  不帶表名前綴
		public str DbCol(Expression<Func<T, obj?>> ExprMemb){
			var t = (ITable)z;
			var memb = ToolExpr.GetMemberName(ExprMemb);
			return t.DbColName(memb);
		}

		public IDbField QtCol(Expression<Func<T, obj?>> ExprMemb){
			var t = (ITable)z;
			return t.QtCol<T>(ExprMemb);
		}

		[Doc(@$"Get the member name of the expression,
		which is the code col, not mapped to db col.
		")]
		public str Memb(Expression<Func<T, obj?>> ExprMemb){
			var t = (ITable)z;
			return t.Memb(ExprMemb);
		}

		public ISqlSplicer<T> SqlSplicer(){
			var t = (ITable)z;
			return t.SqlSplicer<T>();
		}

		public T DbDictToEntity(
			IStr_Any DbDict
		){
			var t = (ITable)z;
			return t.DbDictToEntity<T>(DbDict);
		}

	}
}
