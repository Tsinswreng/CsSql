using Tsinswreng.CsCore;

namespace Tsinswreng.CsSql;

//TODO配置忽略之字段
public partial interface ITblMgr{
	/// (ITable is ITable<Type>) should be true at runtime,
	/// which means you should register with ITable<> instead of ITable
	public IDictionary<Type, ITable> EntityType_Tbl{get;set;}
	public IDictionary<Type, IAggReg> AggType_Reg{get;set;}
	public EDbSrcType DbSrcType=>DbStuff.DbSrcType;
	public IDbStuff DbStuff{get;set;}
	public ISqlDialect SqlDialect=>DbStuff.SqlDialect;

	public nil AddTbl(ITable Tbl){
		Tbl.TblMgr = this;
		EntityType_Tbl[Tbl.CodeEntityType] = Tbl;
		return NIL;
	}

	public ITable<TPo> GetTbl<TPo>(){
		if(!EntityType_Tbl.TryGetValue(typeof(TPo), out var T)){
			throw new Exception($"GetTbl<> Failed. {typeof(TPo)} is not registered.");
		}
		var R = T as ITable<TPo>;
		if(R is null){
			throw new Exception($"Table is not generic ITable<{typeof(TPo)}> ");
		}
		return R;
	}

	public nil AddAgg(IAggReg Reg){
		AggType_Reg[Reg.AggType] = Reg;
		return NIL;
	}

	public IAggReg GetAgg(Type AggType){
		if(!AggType_Reg.TryGetValue(AggType, out var R)){
			throw new Exception($"GetAgg() Failed. {AggType} is not registered.");
		}
		return R;
	}

	public IAggReg GetAgg<TAgg>(){
		return GetAgg(typeof(TAgg));
	}
}
