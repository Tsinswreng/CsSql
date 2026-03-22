using System.Collections;
using Tsinswreng.Srefl;

namespace Tsinswreng.CsSql;

using IStr_Any = System.Collections.Generic.IDictionary<string, object?>;
using Str_Any = System.Collections.Generic.Dictionary<string, object?>;


public partial interface ITable<T>:ITable{

}

public partial interface ITable{
	public ITblMgr TblMgr{get;set;}
	public IDbStuff DbStuff => TblMgr.DbStuff;
	public ISqlMkr SqlMkr=>DbStuff.SqlMkr;

	[Doc($@"Accessor manager to read/write entity properties by string keys")]
	public IPropAccessorReg PropAccessorReg{get;set;}

	[Doc($@"Entity type for this table")]
	public Type CodeEntityType{get;set;}

	[Doc($@"Table name in database")]
	public str DbTblName{get;set;}
#if Impl
	= "";
#endif

	[Doc($@"Columns in the table
	Key: CodeColName (property name in entity class)
	#See[{nameof(ExtnITable.GetCol)}]
	")]
	public IDictionary<str, IColumn> Columns{get;set;}
#if Impl
	= new Dictionary<str, I_Column>();
#endif


	[Doc($@"property name for primary key in entity class")]
	public str CodeIdName{get;set;}
#if Impl
	= "Id";
#endif


	[Doc($@"Soft delete column")]
	public ISoftDeleteCol? SoftDelCol{get;set;}

	[Doc($@"Mapping from database column names to code column names(property names in entity class)")]
	public IDictionary<str, str> DbColName_CodeColName{get;set;}
#if Impl
	= new Dictionary<str, str>();
#endif

	[Doc($@"Code column(property name in entity class) to upper type mapping
	#See[{nameof(IColumn.UpperClrType)}]")]
	public IDictionary<str, Type> CodeCol_UpperType{get;set;}
#if Impl
	= new Dictionary<str, Type>();
#endif

	[Doc($@"Additional SQL statements inside CREATE TABLE() block, e.g., DEFAULT 0")]
	public IList<str> InnerAdditionalSqls{get;set;}
#if Impl
	= new List<str>();
#endif


	[Doc($@"Additional SQL statements outside CREATE TABLE() block, e.g., CREATE INDEX ...")]
	public IList<str> OuterAdditionalSqls{get;set;}
#if Impl
	= new List<str>();
#endif

	[Doc($@"Upper type to default mapper mapping
	#See[{nameof(IColumn.UpperClrType)}]")]
	public IDictionary<Type, IUpperTypeMapFn> UpperType_DfltMapper{get;set;}
#if Impl
	= new Dictionary<Type, IUpperTypeMapperFn>();
#endif
}
