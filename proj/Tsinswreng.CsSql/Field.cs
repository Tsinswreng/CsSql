namespace Tsinswreng.CsSql;

public class Field:IDbField{
	public Field(ITable Tbl, str CodeName){
		this.Tbl = Tbl;
		this.CodeName = CodeName;
		var DbColName = Tbl.Columns[CodeName].DbName;
		this.DbName = DbColName;
		QuotedDbName = Tbl.Qt(DbName);
	}
	public ITable Tbl{get;set;}
	public str CodeName{get;set;}
	public str DbName{get;set;}
	str QuotedDbName = "";

	//"db_name"  不帶表名前綴
	public override string ToString() {
		return QuotedDbName;
	}
}
