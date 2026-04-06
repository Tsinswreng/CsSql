namespace Tsinswreng.CsSql;

public partial interface ISqlMkr:I_DbSrcType{
	public ISqlTypeMapper SqlTypeMapper{get;set;}

	/// 字段加引號 如Name -> "Name"或`Name`或[Name]等
	public str Quote(str Name);
	/// 如Name -> "@Name" 等
	
	public IParam Param(str Name);

	[Doc(@$"Raw.
	if null is passed,
	there will not be the corresponding conditionin the sql.
	")]
	public str LimOfst(str? Lim, str? Ofst);

}


public static class ExtnISqlMkr{
	public static str ParamLimOfst(
		this ISqlMkr z
		,str Limit, str Offset
	){
		return z.LimOfst(z.Param(Limit)+"", z.Param(Offset)+"");
	}
	public static str ParamLimOfstStr(
		this ISqlMkr z
		,out str Lmt, out str Ofst
	){
		Lmt=nameof(Lmt);
		Ofst=nameof(Ofst);
		return z.ParamLimOfst(Lmt, Ofst);
	}

	public static str ParamLimOfst(
		this ISqlMkr z
		,out IParam Lmt, out IParam Ofst
	){
		Lmt=z.Param(nameof(Lmt));
		Ofst=z.Param(nameof(Ofst));
		return z.ParamLimOfst(Lmt.Name, Ofst.Name);
	}

	/// 直轉`=`、不支持比較 null
	public static str Eq(
		this ISqlMkr z
		,str DbColName, IParam Param
	){
		var Col = DbColName;
		//return $"({Col} = {Param} OR ({Col} IS NULL AND {Param} IS NULL))";
		return $"{z.Quote(DbColName)} = {Param}";
	}
}
