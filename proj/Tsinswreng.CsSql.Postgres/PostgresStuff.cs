namespace Tsinswreng.CsSql.Postgres;
using Tsinswreng.CsSql;

public class PostgresStuff:IDbStuff{
	public static PostgresStuff Inst => field??= new PostgresStuff();
	public EDbSrcType DbSrcType{get;set;} = EDbSrcType.Postgres;
	public ISqlMkr SqlMkr{get;set;} = PostgresSqlMkr.Inst;
	public IDbValConvtr DbValConvtr{get;set;} = PostgresValConvtr.Inst;
	public IOptBatch DfltOptBatch{get;set;} = new OptBatch(){
		
	};
}
