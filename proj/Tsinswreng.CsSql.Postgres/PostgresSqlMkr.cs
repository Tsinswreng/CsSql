namespace Tsinswreng.CsSql.Postgres;

public partial class PostgresSqlMkr
	:ISqlMkr
{
	protected static PostgresSqlMkr? _Inst = null;
	public static PostgresSqlMkr Inst => _Inst??= new PostgresSqlMkr();
	public ISqlTypeMapper SqlTypeMapper{get;set;} = PostgresTypeMapper.Inst;
	public EDbSrcType DbSrcType => EDbSrcType.Postgres;

	public str Quote(str Name){
		return "\"" + Name + "\"";
	}

	[Obsolete]
	public str PrmStr(str Name){
		return "@" + Name;
	}

	public IParam Param(str Name){
		var R = new Param(Name, PostgresParamPrefix.Inst);
		return R;
	}

	public str LimOfst(str? Lim, str? Ofst){
		var R = "";
		if(Lim is not null){
			R += " LIMIT " + Lim + " ";
		}
		if(Ofst is not null){
			R += " OFFSET " + Ofst + " ";
		}
		return R;
	}
}
