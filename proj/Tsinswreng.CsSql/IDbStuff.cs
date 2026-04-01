namespace Tsinswreng.CsSql;

[Doc(@$"Collection of things of a specialized Database Type(e,g Sqlite, Postgres)")]
public interface IDbStuff:I_DbSrcType{
	[Doc(@$"Sql string Maker")]
	public ISqlMkr SqlMkr{get;set;}
	
	[Doc(@$"Database Value Converter")]
	public IDbValConvtr DbValConvtr{get;set;}
	
	[Doc(@$"Batch Option")]
	public IOptBatch DfltOptBatch{get;set;}
}
